# Using RzR.DataVigil

Below you'll find setup instructions for a bunch of different scenarios. Web APIs, background workers, plain console apps — it's all covered. Just jump to whichever section fits your situation. No need to read the whole thing top to bottom.

---

## Table of Contents

1. [Package Installation](#1-package-installation)
2. [ASP.NET Core Web API + EF Core (Full Stack)](#2-aspnet-core-web-api--ef-core-full-stack)
3. [ASP.NET Core Web API + File Storage](#3-aspnet-core-web-api--file-storage)
4. [ASP.NET Core Web API + MongoDB](#4-aspnet-core-web-api--mongodb)
5. [Worker Service / Console App (No HttpContext)](#5-worker-service--console-app-no-httpcontext)
6. [GDPR Configuration](#6-gdpr-configuration)
7. [Data Retention](#7-data-retention)
8. [Querying Audit Data](#8-querying-audit-data)
9. [GDPR Right-to-Erasure](#9-gdpr-right-to-erasure)
10. [Entity-Level Audit Control](#10-entity-level-audit-control)
11. [Custom Resolvers](#11-custom-resolvers)
12. [Read (SELECT) Auditing](#12-read-select-auditing)
13. [Manual Audit Entries (No EF Core)](#13-manual-audit-entries-no-ef-core)
14. [Package Reference Summary](#14-package-reference-summary)

---

## 1. Package Installation

The `Core` package is mandatory, every other package is optional. What you add on top depends on your hosting model (web app vs worker) and where the audit records should end up (SQL Server, Postgres, Mongo, flat files).

### Core (always required)

```xml
<PackageReference Include="RzR.DataVigil.Core" />
```

### ASP.NET Core integration (Web API, MVC, Razor Pages)

```xml
<PackageReference Include="RzR.DataVigil.AspNetCore" />
```

This one hooks into `HttpContext` to figure out who the current user is. It also looks for `X-Correlation-Id` or `X-Request-Id` headers so you can trace requests across services. The read-flush middleware lives here too.

### EF Core interception (automatic Create/Update/Delete auditing)

```xml
<PackageReference Include="RzR.DataVigil.EFCore" />
```

### Storage backends (pick one)

| Backend    | Package                                          | Method                         |
|------------|--------------------------------------------------|--------------------------------|
| SQL Server | `RzR.DataVigil.Storage.EfSqlServer`   | `UseSqlServer(connectionString)`             |
| PostgreSQL | `RzR.DataVigil.Storage.EfPostgreSql`  | `UsePostgreSql(connectionString)`            |
| MongoDB    | `RzR.DataVigil.Storage.EfMongoDb`     | `UseMongoDb(connectionString, databaseName)` |
| File (JSON)| `RzR.DataVigil.Storage.File`           | `UseFile(directoryPath)`                     |

---

## 2. ASP.NET Core Web API + EF Core (Full Stack)

This is the setup I'd guess most people end up with. You wire in an EF Core interceptor that keeps an eye on the `ChangeTracker` — whenever something gets saved, it grabs the before/after values and logs them. Pretty hands-off once it's configured.

### 2.1 Install packages

```xml
<!-- Core + ASP.NET Core + EF Core + SQL Server storage -->
<PackageReference Include="RzR.DataVigil.Core" />
<PackageReference Include="RzR.DataVigil.AspNetCore" />
<PackageReference Include="RzR.DataVigil.EFCore" />
<PackageReference Include="RzR.DataVigil.Storage.EfSqlServer" />
```

### 2.2 Mark entities for auditing

```csharp
using RzR.DataVigil.Abstractions.Contracts;

public class Order : IAuditable          // Marker interface, all CUD actions audited
{
    public int Id { get; set; }
    public string CustomerEmail { get; set; }
    public string CustomerPhone { get; set; }
    public decimal TotalAmount { get; set; }
}
```

If an entity doesn't have `IAuditable` on it, nothing happens. The interceptor won't even look at it.

> **The `DbContext` must also implement `IAuditableContext`.** Both interceptors check for it and return
> immediately when it's missing, so without it *nothing is audited at all* — regardless of how the entities
> are marked. See [10.3](#103-context-level-exclusions-iauditablecontext) for the interface itself; the
> `GetExcludedEntityTypes()` member can simply return an empty sequence if you have no exclusions.

### 2.3 Register services in `Program.cs` (Minimal API)

```csharp
using RzR.DataVigil.AspNetCore.Extensions;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.EFCore.Extensions;
using RzR.DataVigil.Storage.EfSqlServer.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register audit trail
builder.Services.AddAuditTrail(options =>
{
    // Tell the audit system which DbContext to intercept
    options.EfCore.Intercept<AppDbContext>();

    // Configure SQL Server as storage backend
    options.Storage.UseSqlServer(
        builder.Configuration.GetConnectionString("AuditDb"));

    // Optional: set the database schema (default is "audit")
    options.Storage.Schema = "audit";
});

// Register EF Core interceptors
builder.Services.AddAuditTrailEfCore();

// Register SQL Server audit store
builder.Services.AddAuditTrailSqlServer();

// Register ASP.NET Core integration
//       (user resolver, correlation provider)
builder.Services.AddAuditTrailAspNetCore();

// Register your DbContext WITH audit interceptors
builder.Services.AddDbContext<AppDbContext>((sp, opts) =>
{
    opts.UseSqlServer(builder.Configuration.GetConnectionString("AppDb"));

    // Wire in the audit interceptors
    opts.AddAuditInterceptors(sp);
});

var app = builder.Build();

// Run audit storage migrations
app.Services.MigrateAuditSqlServerDb();

app.MapControllers();
app.Run();
```

### 2.4 Register services in `Startup.cs` (Classic pattern)

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddAuditTrail(options =>
        {
            options.EfCore.Intercept<BlogDbContext>();
            options.Storage
                .UsePostgreSql(Configuration.GetConnectionString("AuditDb"))
                .WithRetention(90);
        })
        .Services
        .AddAuditTrailEfCore()
        .AddAuditTrailPostgreSqlServer()
        .AddAuditTrailAspNetCore();

    services.AddDbContext<BlogDbContext>((sp, opts) =>
    {
        opts.UseNpgsql(Configuration.GetConnectionString("BlogDb"));
        opts.AddAuditInterceptors(sp);
    });
}

public void Configure(IApplicationBuilder app)
{
    // Run audit migrations at startup
    app.ApplicationServices.MigrateAuditPostgreSqlDb();

    app.UseRouting();
    app.UseAuthorization();
    app.UseEndpoints(endpoints => endpoints.MapControllers());
}
```

### 2.5 What happens in depth

So after you've got everything registered, here's what goes on behind the scenes each time `SaveChanges()` or `SaveChangesAsync()` runs:

First the interceptor goes through the change tracker looking for any Added, Modified, or Deleted entities marked with `IAuditable`. For each one it records the old and new property values. Then it tacks on the user identity (grabbed from `HttpContext.User`), the client IP, and whatever correlation ID it can find. If you set up GDPR rules, those get applied next — masking, hashing, anonymizing, excluding fields, all that stuff. Finally everything gets handed off to `IAuditStore.SaveAsync()`.

You don't have to touch your controllers or repositories at all. It just works.

---

## 3. ASP.NET Core Web API + File Storage

Sometimes standing up a whole separate database just for audit logs feels like overkill. The file storage option is simpler — it dumps everything into JSON files, one per day.

### 3.1 Install packages

```xml
<PackageReference Include="RzR.DataVigil.Core" />
<PackageReference Include="RzR.DataVigil.AspNetCore" />
<PackageReference Include="RzR.DataVigil.EFCore" />
<PackageReference Include="RzR.DataVigil.Storage.File" />
```

### 3.2 Register services

```csharp
using RzR.DataVigil.AspNetCore.Extensions;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.EFCore.Extensions;
using RzR.DataVigil.Storage.File.Extensions;

builder.Services.AddAuditTrail(options =>
{
    options.EfCore.Intercept<AppDbContext>();

    // File storage: one JSON file per day in the specified directory
    options.Storage.UseFile(Path.Combine(Directory.GetCurrentDirectory(), "audit-logs"));
});

builder.Services.AddAuditTrailEfCore();
builder.Services.AddAuditTrailFileStorage();
builder.Services.AddAuditTrailAspNetCore();
```

After this runs for a while you'll see files like `audit-logs/audit-2026-04-16.json` in the output folder. Each file has the day's transactions as a JSON array. Simple and easy to grep through if needed.

---

## 4. ASP.NET Core Web API + MongoDB

If Mongo is your thing, this works through EF Core's MongoDB provider. The nice part? No migration scripts to deal with. Mongo doesn't care about schemas so the collections pop into existence the moment you write the first audit record.

### 4.1 Install packages

```xml
<PackageReference Include="RzR.DataVigil.Core" />
<PackageReference Include="RzR.DataVigil.AspNetCore" />
<PackageReference Include="RzR.DataVigil.EFCore" />
<PackageReference Include="RzR.DataVigil.Storage.EfMongoDb" />
```

### 4.2 Register services

```csharp
using RzR.DataVigil.AspNetCore.Extensions;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.EFCore.Extensions;
using RzR.DataVigil.Storage.EfMongoDb.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register audit trail
builder.Services.AddAuditTrail(options =>
{
    options.EfCore
        .Intercept<BlogDbContext>()
        .IncludeReads()
        .IncludeReadProperties();

    // MongoDB requires both connection string and database name
    options.Storage.UseMongoDb(
        builder.Configuration.GetConnectionString("AuditDb"), // e.g. "mongodb://localhost:27017"
        builder.Configuration["DatabaseNames:AuditDb"]); // e.g. "audit_db"
});

// Register EF Core interceptors
builder.Services.AddAuditTrailEfCore();

// Register MongoDB audit store
builder.Services.AddAuditTrailMongoDb();

// Register ASP.NET Core integration
builder.Services.AddAuditTrailAspNetCore();

// Register your DbContext WITH audit interceptors
builder.Services.AddDbContext<BlogDbContext>((sp, opts) =>
{
    opts.UseMongoDB(
        builder.Configuration.GetConnectionString("BlogDb"),
        builder.Configuration["DatabaseNames:BlogDb"]);

    opts.AddAuditInterceptors(sp);

    // MongoDB only: read auditing runs through AuditMaterializationInterceptor.
    // AddAuditInterceptors() wires the SQL command interceptor, which MongoDB never
    // triggers - without this call IncludeReads() has no effect on Mongo.
    opts.AddAuditReadInterceptor(sp);
});

var app = builder.Build();

// MongoDB is schema-less — no migrations needed.
// Collections are created automatically when data is first inserted.

app.UseRouting();
app.UseAuthorization();
app.UseAuditReadFlush();
app.MapControllers();

app.Run();
```

### 4.3 appsettings.json

```json
{
  "ConnectionStrings": {
    "BlogDb": "mongodb://localhost:27017",
    "AuditDb": "mongodb://localhost:27017"
  },
  "DatabaseNames": {
    "BlogDb": "blog_db",
    "AuditDb": "audit_db"
  }
}
```

### 4.4 How it differs from SQL Server / PostgreSQL

| Aspect          | SQL Server / PostgreSQL                | MongoDB                                    |
|-----------------|----------------------------------------|--------------------------------------------|
| Configuration   | `UseSqlServer(connStr)`                | `UseMongoDb(connStr, databaseName)`        |
| Migrations      | Required (`MigrateAuditSqlServerDb()`) | Not needed (schema-less)                   |
| DI registration | `AddAuditTrailSqlServer()`             | `AddAuditTrailMongoDb()`                   |
| Read auditing   | Via `AuditCommandInterceptor`          | Via `AuditMaterializationInterceptor`      |
| Schema option   | `options.Storage.Schema = "audit"`     | Not applicable                             |

---

## 5. Worker Service / Console App (No HttpContext)

Background services and console apps don't have an HTTP pipeline, which means there's no `HttpContext` floating around. That changes things a bit. You need to tell the audit system who the "user" is yourself, and you're responsible for feeding transactions into `AuditPipeline` by hand.

### 5.1 Install packages

```xml
<PackageReference Include="RzR.DataVigil.Core" />
<PackageReference Include="RzR.DataVigil.Storage.File" />
<!-- or Storage.EfSqlServer / Storage.EfPostgreSql / Storage.EfMongoDb -->
```

> Skip the `RzR.DataVigil.AspNetCore` package in this scenario. It needs `HttpContext` to work, and that obviously isn't a thing in a console or worker process.

### 5.2 Register services

```csharp
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Storage.File.Extensions;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddAuditTrail(options =>
        {
            // Identify this application in audit logs
            options.UseSourceResolver<WorkerSourceResolver>();

            // File storage with 30-day retention
            options.Storage
                .UseFile(Path.Combine(Directory.GetCurrentDirectory(), "audit-logs"))
                .WithRetention(30);
        });

        services.AddAuditTrailFileStorage();
        services.AddAuditRetentionService(); // Background purge every 24h
        services.AddHostedService<MyWorker>();
    });
```

### 5.3 Create a source resolver

```csharp
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Services;

public class WorkerSourceResolver : IAuditSourceResolver
{
    public IResult<string> Resolve()
    {
        return Result<string>.Success("OrderProcessingService");
    }
}
```

### 5.4 Set user and push audit entries in a worker

```csharp
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Pipeline;

public class MyWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MyWorker(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Create a DI scope (isolates IAuditScopeContext)
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            // Set the audit user (no HttpContext available)
            var scopeContext = sp.GetRequiredService<IAuditScopeContext>();
            scopeContext.SetUser(new AuditUserInfo
            {
                UserId = "worker-batch-job",
                UserName = "BatchProcessor",
                IpAddress = "127.0.0.1"
            });

            // Build the audit transaction manually
            var transaction = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Entries = new List<AuditEntry>
                {
                    new AuditEntry
                    {
                        Id = Guid.NewGuid(),
                        EntityName = "Order",
                        EntityId = "42",
                        Action = AuditAction.Update,
                        Properties = new List<AuditEntryProperty>
                        {
                            new AuditEntryProperty
                            {
                                PropertyName = "Status",
                                PropertyType = "System.String",
                                OldValue = "Pending",
                                NewValue = "Shipped"
                            }
                        }
                    }
                }
            };

            //  Push through the pipeline
            //  (enriches user/source/correlation, applies GDPR, persists)
            var pipeline = sp.GetRequiredService<AuditPipeline>();
            await pipeline.ProcessAsync(transaction, ct);

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}
```

Unlike the web API setup where the interceptor handles everything behind the scenes, here you're in charge. Create an `AuditTransaction` object, grab `AuditPipeline` from the service provider, call `ProcessAsync`. That's basically the whole workflow.

---

## 6. GDPR Configuration

There are two separate layers here and they work independently of each other.

Storage policies change (or remove) sensitive data before anything gets saved. The raw value never touches the database. Retrieval policies are different — they kick in when someone reads audit records back, and they decide what that person is allowed to see based on their roles or claims. You can use one layer, or both together.

### 6.1 Storage policies

Whatever transformation you pick runs before the write. After that, the original value is gone for good.

```csharp
services.AddAuditTrail(options =>
{
    options.Gdpr.ForEntity<Customer>(e =>
    {
        e.ExcludeOnStorage(c => c.CreditCard); // Property removed entirely
        e.MaskOnStorage(c => c.Email); // "alice@mail.com" > "a************m"
        e.AnonymizeOnStorage(c => c.FullName); // "Alice Smith"    →> "[ANONYMIZED]"
        e.HashOnStorage(c => c.Ssn); // "123-45-6789"  >   "a1b2c3...f6" (SHA-256)
        e.TransformOnStorage(c => c.Phone, val => // Custom logic
            $"+***-***-{val[^4..]}");
    });
});
```

| Action       | Result                              | Reversible? |
|-------------|--------------------------------------|-------------|
| `Exclude`   | Property not stored at all           | No          |
| `Mask`      | First + `***` + last character       | No          |
| `Anonymize` | Replaced with `[ANONYMIZED]`         | No          |
| `Hash`      | SHA-256 hex (64 chars)               | No          |
| `Custom`    | Your `Func<string, string>`          | Depends     |

One thing to keep in mind: null values are left alone. If a field is null, none of the transforms touch it.

### 6.2 Retrieval policies

Retrieval is about visibility. When somebody pulls up audit records, these rules check their role and claim information to figure out if they should see the real value or a sanitized version.

```csharp
options.Gdpr.ForEntity<Order>(e =>
{
    // Only users with "Admin" role see the real email
    e.MaskOnRetrieval(o => o.CustomerEmail, access => access
        .AllowRoles("Admin"));

    // Only users with claim gdpr=full see the real phone
    e.AnonymizeOnRetrieval(o => o.CustomerPhone, access => access
        .AllowClaim("gdpr", "full"));
});
```

Worth knowing: the access check uses OR logic. Having any one of the allowed roles is enough. Same with claims — one match and you're in. Only when nothing matches does the field stay hidden.

> **WARNING:** omit the `access` lambda and the field is hidden from *everyone*, administrators included.
> `CanAccess` returns true only on a role or claim match, so a rule with no allowed roles and no allowed
> claims never matches. Always state who is allowed to see the real value.

### 6.3 Combining storage + retrieval

Nothing stops you from layering both on a single field. So the value gets masked when it's stored, and then on read it gets masked again unless whoever is asking has the right role or claim. Belt and suspenders.

```csharp
options.Gdpr.ForEntity<Order>(e =>
{
    // Layer 1: mask before writing to database
    e.MaskOnStorage(o => o.CustomerEmail);
    e.MaskOnStorage(o => o.CustomerPhone);

    // Layer 2: mask/anonymize again when reading (role/claim gated)
    e.MaskOnRetrieval(o => o.CustomerEmail, a => a.AllowRoles("Admin"));
    e.AnonymizeOnRetrieval(o => o.CustomerPhone, a => a.AllowClaim("gdpr", "full"));
});
```

### 6.4 GdprRetrievalContext

When querying audit data, pass a `GdprRetrievalContext` to control field visibility:

```csharp
var context = new GdprRetrievalContext
{
    UserRoles  = new[] { "Admin" },
    UserClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
};

var result = await auditStore.QueryAsync(
    new AuditTransactionQuery { Skip = 0, Take = 50 },
    gdprRetrievalContext: context);
```

If you pass `null` for the context, or just leave it empty, the system assumes the caller has no special access. Every retrieval rule applies and all the sensitive stuff comes back sanitized.

---

## 7. Data Retention

Audit tables grow fast, especially in busy systems. The retention feature lets you put a cap on how long records stick around. Set a number of days and anything older gets purged in the background.

### 7.1 Configure retention

```csharp
services.AddAuditTrail(options =>
{
    options.Storage
        .UseSqlServer(connectionString)
        .WithRetention(90); // 90 days
});
```

### 7.2 Register the retention background service

```csharp
services.AddAuditRetentionService();
```

This registers a background service called `AuditRetentionService`. It purges once at host startup and then every 24 hours, checking the retention setting and deleting anything that's too old. If you didn't configure a retention period it just does nothing. And it swallows any exceptions so it won't take down your app if something goes wrong with the cleanup.

### 7.3 Purging manually

```csharp
var store = serviceProvider.GetRequiredService<IAuditStore>();
var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
await store.PurgeBeforeAsync(cutoff);
```

---

## 8. Querying Audit Data

### 8.1 Basic query

```csharp
var store = serviceProvider.GetRequiredService<IAuditStore>();

var result = await store.QueryAsync(
    new AuditTransactionQuery { Skip = 0, Take = 20 });

if (result.IsSuccess)
{
    foreach (var tx in result.Response)
    {
        Console.WriteLine($"{tx.Timestamp} | {tx.UserId} | {tx.Source}");
        foreach (var entry in tx.Entries)
            Console.WriteLine($"  {entry.Action} {entry.EntityName}#{entry.EntityId}");
    }
}
```

### 8.2 Query with GDPR context

```csharp
var gdprCtx = new GdprRetrievalContext
{
    UserRoles = User.Claims
        .Where(c => c.Type == ClaimTypes.Role)
        .Select(c => c.Value),
    UserClaims = User.Claims
        .ToDictionary(c => c.Type, c => c.Value)
};

var result = await store.QueryAsync(
    new AuditTransactionQuery { Skip = 0, Take = 50 },
    gdprRetrievalContext: gdprCtx);
```

---

## 9. GDPR Right-to-Erasure

This covers the "right to be forgotten" from GDPR Article 17. Sometimes a user asks you to delete their data, but you still need the audit trail for compliance reasons. Here's the compromise:

```csharp
var store = serviceProvider.GetRequiredService<IAuditStore>();
await store.AnonymizeByUserAsync("user-123");
```

What this does is go through every audit transaction tied to that user and replace their `UserId`, `UserName`, and `IpAddress` with `[ERASED]`. The records themselves survive — you can still see that actions happened, when they happened, what changed. You just can't tell who did it anymore.

---

## 10. Entity-Level Audit Control

You probably don't want to audit every single table in your database. Temp data, health checks, migration history — that stuff just creates noise. There are several ways to narrow things down depending on how fine-grained you want to get.

### 10.1 Simple marker (`IAuditable`)

```csharp
public class Order : IAuditable { }   // All Create/Update/Delete actions audited
```

### 10.2 Granular control (`IAuditableEntity`)

```csharp
using RzR.DataVigil.Abstractions.Contracts;
using RzR.DataVigil.Abstractions.Enums;

public class SensitiveDocument : IAuditableEntity
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string InternalNotes { get; set; }
    public string Body { get; set; }

    // Only audit Create and Delete — skip Update
    public bool ShouldAudit(AuditAction action)
        => action != AuditAction.Update;

    // Never include InternalNotes in audit records
    public IEnumerable<string> GetExcludedFields()
        => new[] { nameof(InternalNotes) };
}
```

### 10.3 Context-level exclusions (`IAuditableContext`)

```csharp
using RzR.DataVigil.Abstractions.Contracts;

public class AppDbContext : DbContext, IAuditableContext
{
    // These entity types are never audited in this context
    public IEnumerable<Type> GetExcludedEntityTypes()
        => new[] { typeof(AuditLog), typeof(MigrationHistory) };
}
```

### 10.4 Global exclusions (via options)

```csharp
services.AddAuditTrail(options =>
{
    options.Exclude<HealthCheckResult>();
    options.Exclude<TempData>();
});
```

---

## 11. Custom Resolvers

The library comes with built-in logic for figuring out user identity, correlation IDs, and the source application name. It works fine for typical setups but sometimes you need something different. Maybe your user info comes from a custom header, or you want the source name to include a version number. In those cases just write your own resolver class and register it.

### 11.1 Custom user resolver

```csharp
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;

public class MyUserResolver : IAuditUserResolver
{
    private readonly ICurrentUserService _currentUser;

    public MyUserResolver(ICurrentUserService currentUser)
        => _currentUser = currentUser;

    public IResult<AuditUserInfo> Resolve()
    {
            // Set Source when one of the AuditUserSource values describes where this identity
            // came from (ScopeContext, HttpContext, ThreadPrincipal). Leaving it unset records
            // AuditUserSource.Unspecified - a real actor whose provenance was not declared.
        return Result<AuditUserInfo>.Success(new AuditUserInfo
        {
            UserId = _currentUser.Id,
            UserName = _currentUser.DisplayName,
            Roles = _currentUser.Roles
        });
    }
}

// Registration:
services.AddAuditTrail(options =>
{
    options.UseUserResolver<MyUserResolver>();
    // ...
});
```

### 11.2 Custom source resolver

```csharp
public class ApiSourceResolver : IAuditSourceResolver
{
    public IResult<string> Resolve()
        => Result<string>.Success("MyWebApi-v2");
}

// Registration:
services.AddAuditTrail(options =>
{
    options.UseSourceResolver<ApiSourceResolver>();
});
```

### 11.3 Defaults (when you don't provide custom resolvers)

| Resolver      | Without AspNetCore                                      | With AspNetCore                                    |
|---------------|----------------------------------------------------------|----------------------------------------------------|
| User          | `IAuditScopeContext` > `Thread.CurrentPrincipal` > anonymous | `IAuditScopeContext` > `HttpContext.User` > anonymous |
| Correlation   | `System.Diagnostics.Activity.Current`                    | `X-Correlation-Id` header > `X-Request-Id` > Activity |
| Source        | Returns `"Unknown"`                                      | Returns `"Unknown"` (override recommended)        |

> NOTE: In both columns, a manually-set `IAuditScopeContext` user (`scopeContext.SetUser(...)`) always wins — it's checked first, so you can override the ambient/HTTP identity in tests or for background work running inside an otherwise HTTP-driven app.
>
> With `RzR.DataVigil.AspNetCore`, role claims are matched against each identity's configured `ClaimsIdentity.RoleClaimType`, including secondary identities on a multi-identity `ClaimsPrincipal`. If your host uses a non-default role claim type (for example a JWT using `"roles"`), those claims land in `AuditUserInfo.Roles`, not `Claims`.
>
> Whichever branch resolves the user, the resolver stamps `AuditUserInfo.Source` so the audit record says *how* the identity was determined — see [11.5](#115-recording-how-the-identity-was-resolved).
>
> If you're relying on the `Thread.CurrentPrincipal` fallback in the "Without AspNetCore" column, read [11.4](#114-the-threadcurrentprincipal-trust-model) first — it's ambient, process-wide state with a trust model you need to understand before you depend on it.

### 11.4 The Thread.CurrentPrincipal Trust Model

This section applies to `DefaultUserResolver` — the resolver used by every host that doesn't reference `RzR.DataVigil.AspNetCore`: worker services, console apps, background and hosted services. It resolves the audit actor in this order: `IAuditScopeContext` → `Thread.CurrentPrincipal` → anonymous.

For a while, a null-check bug meant the `Thread.CurrentPrincipal` branch could never actually run — every non-HTTP deployment fell straight through to anonymous. That's fixed now, which means `Thread.CurrentPrincipal` is, for the first time, a live source of audit identity in these hosts. That's a strict improvement over silently losing attribution, but it's worth understanding what you're now trusting.

**It's ambient, process-global, mutable state, and nothing checks who's allowed to set it.**

`Thread.CurrentPrincipal` is a plain settable property:

```csharp
Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("anyone"), null);
```

Any code running in the process can assign it — your code, a library, a dependency. There's no authentication behind that assignment. Setting it doesn't mean someone was actually authenticated; it means something in your process decided this thread should look like that user right now. `DefaultUserResolver` trusts whatever it finds there, the same way it would trust a value you set explicitly through `IAuditScopeContext`.

**On .NET Core, it flows across `await` — and nothing resets it for you.**

Under .NET Framework, `Thread.CurrentPrincipal` was `[ThreadStatic]`: set it, and it stayed pinned to that OS thread. .NET Core changed this — the value now flows with `ExecutionContext`, so it rides along across `await` continuations. In practice, if something upstream in an async call chain sets `Thread.CurrentPrincipal` and never clears it, that value can leak into the *next* logical unit of work processed on the same continuation — a later queue message, a later loop iteration — even though that later work has nothing to do with the original identity.

`DataVigil` never sets or clears `Thread.CurrentPrincipal` itself, before or after resolving it. Managing its lifetime is entirely the host's responsibility.

> WARNING: If your worker sets `Thread.CurrentPrincipal` once — at startup, or the first time it handles a message — and never clears it, every later unit of work on that async chain can get audited under a stale identity. That isn't a bug in the resolver; it's how ambient state behaves by design.

**What to do about it**

- If you set `Thread.CurrentPrincipal` in a worker or console host, set it **and clear it** (back to `null`, or an explicit anonymous principal) around each logical unit of work — per queue message, per job run, per loop iteration. Don't assume it resets between iterations, because it doesn't.
- Prefer `IAuditScopeContext.SetUser()` for worker and console hosts instead — see [Worker Service / Console App](#5-worker-service--console-app-no-httpcontext). It's scoped to a DI scope rather than ambient to the process, so there's no cross-message leakage to reason about, and `DefaultUserResolver` already checks it before `Thread.CurrentPrincipal`. It's also a more auditable choice: the assignment is explicit, in your code, at the point you know who the actor is.
- If your host calls `AppDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal)` — seen in some legacy Windows-service or IIS-adjacent setups — every thread that hasn't had `Thread.CurrentPrincipal` explicitly set gets a non-null, *authenticated* `WindowsPrincipal` for the process or service account automatically. With the fix, `DefaultUserResolver` now picks that up and attributes audit entries to the machine/service account instead of leaving them anonymous. If your host uses this policy, decide deliberately whether that's the attribution you want; set an explicit user via `IAuditScopeContext.SetUser()` if it isn't.

---

### 11.5 Recording how the identity was resolved

An audit record with no `UserId` is ambiguous on its own. It could mean the action was genuinely anonymous, or it could mean identity resolution broke down — and those are very different facts to an auditor. Every transaction therefore records *how* the actor was determined, in `AuditTransaction.Metadata` under the reserved key `__datavigil.user.source`.

| `AuditUserSource` | Recorded when |
|-------------------|---------------|
| `ScopeContext`    | The user was set explicitly via `IAuditScopeContext.SetUser()` |
| `HttpContext`     | The user came from an authenticated `HttpContext.User` |
| `ThreadPrincipal` | The user came from `Thread.CurrentPrincipal` |
| `Anonymous`       | Resolution succeeded and there genuinely was no user |
| `Unresolved`      | Resolution failed — the absence of a user proves nothing about the action |
| `Unspecified`     | A resolver returned a real user but did not declare where it came from |

Reading it back:

```csharp
var source = transaction.Metadata.TryGetValue(AuditMetadataKeys.UserSource, out var value)
    ? value
    : null;
```

The value is stored as the enum member **name**, not its numeric value.

#### Writing SOURCE from a custom resolver

The built-in resolvers stamp `Source` themselves. If you write your own `IAuditUserResolver`, set it on the user you return:

```csharp
public IResult<AuditUserInfo> Resolve()
{
    var user = _currentUser.Get();
    if (user is null)
        return Result<AuditUserInfo>.Success();   // recorded as Anonymous

    return Result<AuditUserInfo>.Success(new AuditUserInfo
    {
        UserId = user.Id,
        UserName = user.Name,
        Source = AuditUserSource.ScopeContext
    });
}
```

Two rules the pipeline enforces regardless of what you stamp:

- Return a **success result with a null response** for a genuinely anonymous action. It is recorded as `Anonymous`.
- Return a **failure result** only when resolution actually broke down. It is recorded as `Unresolved`.

In both of those cases the pipeline overrides whatever `Source` you set, because the outcome of the call is more trustworthy than a field on a payload. Never return a bare `null` — the contract is `IResult<AuditUserInfo>`.

If none of the values describes your source, leave `Source` unset. It records `Unspecified`, which honestly says "a real actor, provenance not declared" rather than falsely claiming resolution failed.

> NOTE: `Metadata` is a public, consumer-writable dictionary, but the `__datavigil.user.source` key is reserved and the pipeline overwrites it on every transaction. Do not use that key for your own data.

---

## 12. Read (SELECT) Auditing

This feature is disabled unless you explicitly opt in. It tracks SELECT queries — basically, who looked at what data and when. Useful for compliance-heavy environments where read access itself is sensitive.

### 12.1 Enable reads

```csharp
services.AddAuditTrail(options =>
{
    options.EfCore
        .Intercept<AppDbContext>()
        .IncludeReads() // Log which entities were read
        .IncludeReadProperties(); // Also log which columns were queried
});
```

### 12.2 Register the read-flush middleware (ASP.NET Core)

```csharp
var app = builder.Build();

app.UseRouting();
app.UseAuditReadFlush(); // Flushes collected read entries after each request
app.MapControllers();
```

While a request is being processed, read audit entries pile up inside `AuditReadCollector`. The middleware takes care of flushing all of them through the audit pipeline after the response has been sent back to the client.

### 12.3 Manual read logging (EF Core)

```csharp
var readService = serviceProvider.GetRequiredService<AuditReadService>();
var order = await dbContext.Orders.FindAsync(42);

await readService.LogReadAsync<Order>(dbContext, order);
```

---

## 13. Manual Audit Entries (No EF Core)

Not everything goes through Entity Framework. Maybe you're calling a stored procedure directly, or hitting an external API, or your project doesn't use EF at all. For those situations you construct the audit transaction yourself and feed it into the pipeline. Here's a full example:

```csharp
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Pipeline;

// Resolve the pipeline from DI
var pipeline = serviceProvider.GetRequiredService<AuditPipeline>();

var transaction = new AuditTransaction
{
    Id = Guid.NewGuid(),
    Timestamp = DateTimeOffset.UtcNow,
    Entries = new List<AuditEntry>
    {
        new AuditEntry
        {
            Id = Guid.NewGuid(),
            EntityName = "Payment",
            EntityId = "PAY-001",
            Action = AuditAction.Create,
            Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty
                {
                    PropertyName = "Amount",
                    PropertyType = "System.Decimal",
                    OldValue = null, // null for Create
                    NewValue = "250.00"
                },
                new AuditEntryProperty
                {
                    PropertyName = "Currency",
                    PropertyType = "System.String",
                    OldValue = null,
                    NewValue = "USD"
                }
            }
        }
    }
};

// Enriches with user/source/correlation info, runs GDPR rules, then saves
var result = await pipeline.ProcessAsync(transaction, cancellationToken);
```

---

## 14. Package Reference Summary

### Minimal setup (Worker + File)

```xml
<PackageReference Include="RzR.DataVigil.Core" />
<PackageReference Include="RzR.DataVigil.Storage.File" />
```

### Web API + EF Core + SQL Server

```xml
<PackageReference Include="RzR.DataVigil.Core" />
<PackageReference Include="RzR.DataVigil.AspNetCore" />
<PackageReference Include="RzR.DataVigil.EFCore" />
<PackageReference Include="RzR.DataVigil.Storage.EfSqlServer" />
```

### Web API + EF Core + PostgreSQL

```xml
<PackageReference Include="RzR.DataVigil.Core" />
<PackageReference Include="RzR.DataVigil.AspNetCore" />
<PackageReference Include="RzR.DataVigil.EFCore" />
<PackageReference Include="RzR.DataVigil.Storage.EfPostgreSql" />
```

### Web API + EF Core + MongoDB

```xml
<PackageReference Include="RzR.DataVigil.Core" />
<PackageReference Include="RzR.DataVigil.AspNetCore" />
<PackageReference Include="RzR.DataVigil.EFCore" />
<PackageReference Include="RzR.DataVigil.Storage.EfMongoDb" />
```

### Registration order matters (for one reason, not the one you might expect)

`AddAuditTrail` still has to run at some point before the host resolves services — it creates the options object (and the DI builder) that other registrations read from. But `AddAuditTrailEfCore()`, `AddAuditTrailAspNetCore()`, `AddAuditRetentionService()`, and the storage provider methods (`AddAuditTrailFileStorage()`, `AddAuditTrailSqlServer()`, `AddAuditTrailPostgreSqlServer()`, `AddAuditTrailMongoDb()`) do not touch the options object at the point they're called — each only registers concrete types, or a factory delegate that resolves `AuditTrailOptions`/`StorageOptions` from DI when the service is first constructed, not when the extension method runs. So calling any of them before `AddAuditTrail()` does not fail; it only matters that `AddAuditTrail()` has run by the time the host actually builds and resolves services, which is virtually always the case during startup configuration.

Beyond that, resolver registration order no longer matters. `IAuditUserResolver` and `IAuditCorrelationProvider` now resolve by explicit precedence — an explicitly configured resolver (`options.UseUserResolver<T>()`) wins over the ASP.NET Core HTTP-based resolver, which wins over the built-in default — regardless of which order you call `AddAuditTrail()` and `AddAuditTrailAspNetCore()` in. Previously, calling `AddAuditTrailAspNetCore()` before `AddAuditTrail()` could silently leave a web app on the non-HTTP `DefaultUserResolver`, with no error. That's fixed. If an ASP.NET Core host still ends up on the non-HTTP resolver (for example, `AddAuditTrailAspNetCore()` was never called), a startup diagnostic now logs a warning so it doesn't go unnoticed.

```
1. services.AddAuditTrail(options => { ... })     ← always first
2. services.AddAuditTrailEfCore()                 ← if using EF Core
3. services.AddAuditTrailSqlServer()              ← storage provider
   services.AddAuditTrailPostgreSqlServer()
   services.AddAuditTrailMongoDb()
   services.AddAuditTrailFileStorage()
4. services.AddAuditTrailAspNetCore()             ← if ASP.NET Core
5. services.AddAuditRetentionService()            ← if retention enabled
```

This sequence is still a reasonable default to follow — it's just no longer load-bearing for which resolver you end up with.
