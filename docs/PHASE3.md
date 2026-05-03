# Fase 3 — API pública + baseline de seguridad

Esta fase expone la primera API pública del backend (catálogo + creación de pedidos)
y deja sentadas las bases de seguridad transversales: manejo global de errores,
cabeceras de seguridad, CORS estricto, rate limiting y HSTS.

> **Recordatorio de arquitectura**
>
> ```
> Domain  ←  Application  ←  Infrastructure
>                ↑                ↑
>                └──── API ───────┘
> ```
> La capa `API` es solo composición + transporte HTTP. Toda la lógica vive en
> `Application` (CQRS con MediatR + FluentValidation). El handler de un comando
> nunca confía en datos enviados por el cliente para algo sensible (ej. precios).

---

## 1. Paquetes NuGet añadidos en `GloryCafe.Application`

| Paquete | Versión | Para qué |
|---|---|---|
| `MediatR` | 12.x | Mediador in-process para CQRS (Queries + Commands + Handlers). |
| `FluentValidation` | 11.x | DSL para validaciones declarativas. |
| `FluentValidation.DependencyInjectionExtensions` | 11.x | `AddValidatorsFromAssembly`. |

> No instalamos `MediatR.Extensions.Microsoft.DependencyInjection` porque a
> partir de MediatR v12 el método `AddMediatR` ya viene en el paquete principal.

Los paquetes se añadieron al `.csproj` de `GloryCafe.Application`. La capa
`Domain` sigue **sin dependencias externas** (regla de Clean Architecture).

---

## 2. Pipeline de MediatR + Validación

