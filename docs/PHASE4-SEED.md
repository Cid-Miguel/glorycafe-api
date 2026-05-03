# Fase 4 — Seed de catálogo (slice 4)

Esta slice añade datos de catálogo placeholder al arranque de la app.
Sirve para que el front pueda mostrar algo realista durante todo el
desarrollo, y para tener una demo lista al pitch con los dueños.

> **Decisiones clave**
>
> - **Seed en código, no en SQL**: vive en `DbInitializer` junto al seed
>   admin. Misma infraestructura, misma garantía idempotente.
> - **Idempotente por presencia**: si hay alguna categoría en BD, no se
>   toca nada. Esto protege a la cafetería el día que tengan su catálogo
>   real cargado y haya un redeploy.
> - **Sin imágenes**: los productos se seedean con `ImageUrl = null`. Las
>   fotos reales se subirán desde el admin cuando estén disponibles
>   (endpoint `POST /api/admin/products/{id}/image` ya existe).

---

## 1. Idempotencia: la regla del cheque previo

```csharp
private async Task SeedCatalogAsync(CancellationToken cancellationToken)
{
    if (await _db.Categories.AnyAsync(cancellationToken))
        return;
    // ... inserciones
}
```

La primera línea es lo único que hace que esto sea seguro de ejecutar en
cada arranque:

- **Primer arranque** (BD recién creada por la migration) → no hay
  categorías → seed inserta 3 categorías + 8 productos.
- **Arranques siguientes** (con datos) → ya hay categorías → return inmediato.
- **Producción**: si la cafetería ya cargó su catálogo real, el seed nunca
  pisa esos datos, ni siquiera por accidente.

> **Por qué chequear `Categories.AnyAsync()` y no `Products`**: si alguien
> en algún momento crea una categoría sin productos, el seed no debe
> volver a meter el placeholder. La presencia de categoría es la señal
> "alguien ya tomó posesión de este sistema".

---

## 2. Catálogo placeholder

3 categorías con `DisplayOrder` semántico:

| Id | Nombre | Order |
|---|---|---|
| 1 | Coffee | 1 |
| 2 | Pastries | 2 |
| 3 | Cold Drinks | 3 |

8 productos con precios realistas Brisbane (AUD):

| Categoría | Producto | Precio |
|---|---|---|
| Coffee | Flat White | $5.50 |
| Coffee | Long Black | $5.00 |
| Coffee | Cappuccino | $5.50 |
| Coffee | Latte | $5.50 |
| Pastries | Croissant | $6.50 |
| Pastries | Banana Bread | $5.50 |
| Cold Drinks | Iced Latte | $6.00 |
| Cold Drinks | Sparkling Water | $4.00 |

Todos con `IsAvailable = true`, `ImageUrl = null`, descripciones cortas
en inglés (Brisbane, audiencia local).

---

## 3. Implementación

`SeedCatalogAsync` en `Infrastructure/Persistence/Seeding/DbInitializer.cs`:

1. Cheque idempotente.
2. Crear las 3 entidades `Category` con `DisplayOrder` y `CreatedAt`.
3. Crear los 8 `Product` con `Category = ...` (referencia al objeto, no
   a un id). EF Core resuelve la relación al `SaveChangesAsync`: el
   primer round-trip inserta categorías obteniendo sus ids, el segundo
   inserta productos con esos ids ya rellenos.
4. `_db.SaveChangesAsync` — todo en una transacción implícita de EF.
5. Log informativo `Seeded catalog with N categories and M products.`

Llamada añadida en `InitializeAsync`:
```csharp
await _db.Database.MigrateAsync(cancellationToken);
await SeedAdminAsync(cancellationToken);
await SeedCatalogAsync(cancellationToken);
```

---

## 4. Smoke-tests realizados

**Setup**: drop + create `glorycafedb` (BD vacía).

| Caso | Esperado | Resultado |
|---|---|---|
| Arranque con BD vacía | Log `Seeded catalog with 3 categories and 8 products.` | ✅ |
| `GET /api/categories` | 3 items, ordenados por DisplayOrder | ✅ |
| `GET /api/products` | 8 items con `categoryName` poblado | ✅ |
| `GET /api/products?categoryId=1` | 4 cafés | ✅ |
| Reinicio API (BD ya tiene datos) | NO emite log de seed (skip silencioso) | ✅ |
| Conteo tras reinicio | sigue siendo 3 categorías y 8 productos (no duplicó) | ✅ |

---

## 5. Cómo refrescar el seed manualmente

Si en algún momento quieres recargar el catálogo placeholder durante el
desarrollo (por ejemplo, si cambian los productos en este archivo):

```sh
psql -h localhost -U postgres -c "DROP DATABASE glorycafedb;" -c "CREATE DATABASE glorycafedb;"
dotnet run --project src/GloryCafe.API
```

Las migrations recrean toda la schema y el seed corre de nuevo.

> **Importante**: esto borra cualquier orden, imagen subida y cambio
> manual hecho desde el admin. Solo hacerlo en desarrollo.

---

## 6. Próxima slice de Fase 4

**SignalR hub**: notificación push al tablet del admin cada vez que entra
un pedido nuevo, con sonido en el front. Es la última pieza de la fase
de admin.
