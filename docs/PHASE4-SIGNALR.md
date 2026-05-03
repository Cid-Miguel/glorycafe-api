# Fase 4 — SignalR para notificaciones admin (slice 5)

Esta slice añade un canal en tiempo real entre el backend y el panel
admin: cada vez que entra un pedido nuevo, el barista recibe una
notificación push instantánea sin tener que refrescar la pantalla.

> **Decisiones clave**
>
> - **SignalR sobre WebSocket** (con fallback a SSE/long-polling).
>   Microsoft mantenido, integración nativa con ASP.NET Core, autorización
>   reutiliza el `[Authorize]` que ya tenemos.
> - **Solo Admin escucha**: el hub está protegido con
>   `[Authorize(Roles="Admin")]`. Clientes anónimos no pueden conectarse
>   ni hacer "negotiate".
> - **JWT por query string**: los WebSockets no permiten `Authorization`
>   header desde el navegador. Reconfiguramos el handler JWT para leer
>   `?access_token=` solo en rutas `/hubs/*`.
> - **Notificación dispara tras `SaveChanges`**: nunca antes. Si la BD
>   falla, no hay notificación ruidosa por nada.
> - **Stub temporal**: hoy notifica al crear la orden. Cuando llegue
>   Stripe, esto se moverá al webhook `payment_intent.succeeded` — solo
>   pedidos pagados llegan al barista.

---

## 1. Arquitectura: dónde vive cada cosa

| Capa | Pieza | Por qué ahí |
|---|---|---|
| Application | `IOrderNotificationService` (interfaz) | Application no debe saber de SignalR |
| Application | `OrderCreatedNotification` (record/DTO) | Payload neutral, sin tipos de transporte |
| Application | `CreateOrderCommandHandler` (consume la interfaz) | Inversión de dependencias clásica |
| API | `OrdersHub` (la clase del hub) | Depende de `Microsoft.AspNetCore.SignalR` |
| API | `SignalROrderNotificationService` (implementación) | Necesita `IHubContext<OrdersHub>` |

> **¿Por qué no en Infrastructure como otras integraciones?**
> `IHubContext<T>` requiere conocer el tipo del hub (`OrdersHub`), y los
> hubs tienen que vivir donde se hace `MapHub<T>()`. Mover el hub a
> Infrastructure exigiría que ese proyecto referenciara
> `Microsoft.AspNetCore.App` — pollución innecesaria.

---

## 2. Flujo de un pedido nuevo

```
Cliente anónimo ──POST /api/orders──┐
                                    ▼
                       CreateOrderCommandHandler
                                    │
                       1. Validar productos
                       2. Calcular total
                       3. INSERT orders + items
                       4. SaveChangesAsync ✓
                                    │
                                    ▼
              IOrderNotificationService.NotifyOrderCreatedAsync(...)
                                    │
                                    ▼
                  IHubContext<OrdersHub>.Clients.All.SendAsync("orderCreated", payload)
                                    │
                                    ▼
                      Tablet admin (escuchando) ─── 🔔 Suena
```

**Importante**: la notificación va **después** del commit. Si SaveChanges
falla, el handler tira la excepción y nunca se llega al `Notify…`. No
queremos baristas reaccionando a pedidos que no existen.

---

## 3. El payload (`OrderCreatedNotification`)

```csharp
public record OrderCreatedNotification(
    int Id,
    string CustomerFirstName,
    string CustomerLastName,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt);
```

Lo mínimo para que el front pinte una tarjeta en el dashboard sin tener
que hacer otro fetch. Si el barista quiere ver los ítems, hace clic y
llama a `GET /api/admin/orders/{id}` — eso ya existe.

> **Por qué no mandar la orden completa**: payloads pequeños viajan más
> rápido y exponen menos. Los ítems tienen snapshot de precios y nombres
> que no son secretos, pero la regla "menos es más" siempre aplica en
> realtime.

---

## 4. El hub: `OrdersHub`

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
           Roles = "Admin")]
