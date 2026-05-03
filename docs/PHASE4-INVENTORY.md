# Fase 4 — Inventario admin (slice 2: CRUD de categorías y productos)

Esta slice añade los endpoints `/api/admin/categories` y `/api/admin/products`
con su CRUD completo, más subida de imágenes para productos. Todo está
protegido por el JWT de la slice anterior y por el rol `Admin`.

> **Decisiones clave**
>
> - **Soft-protect** en lugar de borrado en cascada: borrar una categoría con
>   productos devuelve `409 Conflict`, y borrar un producto con historial de
>   pedidos también — los productos vendidos se ocultan con
>   `IsAvailable = false`, no se eliminan, para preservar la trazabilidad de
>   pedidos pasados.
> - **Defensa en profundidad** para imágenes: MIME + tamaño + magic bytes.
>   Validar solo el `Content-Type` que envía el cliente sería trivial de
>   saltar.
> - **Static files acotados**: el middleware solo expone `/uploads`, no todo
>   `wwwroot`. Si en el futuro hay otros archivos en `wwwroot` no quedarán
>   accesibles por accidente.
> - **Two-phase upload**: primero guardar el archivo en disco, después enviar
>   el comando que actualiza la BD. Si el comando falla (producto no existe,
>   etc.), borramos el archivo recién creado para evitar archivos huérfanos.

---

## 1. Capa `Application`: contratos y excepción nuevas

### `Common/Exceptions/ConflictException.cs`
Excepción específica que el `GlobalExceptionHandler` mapea a `409 Conflict`
con `ProblemDetails`. Sirve para todos los casos donde la operación es
gramaticalmente válida pero choca con el estado del sistema (nombre duplicado,
referencias FK, historial de pedidos…).

### `Common/Interfaces/IFileStorage.cs`
```csharp
Task<string> SaveAsync(Stream content, string extension, string folder, CancellationToken ct);
Task DeleteAsync(string relativeUrl, CancellationToken ct);
```
La capa `Application` no conoce el sistema de archivos: solo sabe que existe
un storage que recibe un stream y devuelve una URL pública relativa. La
implementación local vive en `Infrastructure/Storage/LocalFileStorage.cs`.

> Esto deja el camino abierto a S3 / Azure Blob en el futuro sin tocar handlers.

---

## 2. CRUD de categorías

### Comandos en `Application/Admin/Categories/Commands/`
- `CreateCategoryCommand` → `(Name, DisplayOrder)` → `int` (id).
- `UpdateCategoryCommand` → `(Id, Name, DisplayOrder)` → void.
- `DeleteCategoryCommand` → `(Id)` → void.

### Validaciones (FluentValidation)
- `Name`: requerido, máximo 100 caracteres.
- `DisplayOrder`: ≥ 0.

### Reglas en los handlers
1. **Unicidad de nombre** (case-insensitive). En `Update` se excluye el
   propio registro para que cambiar capitalización del propio nombre no
   colisione consigo mismo.
2. **Borrado protegido**: `Delete` consulta si hay productos asociados; si
   los hay, lanza `ConflictException`. Esto evita el típico `FK violation`
   crudo de PostgreSQL devuelto al cliente como 500.

---

## 3. CRUD de productos

### Comandos en `Application/Admin/Products/Commands/`
- `CreateProductCommand` → `(Name, Description, Price, CategoryId, IsAvailable)` → `int`.
- `UpdateProductCommand` → `(Id, Name, Description, Price, CategoryId, IsAvailable)` → void.
- `DeleteProductCommand` → `(Id)` → void.
- `SetProductImageCommand` → `(ProductId, ImageUrl?)` — null limpia.

### Validaciones
- `Name`: requerido, ≤ 150.
- `Description`: ≤ 1000.
- `Price`: `> 0` y `≤ 9999.99` (techo defensivo contra typos accidentales).
- `CategoryId`: `> 0`.

### Reglas en los handlers
1. **Existencia de categoría**: `Create` y `Update` (cuando cambia
   `CategoryId`) verifican que la categoría existe; si no, `NotFoundException`
   → `404`.
