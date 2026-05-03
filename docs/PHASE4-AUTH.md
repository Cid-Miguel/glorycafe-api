# Fase 4 — Auth admin (slice 1: login con JWT)

Esta slice añade autenticación de administradores con JWT (HMAC SHA-256) y un
seed automático del primer admin al arrancar la aplicación. Los endpoints de
admin para gestionar pedidos e inventario llegan en la siguiente slice.

> **Recordatorio de seguridad**
>
> No hay registro público de admins. Las cuentas se crean por **seed** desde
> configuración (`AdminSeed`). En producción la contraseña real no va al repo
> — se inyecta por variable de entorno o secret manager.

---

## 1. Paquetes NuGet añadidos

| Proyecto | Paquete | Versión | Para qué |
|---|---|---|---|
| `Infrastructure` | `BCrypt.Net-Next` | 4.1 | Hash y verificación de passwords. |
| `Infrastructure` | `System.IdentityModel.Tokens.Jwt` | 8.2 | Construcción de JWT firmados. |
| `Infrastructure` | `Microsoft.IdentityModel.Tokens` | 8.2 | Llaves y algoritmos de firma. |
| `Infrastructure` | `Microsoft.Extensions.Options.ConfigurationExtensions` | 9.0 | `services.Configure<T>(IConfigurationSection)`. |
| `API` | `Microsoft.AspNetCore.Authentication.JwtBearer` | 9.0 | Middleware que valida el `Authorization: Bearer ...`. |

---

## 2. Abstracciones en `Application`

La capa `Application` no debe saber de BCrypt ni de JWT. Solo conoce
**contratos**:

### `Common/Interfaces/IPasswordHasher.cs`
```csharp
string Hash(string password);
bool Verify(string password, string passwordHash);
```

### `Common/Interfaces/IJwtTokenGenerator.cs`
```csharp
JwtToken Generate(AdminUser user);
public record JwtToken(string AccessToken, DateTime ExpiresAtUtc);
```

### `Common/Exceptions/InvalidCredentialsException.cs`
Excepción específica para email/password incorrectos. Mensaje genérico
("`Invalid email or password.`") para **no filtrar** si fue el email o la
contraseña lo que falló.

---

## 3. `LoginCommand`

### `Application/Admin/Auth/Login/`
- `LoginCommand.cs` — `record (string Email, string Password)` → `LoginResult`.
- `LoginCommandValidator.cs` — email válido, password no vacío, ambos limit
  a 200 caracteres (defensa contra DoS por payload).
- `LoginCommandHandler.cs` — busca por email **normalizado** (lowercase + trim),
  verifica el hash, y si todo bien delega al `IJwtTokenGenerator`.

### Reglas de seguridad implementadas en el handler

1. **Email normalizado**: `Trim().ToLowerInvariant()` antes del lookup. Evita
   duplicados o bypasses por capitalización.
2. **Mismo mensaje para usuario inexistente y contraseña errada**. Nunca
   distinguir "ese email no existe" de "esa contraseña es incorrecta": filtrar
   esa diferencia es un vector de enumeración de cuentas.
3. **Verificación con BCrypt**: el hash incluye el salt y el work factor.
   `Verify` es **timing-safe** (comparación constant-time).
4. **No se loguea la password** en ningún punto.

---

## 4. Implementaciones en `Infrastructure/Auth/`

### `JwtSettings.cs`
POCO bindeado desde `appsettings`:
```csharp
public string Issuer { get; set; }
public string Audience { get; set; }
public string SigningKey { get; set; }
public int AccessTokenMinutes { get; set; } = 60;
```

### `BcryptPasswordHasher.cs`
- `WorkFactor = 12` (~250 ms por hash en hardware moderno → suficiente
  resistencia a fuerza bruta sin penalizar UX de login normal).
- `Verify` envuelto en `try/catch` para devolver `false` ante hashes
  malformados (defense in depth).

