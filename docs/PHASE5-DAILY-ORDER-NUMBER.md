# Fase 5 — Número de orden diario (slice A)

Esta slice resuelve un detalle de UX importante: el cliente ya no ve
`#3847` en el recibo (que es el `Id` interno auto-incremental de
Postgres y nunca se reinicia), sino `#5` — su quinta orden del día.
Internamente el `Id` global sigue existiendo para integridad
referencial, snapshots y futuros joins; solo se *muestra* el número
diario.

> **Decisiones clave**
>
> - **Dos campos nuevos en `Order`**: `OrderDate` (`DateOnly`) y
>   `DailyOrderNumber` (`int`). Índice único `(OrderDate, DailyOrderNumber)`
>   para garantía de no-duplicados y como índice de búsqueda en el
>   query del allocator.
> - **Timezone fija de Brisbane** (`Australia/Brisbane`, UTC+10
>   sin DST): "today" significa el día calendario en la zona del
>   negocio, no UTC. Encapsulado en `IClock.TodayInBusinessTimeZone`.
> - **Advisory lock por día (`pg_advisory_xact_lock`)**: serializa
>   las inserciones concurrentes para el mismo día sin bloquear las de
>   otros días. El lock es transaction-scoped, así que si la
>   transacción hace rollback el slot vuelve disponible — sin huecos
>   en la secuencia visible al cliente.
> - **El `Id` interno sigue siendo la PK**: nada de cambiar primary
>   keys ni romper FKs (`OrderItem.OrderId` apunta al Id global).
>   El número diario es UI-only.

---

## 1. Por qué los huecos son un problema (y por qué este diseño los evita)

El `Id` de Postgres es un `IDENTITY`, que detrás usa una **secuencia**.
Las secuencias son no-transaccionales: `nextval()` consume el número
incluso si la transacción que lo pidió termina en rollback. En
práctica, un FluentValidation que falla a mitad del handler ya gastó
el Id que nunca verá la luz.

Eso es razonable para un `Id` de base de datos (vela por el
throughput), pero un cliente esperando su café no debería ver
`#42 → #44` y preguntarse qué pasó con el 43.

Por eso el `DailyOrderNumber` se calcula con `MAX + 1` **dentro de la
misma transacción que inserta la orden**. Si el insert hace rollback,
el `MAX` lo refleja — la próxima orden lee `MAX = 41` y reutiliza el
42 limpiamente.

Para evitar que dos inserciones concurrentes lean el mismo `MAX = 41`
y ambas escriban 42, el [allocator](../GloryCafeAPI/src/GloryCafe.Infrastructure/Persistence/PostgresOrderNumberAllocator.cs)
acquire un advisory lock por día antes de leer:

```csharp
var lockKey = (long)orderDate.DayNumber;
await _db.Database.ExecuteSqlInterpolatedAsync(
    $"SELECT pg_advisory_xact_lock({lockKey})",
    cancellationToken);

var current = await _db.Orders
    .Where(o => o.OrderDate == orderDate)
    .MaxAsync(o => (int?)o.DailyOrderNumber, cancellationToken);

return (current ?? 0) + 1;
```

`pg_advisory_xact_lock` es:
- **Transaction-scoped**: se libera al `COMMIT` o `ROLLBACK`, no hay
  fuga de locks.
- **Por clave**: solo bloquea inserciones del mismo día. Una orden a
  las 23:59 y una a las 00:01 del día siguiente no se pisan.
- **Sin tabla extra**: no hace falta una tabla `daily_counters`. La
  fuente de verdad son las propias órdenes.

---

## 2. La transacción en el handler

[`CreateOrderCommandHandler`](../GloryCafeAPI/src/GloryCafe.Application/Orders/Commands/CreateOrder/CreateOrderCommandHandler.cs)
ahora envuelve la inserción en un `IExecutionStrategy` para retries
en errores transitorios de conexión, y dentro abre una transacción
explícita:

```csharp
var strategy = _db.Database.CreateExecutionStrategy();
return await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _db.Database.BeginTransactionAsync(ct);

    var orderDate = _clock.TodayInBusinessTimeZone;
    var dailyNumber = await _orderNumbers.AllocateAsync(orderDate, ct);

    var order = BuildOrder(request, products, orderDate, dailyNumber);

    _db.Orders.Add(order);
    await _db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);

    return order;
});
```

> **Por qué la notificación SignalR queda fuera de la estrategia**:
> si quedara dentro y el strategy reintenta, el barista recibiría
> el mismo `orderCreated` dos veces. Fuera de `ExecuteAsync` se
> dispara una sola vez, y solo después del `CommitAsync` exitoso.

> **Por qué la validación de productos también queda fuera**: es una
> lectura idempotente y no necesita correr dentro de la transacción
> de escritura. Reduce el tiempo en que el lock de día está tomado.

---

## 3. La migración (`AddDailyOrderNumber`)

