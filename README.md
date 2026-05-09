# Glory Cafe API

Backend for **Glory Cafe** (Brisbane, QLD) — online ordering, admin
panel, real-time order notifications. ASP.NET Core 9 with Clean
Architecture (Domain / Application / Infrastructure / API).

The web client lives in a separate repo:
[glorycafe-frontend](https://github.com/Cid-Miguel/glorycafe-frontend).

## Stack

- **Runtime:** .NET 9
- **DB:** PostgreSQL 18 (via Npgsql + EF Core)
- **Auth:** JWT (BCrypt password hashing)
- **Real-time:** SignalR
- **Validation:** FluentValidation + MediatR pipeline
- **Storage:** local filesystem under `wwwroot/uploads` (swap to
  S3/Cloudinary when going to production)

## Local setup

### 1. Install prerequisites

- .NET 9 SDK
- PostgreSQL 18 (running locally on port 5432)

### 2. Restore tools and packages

```powershell
dotnet tool restore
dotnet restore
```

### 3. Configure secrets via .NET User Secrets

The repo intentionally **does not** commit the JWT signing key, the DB
password, or the seed admin password. Set them once per developer:

```powershell
cd src/GloryCafe.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=glorycafedb;Username=postgres;Password=YOURPASSWORD"
dotnet user-secrets set "Jwt:SigningKey" "<paste at least 32 random base64 chars>"
dotnet user-secrets set "AdminSeed:Password" "ChangeMe!2026"
```

Generate a signing key in PowerShell with:
```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

User Secrets are stored under `%APPDATA%\Microsoft\UserSecrets\<id>`
on Windows and never enter the repo.

### 4. Run the API

```powershell
dotnet run --project src/GloryCafe.API
```

On first start it applies migrations and seeds the initial admin
(email + display name come from `appsettings.json`, password from User
Secrets). The admin lands with `MustChangePassword=true`, so the very
first login forces a password change before the dashboard opens.

## Production configuration

In production the same keys are read from environment variables.
.NET maps double-underscores to colons:

| Setting | Env var |
|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `Jwt:SigningKey` | `Jwt__SigningKey` |
| `AdminSeed:Password` | `AdminSeed__Password` |

Render / Fly.io / Azure App Service all expose these as plain env
vars in the dashboard. Never bake secrets into the container image
or `appsettings.Production.json` (that file is gitignored on purpose).

## Branching

- `main` — stable
- `develop` — active development

## Documentation

Each slice has its own design doc under `docs/`:

- `docs/SETUP.md` — Phase 1: solution skeleton
- `docs/PHASE2.md` — Phase 2: entities and first migration
- `docs/PHASE3.md` — Phase 3: public API + security middleware
- `docs/PHASE4-*.md` — Phase 4: admin auth, inventory, orders, seed, SignalR
- `docs/PHASE5-DAILY-ORDER-NUMBER.md` — daily order number with advisory lock
- `docs/PHASE6-FORCED-PASSWORD-ROTATION.md` — forced first-login password change + secrets management