### `JwtTokenGenerator.cs`
- Algoritmo: **HMAC SHA-256** (`HS256`).
- Claims emitidos:
  - `sub` = `user.Id`
  - `email` = `user.Email`
  - `jti` = GUID aleatorio (token único, base para revocación futura)
  - `name` = `user.DisplayName`
  - `role` = `"Admin"`
- `nbf` = ahora, `exp` = ahora + `AccessTokenMinutes`.
- Issuer y Audience se validan en el middleware (ver sección 6).

---

## 5. Seed automático del primer admin

### `Persistence/Seeding/AdminSeedSettings.cs`
```csharp
public string Email { get; set; }
public string Password { get; set; }
public string DisplayName { get; set; }
```

### `Persistence/Seeding/DbInitializer.cs`
Se ejecuta al arrancar la app (`await app.Services.InitializeDatabaseAsync()`)
y hace dos cosas:

1. `MigrateAsync()` — aplica migraciones pendientes automáticamente.
2. `SeedAdminAsync()`:
   - Si `AdminSeed:Email` o `AdminSeed:Password` están vacíos → log warning y
     skip (no falla, útil en CI).
   - Si ya existe un admin con ese email → no hace nada (idempotente).
   - Si no existe → crea el usuario con `IPasswordHasher.Hash(password)`.

> **Por qué seed via configuración y no comando manual**: el cliente final
> (Glory Cafe) no tiene un dev cerca cuando se hace el primer deploy. El
> seed se ejecuta en cada arranque, es idempotente, y la contraseña inicial
> se inyecta por variable de entorno o `appsettings.Production.json`.

---

## 6. Middleware JWT en `Program.cs`

```csharp
var jwt = jwtSection.Get<JwtSettings>();

if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
```

### Decisiones explicadas

- **`RequireHttpsMetadata = !IsDevelopment()`**: en producción exige HTTPS para
  intercambiar tokens. En dev se permite HTTP por practicidad.
- **`SaveToken = false`**: no persiste el token en `HttpContext` (menos
  superficie). Si se necesitara más adelante, se cambia.
- **`ValidateIssuer/Audience/Lifetime/SigningKey = true`**: los cuatro pilares
  de validación de un JWT. Sin esto un atacante podría forjar tokens triviales.
- **`ClockSkew = 30s`**: tolerancia mínima al desfase de reloj entre servidor y
  cliente. El default (5 min) es excesivo y alarga la vida útil de tokens
  vencidos.
- **Validación al arranque**: si `SigningKey` tiene menos de 32 caracteres, la
  app **no arranca**. HMAC SHA-256 con clave corta es atacable.

### Pipeline (orden importa)
```
UseAuthentication  ← antes de Authorization
UseAuthorization
MapControllers
```

---

## 7. `AdminAuthController`

`Controllers/Admin/AdminAuthController.cs` con ruta explícita
`[Route("api/admin/auth")]` (sobreescribe la convención `api/[controller]`).

Endpoint:
- `POST /api/admin/auth/login` → 200 / 400 / 401 / 429.
- `[EnableRateLimiting("login")]`: **5 intentos / minuto / IP**, mucho más
  estricto que el resto. Esta es la defensa principal contra fuerza bruta de
  contraseñas.

---

## 8. Mapeo de excepciones (`GlobalExceptionHandler`)

Se añadió:
| Excepción | Status | Title |
|---|---|---|
| `InvalidCredentialsException` | **401** | "Invalid credentials" |

Sigue devolviendo `ProblemDetails` con `traceId` para correlación en logs.

---

## 9. Configuración

### `appsettings.Development.json` (no se commitea, está en `.gitignore`)
```json
{
  "ConnectionStrings": { "DefaultConnection": "..." },
  "Jwt": {
    "Issuer": "glorycafe-api-dev",
    "Audience": "glorycafe-admin-dev",
    "SigningKey": "<48 bytes random base64>",
    "AccessTokenMinutes": 60
  },
  "AdminSeed": {
    "Email": "admin@glorycafe.local",
    "Password": "ChangeMe!2026",
    "DisplayName": "Glory Cafe Admin"
  }
}
```