El auto-generador de EF crea la migración con `nullable: false` +
`defaultValue: 0` y `defaultValue: new DateOnly(1, 1, 1)`. Para una
base de datos vacía funciona; para una con órdenes ya insertadas, el
índice único explota porque todas comparten `(0001-01-01, 0)`.

Por eso la
[migración](../GloryCafeAPI/src/GloryCafe.Infrastructure/Persistence/Migrations/20260430082353_AddDailyOrderNumber.cs)
está reescrita a mano:

1. Agrega columnas como `NULL`.
2. **Backfill `OrderDate`** convirtiendo `CreatedAt` (UTC) a Brisbane.
3. **Backfill `DailyOrderNumber`** con `ROW_NUMBER() OVER (PARTITION
   BY OrderDate ORDER BY Id)`.
4. Las marca como `NOT NULL`.
5. Crea el índice único.

Si la tabla está vacía, los pasos 2-3 son no-ops y todo sigue
funcionando.

---

## 4. `IClock` — abstracción del tiempo

```csharp
public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly TodayInBusinessTimeZone { get; }
}
```

Implementación en
[`SystemClock`](../GloryCafeAPI/src/GloryCafe.Infrastructure/Common/SystemClock.cs)
con `TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane")`.

> **Por qué no usar `DateTime.Now` directo**: en producción el server
> puede correr en cualquier zona (UTC en la mayoría de hostings, AEST
> en una VM local). Hardcodear `DateTime.Today` en el handler ata el
> resultado al servidor. La interfaz se inyecta y mañana cuando
> querramos tests con un clock fake (orden creada a las 11:55 PM,
> cobertura del rollover de medianoche), está listo.

> **Por qué Brisbane y no `TimeZoneInfo.Local`**: Brisbane no tiene
> horario de verano, así que UTC+10 todo el año. Si el negocio se
> mudara a Sydney (UTC+10/+11 con DST) habría que pensarlo, pero por
> ahora es estable.

---

## 5. `IApplicationDbContext.Database`

Para que el handler pueda abrir transacciones explícitas y el
allocator pueda ejecutar SQL crudo, expusimos `DatabaseFacade` en la
interfaz:

```csharp
public interface IApplicationDbContext
{
    // ... DbSets ...
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

> **Por qué no abstraer la transacción**: `IDbContextTransaction`
> ya es una abstracción razonable. Crear un `IUnitOfWork` propio
> sería overkill y duplicaría lo que EF ya ofrece. La capa
> Application ya importa `Microsoft.EntityFrameworkCore` (lo hace
> en varios handlers desde antes), así que el costo de acoplamiento
> es marginal.

---

## 6. Cambios en el frontend

- **`OrderConfirm`**: el número diario se muestra grande y centrado
  en una caja ámbar, con copy "Show this at the counter when you
  collect your order." El `Id` interno queda como `Reference #3847`
  pequeño y gris al lado del total.
- **`AdminOrders`**: el `#N` ahora es ámbar y bold (era gris y
  monoespaciado).
- **`AdminOrderDetail`**: idem en el header, y debajo aparece la
  fecha + ref id chiquita.
- **Tipos**: `CreateOrderResponse`, `AdminOrderSummary`,
  `AdminOrderDetail` y `OrderCreatedPayload` ahora incluyen
  `orderDate` (string ISO) y `dailyOrderNumber` (number).

---

## 7. Smoke-tests propuestos

| Caso | Esperado |
|---|---|
| Cliente nuevo coloca primera orden del día | Confirmación muestra `#1` |
| Cliente coloca segunda orden | Confirmación muestra `#2` |
| Admin abre dashboard | Cada fila muestra `#1`, `#2`, ... |
| Admin abre detalle | Header `#1`, abajo "2026-04-30 · ref #3847" |
| Próximo día (cambiar fecha del server o esperar) | Primera orden vuelve a `#1` |
| Dos POSTs simultáneos al endpoint | Ambos se aceptan, números 1 y 2 sin colisión |
| Insert que falla por validación de stock | Sin gap visible en el siguiente número |

> **Cómo simular concurrencia**: en otra terminal, dos `curl -X POST`
> al endpoint `/api/orders` lanzados en paralelo con `&`. Ambos deben
> retornar 200 con `dailyOrderNumber` distinto y consecutivo.

---

## 8. Lo que queda fuera de esta slice

- **Permitir reset manual del contador**: si los dueños quisieran
  forzar un "empezar de nuevo" mid-day, no hay endpoint. No es un
  caso real (¿para qué reiniciar?), pero está aislado al allocator
  si en algún momento aparece.
- **Mostrar la fecha en `OrderConfirm`**: por ahora el cliente solo
  ve el número y el total. Si pidieran "Order placed at 14:32",
  podríamos sumar `createdAt` a la response.
- **Test unitario del allocator**: requiere setup de Testcontainers
  o un fake. Está anotado para cuando arme la suite de Application
  tests más adelante.