public class OrdersHub : Hub
{
    public const string Path = "/hubs/orders";
    public const string OrderCreatedEvent = "orderCreated";
}
```

- **Path**: `/hubs/orders` — cualquier intento de conectar sin pasar la
  auth recibe 401 en el "negotiate" antes de abrir el WebSocket.
- **Constantes**: el nombre del evento (`orderCreated`) está aquí, no
  hardcodeado en strings sueltos. Cuando agreguemos
  `OrderStatusChangedEvent` será obvio dónde va.
- **Hub vacío**: no expone métodos para que el cliente invoque. El flujo
  es solo server → client. Los clientes no pueden mandar nada al hub.

---

## 5. JWT por query string para WebSockets

El navegador no puede setear el header `Authorization` cuando abre un
WebSocket. La solución estándar SignalR: el cliente manda el token como
query string (`?access_token=...`) y el handler JWT lo lee desde ahí
antes de validarlo.

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            context.Token = accessToken;
        return Task.CompletedTask;
    }
};
```

> **Por qué el `path.StartsWithSegments("/hubs")`**: para que las APIs
> REST normales sigan exigiendo el header `Authorization`. Aceptar tokens
> por query string en `/api/...` ampliaría innecesariamente la superficie
> de ataque (logs, referrers, historial del navegador podrían filtrar
> tokens).

---

## 6. CORS y WebSockets

Las conexiones SignalR requieren `AllowCredentials()` en CORS porque la
librería cliente manda cookies/credenciales al hacer "negotiate". Con
eso ya no se puede usar `AllowAnyOrigin()` — hay que listar orígenes
específicos.

`Program.cs`:
```csharp
policy.WithOrigins(allowedOrigins)
      .AllowAnyHeader()
      .AllowCredentials()
      .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS");
```

Los orígenes vienen de `Cors:AllowedOrigins` en `appsettings`. Para
desarrollo: `http://localhost:5173` (Vite). Para prod: el dominio real
del admin.

---

## 7. Wiring en `Program.cs`

Cambios mínimos:
```csharp
// Servicios
builder.Services.AddSignalR();
builder.Services.AddScoped<IOrderNotificationService, SignalROrderNotificationService>();

// Pipeline (después de MapControllers)
app.MapHub<OrdersHub>(OrdersHub.Path);
```

`AddSignalR()` registra todo el ecosistema (hubs, transporte WS, fallback
SSE). `MapHub<T>()` lo monta en la ruta indicada y aplica el `[Authorize]`
del hub automáticamente.

---

## 8. Smoke-tests realizados

**Setup**:
- API corriendo en `http://localhost:5089`
- Cliente Node con `@microsoft/signalr` escuchando

| Caso | Esperado | Resultado |
|---|---|---|
| Conectar con token Admin válido | `Conectado al hub OK` | ✅ |
| `POST /api/orders` (anónimo) | 201 Created | ✅ |
| Crear orden mientras Node escucha | Listener imprime `Nueva orden: {...}` con id, nombre, total, etc. | ✅ |
| Conectar sin token | Rechazo en negotiate | ✅ |
| Conectar con token corrupto | Rechazo en negotiate | ✅ |

---

## 9. Cliente Node mínimo (referencia)

`listen.js`:
```js
const signalR = require("@microsoft/signalr");

const TOKEN = "<accessToken del login admin>";
const URL = "http://localhost:5089/hubs/orders";

const conn = new signalR.HubConnectionBuilder()
  .withUrl(URL, { accessTokenFactory: () => TOKEN })
  .configureLogging(signalR.LogLevel.Information)
  .build();

conn.on("orderCreated", (n) => {
  console.log("Nueva orden:", n);
});

conn.start()
  .then(() => console.log("Conectado al hub OK"))
  .catch(err => console.error("Error:", err));
```

Cuando llegue el frontend React, este mismo patrón vive en un hook
`useOrderNotifications()` que se monta en el dashboard admin.

---

## 10. Migración futura: del stub a Stripe

Hoy:
```
crear orden → SaveChanges → notificar (stub)
```

Con Stripe:
```
crear orden (Pending) → SaveChanges → return clientSecret
                                          │
                                  Stripe Elements paga
                                          │
                                  webhook payment_intent.succeeded
                                          │
                          UpdateOrderStatusToPaidHandler
                                          │
                                   SaveChanges → notificar
```

La interfaz `IOrderNotificationService` no cambia. Solo se mueve la
llamada del `CreateOrderCommandHandler` al handler del webhook. Esa es
toda la gracia de tener la abstracción en Application.

---

## 11. Próximo paso

Fin de Fase 4 (admin completo). Las próximas fases:

- **Fase 5 — Stripe**: PaymentIntents, webhook, transición a `Paid`,
  reubicación del notify.
- **Fase 6 — Frontend**: React + Vite, mobile-first, hook de SignalR
  para el dashboard admin.
