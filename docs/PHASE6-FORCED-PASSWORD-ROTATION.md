# Fase 6 — Rotación obligatoria de password + secretos fuera del repo

Sprint 1 del roadmap pre-Stripe: cerrar las dos fugas de seguridad
más obvias antes de mostrar la demo a los dueños.

1. **Forced password rotation**: el seed admin nace con un password
   conocido (`ChangeMe!2026`). Cualquiera con acceso al repo lo
   sabría. La primera vez que entra al dashboard, ahora obligamos
   a cambiarlo.
2. **Secretos fuera de `appsettings.Development.json`**: el JWT
   signing key, la connection string y el seed admin password ahora
   viven en .NET User Secrets en dev, y se leen como env vars en
   producción.

> **Decisiones clave**
>
> - **Flag en la entidad, no claim en el JWT**: agregamos
>   `MustChangePassword` directo en `AdminUser`. Si lo pusiéramos
>   solo en el JWT, después del cambio el cliente seguiría con un
>   token "stale" hasta el próximo login.
> - **Validación cliente y servidor refleja owasp**: 12+ caracteres,
>   mezcla de mayúsculas, minúsculas y dígito, distinto del actual.
>   El validator del Application layer es la fuente de verdad; el
>   cliente solo evita un round-trip.
> - **Verificación del current password aunque ya esté autenticado**:
>   si un token se filtra (XSS, cookie robada), el atacante no puede
>   cambiar la contraseña porque no la sabe.
> - **`ICurrentUserService` para acceder al user-id**: el
>   `ChangePasswordCommandHandler` necesita saber quién es el admin
>   logueado sin importar `HttpContext`. La interfaz vive en
>   Application; la implementación que lee el claim `sub` vive en
>   API (donde `IHttpContextAccessor` es legítimo).
> - **Frontend gate, no backend filter** (de momento): si
>   `mustChangePassword=true`, el `RequireAuth` redirige al cambio
>   de password. Un atacante con token podría llamar otros endpoints
>   directamente — endurecimiento futuro: filtro server-side que
>   lea el flag de la DB y bloquee todo excepto el endpoint de
>   cambio. No bloquea el demo.

---

## 1. Forced password rotation

### Cambios en backend

- `AdminUser.MustChangePassword (bool)` — default `true` para que
  cualquier admin nuevo (seed o creado a futuro) pase por la
  pantalla.
- `AdminUserConfiguration` mapea la columna con
  `HasDefaultValue(true)`.
- Migración `AddMustChangePassword` agrega `boolean NOT NULL DEFAULT
  TRUE`. Esto convierte al admin existente en uno que debe rotar la
  próxima vez que entre — perfecto.
- `LoginResult` ahora retorna `MustChangePassword`. El cliente sabe
  inmediatamente si tiene que ir al cambio.
- `ChangePasswordCommand + Handler + Validator`:
  - Verifica el password actual con BCrypt antes de aceptar.
  - Hashea el nuevo y limpia el flag.
  - Endpoint: `POST /api/admin/auth/change-password` (requiere JWT).

### Cambios en frontend

- `useAuthStore` persiste `mustChangePassword` y expone
  `markPasswordChanged()` para limpiarlo después de un cambio
  exitoso.
- `RequireAuth` redirige a `/admin/change-password` si el flag está
  activo. La pantalla de cambio rechaza visitas voluntarias cuando
  el flag está en `false` (volverá a `/admin/orders`).
- `AdminLogin` también respeta el flag: si llegan con sesión válida
  y `mustChangePassword=true`, no rebotan a `/admin/orders`.
- `AdminChangePassword` valida en cliente con las mismas reglas
  que el server (mínimo 12, mix de cases, dígito, distinto del
  actual) y mapea errores 400/401 al campo correcto.

---

## 2. Secretos fuera del repo

### Antes

`appsettings.Development.json` (commiteable como example) traía
literal:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=...;Password=6142"
},
"Jwt": {
  "SigningKey": "LFIdEmXz..."
},
"AdminSeed": {
  "Password": "ChangeMe!2026"
}
```

Esto era un riesgo a futuro: cualquier desarrollador que clonara y
levantara veía secretos reales (aunque sean de dev).

### Ahora

- `appsettings.json` (commited) tiene placeholders **vacíos** para
  los tres secretos.
- `appsettings.Development.json` (commited) trae solo overrides
  no-secretos (sufijo `-dev` en Issuer/Audience).
- Los tres valores reales viven en **.NET User Secrets** por
  developer — encriptados en
  `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>` y nunca
  entran al repo.
- Producción usa env vars (`ConnectionStrings__DefaultConnection`,
  `Jwt__SigningKey`, `AdminSeed__Password`).

### Validación al startup

`Program.cs` rompe el arranque si `Jwt:SigningKey` está vacío o tiene
< 32 caracteres, con un mensaje claro:

> "In development, set it via `dotnet user-secrets set "Jwt:SigningKey" "<random base64>"`. In production, set the env var Jwt__SigningKey."

`DependencyInjection.cs` hace lo mismo para la connection string.

---

## 3. Smoke-tests propuestos

| Caso | Esperado |
|---|---|
| Levantar API sin User Secrets | Falla al arrancar con mensaje sobre `Jwt:SigningKey` |
| Login con admin seed por primera vez | `mustChangePassword: true` en respuesta; cliente redirige a `/admin/change-password` |
| Cambio de password con confirmación incorrecta | 400 con error en campo `confirm` |
| Cambio con password nuevo == actual | 400 con error en `next` |
| Cambio con password de 8 chars | 400 mínimo 12 |
| Cambio exitoso | 204; cliente limpia el flag y entra a `/admin/orders` |
| Re-login con la nueva password | `mustChangePassword: false`; entra directo |
| Intentar entrar a `/admin/orders` con flag activo | Redirige a `/admin/change-password` |
| Visitar `/admin/change-password` con flag en `false` | Redirige a `/admin/orders` |

---

## 4. Lo que queda fuera (hardening posterior)

- **Backend filter** que lea `MustChangePassword` de la DB en cada
  request admin y rechace todo lo que no sea `auth/change-password`.
  Más seguro que solo confiar en el cliente, pero suma una query a
  cada request — vale la pena pasar a una claim cacheada en
  Redis/IDistributedCache si vamos a producción con tráfico real.
- **Password rotation periódica** (cada N días). Owasp ya no la
  recomienda como mandatoria para usuarios humanos, pero para
  cuentas de servicio sigue siendo razonable.
- **MFA**: TOTP en una segunda fase — no bloquea el demo.
- **Lockout** después de N intentos fallidos: el rate limiter de la
  política `login` (5/min) ya nos protege razonablemente; un
  lockout por cuenta sería el siguiente paso.
