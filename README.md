# RzR.DataVigil - Overview

[![.NET Standard 2.1](https://img.shields.io/badge/.NET%20Standard-2.1-blue.svg)](#requirements)
[![EF Core 5+](https://img.shields.io/badge/EF%20Core-5.0%2B-purple.svg)](#requirements)

| Name     | Details |
|----------|----------|
| RzR.DataVigil.Abstractions | [![NuGet Version](https://img.shields.io/nuget/v/RzR.DataVigil.Abstractions.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Abstractions/) [![Nuget Downloads](https://img.shields.io/nuget/dt/RzR.DataVigil.Abstractions.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Abstractions) |
| RzR.DataVigil.Core | [![NuGet Version](https://img.shields.io/nuget/v/RzR.DataVigil.Core.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Core/) [![Nuget Downloads](https://img.shields.io/nuget/dt/RzR.DataVigil.Core.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Core)|
| RzR.DataVigil.AspNetCore | [![NuGet Version](https://img.shields.io/nuget/v/RzR.DataVigil.AspNetCore.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.AspNetCore/) [![Nuget Downloads](https://img.shields.io/nuget/dt/RzR.DataVigil.AspNetCore.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.AspNetCore)|
| RzR.DataVigil.EFCore | [![NuGet Version](https://img.shields.io/nuget/v/RzR.DataVigil.EFCore.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.EFCore/) [![Nuget Downloads](https://img.shields.io/nuget/dt/RzR.DataVigil.EFCore.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.EFCore) |
| RzR.DataVigil.Storage.File | [![NuGet Version](https://img.shields.io/nuget/v/RzR.DataVigil.Storage.File.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Storage.File/) [![Nuget Downloads](https://img.shields.io/nuget/dt/RzR.DataVigil.Storage.File.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Storage.File) |
| RzR.DataVigil.Storage.EfPostgreSql | [![NuGet Version](https://img.shields.io/nuget/v/RzR.DataVigil.Storage.EfPostgreSql.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Storage.EfPostgreSql/) [![Nuget Downloads](https://img.shields.io/nuget/dt/RzR.DataVigil.Storage.EfPostgreSql.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Storage.EfPostgreSql) |
| RzR.DataVigil.Storage.EfSqlServer | [![NuGet Version](https://img.shields.io/nuget/v/RzR.DataVigil.Storage.EfSqlServer.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Storage.EfSqlServer/) [![Nuget Downloads](https://img.shields.io/nuget/dt/RzR.DataVigil.Storage.EfSqlServer.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Storage.EfSqlServer) |
| RzR.DataVigil.Storage.EfMongoDb | [![NuGet Version](https://img.shields.io/nuget/v/RzR.DataVigil.Storage.EfMongoDb.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Storage.EfMongoDb/) [![Nuget Downloads](https://img.shields.io/nuget/dt/RzR.DataVigil.Storage.EfMongoDb.svg?style=flat&logo=nuget)](https://www.nuget.org/packages/RzR.DataVigil.Storage.EfMongoDb) |

I needed a way to track data changes in EF Core apps without touching every repository or service. So I built this.

`RzR.DataVigil` hooks into EF Core interceptors and records property-level diffs on Create, Update, Delete (and optionally Read). It tags each audit record with the user, IP, correlation ID, trace ID. You get GDPR field-level controls too - masking, hashing, anonymization - both on write and on read. Supports SQL Server, PostgreSQL, MongoDB, or just flat JSON files for storage.

---

## Table of Contents

- [Features](#features)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Installation](#installation)
- [Configuration](#configuration)
  - [Basic Setup](#basic-setup)
  - [EF Core Interception](#ef-core-interception)
  - [Storage Providers](#storage-providers)
  - [ASP.NET Core Integration](#aspnet-core-integration)
  - [GDPR Policies](#gdpr-policies)
  - [Data Retention](#data-retention)
- [How It Works](#how-it-works)
  - [CUD Auditing (Create/Update/Delete)](#cud-auditing-createupdatedelete)
  - [Read Auditing](#read-auditing)
  - [Audit Data Model](#audit-data-model)
  - [User & Correlation Enrichment](#user--correlation-enrichment)
  - [Column Length Guards](#column-length-guards)
- [GDPR Compliance](#gdpr-compliance)
  - [Storage Policies](#storage-policies)
  - [Retrieval Policies](#retrieval-policies)
  - [Right to Erasure](#right-to-erasure)
- [Non-Web Scenarios](#non-web-scenarios)
- [Querying Audit Logs](#querying-audit-logs)
  - [Filters](#filters)
  - [Paging](#paging)
- [Customization](#customization)
  - [Custom User Resolver](#custom-user-resolver)
  - [Custom Source Resolver](#custom-source-resolver)
  - [Selective Auditing](#selective-auditing)
- [Storage Providers Reference](#storage-providers-reference)
- [Samples](#samples)
- [Requirements](#requirements)
  - [Supported Runtimes](#supported-runtimes)
  - [PostgreSQL Version](#postgresql-version)

---

## Features

- EF Core interceptor-based, so your entities don't need any changes
- Tracks Create, Update, Delete at the property level (old value vs new value)
- Read auditing too, if you want it - pulls table/column/ID info from executed SQL
- GDPR: mask, hash, anonymize, exclude, or write your own transform per field
- Retrieval access control by role or claim
- Ships with SQL Server, PostgreSQL, MongoDB, and file (JSON) storage providers. Or implement `IAuditStore` yourself
- ASP.NET Core: grabs user + correlation IDs from `HttpContext` automatically. Console/worker apps: use `AuditScopeContext` instead
- Records which HTTP operation a change arrived through - method, and the route template if you supply a route accessor
- Query filters on time range, user, correlation ID, and GDPR state, on top of paging (page size capped at 500)
- Over-long user, source, correlation and trace values are bounded before the write, so a long value can't cost you the whole audit record
- Built-in retention service (runs daily, deletes old records)
- `AnonymizeByUserAsync()` for right-to-erasure (GDPR Art. 17)
- Opt entities in/out with `IAuditable`, or exclude globally

---

## Architecture

```
┌───────────────────────────────────────────────────────────┐
│                    Your Application                       │
│  DbContext (SaveChanges) ──► EF Core Interceptors         │
└─────────────┬───────────────────────────┬─────────────────┘
              │                           │
              ▼                           ▼
┌─────────────────────────┐     ┌───────────────────────────┐
│     Audit Pipeline      │     │   User / Source / Corr.   │
│  (enrichment + GDPR)    │ ◄── │       Resolvers           │
└─────────────┬───────────┘     └───────────────────────────┘
              │
              ▼
┌───────────────────────────────────────────────────────────┐
│                    IAuditStore                            │
│    ┌───────────┐ ┌──────────┐ ┌─────────┐ ┌───────────┐   │
│    │ SQL Server│ │PostgreSQL│ │ MongoDB │ │   File    │   │
│    └───────────┘ └──────────┘ └─────────┘ └───────────┘   │
└───────────────────────────────────────────────────────────┘
```

### Package Dependency Graph

```
Abstractions ◄── Core ◄── AspNetCore
                  ▲
                  ├── EFCore
                  │     ▲
                  │     ├── Storage.EfSqlServer
                  │     ├── Storage.EfPostgreSql
                  │     └── Storage.EfMongoDb
                  │
                  └── Storage.File
```

---

## Quick Start

First, mark which entities should be audited:

```csharp
using RzR.DataVigil.Abstractions.Contracts;

public class AbbDbContext : DbContext, IAuditableContext
{
	
}

public class Order : IAuditable
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; }
    public decimal Total { get; set; }
}
```

Then register everything in DI:

```csharp
// Program.cs or Startup.cs
services.AddAuditTrail(options =>
{
    options.EfCore.Intercept<AppDbContext>();
    options.Storage.UseSqlServer(connectionString);
})
.Services
.AddAuditTrailEfCore()
.AddAuditTrailAspNetCore();

services.AddAuditTrailSqlServer();
```

And wire the interceptors into your DbContext registration:

```csharp
services.AddDbContext<AppDbContext>((sp, opts) =>
{
    opts.UseSqlServer(connectionString);
    opts.AddAuditInterceptors(sp);
});
```

Any `SaveChanges` on entities implementing `IAuditable` will be captured from now on.

---

## Installation

Grab the right packages depending on your storage:

### SQL Server
```
RzR.DataVigil.Core
RzR.DataVigil.EFCore
RzR.DataVigil.Storage.EfSqlServer
RzR.DataVigil.AspNetCore          # for web apps
```

### PostgreSQL
```
RzR.DataVigil.Core
RzR.DataVigil.EFCore
RzR.DataVigil.Storage.EfPostgreSql
RzR.DataVigil.AspNetCore
```

### MongoDB
```
RzR.DataVigil.Core
RzR.DataVigil.EFCore
RzR.DataVigil.Storage.EfMongoDb
RzR.DataVigil.AspNetCore
```

### File (JSON)
```
RzR.DataVigil.Core
RzR.DataVigil.Storage.File
```

---

## Configuration

### Basic Setup

```csharp
services.AddAuditTrail(options =>
{
    // EF Core interception
    options.EfCore.Intercept<AppDbContext>();

    // Storage backend (pick one)
    options.Storage.UseSqlServer(connectionString);
    options.Storage.Schema = "audit"; // default: "audit"
    options.Storage.WithRetention(90); // auto-purge after 90 days

    // GDPR policies (optional)
    options.Gdpr.ForEntity<Customer>(e =>
    {
        e.MaskOnStorage(c => c.Email);
        e.AnonymizeOnRetrieval(c => c.Ssn, a => a.AllowRoles("Admin"));
    });

    // Global exclusions (optional)
    options.Exclude<MigrationHistory>();

    // Custom resolvers (optional)
    options.UseUserResolver<MyUserResolver>();
    options.UseSourceResolver<MySourceResolver>();
});
```

### EF Core Interception

```csharp
options.EfCore
    .Intercept<AppDbContext>() // register which DbContexts to audit
    .IncludeReads() // audit SELECT queries (off by default)
    .IncludeReadProperties() // capture column names in read entries
```

### Storage Providers

```csharp
// SQL Server
options.Storage.UseSqlServer("Server=...;Database=AuditDb;...");

// PostgreSQL
options.Storage.UsePostgreSql("Host=...;Database=AuditDb;...");

// MongoDB
options.Storage.UseMongoDb("mongodb://localhost:27017", "AuditDb");

// File (JSON, one file per day)
options.Storage.UseFile(@"C:\AuditLogs");
```

You also need to register the provider in DI:

```csharp
// Pick the matching one:
services.AddAuditTrailSqlServer();
services.AddAuditTrailPostgreSqlServer();
services.AddAuditTrailMongoDb();
services.AddAuditTrailFileStorage();
```

SQL Server and PostgreSQL need their migrations applied:

```csharp
app.ApplicationServices.MigrateAuditSqlServerDb();
// or
app.ApplicationServices.MigrateAuditPostgreSqlDb();
```

### ASP.NET Core Integration

```csharp
services.AddAuditTrailAspNetCore();
```

That call registers five things:

- `IHttpContextAccessor`, unless your host already registered one.
- `AspNetCoreUserResolver`, which grabs user info from `HttpContext`.
- `AspNetCoreCorrelationProvider`. Its resolution order is in [User & Correlation Enrichment](#user--correlation-enrichment).
- `HttpOperationMetadataEnricher`, which records the HTTP method each change arrived through, in `Metadata` under `__datavigil.http.method`.
- `AuditIdentityResolutionDiagnostic`, a hosted service that checks at startup whether `IAuditUserResolver` still resolves to the built-in `DefaultUserResolver`, which is the symptom of a registration-order mistake that would otherwise silently drop HTTP identity from every audit record. It logs a warning and never fails startup.

The user resolver and the correlation provider only take over an empty slot or one still holding the built-in default, so your own `IAuditUserResolver` is left alone whatever the call order.

Pass a route accessor and the matched route **template** is recorded too, under `__datavigil.http.route`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

// ASP.NET Core 3.0+ endpoint routing
static string ResolveRouteTemplate(HttpContext httpContext)
    => (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern?.RawText;

services.AddAuditTrailAspNetCore(ResolveRouteTemplate);
```

Without the accessor the route key is never stamped. You supply it rather than the library reading the route itself because `RzR.DataVigil.AspNetCore` targets `netstandard2.1` against `Microsoft.AspNetCore.Http` 2.2.0 - `GetEndpoint()` doesn't exist there, and the routing surface differs across ASP.NET Core versions. Your host knows its own version.

Call order doesn't matter and the last non-null accessor wins, so a parameterless call never clears an accessor installed elsewhere. Full details in [HTTP Operation Metadata](docs/using.md#13-http-operation-metadata).

Using read auditing? You'll also want the flush middleware - otherwise buffered read entries won't get persisted:

```csharp
app.UseAuditReadFlush();
```

### GDPR Policies

See the dedicated [GDPR Compliance](#gdpr-compliance) section below.

### Data Retention

```csharp
options.Storage.WithRetention(90); // purge entries older than 90 days

// Register the background service
services.AddAuditRetentionService();
```

The retention service purges once at host startup and then every 24h, calling `IAuditStore.PurgeBeforeAsync()` to clean up.

---

## How It Works

### CUD Auditing (Create/Update/Delete)

The interceptor (`AuditSaveChangesInterceptor`) works in two phases.

On `SavingChanges`/`SavingChangesAsync` it walks the `ChangeTracker`, picks up Added/Modified/Deleted entries that implement `IAuditable`, and builds an `AuditTransaction` - one `AuditEntry` per changed entity, with property-level diffs inside. Collection has to happen here, because EF resets `EntityState` and refreshes original values once the write completes.

The transaction is then held until `SavedChanges`/`SavedChangesAsync`, after the write has actually succeeded. Only then are database-generated keys (`int` IDENTITY, PostgreSQL `serial`) real rather than EF's temporary placeholders, so they are patched into the record before the pipeline attaches user/source/correlation data, runs GDPR storage rules, and writes to `IAuditStore.SaveAsync()`.

A `SaveChanges` call that throws produces **no** audit record - the collected transaction is discarded. An audit trail should describe what happened, not what was attempted.

Most stores write to independent storage and leave the audited context alone. A store that instead persists through that same context - a transactional outbox - appends its row there and relies on the context being saved. Since it runs after the business write, the interceptor flushes those writes for it. That flush is covered by your transaction when you opened one, and is a separate transaction when you did not, so own the transaction if the audit row must commit atomically with the change it describes.

> **Read auditing is not covered by that flush.** `IncludeReads()` records through `AuditCommandInterceptor`, which fires mid-query with the connection still busy and has no save of its own, so a store persisting through the audited context receives Read transactions but never gets them committed. Create, Update and Delete are unaffected. If you enable `IncludeReads()`, use a store that writes to independent storage.

> The `DbContext` must implement `IAuditableContext`. Without it the interceptor returns before collecting anything, no matter how the entities are marked.

Here's what gets captured per property:

| Action | OldValue | NewValue |
|--------|----------|----------|
| Create | `null` | New value |
| Update | Previous value | Current value (changed props only) |
| Delete | Previous value | `null` |

`OldValue`/`NewValue` are recorded through a fixed precedence: `null` stays `null`, enums record their member name, EF `ValueConverter`-mapped properties record the converter's *provider* value (so an encrypting converter records ciphertext, never the CLR value), simple types use `ToString()`, `byte[]` records its length as `byte[N]`, and collections or complex/owned types without a meaningful `ToString()` are serialized to JSON, capped at 8,000 characters with a length+SHA-256 truncation marker. A value the pipeline could not safely capture - a converter that threw, a graph that failed to serialize - is recorded as an `[unrecordable: ...]` marker instead of falling back to a value that could be mistaken for real data. See [How values are recorded](docs/using.md#how-values-are-recorded) for the full precedence and how it interacts with GDPR storage rules.

### Read Auditing

Different approach depending on the database. SQL Server and PostgreSQL use `AuditCommandInterceptor` - it parses the SQL that EF generates to figure out which tables/columns/IDs were queried. MongoDB uses `AuditMaterializationInterceptor` instead, since there's no SQL to parse.

Either way, read entries pile up in `AuditReadCollector` during the request. They get flushed when the request ends (via `UseAuditReadFlush()`) or you can flush manually.

### Audit Data Model

```
AuditTransaction (1)
├── Id                  : Guid
├── Timestamp           : DateTimeOffset
├── UserId              : string
├── UserName            : string
├── IpAddress           : string
├── CorrelationId       : string
├── TraceId             : string
├── Source              : string
├── GdprState           : GdprStorageState
├── Metadata            : Dictionary<string, string>
│
└── Entries (*)
    ├── Id              : Guid
    ├── Action          : Create | Read | Update | Delete
    ├── EntityName      : string
    ├── EntityId        : string
    ├── EntityTypeName  : string (CLR FullName)
    │
    └── Properties (*)
        ├── PropertyName  : string
        ├── PropertyType  : string
        ├── OldValue      : string
        └── NewValue      : string
```

**Database schema (SQL Server / PostgreSQL):**

| Table | Schema | Key Indexes |
|-------|--------|-------------|
| `AuditTransactions` | `audit` | (Timestamp, Id), (UserId, Timestamp, Id), (CorrelationId, Timestamp, Id), (GdprState, Timestamp, Id) |
| `AuditEntries` | `audit` | TransactionId, EntityName |
| `AuditEntryProperties` | `audit` | AuditEntryId (shadow FK) |

Each `AuditTransactions` index ends in `Id` so it covers the filter *and* the `Timestamp DESC, Id DESC` ordering every query uses, instead of leaving the sort to a separate step.

### User & Correlation Enrichment

Each transaction gets context data attached. The source differs based on your hosting:

| Field | ASP.NET Core | Console/Worker |
|-------|-------------|----------------|
| UserId | `NameIdentifier` or `sub` claim | `AuditScopeContext.SetUser()` or `Thread.CurrentPrincipal` |
| UserName | `Name` claim | Same fallback chain |
| IpAddress | `RemoteIpAddress` | Manual via scope context |
| CorrelationId | `SetCorrelationId()` > `X-Correlation-Id` > `X-Request-Id` > `HttpContext.TraceIdentifier` > `Activity.Current.TraceId` | `SetCorrelationId()` > `Activity.Current.TraceId` |
| TraceId | `Activity.Current.TraceId` | `Activity.Current.TraceId` |
| Source | Custom `IAuditSourceResolver` (default: "Unknown") | Same |

Every transaction also records **how** that actor was determined, in `AuditTransaction.Metadata` under the reserved key `__datavigil.user.source`:

| Value | Meaning |
|-------|---------|
| `ScopeContext` | Set explicitly via `IAuditScopeContext.SetUser()` |
| `HttpContext` | Taken from the authenticated `HttpContext.User` |
| `ThreadPrincipal` | Taken from `Thread.CurrentPrincipal` |
| `Anonymous` | Resolution succeeded and there genuinely was no user |
| `Unresolved` | Resolution failed - the absence of a user is **not** evidence the action was anonymous |
| `Unspecified` | A resolver returned a real user but did not declare where it came from |

Without this, an audit record with no `UserId` is ambiguous: `Anonymous` and `Unresolved` look identical in the data and mean very different things to anyone reviewing the trail.

Writing the key needs no schema change - `Metadata` is already persisted - but note it now carries at least one entry on every transaction, where it was often empty before.

`AuditUserInfo.Claims` and `.Roles` are resolved for use at retrieval time and are **not** persisted; `AuditTransaction` has no such columns.

Two more reserved keys record the HTTP operation the change arrived through, when there was one:

| Key | Constant | Example | Stamped |
|-----|----------|---------|---------|
| `__datavigil.http.method` | `AuditMetadataKeys.HttpMethod` | `POST` | Whenever the change happened inside a request |
| `__datavigil.http.route` | `AuditMetadataKeys.HttpRoute` | `/orders/{id}` | Only when a route accessor was supplied |

The route value is the route *template*, not the resolved path, so it groups requests by endpoint and can't leak an identifier out of the URL into the audit record. Neither key appears in a worker or console host. See [HTTP Operation Metadata](docs/using.md#13-http-operation-metadata).

See [Custom Resolvers](docs/using.md#11-custom-resolvers) for how to stamp `Source` from your own `IAuditUserResolver`.

### Column Length Guards

Every length-bound field on `AuditTransaction` is brought within its storage column before the store sees it. The guard sits in the pipeline rather than in the resolvers, because the resolvers are a public extension point and a third-party implementation would bypass a guard placed there.

| Field | Max characters | Over the limit |
|-------|----------------|----------------|
| `UserId` | 256 | Truncated |
| `UserName` | 256 | Truncated |
| `IpAddress` | 64 | Truncated |
| `Source` | 512 | Truncated |
| `CorrelationId` | 256 | Rejected to `null` |
| `TraceId` | 256 | Rejected to `null` |

The limits come from `AuditColumnLengths`. Actor attributes are truncated because a partially attributed record is still an investigative lead, while an unattributed one is not. The two machine join keys are rejected instead: a truncated correlation or trace ID looks valid and joins to nothing, which is false evidence. Truncation never splits a surrogate pair, so the stored value stays encodable.

Whenever a value was guarded, the affected property names go into `Metadata` under the reserved key `__datavigil.oversize` (`AuditMetadataKeys.Oversize`), comma separated, in the fixed order `UserId,UserName,IpAddress,Source,CorrelationId,TraceId`:

```
__datavigil.oversize = UserId,CorrelationId
```

The pipeline also logs a warning with the observed length against the column maximum. The key is absent when nothing was guarded.

Why this exists: an over-long value used to fail the audit `INSERT`. The business write had already committed, so the change happened and no audit record described it. Bounding the value keeps the record, and the metadata key keeps the fact that it was bounded.

---

## GDPR Compliance

### Storage Policies

Applied before anything hits the database. Configure per entity, per field:

```csharp
options.Gdpr.ForEntity<Customer>(e =>
{
    e.ExcludeOnStorage(c => c.CreditCardNumber); // not stored at all
    e.MaskOnStorage(c => c.Email); // "j***n@mail.com"
    e.AnonymizeOnStorage(c => c.Phone); // "[ANONYMIZED]"
    e.HashOnStorage(c => c.Ssn); // SHA-256 hex string
    e.TransformOnStorage(c => c.Notes, val => val.Substring(0, 10) + "...");
});
```

| Action | Stored Value | Reversible |
|--------|-------------|------------|
| `Exclude` | Field removed entirely | N/A |
| `Mask` | First + last char visible, middle masked: `j***e` | No |
| `Anonymize` | `[ANONYMIZED]` | No |
| `Hash` | SHA-256 hex digest | No |
| `Transform` | Custom `Func<string, string>` | Depends |

### Retrieval Policies

Separate from storage policies. This controls what the *reader* sees when querying. Roles are checked first, then claims:

```csharp
options.Gdpr.ForEntity<Customer>(e =>
{
    // Only Admin or Auditor roles can see the email
    e.MaskOnRetrieval(c => c.Email, access => access
        .AllowRoles("Admin", "Auditor"));

    // Only users with gdpr=full claim can see SSN
    e.AnonymizeOnRetrieval(c => c.Ssn, access => access
        .AllowClaim("gdpr", "full"));

    // Either role or claim grants access
    e.MaskOnRetrieval(c => c.Phone, access => access
        .AllowRoles("Admin")
        .AllowClaim("support", "tier2"));
});
```

Role match = full access. No role match = check claims. Neither = the field stays masked.

### Right to Erasure

GDPR Article 17. Wipes a user's identity from all their audit records:

```csharp
await auditStore.AnonymizeByUserAsync("user-123");
```

Replaces `UserId`, `UserName`, `IpAddress` with `[ERASED]` across all their transactions.

---

## Non-Web Scenarios

No `HttpContext`? No problem. Just drop the `AddAuditTrailAspNetCore()` call:

```csharp
// Register without ASP.NET Core
services.AddAuditTrail(opts =>
{
    opts.EfCore.Intercept<AppDbContext>();
    opts.Storage.UseSqlServer(connectionString);
})
.Services
.AddAuditTrailEfCore();

services.AddAuditTrailSqlServer();
```

Set the user yourself via `IAuditScopeContext`:

```csharp
using (var scope = serviceProvider.CreateScope())
{
    var scopeContext = scope.ServiceProvider.GetRequiredService<IAuditScopeContext>();
    scopeContext.SetUser(new AuditUserInfo
    {
        UserId = "worker-1",
        UserName = "BackgroundWorker",
        IpAddress = "127.0.0.1"
    });

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Orders.Add(new Order { CustomerName = "Alice", Total = 100 });
    await db.SaveChangesAsync(); // Audited with worker-1 as the user
}
```

---

## Querying Audit Logs

Pass a `GdprRetrievalContext` so the store knows which fields the current user is allowed to see:

```csharp
// Inject IAuditStore
var context = new GdprRetrievalContext
{
    UserRoles = currentUser.Roles,
    UserClaims = currentUser.Claims
};

var result = await auditStore.QueryAsync(
    new AuditTransactionQuery { Skip = 0, Take = 50 },
    context);

// result.Response contains IEnumerable<AuditTransaction>
// Fields are masked/anonymized based on the user's roles and claims
```

Or from a controller - pretty much the same, just pull roles/claims from the `ClaimsPrincipal`:

```csharp
[HttpPost("query")]
public async Task<IActionResult> Query(CancellationToken ct)
{
    var context = new GdprRetrievalContext
    {
        UserRoles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value),
        UserClaims = User.Claims
            .ToDictionary(c => c.Type, c => c.Value)
    };

    var result = await _auditStore.QueryAsync(
        new AuditTransactionQuery(), context, ct);

    return Ok(result.Response);
}
```

### Filters

`AuditTransactionQuery` also carries five optional filters. Leave one at its default and it isn't applied; supplied filters combine with AND:

| Property | Type | Matches |
|----------|------|---------|
| `FromUtc` | `DateTimeOffset?` | `Timestamp >= FromUtc` |
| `ToUtc` | `DateTimeOffset?` | `Timestamp < ToUtc` |
| `UserId` | `string` | exact equality (null/whitespace = not applied) |
| `CorrelationId` | `string` | exact equality (null/whitespace = not applied) |
| `GdprState` | `GdprStorageState?` | exact equality |

```csharp
var result = await auditStore.QueryAsync(new AuditTransactionQuery
{
    FromUtc = DateTimeOffset.UtcNow.AddDays(-7),
    UserId = "alice",
    Take = 100
});
```

The date range is half-open - `>= FromUtc`, `< ToUtc` - so adjacent windows tile without overlap.

> **WARNING:** string matching follows each store's own equality semantics. A relational provider compares under the column collation (case-*insensitive* on a default SQL Server); the file store compares ordinally and is case-*sensitive*. The same query object can return different result sets on different stores.

Results come back newest-first by `Timestamp`, tie-broken on `Id` descending so paging stays stable within a store.

### Paging

`Skip` and `Take` are bounded the same way by every store, through `AuditQueryLimits`:

| Requested | Executed |
|-----------|----------|
| `Take` above 500 (`AuditQueryLimits.MaxTake`) | Capped to 500, logged at Warning |
| `Take` of 0 | Empty successful result; storage is not read at all |
| `Take` below 0 | 10 (`AuditQueryLimits.DefaultTake`) |
| `Skip` below 0 | 0 (`AuditQueryLimits.MinSkip`) |

> **WARNING:** a short page does **not** mean there are no more records. Ask for 2000 and you get 500 back with the rest still there, reachable through `Skip`. Code shaped like `while (page.Count == pageSize)` stops early and silently for any page size above the cap - page on `Skip` instead, and stop when a page comes back empty.

Nothing is ever rejected for paging reasons: an out-of-range value is corrected and the query runs.

See [Querying Audit Data](docs/using.md#8-querying-audit-data) for the full semantics and the `IAuditStore` implementer contract.

---

## Customization

### Custom User Resolver

If the built-in resolvers don't fit:

```csharp
public class MyUserResolver : IAuditUserResolver
{
    public IResult<AuditUserInfo> Resolve()
    {
            // Set Source when one of the AuditUserSource values describes where this identity
            // came from (ScopeContext, HttpContext, ThreadPrincipal). Leaving it unset records
            // AuditUserSource.Unspecified - a real actor whose provenance was not declared.
        return Result<AuditUserInfo>.Success(new AuditUserInfo
        {
            UserId = "system",
            UserName = "SystemService"
        });
    }
}

// Register
options.UseUserResolver<MyUserResolver>();
```

### Custom Source Resolver

```csharp
public class MySourceResolver : IAuditSourceResolver
{
    public IResult<string> Resolve()
        => Result<string>.Success("OrderService-v2");
}

// Register
options.UseSourceResolver<MySourceResolver>();
```

### Selective Auditing

A few ways to control what gets audited.

**Marker interface** - everything gets audited:
```csharp
public class Order : IAuditable { }
```

**More control** - pick actions, exclude fields:
```csharp
public class Order : IAuditableEntity
{
    public bool ShouldAudit(AuditAction action)
        => action != AuditAction.Read; // Skip read auditing

    public IEnumerable<string> GetExcludedFields()
        => new[] { "InternalNotes" }; // Exclude specific fields
}
```

**At the DbContext level:**
```csharp
public class AppDbContext : DbContext, IAuditableContext
{
    public IEnumerable<Type> GetExcludedEntityTypes()
        => new[] { typeof(MigrationHistory) };
}
```

**Or just exclude globally:**
```csharp
options.Exclude<MigrationHistory>();
```

---

## Storage Providers Reference

| Provider | Package | Target | Database |
|----------|---------|--------|----------|
| SQL Server | `Storage.EfSqlServer` | netstandard2.1 | SQL Server via EF Core |
| PostgreSQL | `Storage.EfPostgreSql` | netstandard2.1 | PostgreSQL via EF Core + Npgsql |
| MongoDB | `Storage.EfMongoDb` | net8.0 | MongoDB via EF Core + MongoDB provider |
| File | `Storage.File` | netstandard2.1 | JSON files (one per day) |

They all implement `IAuditStore`:

| Method | Description |
|--------|-------------|
| `SaveAsync(AuditTransaction)` | Persist a transaction |
| `QueryAsync(query, gdprContext)` | Retrieve with paging + GDPR retrieval policies |
| `AnonymizeByUserAsync(userId)` | Right to erasure |
| `PurgeBeforeAsync(DateTimeOffset)` | Retention cleanup |

---

## Samples

Check the `src/samples/` folder for working examples:

| Sample | Runtime | Storage | Path |
|--------|---------|---------|------|
| WebApiEfSqlServerNet5 | .NET 5 | SQL Server | `src/samples/ef/WebApiEfSqlServerNet5` |
| WebApiEfSqlServerNet6 | .NET 6 | SQL Server | `src/samples/ef/WebApiEfSqlServerNet6` |
| WebApiEfPostgreSqlNet5 | .NET 5 | PostgreSQL | `src/samples/ef/WebApiEfPostgreSqlNet5` |
| WebApiEfPostgreSqlNet6 | .NET 6 | PostgreSQL | `src/samples/ef/WebApiEfPostgreSqlNet6` |
| WebApiEfPostgreSqlNet7 | .NET 7 | PostgreSQL | `src/samples/ef/WebApiEfPostgreSqlNet7` |
| WebApiEfPostgreSqlNet8 | .NET 8 | PostgreSQL | `src/samples/ef/WebApiEfPostgreSqlNet8` |
| WebApiEfPostgreSqlNet9 | .NET 9 | PostgreSQL | `src/samples/ef/WebApiEfPostgreSqlNet9` |
| WebApiEfMongoDbNet8 | .NET 8 | MongoDB | `src/samples/ef/WebApiEfMongoDbNet8` |
| SampleWorkerService | Worker | File (JSON) | `src/samples/worker/SampleWorkerService` |

The Web API samples are a simple Blog API (Posts + Comments) with GDPR policies, Swagger, and an `AuditController` for browsing the audit trail. `SampleWorkerService` shows the non-HTTP path: setting the actor through `IAuditScopeContext` instead of `HttpContext`.

---

## Requirements

- **.NET Standard 2.1** (library targets)
- **EF Core 5.0+** (compatible with 5.x through 9.x)
- **PostgreSQL 13 or later** (tested minimum)
- **Test projects:** .NET 8.0, MSTest 3.3.1

### Supported Runtimes

Targets `netstandard2.1`, so anything .NET Core 3.x or later (.NET 5 through 9+).

### PostgreSQL Version

PostgreSQL 13 is the lowest major version this library is tested against. Older servers are not blocked and are expected to work, but they are untested, and PostgreSQL 12 has reached end of life.

`MigrateAuditPostgreSqlDb()` reads the server version and logs a warning when it is below 13. To turn that into a startup failure instead:

```csharp
services.AddAuditTrail(options =>
{
    options.Storage.UsePostgreSql(connectionString);
    options.Storage.ThrowOnUnsupportedPostgreSqlVersion = true; // default: false
});
```

With the flag set, the version is checked *before* any migration runs, so an unsupported server throws `NotSupportedException` rather than leaving the audit database half-migrated.