### `Application/Common/Behaviors/ValidationBehavior.cs`
Es un `IPipelineBehavior<TRequest, TResponse>` que se ejecuta **antes** de cada
handler. Busca todos los `IValidator<TRequest>` registrados, los corre, y si
alguno falla lanza `ValidationException` con el diccionario de errores.

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
services.AddValidatorsFromAssembly(assembly);
```

Esto significa que **cualquier comando con un validator registrado se valida
automáticamente** sin tener que llamar al validator manualmente en cada handler.

### `Application/Common/Exceptions/ValidationException.cs`
Encapsula los fallos como `IDictionary<string, string[]>` para que el
`GlobalExceptionHandler` los pueda mapear directamente a un
`ValidationProblemDetails` (RFC 7807).

### `Application/Common/Exceptions/NotFoundException.cs`
Constructor `(string entityName, object key)` para mensajes consistentes
("`Product with key '42' was not found.`").

---

## 3. Read side — Queries

| Archivo | Endpoint | Notas |
|---|---|---|
| `Categories/Queries/GetCategories/CategoryDto.cs` | — | Record DTO. |
| `Categories/Queries/GetCategories/GetCategoriesQuery.cs` | `GET /api/categories` | Lista todas, ordenadas por `DisplayOrder` luego `Name`. |
| `Products/Queries/GetProducts/ProductDto.cs` | — | Incluye `CategoryName` (join). |
| `Products/Queries/GetProducts/GetProductsQuery.cs` | `GET /api/products?categoryId=N` | Filtro opcional. |
| `Products/Queries/GetProductById/GetProductByIdQuery.cs` | `GET /api/products/{id}` | Devuelve 404 si no existe. |

Todos los handlers usan `AsNoTracking()` (lectura pura, mejor performance) y
proyectan directamente al DTO en la query SQL (no traen entidades completas).

---

## 4. Write side — `CreateOrderCommand`

### Archivo: `Application/Orders/Commands/CreateOrder/`
- `CreateOrderCommand.cs` — record con datos del cliente, hora estimada de
  recogida, y lista de items `{ ProductId, Quantity }`.
- `CreateOrderCommandValidator.cs` — FluentValidation:
  - `FirstName`, `LastName` requeridos, `MaximumLength(100)`.
  - **Al menos uno de `Phone` o `Email`** (validación cruzada con `Must`).
  - Email con formato válido si se provee.
  - Teléfono con regex `^\+?[0-9 \-]{6,20}$` si se provee.
  - `EstimatedPickupTime` entre **+10 minutos** (mínimo) y **+24 horas**
    (máximo). Las constantes están en el validator para fácil ajuste.
  - `Items.Count` entre 1 y 50.
  - Cada item: `ProductId > 0`, `Quantity` entre 1 y 99.
- `CreateOrderCommandHandler.cs` — la pieza más sensible de toda la fase.

### Reglas de seguridad implementadas en el handler

> **El cliente nunca dicta el precio.** Estas son las defensas explícitas:

1. **Lookup de productos en BD por los IDs enviados.** Si falta alguno, lanza
   `NotFoundException` (→ 404).
2. **Filtra productos no disponibles** (`IsAvailable == false`) y los rechaza
   con `ValidationException` (→ 400) con mensajes específicos por producto.
3. **Calcula el total server-side** sumando `producto.Price * cantidad`. El
   cliente no puede mandar `total` ni `unitPrice`: ni siquiera están en el DTO.
4. **Snapshot por línea**: guarda `ProductNameSnapshot` y `UnitPrice` en cada
   `OrderItem`. Si el producto cambia de nombre o de precio mañana, el pedido
   histórico se mantiene íntegro.
5. **Consolidación de líneas duplicadas**: si el cliente manda dos veces el
   mismo `ProductId`, se agrupan en una sola línea con cantidades sumadas.
   Evita inconsistencias y abuso.
6. **Normalización de input**:
   - `Trim()` en todos los strings.
   - `Email.ToLowerInvariant()` para evitar duplicados por mayúsculas.
   - `EstimatedPickupTime.ToUniversalTime()` (la BD guarda UTC).
7. **`Status = OrderStatus.Pending`** forzado por código. El cliente no puede
   pedir crear un pedido ya pagado.

---

## 5. Controllers (capa API)

`Controllers/ApiControllerBase.cs` declara una vez `[ApiController]`,
`[Route("api/[controller]")]` y `[Produces("application/json")]`, y expone
`ISender Mediator`. Todos los controladores heredan de aquí.

| Controller | Endpoints | Rate-limit |
|---|---|---|
| `CategoriesController` | `GET /api/categories` | global (100/min/IP) |
| `ProductsController` | `GET /api/products`, `GET /api/products/{id}` | global |
| `OrdersController` | `POST /api/orders` | **policy `orders` (10/min/IP)** + global |

Los controllers son **finos**: solo reciben el request, lo mandan al Mediator,
y devuelven la respuesta. No tienen lógica de negocio.

---

## 6. Manejo global de errores → ProblemDetails

### `API/Infrastructure/GlobalExceptionHandler.cs`
Implementa `IExceptionHandler` (.NET 8+). Mapea excepciones a respuestas JSON
RFC 7807 (`application/problem+json`):

| Excepción | Status | Body |
|---|---|---|
| `ValidationException` | **400** | `ValidationProblemDetails` con `errors: { campo: [mensajes] }`. |
| `NotFoundException` | **404** | `ProblemDetails` con `detail`. |
| Cualquier otra | **500** | `ProblemDetails` con `detail` (en producción → genérico; en dev → stack trace). |

Todos los responses incluyen `traceId` (para correlacionar con logs) e
`instance` (la ruta llamada).

> **Detalle no obvio**: hay que serializar con `problem.GetType()` como tipo
> en `WriteAsJsonAsync`, porque si se pasa `ProblemDetails` (la base) System.Text.Json
> no incluye el campo `errors` del `ValidationProblemDetails` derivado.

Wireup en `Program.cs`:
```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
...
app.UseExceptionHandler();
```

---

## 7. Cabeceras de seguridad

### `API/Infrastructure/SecurityHeadersMiddleware.cs`
Middleware propio que escribe en cada response:

| Cabecera | Valor | Por qué |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | Evita MIME-sniffing del navegador. |
| `X-Frame-Options` | `DENY` | Previene clickjacking (no se puede embeber en iframe). |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Limita info de referer. |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Bloquea APIs sensibles del navegador. |
| `Cross-Origin-Opener-Policy` | `same-origin` | Aísla el browsing context. |
| `Cross-Origin-Resource-Policy` | `same-origin` | Bloquea cargas cross-origin no autorizadas. |

Adicionalmente, en `Program.cs`:
```csharp
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
```
quita la cabecera `Server: Kestrel` (information disclosure).

---

## 8. CORS estricto

Configuración en `appsettings.json`:
```json
"Cors": {
  "AllowedOrigins": [ "http://localhost:5173", "https://localhost:5173" ]
}
```
- **Sin `AllowAnyOrigin`** — solo orígenes explícitos (donde correrá Vite/React).
- **Sin `AllowCredentials`** por ahora — no enviamos cookies entre orígenes
  todavía.
- Métodos permitidos: solo `GET, POST, PUT, DELETE, OPTIONS`.

Cuando despleguemos en producción, se agregará el dominio real (ej.
`https://shop.glorycafe.com.au`) en `appsettings.Production.json`.

---

## 9. Rate limiting

Usa `Microsoft.AspNetCore.RateLimiting` (built-in en .NET 7+).

| Limit | Permit | Ventana | Aplica a |
|---|---|---|---|
| **Global** (por IP) | 100 | 1 minuto | Todos los endpoints |
| **Policy `orders`** (por IP) | 10 | 1 minuto | `POST /api/orders` |