2. **Borrado con historial**: `Delete` revisa `OrderItems.Any(i => i.ProductId == id)`.
   Si hay historial → `ConflictException` con mensaje claro recomendando
   `IsAvailable = false`. Esto preserva pedidos pasados intactos (snapshot
   no se rompe, pero el producto seguirían apareciendo en queries de admin
   referencias).
3. **Limpieza de imagen**: `Delete` borra primero el archivo (`IFileStorage.DeleteAsync`)
   y después el registro. `SetProductImage` borra el archivo viejo si lo había.

---

## 4. Subida de imágenes

### `Infrastructure/Storage/LocalFileStorage.cs`
- `PublicPrefix = "/uploads/"` — todas las URLs públicas comienzan así.
- `SaveAsync`:
  1. **Sanitiza** la carpeta (`alphanumerics + - _`) y la extensión
     (`alphanumerics`). Si llegan caracteres raros, lanza `ArgumentException`.
     Esto convierte cualquier intento de path traversal vía nombres de
     carpeta/extensión en un error fail-fast.
  2. Genera nombre con `Guid.NewGuid():N` — los nombres originales del
     usuario nunca tocan disco. Evita colisiones, evita interpretaciones
     extrañas del nombre por parte del sistema operativo.
  3. Abre el archivo con `FileMode.CreateNew` — si por alguna casualidad
     existiera ese GUID ya, falla en lugar de pisarlo.
- `DeleteAsync`:
  1. Solo procesa URLs que empiecen exactamente con `/uploads/`.
  2. Calcula la ruta absoluta y la **valida** comparando con el root real
     usando `Path.GetFullPath`. Si el path resuelto se sale del root,
     loggea warning y no borra. Defensa contra `../` aunque la URL haya
     sido validada antes.

### `API/Infrastructure/ImageUploadValidator.cs`
Defensa en profundidad para uploads:

| Capa | Qué chequea |
|---|---|
| Tamaño | `MaxBytes = 5 MB`. Limite también propagado al endpoint con `[RequestSizeLimit]`. |
| MIME | Solo `image/jpeg`, `image/png`, `image/webp`. |
| Magic bytes | Lee los primeros bytes y compara con la firma esperada del MIME declarado. |

Validar solo el MIME del cliente sería trivial de saltar (el cliente lo
manda). Validar solo la extensión también. Combinar tamaño + MIME + magic
bytes deja muy poco margen.

---

## 5. Controllers y rutas

### `AdminControllerBase`
Hereda de `ApiControllerBase` y añade
`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]`.
Centralizar la autorización aquí significa que cada controlador admin nuevo
queda protegido sin tener que recordar el atributo.

### `CategoriesAdminController` (`/api/admin/categories`)
- `POST`        → 201 / 400 / 401 / 409
- `PUT {id}`    → 204 / 400 / 401 / 404 / 409
- `DELETE {id}` → 204 / 401 / 404 / 409

### `ProductsAdminController` (`/api/admin/products`)
- `POST`              → 201 / 400 / 401 / 404
- `PUT {id}`          → 204 / 400 / 401 / 404
- `DELETE {id}`       → 204 / 401 / 404 / 409
- `POST {id}/image`   → 200 / 400 / 401 / 404 (multipart/form-data)
- `DELETE {id}/image` → 204 / 401 / 404

El endpoint `POST {id}/image` declara `[Consumes("multipart/form-data")]` y
`[RequestSizeLimit(MaxBytes)]`. La lógica del handler está intencionalmente
**en el controller**: la capa `Application` no toca `IFormFile` ni headers,
solo recibe la URL ya generada vía `SetProductImageCommand`.

### Two-phase con rollback de archivo
```csharp
var url = await _fileStorage.SaveAsync(stream, extension, "products", ct);
try
{
    await Mediator.Send(new SetProductImageCommand(id, url), ct);
}
catch
{
    await _fileStorage.DeleteAsync(url, ct);
    throw;
}
```
Si el producto no existe (404) o el handler explota por cualquier otra
razón, no dejamos archivos huérfanos en disco.

---

## 6. `Program.cs`: registro de `IFileStorage` y static files

