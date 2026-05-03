# Glory Cafe — Phase 2: Domain entities and first migration

This document explains, step by step, **every command, every file and every decision** for Phase 2. Same structure and depth as [`SETUP.md`](SETUP.md). You should be able to delete the `Domain/Entities`, `Infrastructure/Persistence/Configurations`, the migration files, and the database, and recreate everything from scratch using only this file.

---

## Table of contents

1. [Goals of Phase 2](#goals-of-phase-2)
2. [What we are modeling](#what-we-are-modeling)
3. [Entity decisions and trade-offs](#entity-decisions-and-trade-offs)
4. [Installing the EF Core tool locally](#installing-the-ef-core-tool-locally)
5. [Domain entities explained](#domain-entities-explained)
6. [Exposing DbSets through the interface](#exposing-dbsets-through-the-interface)
7. [EF Core configurations explained](#ef-core-configurations-explained)
8. [Generating the first migration](#generating-the-first-migration)
9. [Applying the migration](#applying-the-migration)
10. [Verifying the database](#verifying-the-database)
11. [What you can do now](#what-you-can-do-now)
12. [Glossary additions](#glossary-additions)
13. [Next phase preview](#next-phase-preview)

---

## Goals of Phase 2

Take the empty backend skeleton from Phase 1 and:

- Define the **business model** (the entities) in `Domain`.
- Tell EF Core how to map each entity to a PostgreSQL table.
- Generate the first **migration** (a versioned SQL script).
- Run the migration so the `glorycafedb` database and its tables actually exist.

After this phase the database is ready to receive real data. We still don't have HTTP endpoints — those land in Phase 3.

---

## What we are modeling

The cafe needs to track five concepts:

| Entity        | Represents                                                          |
|---------------|---------------------------------------------------------------------|
| **Category**  | A section in the Shop ("Drinks", "Sandwiches", …).                  |
| **Product**   | A purchasable item (name, price, image, availability).              |
| **Order**     | A customer checkout: who, when, total, status, payment ID.          |
| **OrderItem** | A line inside an order (which product, quantity, snapshot of price).|
| **AdminUser** | Login credentials for the tablet at the cafe.                       |

Plus one supporting type:

- **OrderStatus** (enum) — `Pending`, `Paid`, `Completed`, `Cancelled`.

And one base class:

- **BaseEntity** — shared `Id` and `CreatedAt` so every table follows the same shape.

---

## Entity decisions and trade-offs

These are the design calls made while modeling, and **why**:

### Integer IDs, not GUIDs

Auto-increment integers are simpler, smaller and faster to index than GUIDs. The trade-off is that they expose order numbers to enumeration — if order URLs ever become public we will add a separate `PublicOrderCode` field rather than switch the primary key.

### Single inheritance via `BaseEntity`

Every table has `Id` and `CreatedAt`. Putting them on a base class means we set the column defaults once in EF configurations and forget about it.

### `OrderItem.ProductNameSnapshot` and `OrderItem.UnitPrice`

Instead of trusting that `Product.Name` and `Product.Price` will stay the same forever, we **copy** them onto the order line at the time of purchase. If the cafe later raises the price of a latte, old receipts still show what the customer actually paid. This is called a "snapshot" or "capture" pattern, and is standard for any line-item that touches money.

### `OrderStatus` stored as text, not integer

Internally it's a C# enum (compact and type-safe). In the database we store it as a string (`"Pending"`, `"Completed"`). That way you can run `SELECT * FROM orders WHERE status = 'Pending'` directly in psql without remembering that `0` means pending.

### `IsAvailable` boolean, not `Stock` integer

The dueños just want a "sold out" toggle, not real inventory counting. A simple boolean is right. If they ever ask for actual stock numbers, we add an `int Stock` column — non-breaking change.

### `CustomerPhone` AND `CustomerEmail`, both nullable

Per the spec, the customer fills in **phone and/or email**. At least one is required, but the database doesn't enforce that — we'll do it at the application/validation layer in Phase 3.

### `decimal` for money, mapped to `numeric(10,2)`

Never store currency as `float` or `double` — they round in inexact ways. `decimal` in C# maps to PostgreSQL `numeric(10,2)`: up to 8 digits before the decimal point, 2 after. Plenty for any item under $1,000,000.00.

### `ON DELETE` rules

- **Category → Products**: `Restrict`. You cannot delete a category that still has products. This forces the admin to deal with stragglers explicitly.
- **Order → OrderItems**: `Cascade`. Deleting an order deletes its line items. They have no meaning on their own.
- **Product → OrderItems**: `Restrict`. You cannot delete a product that appears in any historical order — that would corrupt receipts.

### `CreatedAt` defaults at the database level

We set `HasDefaultValueSql("CURRENT_TIMESTAMP")` in every configuration. PostgreSQL fills the timestamp itself on insert, so application code doesn't have to remember.

---

## Installing the EF Core tool locally

EF Core ships a CLI tool (`dotnet-ef`) for generating migrations and applying them. The version of the tool must match the version of EF Core in the project.

You already have a global tool at version `10.0.5` (from another project), but Glory Cafe uses EF Core `9.0.x`. Mixing versions can cause subtle bugs, so we install a **local** tool pinned to the project.

```bash
cd GloryCafeAPI
dotnet new tool-manifest
```

`dotnet new tool-manifest` creates `.config/dotnet-tools.json`, a per-repo manifest declaring which CLI tools the repo expects. Anyone who clones the repo and runs `dotnet tool restore` gets the exact same tools — no "works on my machine" issues.

```bash
dotnet tool install dotnet-ef --version 9.0.0
```

This adds `dotnet-ef` 9.0.0 to the manifest. From inside the manifest folder, `dotnet ef ...` resolves to the local copy first. The local install is checked into Git via `.config/dotnet-tools.json` — no executable is committed, only the manifest.

**Why this matters:** Phase 1 had no tool manifest, so `dotnet ef` ran the global 10.x against EF Core 9.x. With the manifest in place, the right version always runs.

---

## Domain entities explained

All under `src/GloryCafe.Domain/`. Domain is the inner ring of Clean Architecture: **no references to EF Core, ASP.NET, or anything else**. These are plain C# classes describing business concepts.

### `Domain/Common/BaseEntity.cs`

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

Abstract because you should never instantiate "a base entity" by itself. Every concrete entity inherits from it.

### `Domain/Enums/OrderStatus.cs`

```csharp
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Completed = 2,
    Cancelled = 3
}
```

Explicit numbers so re-ordering the values later does not silently change stored data (even though we store as text — defensive).

### `Domain/Entities/Category.cs`

```csharp
public class Category : BaseEntity
{
    public string Name { get; set; } = null!;
    public int DisplayOrder { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
```

`= null!` is the **null-forgiving operator**. It tells the C# compiler "trust me, this will be set before anyone reads it" — required because nullable reference types are on by default in .NET 9 and the property is non-nullable but has no constructor setting it. EF Core fills it via reflection.

`DisplayOrder` lets the admin reorder categories (Drinks before Food, etc.) without renaming them.

### `Domain/Entities/Product.cs`

```csharp
public class Product : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
```

`ImageUrl` is `string?` (nullable) because a brand-new product may not have an image uploaded yet.

`CategoryId` + `Category` together describe the **navigation property** pattern: the `int` is the foreign key column actually stored in the DB; the `Category` reference is what EF Core fills in when you query with `.Include(p => p.Category)`.

### `Domain/Entities/Order.cs`

```csharp
public class Order : BaseEntity
{
    public string CustomerFirstName { get; set; } = null!;
    public string CustomerLastName { get; set; } = null!;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    public DateTime EstimatedPickupTime { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }

    public string? StripePaymentIntentId { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
```

`Status = OrderStatus.Pending` sets the C#-side default. We **also** set the DB-side default via configuration so a hand-crafted `INSERT` from psql is correct too.

`StripePaymentIntentId` is the ID Stripe gives us when the customer pays. We store it for refunds and reconciliation.

### `Domain/Entities/OrderItem.cs`

```csharp
public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string ProductNameSnapshot { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```

Two foreign keys (Order, Product) and three captured fields (name, price, qty). The "snapshot" idea explained earlier lives here.

### `Domain/Entities/AdminUser.cs`

```csharp
public class AdminUser : BaseEntity
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}
```

We never store `Password` — only `PasswordHash`. The hash is computed in Phase 4 with BCrypt or ASP.NET Identity. `DisplayName` is what shows in the admin tablet ("Hi, Maria").

---

## Exposing DbSets through the interface

### Why Application takes a dependency on EF Core

In strict Clean Architecture, Application would not know EF Core exists. In practice, every popular Clean Architecture template (Jason Taylor's, Microsoft's eShop) **does** let Application reference `Microsoft.EntityFrameworkCore` because:

- `DbSet<T>` is the most ergonomic way to express "a queryable collection of T".
- EF Core itself is an abstraction over SQL providers — being EF-bound is not the same as being PostgreSQL-bound.
- The alternative (custom repositories per entity) leads to dozens of one-line classes with no benefit.

So we add the package:

```bash
dotnet add src/GloryCafe.Application/GloryCafe.Application.csproj \
       package Microsoft.EntityFrameworkCore --version 9.0.0
```

### `Application/Common/Interfaces/IApplicationDbContext.cs`

```csharp
using GloryCafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GloryCafe.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<AdminUser> AdminUsers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

Read-only properties (`{ get; }` only — no setter) prevent callers from replacing the DbSets at runtime.

### `Infrastructure/Persistence/ApplicationDbContext.cs`

```csharp
public DbSet<Category> Categories => Set<Category>();
public DbSet<Product> Products => Set<Product>();
public DbSet<Order> Orders => Set<Order>();
public DbSet<OrderItem> OrderItems => Set<OrderItem>();
public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
```

Using `Set<T>()` (an EF Core method on `DbContext`) instead of declaring `public DbSet<X> Xs { get; set; }` skips the auto-property setter, which means no warnings about EF having to set non-nullable properties via reflection.

---

## EF Core configurations explained

Each entity gets its own file under `Infrastructure/Persistence/Configurations/`. Each implements `IEntityTypeConfiguration<T>` and is auto-discovered by `modelBuilder.ApplyConfigurationsFromAssembly(...)` in `OnModelCreating`.

### `CategoryConfiguration.cs`

```csharp
builder.ToTable("categories");
builder.HasKey(c => c.Id);
builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
builder.HasIndex(c => c.Name).IsUnique();
builder.Property(c => c.DisplayOrder).HasDefaultValue(0);
builder.Property(c => c.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

builder.HasMany(c => c.Products)
    .WithOne(p => p.Category)
    .HasForeignKey(p => p.CategoryId)
    .OnDelete(DeleteBehavior.Restrict);
```

- `ToTable("categories")` — explicit lower-snake-case table name (PostgreSQL convention; case-sensitive otherwise).
- `IsUnique` on `Name` — prevents duplicate category names from being created.
- `Restrict` on delete — explained earlier.

### `ProductConfiguration.cs`

```csharp
builder.Property(p => p.Price).IsRequired().HasColumnType("numeric(10,2)");
builder.Property(p => p.IsAvailable).HasDefaultValue(true);
builder.HasIndex(p => p.CategoryId);
```

- `numeric(10,2)` — PostgreSQL's exact-precision decimal type, with 10 total digits and 2 after the decimal point. C# `decimal` maps to it perfectly.
- An index on `CategoryId` makes "give me all drinks" queries fast.

### `OrderConfiguration.cs`

```csharp
builder.Property(o => o.Status)
    .IsRequired()
    .HasConversion<string>()
    .HasMaxLength(20)
    .HasDefaultValue(OrderStatus.Pending);

builder.HasIndex(o => o.Status);
builder.HasIndex(o => o.CreatedAt);

builder.HasMany(o => o.Items)
    .WithOne(i => i.Order)
    .HasForeignKey(i => i.OrderId)
    .OnDelete(DeleteBehavior.Cascade);
```

- `HasConversion<string>()` — store the enum as text in PostgreSQL.
- Indexes on `Status` and `CreatedAt` — the admin dashboard queries by both ("show me pending orders, newest first").
- `Cascade` on Items — deleting an order removes its lines.

### `OrderItemConfiguration.cs`

```csharp
builder.HasOne(oi => oi.Product)
    .WithMany()                       // Product has no back-reference to OrderItems
    .HasForeignKey(oi => oi.ProductId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(oi => oi.OrderId);
builder.HasIndex(oi => oi.ProductId);
```

`.WithMany()` with no argument: we don't want a `Product.OrderItems` collection — products shouldn't carry their order history around in memory.

### `AdminUserConfiguration.cs`

```csharp
builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
builder.HasIndex(u => u.Email).IsUnique();
builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
```

500 chars for the hash — comfortably fits a BCrypt or Argon2 hash with all parameters embedded.

---

## Generating the first migration

```bash
dotnet ef migrations add InitialCreate \
  --project src/GloryCafe.Infrastructure \
  --startup-project src/GloryCafe.API \
  --output-dir Persistence/Migrations
```

What each flag means:

| Flag                   | Meaning                                                                     |
|------------------------|-----------------------------------------------------------------------------|
| `migrations add InitialCreate` | The verb (add a migration) and a human-readable name.               |
| `--project src/GloryCafe.Infrastructure` | Which project owns the DbContext and where migration code is saved. |
| `--startup-project src/GloryCafe.API`    | Which project provides configuration (the connection string).        |
| `--output-dir Persistence/Migrations`    | Folder inside the Infrastructure project for the generated files.    |

EF Core compares the current `ApplicationDbContext` model against the previous snapshot and writes a C# class describing the SQL needed to bring an old database up to the new schema. Three files were created:

```
Infrastructure/Persistence/Migrations/
  20260426105938_InitialCreate.cs           ← Up() and Down() with the SQL
  20260426105938_InitialCreate.Designer.cs  ← Snapshot used at design time
  ApplicationDbContextModelSnapshot.cs      ← Latest model snapshot, kept in sync
```

The timestamp prefix (`20260426105938`) makes migrations sortable so EF knows the order to apply them.

> Migrations are **code, not magic**. Open `20260426105938_InitialCreate.cs` and you'll see plain `migrationBuilder.CreateTable(...)` calls. You can edit them by hand if you ever need to tweak generated SQL.

---

## Applying the migration

```bash
dotnet ef database update \
  --project src/GloryCafe.Infrastructure \
  --startup-project src/GloryCafe.API
```

`database update` connects to PostgreSQL using the connection string from `appsettings.Development.json`, checks the `__EFMigrationsHistory` table to see which migrations are already applied, and runs the pending ones in order.

On first run, the database `glorycafedb` does not exist yet. **The Npgsql provider creates it automatically** — provided the user (`postgres`) has permission, which the default install does.

EF Core then creates `__EFMigrationsHistory` and inserts a row recording that `20260426105938_InitialCreate` ran. Re-running `database update` later is idempotent: it sees the row and does nothing.

### What happens if the password is wrong

You will see:

```
Npgsql.PostgresException: 28P01: password authentication failed for user "postgres"
```

That is the database server rejecting the credentials. Fix the password in `appsettings.Development.json` and rerun `database update` — no need to regenerate the migration.

---

## Verifying the database

```bash
PGPASSWORD=YOURPASSWORD "/c/Program Files/PostgreSQL/18/bin/psql.exe" -U postgres -d glorycafedb -c "\dt"
```

`\dt` is psql's "describe tables" meta-command. Expected output:

```
                  List of tables
 Schema |         Name          | Type  |  Owner
--------+-----------------------+-------+----------
 public | __EFMigrationsHistory | table | postgres
 public | admin_users           | table | postgres
 public | categories            | table | postgres
 public | order_items           | table | postgres
 public | orders                | table | postgres
 public | products              | table | postgres
```

Six tables: the five we modeled plus EF's bookkeeping table.

To inspect a specific table's schema:

```bash
psql -U postgres -d glorycafedb -c "\d products"
```

You should see columns matching the configuration: `Id`, `Name varchar(150)`, `Price numeric(10,2)`, `IsAvailable boolean DEFAULT true`, etc.

---

## What you can do now

- Open pgAdmin (or any PostgreSQL GUI) and browse `glorycafedb` — the schema is real.
- Hand-insert a category and a product via psql to confirm constraints kick in:
  ```sql
  INSERT INTO categories ("Name", "DisplayOrder") VALUES ('Drinks', 0);
  INSERT INTO categories ("Name", "DisplayOrder") VALUES ('Drinks', 1); -- fails: unique index
  ```
- Roll the database back to "empty" and reapply, to feel the migration round-trip:
  ```bash
  dotnet ef database update 0 \
    --project src/GloryCafe.Infrastructure \
    --startup-project src/GloryCafe.API   # drops everything
  dotnet ef database update \
    --project src/GloryCafe.Infrastructure \
    --startup-project src/GloryCafe.API   # rebuilds
  ```

What we still cannot do: hit the API over HTTP, register an admin, place an order. That arrives in Phase 3.

---

## Glossary additions

- **Migration** — A versioned C# file describing schema changes. `Up()` applies them, `Down()` reverts them.
- **DbSet&lt;T&gt;** — EF Core's queryable collection of an entity type.
- **Navigation property** — A reference property on an entity that EF Core fills in based on a foreign key.
- **Foreign key** — A column whose value must match a primary key in another table.
- **Cascade / Restrict / SetNull** — `ON DELETE` behaviors. Cascade deletes children; Restrict blocks the delete; SetNull leaves the foreign key column null.
- **Snapshot pattern** — Copying a value (price, name) onto a child record so historical data does not mutate when the parent is edited.
- **Idempotent** — An operation safe to repeat. `database update` is idempotent because it skips already-applied migrations.

---

## Next phase preview

**Phase 3 — Public API endpoints**

We will:

1. Add DTOs in `Application/Categories/Dtos`, `Application/Products/Dtos`, etc.
2. Create use cases (e.g. `GetCategoriesQuery`, `GetProductsQuery`, `CreateOrderCommand`) using a request/handler pattern (likely with MediatR).
3. Add validators with FluentValidation (enforce "phone or email required", "pickup time at least 10 minutes from now", etc.).
4. Create thin controllers in `API/Controllers/` for `categories`, `products`, `orders`.
5. Configure CORS so the React frontend (different port in dev) can call the API.
6. Test the endpoints from the Swagger UI generated by `AddOpenApi()`.

By the end of Phase 3 the public part of the menu and the order-creation flow are reachable over HTTP — even though no frontend exists yet.