### `appsettings.Development.example.json` (sí se commitea)
Misma estructura con valores placeholder. Cuando alguien clona el repo,
hace `cp appsettings.Development.example.json appsettings.Development.json`
y completa.

### Generación de la signing key
```powershell
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$bytes = New-Object byte[] 48
$rng.GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```
**Nunca** uses una clave fija, copiada de internet, o derivada de algo
predecible. **Para producción** la clave debe inyectarse por variable de
entorno (`Jwt__SigningKey`) o un secret manager — nunca en archivos del repo.

---

## 10. Smoke test (verificado)

| Request | Status | Body |
|---|---|---|
| `POST /api/admin/auth/login` con credenciales correctas | **200** | `{ accessToken, expiresAtUtc, displayName }` |
| Password incorrecta | **401** | `ProblemDetails` "Invalid email or password." |
| Email inexistente | **401** | **mismo mensaje** ↑ (no enumeración) |
| Body inválido (email malformado, password vacío) | **400** | `ValidationProblemDetails` con `errors` por campo |

El JWT devuelto se decodifica en [jwt.io](https://jwt.io) (no pegar el token
real ahí — es válido durante 60 minutos). Contiene los claims `sub`, `email`,
`jti`, `name`, `role: Admin`, `iss`, `aud`, `nbf`, `exp`.

---

## 11. Archivos creados/modificados

```
src/GloryCafe.Application/
  Admin/Auth/Login/
    LoginCommand.cs                                     (nuevo)
    LoginCommandValidator.cs                            (nuevo)
    LoginCommandHandler.cs                              (nuevo)
  Common/Interfaces/
    IPasswordHasher.cs                                  (nuevo)
    IJwtTokenGenerator.cs                               (nuevo)
  Common/Exceptions/
    InvalidCredentialsException.cs                      (nuevo)

src/GloryCafe.Infrastructure/
  DependencyInjection.cs                                (modificado)
  Auth/
    JwtSettings.cs                                      (nuevo)
    BcryptPasswordHasher.cs                             (nuevo)
    JwtTokenGenerator.cs                                (nuevo)
  Persistence/Seeding/
    AdminSeedSettings.cs                                (nuevo)
    DbInitializer.cs                                    (nuevo)
  GloryCafe.Infrastructure.csproj                       (paquetes)

src/GloryCafe.API/
  Program.cs                                            (modificado)
  appsettings.Development.example.json                  (modificado)
  Controllers/Admin/
    AdminAuthController.cs                              (nuevo)
  Infrastructure/
    GlobalExceptionHandler.cs                           (modificado: +401)
  GloryCafe.API.csproj                                  (paquetes)
```

---

## 12. Lo que **no** está en esta slice

- **Refresh tokens**: por ahora un solo access token de 60 min. La tablet
  pide login de nuevo cuando expira. Si en uso real es molesto, se añade.
- **`/me` endpoint**: aún no necesario; el frontend ya conoce su `displayName`
  desde la respuesta del login.
- **Logout**: al ser stateless (JWT firmado), el "logout" es client-side
  (borrar el token). Para revocación real haría falta una blacklist de `jti`s.
- **Endpoints admin protegidos**: la siguiente slice de Fase 4.

---

## 13. Próximos pasos (siguiente slice)

1. Endpoints **`/api/admin/orders`** (listar con filtros, marcar completada).
2. CRUD de **categorías** y **productos** (`[Authorize]`).
3. Upload de imágenes (multipart) → `wwwroot/uploads/`.
4. Seed de catálogo placeholder para probar end-to-end.

Después, SignalR para push a la tablet.
