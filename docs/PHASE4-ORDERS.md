# Fase 4 — Pedidos admin (slice 3: lectura + máquina de estados)

Esta slice añade los endpoints de admin para **ver pedidos** y **avanzar
su estado** a lo largo del flujo del barista. Es lo que el tablet del
mostrador va a usar todo el día.

> **Decisiones clave**
>
> - **Máquina de estados estricta** en el handler. La transición no puede
>   "saltar" pasos ni ir hacia atrás. Esto evita estados imposibles si el
>   front tiene un bug o si dos baristas tocan el mismo pedido a la vez.
> - `Cancelled` se quita del enum: el cliente decidió que no se va a usar
>   en el negocio. YAGNI — si mañana se necesita, se vuelve a añadir.
> - `Paid` se mantiene en el enum pero **no entra en la máquina de estados
>   actual**. Está reservado para cuando enchufemos Stripe.
> - `JsonStringEnumConverter` global: status sale como `"Pending"` no como
>   `0`, y los PATCH aceptan `"status":"Preparing"` desde el front.

---

## 1. `OrderStatus` enum

```csharp
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,        // reservado para Stripe (no se usa todavía)
    Preparing = 2,
    Ready = 3,
    Completed = 4
}
```

> El valor numérico no importa para la BD: `OrderConfiguration` usa
> `HasConversion<string>()`, así que la columna almacena `"Pending"`,
> `"Preparing"`, etc. — añadir/quitar valores no requiere migration siempre
> que ningún registro contenga el valor eliminado.

---

## 2. Máquina de estados

| Desde | Permite avanzar a |
|---|---|
| `Pending` | `Preparing` |
| `Preparing` | `Ready` |
| `Ready` | `Completed` |
| `Completed` | (terminal, sin transición) |
| `Paid` | (no se usa hoy) |

Implementada como diccionario en
`UpdateOrderStatusCommandHandler.AllowedTransitions`. Cualquier intento de
saltar de paso (`Pending → Ready`), ir hacia atrás (`Preparing → Pending`)
o salir de un estado terminal devuelve `409 Conflict` con el mensaje
*"Cannot transition order N from X to Y."*

> **Por qué tan estricto**: el front del tablet tendrá un solo botón de
> "siguiente paso", así que normalmente nunca llegará una transición
> inválida. Pero si llega (race entre dos tablets, bug, replay), el
> servidor es la última línea de defensa contra estados inconsistentes.

---

## 3. Capa `Application`

### `Admin/Orders/Commands/UpdateOrderStatus/`
- `UpdateOrderStatusCommand` → `(int Id, OrderStatus NewStatus)` → void.
- `UpdateOrderStatusCommandHandler`:
  1. Busca la orden (`FindAsync`); si no existe → `NotFoundException` → 404.
  2. Aplica la máquina de estados; si la transición no está en
     `AllowedTransitions` o no coincide con el siguiente paso → `ConflictException` → 409.
  3. Asigna el nuevo status y guarda.

### `Admin/Orders/Queries/GetAdminOrders/`
- `GetAdminOrdersQuery(OrderStatus? Status)` — filtro opcional por estado.
- `AdminOrderSummaryDto` — vista de lista (id, nombre cliente, pickup,
  status, total, **itemCount**, createdAt). No expone email/teléfono ni
  los items en la lista para minimizar payload.
- Handler: `OrderBy(o => o.CreatedAt)` ascendente — el más viejo primero,
  que es el más urgente desde el punto de vista del barista.

### `Admin/Orders/Queries/GetAdminOrderById/`
- `GetAdminOrderByIdQuery(int Id)`.
- `AdminOrderDetailDto` — incluye contacto del cliente, items con
  `ProductNameSnapshot` y precios, status y total.
- Handler: proyección con `Select` y subquery de `Items` ordenado por id.
  Si la orden no existe → `NotFoundException` → 404.

---

## 4. Controller

### `OrdersAdminController` (`/api/admin/orders`)
| Verbo | Ruta | Códigos |
|---|---|---|
| `GET` | `/` (con `?status=` opcional) | 200 / 401 |
| `GET` | `/{id}` | 200 / 401 / 404 |
| `PATCH` | `/{id}/status` | 204 / 400 / 401 / 404 / 409 |

`PATCH` recibe `{ "status": "Preparing" }` (string, no número). El
`UpdateOrderStatusRequest` se declara como `record` inline en el mismo
archivo del controller siguiendo el patrón de los otros controllers admin.

> El método HTTP correcto aquí es `PATCH` y no `PUT` porque solo modificamos
> **una propiedad** de la orden (status). `PUT` semánticamente significa
> "reemplaza el recurso entero".

---

## 5. `JsonStringEnumConverter` global

En `Program.cs`:
```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

Aplica a **todo el pipeline JSON de MVC** (request body + response body).
Beneficios:
- **Legibilidad**: `"status":"Pending"` en lugar de `"status":0`.
- **Estabilidad**: si mañana añadimos un valor al enum y otro queda con
  el mismo número (porque borramos uno intermedio), los clientes no se
  rompen — siguen leyendo nombres, no números.
- **Errores claros en input**: mandar `"status":"Foo"` da un 400 con
  mensaje específico, no un 500.

---

## 6. Smoke-tests realizados

Setup: login admin → crear category + product → crear orden anónima vía
`POST /api/orders` (deja `OrderId=1` en estado `Pending`).

| Caso | Endpoint | Esperado | Resultado |
|---|---|---|---|
| Sin token | `GET /api/admin/orders` | 401 | ✅ 401 |
| Lista | `GET /api/admin/orders` | 200 + array, status como string | ✅ |
| Filtro Pending | `?status=Pending` | 200 + 1 item | ✅ |
| Filtro Completed | `?status=Completed` | 200 + `[]` | ✅ |
| Detalle existente | `GET /api/admin/orders/1` | 200 + items | ✅ |
| Detalle inexistente | `GET /api/admin/orders/999` | 404 | ✅ |
| Skip de paso | `PATCH /1/status {Ready}` desde Pending | 409 | ✅ |
| Avance válido | `PATCH /1/status {Preparing}` | 204 | ✅ |
| Backwards | `PATCH /1/status {Pending}` desde Preparing | 409 | ✅ |
| Preparing → Ready | `PATCH /1/status {Ready}` | 204 | ✅ |
| Ready → Completed | `PATCH /1/status {Completed}` | 204 | ✅ |
| Terminal | `PATCH /1/status {Preparing}` desde Completed | 409 | ✅ |
| Orden inexistente | `PATCH /999/status` | 404 | ✅ |
| Estado final | `GET /api/admin/orders/1` | status `Completed` | ✅ |

---

## 7. Lo que **no** hace esta slice

- **No hay paginación** todavía. Para el MVP de la cafetería el volumen
  de pedidos diario es bajo; cuando crezca, se añadirá `?page=&pageSize=`.
- **No se pueden editar los items** de una orden ya creada. La política
  es: si hay un error, el barista llama al cliente. Modificar items
  abriría una caja de Pandora (recálculo de total, snapshots, refunds…).
- **No se puede borrar una orden**. El historial es trazabilidad.
- **No hay endpoint público para que el cliente vea el estado de su pedido**
  todavía. Se añadirá si se decide enviar SMS/email con un link.

---

## 8. Próximas slices de Fase 4

1. **Seed de catálogo placeholder** — 2-3 categorías + 6-8 productos para
   tener algo cuando arranque el front.
2. **SignalR hub** para push al tablet del admin cada vez que entra un
   pedido nuevo (con sonido en el front).
