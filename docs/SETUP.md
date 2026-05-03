# Glory Cafe — Setup Documentation

This document explains, step by step, **every command, every file and every decision** taken to set up the Glory Cafe project. It is meant as a learning reference: you should be able to delete the whole `GloryCafe/` folder and recreate it from scratch using only this file.

---

## Table of contents

1. [Goals of Phase 1](#goals-of-phase-1)
2. [Prerequisites and tooling](#prerequisites-and-tooling)
3. [Folder structure](#folder-structure)
4. [Git initialization](#git-initialization)
5. [Why Clean Architecture](#why-clean-architecture)
6. [Creating the .NET solution](#creating-the-net-solution)
7. [Project references](#project-references)
8. [NuGet packages](#nuget-packages)
9. [Code files explained](#code-files-explained)
10. [Configuration files](#configuration-files)
11. [Verifying the build](#verifying-the-build)
12. [Glossary](#glossary)
13. [Next phase preview](#next-phase-preview)

---

## Goals of Phase 1

In this phase we only build the **skeleton** of the backend. No business entities, no endpoints, no migrations yet. The goal is to leave a clean, conventional foundation so every future feature has an obvious place to live.

Concretely, after Phase 1 you have:

- A working Git repo on branch `develop` with a clean ignore list.
- A .NET 9 solution with four projects following Clean Architecture.
- Entity Framework Core wired up against PostgreSQL via Npgsql.
- A connection string in development settings, kept out of Git.
- The whole thing compiles with zero warnings and zero errors.

---

## Prerequisites and tooling

| Tool         | Version used   | Purpose                                              |
|--------------|----------------|------------------------------------------------------|
| .NET SDK     | 9.0.313        | Builds and runs the backend                          |
| Git          | 2.53           | Version control                                      |
| Node.js      | 20.13.1        | Will be used for the React frontend (later phase)    |
| PostgreSQL   | 18.3           | Database, runs as Windows service `postgresql-x64-18` |

Verify them at any time with:

```bash
dotnet --version
git --version
node --version
"/c/Program Files/PostgreSQL/18/bin/psql.exe" --version
```

---

## Folder structure

We created this layout:

```
GloryCafe/
├── GloryCafeAPI/                        Backend solution
│   ├── GloryCafe.sln                    Solution file referencing all .NET projects
│   ├── src/
│   │   ├── GloryCafe.Domain/            Pure business entities, no dependencies
│   │   ├── GloryCafe.Application/       Use cases, interfaces, DTOs
│   │   ├── GloryCafe.Infrastructure/    DB, external services, EF Core
│   │   └── GloryCafe.API/               HTTP entry point, controllers, DI composition
│   └── tests/                           (empty for now, unit/integration tests later)
├── glory-cafe-web/                      Frontend (created later)
├── docs/
│   └── SETUP.md                         This file
├── .gitignore
└── README.md
```

**Why `src/` and `tests/` subfolders?** Standard .NET convention — keeps production code separate from test code so that test packages are never accidentally shipped, and so that CI can run `dotnet test` only against `tests/`.

The folder was created with:

```bash
mkdir -p "GloryCafe/GloryCafeAPI/src" \
         "GloryCafe/GloryCafeAPI/tests" \
         "GloryCafe/glory-cafe-web" \
         "GloryCafe/docs"
```

`mkdir -p` creates parent directories as needed and does not error if any already exist.

---

## Git initialization

```bash
git init -b main
```

`-b main` initializes the repo with the default branch named `main` (otherwise newer Git defaults to `master`/`main` depending on global config — `-b` is explicit).

```bash
git add .gitignore README.md
git commit -m "chore: initial repo with .gitignore and README"
```

Standard "initial commit" on `main`. We commit the ignore file *before* anything else so we never accidentally track build artefacts.

```bash
git checkout -b develop
```

Creates and switches to a `develop` branch. From here on, all work for Phase 1 happens on `develop`. `main` stays as the stable branch — only PRs from `develop` (after testing) ever land there.

### `.gitignore`

The ignore file blocks four families of files:

| Group         | Why                                                                           |
|---------------|--------------------------------------------------------------------------------|
| .NET (`bin/`, `obj/`, `.vs/`, etc.)   | Build artefacts, regenerated on every `dotnet build`. No need to version. |
| Node (`node_modules/`, `dist/`)        | Installed via `npm install`, gigantic and reproducible.                  |
| Secrets (`appsettings.Development.json`, `*.env`, `secrets.json`) | Contain DB passwords, JWT keys, Stripe secrets — must never reach the repo. |
| OS junk (`.DS_Store`, `Thumbs.db`)     | Local OS metadata.                                                       |
| Local uploads (`wwwroot/uploads/`)     | User-uploaded product images during development.                         |

> Note: `appsettings.Development.json` is **ignored**. Production deployments use environment variables or a secret manager instead. Local devs each generate their own copy.

---

## Why Clean Architecture

The four projects map to the four "rings" of Clean Architecture. The rule is simple: **dependencies point inward only**.

```
   ┌────────────────────────────────────────┐
   │             API (outermost)            │  HTTP, JSON, controllers
   │   ┌──────────────────────────────────┐ │
   │   │         Infrastructure           │ │  EF Core, Npgsql, SignalR, Stripe
   │   │   ┌──────────────────────────┐   │ │
   │   │   │       Application        │   │ │  Use cases, interfaces, DTOs
   │   │   │   ┌──────────────────┐   │   │ │
   │   │   │   │     Domain       │   │   │ │  Entities, value objects, rules
   │   │   │   └──────────────────┘   │   │ │
   │   │   └──────────────────────────┘   │ │
   │   └──────────────────────────────────┘ │
   └────────────────────────────────────────┘
```

| Layer              | Knows about         | Does NOT know about            |
|--------------------|---------------------|--------------------------------|
| **Domain**         | Nothing             | EF Core, HTTP, anything        |
| **Application**    | Domain              | EF Core, HTTP, controllers     |
| **Infrastructure** | Application, Domain | Controllers, HTTP              |
| **API**            | Application, Infrastructure (Domain transitively) | — |

**Why does this matter?** If we ever swap PostgreSQL for SQL Server, only `Infrastructure` changes. If we replace the REST API with gRPC, only `API` changes. Business rules in `Domain` and `Application` stay untouched. That is what "the database is a detail" means in Clean Architecture.

---

## Creating the .NET solution

A **solution** (`.sln`) is just a file that groups projects together so you can build/restore them as one unit.

```bash
dotnet new sln -n GloryCafe
```

`dotnet new` is the project/template generator. `sln` is the template name. `-n GloryCafe` sets the file name (`GloryCafe.sln`). We ran this from inside `GloryCafeAPI/` so the solution lives there.

Then the four projects, all targeting **.NET 9** (`-f net9.0`):

```bash
dotnet new classlib -n GloryCafe.Domain        -f net9.0
dotnet new classlib -n GloryCafe.Application   -f net9.0
dotnet new classlib -n GloryCafe.Infrastructure -f net9.0
dotnet new webapi   -n GloryCafe.API           -f net9.0 --use-controllers
```

| Template     | What it generates                                            |
|--------------|--------------------------------------------------------------|
| `classlib`   | Library project. Compiles to a `.dll` you can reference. Used for Domain, Application, Infrastructure because they hold logic, not entry points. |
| `webapi`     | Executable ASP.NET Core project with `Program.cs`, default OpenAPI/Swagger setup. Used for the API. |

`--use-controllers` tells the `webapi` template to scaffold a controller-based project (instead of minimal-API style). This matches a Clean Architecture project where controllers act as a thin HTTP shell over Application use cases.

Add all projects to the solution so `dotnet build` from the solution root builds everything:

```bash
dotnet sln add src/GloryCafe.Domain/GloryCafe.Domain.csproj
dotnet sln add src/GloryCafe.Application/GloryCafe.Application.csproj
dotnet sln add src/GloryCafe.Infrastructure/GloryCafe.Infrastructure.csproj
dotnet sln add src/GloryCafe.API/GloryCafe.API.csproj
```

The `webapi` template also creates a `WeatherForecast.cs` and `WeatherForecastController.cs` as demos. We deleted both — they have nothing to do with the project and would only clutter Swagger.

Each `classlib` template creates an empty `Class1.cs`. We deleted those for the same reason.

---

## Project references

Wire up dependencies according to the Clean Architecture rule (inward only):

```bash
dotnet add GloryCafe.Application/GloryCafe.Application.csproj \
       reference GloryCafe.Domain/GloryCafe.Domain.csproj

dotnet add GloryCafe.Infrastructure/GloryCafe.Infrastructure.csproj \
       reference GloryCafe.Application/GloryCafe.Application.csproj
# Infrastructure transitively gets Domain through Application — no explicit reference needed.

dotnet add GloryCafe.API/GloryCafe.API.csproj \
       reference GloryCafe.Application/GloryCafe.Application.csproj \
                 GloryCafe.Infrastructure/GloryCafe.Infrastructure.csproj
```

**Why does the API reference Infrastructure directly?** Only at the *composition root* — `Program.cs` — do we wire up which concrete implementations satisfy which interfaces. Controllers will only ever depend on Application interfaces, never on Infrastructure types.

---

## NuGet packages

NuGet is .NET's package manager. We added three packages:

### `Npgsql.EntityFrameworkCore.PostgreSQL` (v9.0.4) — added to Infrastructure

```bash
dotnet add GloryCafe.Infrastructure/GloryCafe.Infrastructure.csproj \
       package Npgsql.EntityFrameworkCore.PostgreSQL --version 9.0.4
```

This is the EF Core **provider** for PostgreSQL. It plugs into EF Core and translates LINQ queries into SQL that PostgreSQL understands, manages the Npgsql connection, and supports PostgreSQL-specific types (jsonb, arrays, etc.).

> **Why pin version 9.0.4 and not the latest?** The latest (10.x) requires .NET 10. We are on .NET 9, so we use the 9.x line.

### `Microsoft.EntityFrameworkCore.Design` (v9.0.0) — added to Infrastructure and API

```bash
dotnet add GloryCafe.Infrastructure/GloryCafe.Infrastructure.csproj \
       package Microsoft.EntityFrameworkCore.Design --version 9.0.0
dotnet add GloryCafe.API/GloryCafe.API.csproj \
       package Microsoft.EntityFrameworkCore.Design --version 9.0.0
```

Provides design-time helpers for EF Core tooling — required by `dotnet ef migrations add`, `dotnet ef database update`, etc. `dotnet ef` runs against the API project (the executable), but it needs the design package in both the executable and the project that owns the DbContext.

### `Microsoft.Extensions.DependencyInjection.Abstractions` (v9.0.0) — added to Application

```bash
dotnet add GloryCafe.Application/GloryCafe.Application.csproj \
       package Microsoft.Extensions.DependencyInjection.Abstractions --version 9.0.0
```

Gives Application access to `IServiceCollection` so it can expose its own `AddApplication()` extension method.

---

## Code files explained

### `Application/Common/Interfaces/IApplicationDbContext.cs`

```csharp
namespace GloryCafe.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**Why an interface?** Application code (use cases, services) must not depend on EF Core directly. By declaring this interface in Application, our future use cases will inject `IApplicationDbContext` and stay swappable/testable. The concrete `ApplicationDbContext` lives in Infrastructure and implements this interface.

For now it only exposes `SaveChangesAsync`. As entities are added in Phase 2, we will extend it with `DbSet<>` properties (`DbSet<Product> Products { get; }`, etc.).

### `Infrastructure/Persistence/ApplicationDbContext.cs`

```csharp
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

The concrete EF Core `DbContext`. It implements `IApplicationDbContext` so callers from Application can use it via the interface.

`ApplyConfigurationsFromAssembly` auto-loads any `IEntityTypeConfiguration<T>` classes we put under Infrastructure (Phase 2 onwards). That keeps each entity's mapping in its own file instead of one giant `OnModelCreating`.

### `Infrastructure/DependencyInjection.cs`

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' not found in configuration.");

    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    services.AddScoped<IApplicationDbContext>(sp =>
        sp.GetRequiredService<ApplicationDbContext>());

    return services;
}
```

Extension method that registers everything Infrastructure provides. The API project calls `services.AddInfrastructure(builder.Configuration)` and gets the DbContext + interface mapping for free.

`AddScoped<IApplicationDbContext>` resolves to the same `ApplicationDbContext` instance per HTTP request, so the interface and the concrete class share lifetime — both refer to the same connection.

### `Application/DependencyInjection.cs`

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    return services;
}
```

Empty for now. The shape exists so Phase 2 can register MediatR, FluentValidation, AutoMapper, etc. without touching `Program.cs` again.

### `API/Program.cs`

```csharp
using GloryCafe.Application;
using GloryCafe.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

The composition root. Top-level statements (no explicit `Main` method) — this is a .NET 6+ convention. The pipeline:

1. `AddControllers()` — enables MVC-style controllers.
2. `AddOpenApi()` — adds the OpenAPI document generator (the .NET 9 default; replaces Swashbuckle).
3. `AddApplication()` / `AddInfrastructure()` — our extension methods.
4. `MapOpenApi()` — exposes `/openapi/v1.json` only in development.
5. `UseHttpsRedirection()` — sends HTTP traffic to HTTPS.
6. `UseAuthorization()` — middleware placeholder; will become meaningful once JWT is added.
7. `MapControllers()` — registers endpoints from controller classes.

---

## Configuration files

### `appsettings.json` (committed)

Contains non-secret defaults. The connection string is **empty** here so production deployments are forced to inject one via environment variable.

### `appsettings.Development.json` (NOT committed)

Used only when `ASPNETCORE_ENVIRONMENT=Development`. Holds your local PostgreSQL connection string.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=glorycafedb;Username=postgres;Password=REPLACE_WITH_YOUR_POSTGRES_PASSWORD"
  }
}
```

**Replace `REPLACE_WITH_YOUR_POSTGRES_PASSWORD` with your actual `postgres` user password.** This file is in `.gitignore`, so the password never leaves the machine.

### `appsettings.Development.example.json` (committed)

A template with a fake password. New developers copy this to `appsettings.Development.json` and fill in their real password. This way the repo documents the required shape without leaking secrets.

### Connection string anatomy

```
Host=localhost;Port=5432;Database=glorycafedb;Username=postgres;Password=...
```

| Key       | Meaning                                                                |
|-----------|------------------------------------------------------------------------|
| Host      | Server hostname. `localhost` for local dev.                            |
| Port      | PostgreSQL default is `5432`.                                          |
| Database  | Will be `glorycafedb` (created in Phase 2 with the first migration).   |
| Username  | `postgres` is the default superuser created on install.                |
| Password  | Whatever was set during PostgreSQL installation.                       |

---

## Verifying the build

From `GloryCafeAPI/`:

```bash
dotnet build
```

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`.

What this proves:

- All four projects compile.
- Project references are valid.
- All NuGet packages restore.
- The DI extension methods type-check against EF Core's API.

Running the API at this stage will succeed, but there are no endpoints to call. The real "first run" happens in Phase 2 once we have a controller.

---

## Glossary

- **Solution (`.sln`)** — File grouping multiple .NET projects.
- **Project (`.csproj`)** — A single buildable unit; produces a `.dll` or `.exe`.
- **NuGet** — .NET package manager, equivalent to npm or pip.
- **EF Core** — Microsoft's ORM (Object-Relational Mapper). Translates C# objects/queries into SQL.
- **DbContext** — EF Core class representing a session with the database.
- **Provider** — An EF Core plug-in for a specific DB engine (Npgsql for PostgreSQL, SqlServer for SQL Server, etc.).
- **DI (Dependency Injection)** — Pattern where dependencies are passed into a class instead of created inside it. ASP.NET Core's `IServiceCollection` is the built-in DI container.
- **Composition root** — The single place (`Program.cs`) where all interfaces are bound to concrete implementations.
- **Migration** — A versioned, code-first description of database schema changes; coming in Phase 2.
- **Connection string** — String describing how to reach the database (host, port, user, password).

---

## Next phase preview

**Phase 2 — Domain entities & first migration** is documented in [`PHASE2.md`](PHASE2.md).

It covers:

1. Defining the core entities in `Domain/Entities`: `Category`, `Product`, `Order`, `OrderItem`, `AdminUser`.
2. Adding EF Core configurations under `Infrastructure/Persistence/Configurations/`.
3. Adding `DbSet<>` properties to `ApplicationDbContext` and `IApplicationDbContext`.
4. Running `dotnet ef migrations add InitialCreate` to generate the first migration.
5. Running `dotnet ef database update` to create `glorycafedb` and tables in PostgreSQL.

Before starting Phase 2 you must replace the placeholder password in `appsettings.Development.json` with your real PostgreSQL `postgres` user password.