El status de rechazo es `429 Too Many Requests`. La policy más restrictiva
(`orders`) protege específicamente el endpoint que toca BD-write y que en el
futuro disparará pagos con Stripe.

> **Nota sobre el partition key**: usamos `RemoteIpAddress` directamente. Cuando
> haya un reverse proxy o load balancer delante (Nginx, CloudFront), hay que
> asegurarse de que `UseForwardedHeaders` está antes (ya lo está) para que
> `RemoteIpAddress` sea la IP real del cliente, no la del proxy.

---

## 10. HSTS y HTTPS

```csharp
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
    o.Preload = true;
});
...
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();
```
- **HSTS solo en producción** (en dev rompería localhost http). Le dice al
  navegador "siempre HTTPS para este dominio durante 1 año".
- **HttpsRedirection** activo siempre — manda 307 a la versión HTTPS.

---

## 11. `Program.cs` final — orden del pipeline

```
ConfigureServices:
  CORS, RateLimiter, ExceptionHandler, ProblemDetails,
  ForwardedHeaders, HSTS, Controllers, OpenAPI,
  AddApplication, AddInfrastructure

Pipeline:
  UseForwardedHeaders     ← antes de cualquier middleware que dependa de IP
  UseExceptionHandler     ← captura cualquier excepción del resto
  UseSecurityHeaders      ← antes de generar la response
  (UseHsts en prod)
  UseHttpsRedirection
  UseRouting
  UseCors
  UseRateLimiter          ← después de Routing y Cors
  UseAuthorization
  MapControllers
```

El orden importa: `ExceptionHandler` debe envolver al resto, `Cors` debe ir
después de `Routing`, `RateLimiter` después de `Cors`, etc.

---

## 12. Smoke test

Con la API corriendo (`dotnet run --launch-profile http` desde `src/GloryCafe.API`):

| Request | Esperado |
|---|---|
| `GET /api/categories` | `200` con `[]` (BD vacía) + cabeceras de seguridad presentes. |
| `GET /api/products` | `200` con `[]`. |
| `GET /api/products/9999` | `404` con `ProblemDetails` (`title: "Resource not found"`). |
| `POST /api/orders` con body inválido | `400` con `errors: { ... }` por campo. |
| `POST /api/orders` con `productId` inexistente | `404` con `ProblemDetails`. |

Todos verificados ✅.

---

## 13. Lista de archivos creados/modificados en esta fase

```
src/GloryCafe.Application/
  DependencyInjection.cs                                    (modificado)
  Common/Behaviors/ValidationBehavior.cs                    (nuevo)
  Common/Exceptions/ValidationException.cs                  (nuevo)
  Common/Exceptions/NotFoundException.cs                    (nuevo)
  Categories/Queries/GetCategories/
    CategoryDto.cs                                          (nuevo)
    GetCategoriesQuery.cs                                   (nuevo)
    GetCategoriesQueryHandler.cs                            (nuevo)
  Products/Queries/GetProducts/
    ProductDto.cs                                           (nuevo)
    GetProductsQuery.cs                                     (nuevo)
    GetProductsQueryHandler.cs                              (nuevo)
  Products/Queries/GetProductById/
    GetProductByIdQuery.cs                                  (nuevo)
    GetProductByIdQueryHandler.cs                           (nuevo)
  Orders/Commands/CreateOrder/
    CreateOrderCommand.cs                                   (nuevo)
    CreateOrderCommandValidator.cs                          (nuevo)
    CreateOrderCommandHandler.cs                            (nuevo)

src/GloryCafe.API/
  Program.cs                                                (modificado)
  appsettings.json                                          (modificado)
  Controllers/
    ApiControllerBase.cs                                    (nuevo)
    CategoriesController.cs                                 (nuevo)
    ProductsController.cs                                   (nuevo)
    OrdersController.cs                                     (nuevo)
  Infrastructure/
    GlobalExceptionHandler.cs                               (nuevo)
    SecurityHeadersMiddleware.cs                            (nuevo)
```

---

## 14. Lo que **no** está en esta fase (por diseño)

- **Auth admin (JWT)** → Fase 4.
- **Stripe / pagos** → Fase de pagos. Por eso `OrderStatus = Pending` y
  `StripePaymentIntentId` queda nullable.
- **SignalR push a tablet** → Fase de admin.
- **Subida de imágenes** → Fase de admin.
- **Tests automatizados** → se añadirán en una fase de testing dedicada
  (xUnit + WebApplicationFactory para integración).

---

## 15. Próximos pasos

1. **Sembrar datos** (categorías + productos de ejemplo) para poder probar
   `POST /api/orders` end-to-end.
2. **Fase 4**: autenticación JWT para admin + endpoints de admin (CRUD de
   productos, listado de pedidos en tiempo real).