### Registro
```csharp
builder.Services.AddSingleton<IFileStorage>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
        ? Path.Combine(env.ContentRootPath, "wwwroot")
        : env.WebRootPath;
    Directory.CreateDirectory(webRoot);
    return new LocalFileStorage(webRoot, sp.GetRequiredService<ILogger<LocalFileStorage>>());
});
```
- `Singleton` porque no tiene estado por request.
- `WebRootPath` puede venir nulo si no existe `wwwroot` aún; lo creamos al
  vuelo para que el primer arranque no explote.

### Static files solo para `/uploads`
```csharp
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=2592000";
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
});
```
- `PhysicalFileProvider` está **anclado al directorio `uploads`**, no a todo
  `wwwroot`. Si mañana metemos algo más en `wwwroot` no queda servido sin
  querer.
- `Cache-Control: public, max-age=2592000` (30 días) — las imágenes nunca
  cambian de URL (cada upload genera un GUID nuevo), así que cachear agresivo
  está OK.
- `X-Content-Type-Options: nosniff` — el navegador respeta el
  `Content-Type` que servimos, sin "adivinar".

---

## 7. Smoke-tests realizados

Con la API arriba en `https://localhost:7262` y un Bearer token válido del
seed admin:

| Caso | Endpoint | Resultado esperado | Resultado real |
|---|---|---|---|
| Sin token | `POST /api/admin/categories` | 401 | ✅ 401 |
| Login válido | `POST /api/admin/auth/login` | 200 + JWT | ✅ 200 |
| Crear categoría | `POST /api/admin/categories` `{Coffee, 1}` | 201 + id | ✅ 201 |
| Nombre duplicado | mismo body otra vez | 409 ConflictException | ✅ 409 |
| Validación | `name=""` | 400 ValidationProblem | ✅ 400 |
| Renombrar | `PUT /api/admin/categories/1` | 204 | ✅ 204 |
| Crear producto | `POST /api/admin/products` válido | 201 + id | ✅ 201 |
| Precio negativo | `price = -1` | 400 | ✅ 400 |
| Categoría inexistente | `categoryId = 999` | 404 NotFound | ✅ 404 |
| Borrar categoría con productos | `DELETE /api/admin/categories/1` | 409 | ✅ 409 |
| Subir PNG válido | `POST /api/admin/products/1/image` | 200 + imageUrl | ✅ 200 |
| Texto disfrazado de PNG | mismo endpoint, fake.png | 400 magic bytes | ✅ 400 |
| MIME GIF no soportado | mismo endpoint, gif | 400 MIME | ✅ 400 |
| Servir imagen estática | `GET /uploads/products/{guid}.png` | 200 image/png | ✅ 200 |
| Borrar imagen | `DELETE /api/admin/products/1/image` | 204 | ✅ 204 |
| Imagen tras borrado | mismo GET | 404 | ✅ 404 |
| Borrar producto sin pedidos | `DELETE /api/admin/products/1` | 204 | ✅ 204 |
| Borrar categoría vacía | `DELETE /api/admin/categories/{empty}` | 204 | ✅ 204 |

---

## 8. Lo que **no** hace esta slice (por diseño)

- No hay endpoint para listar categorías/productos en `/api/admin/...` —
  el admin reutiliza `/api/categories` y `/api/products` (públicos) por
  ahora. Si en el futuro queremos ver ítems no disponibles desde admin,
  se añadirá `GET /api/admin/products?includeUnavailable=true`.
- No hay reordenamiento masivo de `DisplayOrder` (drag & drop). Por ahora
  se actualiza uno a uno.
- No hay versionado de imágenes. Cada upload reemplaza la anterior y
  borra el archivo antiguo.

---

## 9. Próximas slices de Fase 4

1. **Admin orders**: `GET /api/admin/orders` y
   `PATCH /api/admin/orders/{id}/status` (Pending → Preparing → Ready → Completed).
2. **Seed de catálogo placeholder**: 2-3 categorías y 6-8 productos para
   tener datos de demo cuando arranque el front.
3. **SignalR hub** para notificación push al tablet del admin cuando entra
   un pedido nuevo (con sonido en el front).
