# Using RzR.DataVigil

Below you'll find setup instructions for a bunch of different scenarios. Web APIs, background workers, plain console apps - it's all covered. Just jump to whichever section fits your situation. No need to read the whole thing top to bottom.

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
13. [HTTP Operation Metadata](#13-http-operation-metadata)
14. [Manual Audit Entries (No EF Core)](#14-manual-audit-entries-no-ef-core)
15. [Column Length Guards](#15-column-length-guards)
16. [Cancellation](#16-cancellation)
17. [Upgrading: The Audit Index Migrations](#17-upgrading-the-audit-index-migrations)
18. [Package Reference Summary](#18-package-reference-summary)

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

This is the setup I'd guess most people end up with. You wire in an EF Core interceptor that keeps an eye on the `ChangeTracker` - whenever something gets saved, it grabs the before/after values and logs them. Pretty hands-off once it's configured.

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
> immediately when it's missing, so without it *nothing is audited at all* - regardless of how the entities
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

First the interceptor goes through the change tracker looking for any Added, Modified, or Deleted entities marked with `IAuditable`. For each one it records the old and new property values. Then it tacks on the user identity (grabbed from `HttpContext.User`), the client IP, and whatever correlation ID it can find. If you set up GDPR rules, those get applied next - masking, hashing, anonymizing, excluding fields, all that stuff. Finally everything gets handed off to `IAuditStore.SaveAsync()`.

You don't have to touch your controllers or repositories at all. It just works.

---

## 3. ASP.NET Core Web API + File Storage

Sometimes standing up a whole separate database just for audit logs feels like overkill. The file storage option is simpler - it dumps everything into JSON files, one per day.

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

`AddAuditTrailFileStorage()` hands `FileAuditStore` an `ILogger<FileAuditStore>` from the container, so a capped query page size is reported (see [Paging rules](#paging-rules)). If you new up the store yourself, prefer the three-argument constructor:

```csharp
// Two-argument constructor: still supported, but a capped page size is reported to nothing.
var store = new FileAuditStore(storageOptions, gdprProcessor);

// Three-argument constructor: pass a logger and the cap becomes visible.
var store = new FileAuditStore(storageOptions, gdprProcessor,
    loggerFactory.CreateLogger<FileAuditStore>());
```

Passing `null` for the logger is allowed and falls back to `NullLogger<FileAuditStore>.Instance`, which is exactly what the two-argument overload does.

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

// MongoDB is schema-less - no migrations needed.
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

`ProcessAsync` returns a failure when `ct` is cancelled mid-flight, and that failure is not a defect - see [Cancellation](#16-cancellation) for how to tell it apart from a real one.

### 5.5 Disposing the scope context no longer clears it

> **WARNING - `IAuditScopeContext.Dispose()` is a no-op. It does not clear the user or the correlation id.**
>
> `AuditScopeContext.Dispose()` has an empty body. Wrapping the context in a `using`, or disposing it by
> hand, leaves whatever you set through `SetUser()` and `SetCorrelationId()` exactly where it was. To clear
> a value, clear it explicitly:
>
> ```csharp
> scopeContext.SetUser(null);
> scopeContext.SetCorrelationId(null);
> ```
>
> This changed deliberately. Clearing on dispose meant that work still in flight after its scope had been
> torn down - a fire-and-forget task started inside a request, a continuation that outlived the request
> pipeline - read the context back as "nothing was ever set" and silently fell through to the ambient
> identity, so the record was attributed to whoever happened to own the surrounding request. A stale value
> you can see is better than a fallback you cannot.
>
> **There is no compiler error for this change.** `Dispose()` still exists and still compiles; only the
> behaviour is different. If you relied on dispose to reset the actor between units of work, that reset is
> now silently gone.

`IAuditScopeContext` is registered with `AddScoped`, so a *fresh* DI scope always starts empty - the worker
loop above creates one per iteration and is unaffected. The hazard is reusing a single scope, or a single
resolved context, across more than one unit of work.

---

## 6. GDPR Configuration

There are two separate layers here and they work independently of each other.

Storage policies change (or remove) sensitive data before anything gets saved. The raw value never touches the database. Retrieval policies are different - they kick in when someone reads audit records back, and they decide what that person is allowed to see based on their roles or claims. You can use one layer, or both together.

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

Worth knowing: the access check uses OR logic. Having any one of the allowed roles is enough. Same with claims - one match and you're in. Only when nothing matches does the field stay hidden.

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

### 8.3 Filtering

`AuditTransactionQuery` carries five optional filters alongside `Skip`/`Take`:

| Property | Type | Applied when | Matches |
|----------|------|--------------|---------|
| `FromUtc` | `DateTimeOffset?` | not null | `Timestamp >= FromUtc` |
| `ToUtc` | `DateTimeOffset?` | not null | `Timestamp < ToUtc` |
| `UserId` | `string` | not null or whitespace | exact equality |
| `CorrelationId` | `string` | not null or whitespace | exact equality |
| `GdprState` | `GdprStorageState?` | not null | exact equality |

Leave one alone and it isn't applied - no predicate is composed at all. So a default `new AuditTransactionQuery()` returns the same unfiltered page it always did. Supplied filters combine with **AND** only; there is no OR and no negation.

"Everything user `alice` did yesterday, where GDPR storage rules touched at least one field":

```csharp
var yesterday = DateTimeOffset.UtcNow.Date.AddDays(-1);

var result = await store.QueryAsync(new AuditTransactionQuery
{
    FromUtc = yesterday,
    ToUtc = yesterday.AddDays(1),
    UserId = "alice",
    GdprState = GdprStorageState.PartiallyProcessed,
    Skip = 0,
    Take = 100
});
```

`GdprState` takes the same `GdprStorageState` values the store writes: `Original`, `PartiallyProcessed`, `FullyAnonymized`, `Erased`. Filtering on `Erased` is the quick way to see what right-to-erasure has already covered.

Or chase a single request across the trail, which is what `CorrelationId` is really for:

```csharp
// Operator / back-office context: the correlation id comes from an incident ticket
// or a log line, not from the inbound request.
var result = await store.QueryAsync(new AuditTransactionQuery
{
    CorrelationId = incident.CorrelationId,
    Take = 500
});
```

> **WARNING - `AuditTransactionQuery` performs no authorization and no tenant scoping.**
>
> Every filter narrows a result set that `QueryAsync` would otherwise return in full. Nothing in this
> library decides *who* is allowed to see a transaction; that decision is entirely yours, and it has to
> happen before you call the store.
>
> `CorrelationId` deserves particular care, because it is **client-supplied on the write side** - the
> ASP.NET Core provider takes it from the `X-Correlation-Id` or `X-Request-Id` request header. It is
> therefore guessable and forgeable, and it is not an identity. Never build an endpoint that reads a
> correlation id straight off the incoming request and hands it to `QueryAsync`: a caller who can guess or
> force a value can read back every transaction stamped with it, including other tenants'. If a query runs
> on behalf of an end user, constrain it to that user as well - for example by also setting `UserId` from
> the authenticated principal rather than from anything the caller sent.

**The date range is half-open:** `>= FromUtc`, `< ToUtc`. Adjacent windows tile without overlapping and without gaps, so paging a month a day at a time never double-counts a transaction that landed exactly on midnight. Each bound is independent - set only one to leave the range open at the other end. `FromUtc` later than `ToUtc` returns zero rows rather than an error.

Values are compared as absolute time. The offset you supply is honoured and never reinterpreted, so a `DateTimeOffset` with a `+02:00` offset means what it says.

> **WARNING:** `UserId` and `CorrelationId` match using **each store's own equality semantics**, and the stores do not agree.
>
> On a relational provider the comparison happens under the column's collation - case-*insensitive* on a default SQL Server installation, so `"Alice"` finds a row stored as `"alice"`. In the file store the same comparison is plain .NET `string ==`, which is ordinal and case-*sensitive*, so it finds nothing.
>
> The same query object can legitimately return different result sets on different stores. If you need a result you can reason about across backends, normalise case when you *write* the value rather than relying on how it's matched when you read it.

Only exact equality is supported. There is no `Contains` or `StartsWith` filter, no `TraceId` filter, and no metadata filter.

#### Paging rules

`Skip` and `Take` are no longer passed to the database as you wrote them. Every store now resolves them
through `AuditTransactionQueryExtensions.GetEffectivePaging(...)` first, against the bounds in
`AuditQueryLimits`, so the same query produces the same page on every backend:

| You request | The store executes | Constant |
|-------------|--------------------|----------|
| `Skip` below 0 | `0` | `AuditQueryLimits.MinSkip` |
| `Take` below 0 | `10` | `AuditQueryLimits.DefaultTake` |
| `Take` above 500 | `500` | `AuditQueryLimits.MaxTake` |
| `Take` of exactly 0 | nothing - the store is never queried | - |

The defaults on a `new AuditTransactionQuery()` are `Skip = 0` and `Take = 10`, both already inside the
bounds, so an unmodified query behaves exactly as before.

`Take = 0` returns a **successful** result with an empty sequence. No connection is opened and no command is
sent. It means "I asked for no records", not "there are no records".

Negative values are a caller mistake rather than a request, so they are normalised identically in all four
stores and the substitution is reported at Debug level. Nothing fails and nothing is returned to tell you it
happened - check the log if a page comes back a size you did not ask for.

> **WARNING - a short page does not mean there are no more records.**
>
> Asking for more than 500 gets you 500, and nothing in the returned result says it was capped. Code shaped
> like this now stops early and under-reports:
>
> ```csharp
> // BROKEN: with pageSize > 500 this exits after the first page.
> var skip = 0;
> var page = (await store.QueryAsync(new AuditTransactionQuery { Skip = skip, Take = pageSize }))
>     .Response.ToList();
>
> while (page.Count == pageSize)
> {
>     Export(page);
>     skip += pageSize;
>     page = (await store.QueryAsync(new AuditTransactionQuery { Skip = skip, Take = pageSize }))
>         .Response.ToList();
> }
> ```
>
> Page with `Skip` instead, and keep `Take` at or below `AuditQueryLimits.MaxTake`:
>
> ```csharp
> var pageSize = AuditQueryLimits.MaxTake;   // 500
> var skip = 0;
>
> while (true)
> {
>     var result = await store.QueryAsync(new AuditTransactionQuery { Skip = skip, Take = pageSize });
>     if (!result.IsSuccess)
>         throw new InvalidOperationException("Audit export aborted; the page could not be read.");
>
>     var page = result.Response.ToList();
>     if (page.Count == 0)
>         break;                              // an empty page is the only reliable end-of-data signal
>
>     Export(page);
>     skip += page.Count;
> }
> ```
>
> The cap is not silent on the store side: it is logged at **Warning**, naming the requested `Take` and the
> maximum. The one exception is `FileAuditStore` constructed through its two-argument constructor
> (`options`, `gdprProcessor`), which substitutes a no-op logger - build it that way and the cap produces no
> signal at all. Resolved from DI, every store including the file store logs the warning.
>
> Capping never drops data. The records beyond the cap stay reachable through `Skip`.

### 8.4 If you implement `IAuditStore` yourself

Filter properties are added to `AuditTransactionQuery` over time, and the interface signature never changes. That keeps existing implementations compiling, at one deliberate cost: **a filter your implementation doesn't know about is ignored by construction**, and the caller gets back a wider result set than they asked for, with no error. When you take a new version of the contracts package, diff `AuditTransactionQuery` and pick up whatever was added.

The contract for a filter you *do* recognise, spelled out in the `<remarks>` on `IAuditStore.QueryAsync`:

- **Apply every filter you recognise.** Don't accept one and quietly drop it.
- **Combine them with AND.**
- **Filter before paging.** `Skip`/`Take` must page over the filtered set, not over the whole store.
- **Don't compose a filter that's at its default.** Null, or null-or-whitespace for the strings, means no predicate - not `WHERE UserId IS NULL`.
- **Fail rather than fake it.** If your provider can't translate a recognised filter, return `Result.Failure`. Do not throw, do not silently drop it, and do not pull the whole table into memory to evaluate it client-side. Returning a wrong-but-plausible page of an audit trail is worse than returning an error.

Order results newest-first by `Timestamp`, then by `Id` descending. The tie-break is not decoration: timestamps aren't unique - every transaction written in one save shares one - so ordering by timestamp alone lets a row appear on two pages and on neither. The tie-break makes paging stable **within your store**. It is not a guarantee that two different stores order ties identically, and it shouldn't be presented to users as one.

---

## 9. GDPR Right-to-Erasure

This covers the "right to be forgotten" from GDPR Article 17. Sometimes a user asks you to delete their data, but you still need the audit trail for compliance reasons. Here's the compromise:

```csharp
var store = serviceProvider.GetRequiredService<IAuditStore>();
await store.AnonymizeByUserAsync("user-123");
```

What this does is go through every audit transaction tied to that user and replace their `UserId`, `UserName`, and `IpAddress` with `[ERASED]`. The records themselves survive - you can still see that actions happened, when they happened, what changed. You just can't tell who did it anymore.

---

## 10. Entity-Level Audit Control

You probably don't want to audit every single table in your database. Temp data, health checks, migration history - that stuff just creates noise. There are several ways to narrow things down depending on how fine-grained you want to get.

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

    // Only audit Create and Delete - skip Update
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

> NOTE: In both columns, a manually-set `IAuditScopeContext` user (`scopeContext.SetUser(...)`) always wins - it's checked first, so you can override the ambient/HTTP identity in tests or for background work running inside an otherwise HTTP-driven app.
>
> With `RzR.DataVigil.AspNetCore`, role claims are matched against each identity's configured `ClaimsIdentity.RoleClaimType`, including secondary identities on a multi-identity `ClaimsPrincipal`. If your host uses a non-default role claim type (for example a JWT using `"roles"`), those claims land in `AuditUserInfo.Roles`, not `Claims`.
>
> Whichever branch resolves the user, the resolver stamps `AuditUserInfo.Source` so the audit record says *how* the identity was determined - see [11.5](#115-recording-how-the-identity-was-resolved).
>
> If you're relying on the `Thread.CurrentPrincipal` fallback in the "Without AspNetCore" column, read [11.4](#114-the-threadcurrentprincipal-trust-model) first - it's ambient, process-wide state with a trust model you need to understand before you depend on it.

### 11.4 The Thread.CurrentPrincipal Trust Model

This section applies to `DefaultUserResolver` - the resolver used by every host that doesn't reference `RzR.DataVigil.AspNetCore`: worker services, console apps, background and hosted services. It resolves the audit actor in this order: `IAuditScopeContext` → `Thread.CurrentPrincipal` → anonymous.

For a while, a null-check bug meant the `Thread.CurrentPrincipal` branch could never actually run - every non-HTTP deployment fell straight through to anonymous. That's fixed now, which means `Thread.CurrentPrincipal` is, for the first time, a live source of audit identity in these hosts. That's a strict improvement over silently losing attribution, but it's worth understanding what you're now trusting.

**It's ambient, process-global, mutable state, and nothing checks who's allowed to set it.**

`Thread.CurrentPrincipal` is a plain settable property:

```csharp
Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("anyone"), null);
```

Any code running in the process can assign it - your code, a library, a dependency. There's no authentication behind that assignment. Setting it doesn't mean someone was actually authenticated; it means something in your process decided this thread should look like that user right now. `DefaultUserResolver` trusts whatever it finds there, the same way it would trust a value you set explicitly through `IAuditScopeContext`.

**On .NET Core, it flows across `await` - and nothing resets it for you.**

Under .NET Framework, `Thread.CurrentPrincipal` was `[ThreadStatic]`: set it, and it stayed pinned to that OS thread. .NET Core changed this - the value now flows with `ExecutionContext`, so it rides along across `await` continuations. In practice, if something upstream in an async call chain sets `Thread.CurrentPrincipal` and never clears it, that value can leak into the *next* logical unit of work processed on the same continuation - a later queue message, a later loop iteration - even though that later work has nothing to do with the original identity.

`DataVigil` never sets or clears `Thread.CurrentPrincipal` itself, before or after resolving it. Managing its lifetime is entirely the host's responsibility.

> WARNING: If your worker sets `Thread.CurrentPrincipal` once - at startup, or the first time it handles a message - and never clears it, every later unit of work on that async chain can get audited under a stale identity. That isn't a bug in the resolver; it's how ambient state behaves by design.

**What to do about it**

- If you set `Thread.CurrentPrincipal` in a worker or console host, set it **and clear it** (back to `null`, or an explicit anonymous principal) around each logical unit of work - per queue message, per job run, per loop iteration. Don't assume it resets between iterations, because it doesn't.
- Prefer `IAuditScopeContext.SetUser()` for worker and console hosts instead - see [Worker Service / Console App](#5-worker-service--console-app-no-httpcontext). It's scoped to a DI scope rather than ambient to the process, so there's no cross-message leakage to reason about, and `DefaultUserResolver` already checks it before `Thread.CurrentPrincipal`. It's also a more auditable choice: the assignment is explicit, in your code, at the point you know who the actor is.
- If your host calls `AppDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal)` - seen in some legacy Windows-service or IIS-adjacent setups - every thread that hasn't had `Thread.CurrentPrincipal` explicitly set gets a non-null, *authenticated* `WindowsPrincipal` for the process or service account automatically. With the fix, `DefaultUserResolver` now picks that up and attributes audit entries to the machine/service account instead of leaving them anonymous. If your host uses this policy, decide deliberately whether that's the attribution you want; set an explicit user via `IAuditScopeContext.SetUser()` if it isn't.

---

### 11.5 Recording how the identity was resolved

An audit record with no `UserId` is ambiguous on its own. It could mean the action was genuinely anonymous, or it could mean identity resolution broke down - and those are very different facts to an auditor. Every transaction therefore records *how* the actor was determined, in `AuditTransaction.Metadata` under the reserved key `__datavigil.user.source`.

| `AuditUserSource` | Recorded when |
|-------------------|---------------|
| `ScopeContext`    | The user was set explicitly via `IAuditScopeContext.SetUser()` |
| `HttpContext`     | The user came from an authenticated `HttpContext.User` |
| `ThreadPrincipal` | The user came from `Thread.CurrentPrincipal` |
| `Anonymous`       | Resolution succeeded and there genuinely was no user |
| `Unresolved`      | Resolution failed - the absence of a user proves nothing about the action |
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

In both of those cases the pipeline overrides whatever `Source` you set, because the outcome of the call is more trustworthy than a field on a payload. Never return a bare `null` - the contract is `IResult<AuditUserInfo>`.

If none of the values describes your source, leave `Source` unset. It records `Unspecified`, which honestly says "a real actor, provenance not declared" rather than falsely claiming resolution failed.

> NOTE: `Metadata` is a public, consumer-writable dictionary, but the `__datavigil.user.source` key is reserved and the pipeline overwrites it on every transaction. Do not use that key for your own data.

---

## 12. Read (SELECT) Auditing

This feature is disabled unless you explicitly opt in. It tracks SELECT queries - basically, who looked at what data and when. Useful for compliance-heavy environments where read access itself is sensitive.

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

## 13. HTTP Operation Metadata

Knowing that `Order#42` changed is useful. Knowing it changed through `PUT /orders/{id}` rather than through an admin bulk import is a lot more useful. `AddAuditTrailAspNetCore()` registers a metadata enricher that records the HTTP operation each change arrived through, into `AuditTransaction.Metadata`.

No schema change is involved - `Metadata` is already persisted.

### 13.1 The two keys

| Constant | Key | Example value | Stamped |
|----------|-----|---------------|---------|
| `AuditMetadataKeys.HttpMethod` | `__datavigil.http.method` | `POST` | By both overloads |
| `AuditMetadataKeys.HttpRoute` | `__datavigil.http.route` | `/orders/{id}` | **Only when you supply a route accessor** |

The route value is the route **template**, not the request path. `/orders/{id}` and not `/orders/42`. That is deliberate: the template groups every request that hit the same endpoint, and it can't accidentally carry an identifier or a personal detail out of the URL and into the audit record.

Both keys are reserved. As with `__datavigil.user.source`, don't write your own data under them.

### 13.2 Method only

```csharp
builder.Services.AddAuditTrailAspNetCore();
```

Audit transactions produced inside a request get `__datavigil.http.method`. The route key never appears. See [13.6](#136-reading-it-back) for the cases where even the method is absent.

### 13.3 Adding the route template

The second overload takes a `Func<HttpContext, string>` that returns the matched route template, or `null` when nothing matched:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

// ASP.NET Core 3.0+ endpoint routing
static string ResolveRouteTemplate(HttpContext httpContext)
    => (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern?.RawText;

builder.Services.AddAuditTrailAspNetCore(ResolveRouteTemplate);
```

Both keys are stamped from then on. `src/samples/ef/WebApiEfPostgreSqlNet9` is wired up this way.

The accessor runs while the audit transaction is being built, so `UseRouting()` must already have run for the request - inside a controller action or a mapped endpoint handler, it has. Called earlier in the pipeline, `GetEndpoint()` returns null and the route key is simply left off.

### 13.4 Why you pass a delegate instead of the library reading the route

`RzR.DataVigil.AspNetCore` targets `netstandard2.1` and builds against `Microsoft.AspNetCore.Http` 2.2.0, deliberately, so that one assembly works on .NET Core 3.1 through .NET 9+. Reading the matched route from inside the library would break that:

- `GetEndpoint()` doesn't exist in the 2.2.0 surface the library compiles against. It was added in ASP.NET Core 3.0.
- The type you need, `RouteEndpoint`, lives in `Microsoft.AspNetCore.Routing`, a package the library would then have to reference and force onto every consumer, including worker hosts that have no routing at all.
- How you get a template out has changed. Classic routing exposes it through `IRoutingFeature`/`RouteData`; endpoint routing exposes it through `RouteEndpoint.RoutePattern.RawText`. Picking one would be wrong for consumers on the other.

Your host knows exactly which ASP.NET Core version it is on, so it's the right place for that one version-specific line. The delegate is the whole of the version-specific surface.

### 13.5 Call order doesn't matter

Registration is order-independent, and the **last non-null accessor wins**:

| Call sequence | Accessor used |
|---------------|---------------|
| Parameterless only | none - method key only |
| Accessor only | that accessor |
| Parameterless, then accessor | that accessor |
| Accessor, then parameterless | the accessor is kept |
| Accessor A, then accessor B | B |

The parameterless overload never clears an accessor installed by an earlier call. This matters if you have a shared composition-root helper calling `AddAuditTrailAspNetCore()` and an application-specific one adding the accessor - previously the order of those two decided whether you got route metadata, silently. Passing a second, different accessor replaces the first rather than throwing.

Repeated calls still register exactly one enricher.

### 13.6 Reading it back

```csharp
transaction.Metadata.TryGetValue(AuditMetadataKeys.HttpMethod, out var method);
transaction.Metadata.TryGetValue(AuditMetadataKeys.HttpRoute, out var route);
```

Treat both as optional. Cases where one or both keys are absent are normal, not failures:

| Situation | Result |
|-----------|--------|
| No `HttpContext` (worker, console, hosted service) | Neither key |
| No accessor supplied | Method only |
| Routing hasn't run, or no endpoint matched | Method only |
| The accessor returns null/empty, or throws | Method only - the exception is swallowed and the audit record is still written |
| A user was set explicitly via `IAuditScopeContext.SetUser()` | Neither key |
| A method longer than 24 characters, or not purely `[A-Za-z]` | Route only, and neither key if no accessor was supplied |

That fifth row is the one worth internalising. Setting a scope user inside a request says "this work is no longer the ambient request's" - a queued job kicked off from a controller, say - so attaching that request's HTTP operation to it would be misleading. Templates are also capped at 512 characters and truncated past that.

---

## 14. Manual Audit Entries (No EF Core)

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

## 15. Column Length Guards

Every audit column the pipeline fills is bounded before the record is written. This is not validation you
opt into - `AuditPipeline.ProcessAsync` applies it to every transaction, including ones you build by hand.

### 15.1 Why the guard exists

An over-long value used to fail the `INSERT`. The store returned a failure, and the business write -
already committed by the time the audit trail is persisted - stayed committed. The change happened and no
audit record of it existed. That is the single worst outcome an audit library can produce, and it was
reachable from a single long HTTP header.

The guard lives in the pipeline rather than in the resolvers on purpose. Resolvers are a public extension
point ([Custom Resolvers](#11-custom-resolvers)), so a third-party `IAuditUserResolver` would bypass a
guard placed there and reintroduce the failed insert.

### 15.2 The limits, and what happens when a value exceeds one

| Field | Limit (characters) | Over the limit |
|-------|--------------------|----------------|
| `UserId` | 256 | Truncated |
| `UserName` | 256 | Truncated |
| `IpAddress` | 64 | Truncated |
| `Source` | 512 | Truncated |
| `CorrelationId` | 256 | **Rejected to `null`** |
| `TraceId` | 256 | **Rejected to `null`** |

The limits are the `AuditColumnLengths` constants, shared by the storage mapping and the guard so the two
cannot drift apart.

Truncation is surrogate-safe. `AuditColumnValue.TruncateToColumnLength` never cuts between the halves of a
surrogate pair, because a lone high surrogate is not encodable as valid UTF-8: PostgreSQL either rejects it
or silently substitutes U+FFFD, which is the same failed insert the guard exists to prevent.

### 15.3 Why the policy differs between the two groups

The split is deliberate, and it is about what a damaged value is *worth to an investigator*.

**Actor attributes are truncated.** `UserId`, `UserName`, `IpAddress` and `Source` describe who acted. A
256-character prefix of a user id is still an investigative lead - you can recognise it, match it, ask about
it. Nulling it produces an unattributed record, which is worse than a partial one: the action is preserved
but the actor is gone entirely.

**Join keys are rejected.** `CorrelationId` and `TraceId` exist to be matched exactly against other records
and other systems. A truncated key looks perfectly valid and joins to nothing. Worse, it can join to the
*wrong* thing. That is false evidence, and an audit trail must not contain any. A null key honestly says
"this record cannot be correlated".

### 15.4 The `__datavigil.oversize` metadata key

When at least one field was guarded, the pipeline records which ones under the reserved metadata key
`AuditMetadataKeys.Oversize` (`__datavigil.oversize`). The value is the affected property names, comma
separated with no spaces, in the fixed order
`UserId,UserName,IpAddress,Source,CorrelationId,TraceId`.

```csharp
if (transaction.Metadata.TryGetValue(AuditMetadataKeys.Oversize, out var guarded))
{
    // e.g. "UserId,CorrelationId"
    foreach (var field in guarded.Split(','))
        Console.WriteLine($"{field} did not fit its column and was guarded.");
}
```

When nothing was guarded, the pipeline **removes** the key rather than leaving whatever was there. That is
what makes the key trustworthy: `Metadata` is public and consumer-writable, so a caller could set
`__datavigil.oversize` on a transaction before submitting it. The removal means a forged value cannot
survive into storage, and the key's presence therefore always reflects what the pipeline actually did.

Treat the key as reserved. Do not write your own data under it.

### 15.5 What gets logged

The guard is reported once per transaction, and the guarded values themselves are never logged - only the
field names, the observed length against the column maximum, and how the actor was resolved:

- **Warning**, in general. Something produced a value too long for its column and you should find out what.
- **Debug**, in one specific case: the actor came from `HttpContext` *and* every guarded field is an actor
  attribute (`UserId`, `UserName`, `IpAddress`). An oversized identity claim arriving from a token is
  common enough, and harmless enough once truncated, that warning on it would be noise.

Either way the audit record is still written. The guard never fails the write.

---

## 16. Cancellation

`AuditPipeline.ProcessAsync` takes a `CancellationToken`. When **your own** token is cancelled while the
pipeline is working, it returns a failure carrying the message key `AuditResultCodes.OperationCanceled`
(`"DATAVIGIL.AUDIT.CANCELED"`) instead of letting the `OperationCanceledException` escape.

Test for it with the extension on `IResult`:

```csharp
using RzR.DataVigil.Abstractions.Extensions;

var result = await pipeline.ProcessAsync(transaction, stoppingToken);

if (result.IsSuccess)
    return;

if (result.IsAuditCanceled())
{
    // Shutdown or an aborted request. Expected. Demote it.
    _logger.LogDebug("Audit write abandoned because the host is shutting down.");
    return;
}

_logger.LogError("Audit write failed: {Reason}", string.Join("; ", result.Messages.Select(m => m.Message)));
```

Two things to be clear about.

**It is still a failure, and no record was written.** `IsAuditCanceled()` does not turn the result into a
success. The audit record is genuinely missing. What the flag tells you is *why* - an expected shutdown
rather than a defect - so you can keep it out of your error reporting without pretending the write happened.
If the missing record matters to you, the cancellation is your signal to retry or to reconcile.

**Match on the key, not on the message text.** The failure text comes from an exception the library does not
own and is culture dependent. `AuditResultCodes.OperationCanceled` is the stable identifier.

The classification is narrow on purpose: the pipeline only maps to this code when
`cancellationToken.IsCancellationRequested` is true. A cancellation raised by something else - a command
timeout inside the store, for instance - stays an ordinary failure, because it is one.

### 16.1 Why the EF Core path never reports cancellation

The `SaveChanges` interceptor calls the pipeline with `CancellationToken.None`, deliberately, not with the
token passed to `SaveChangesAsync`.

Audit records are persisted in `SavedChanges`/`SavedChangesAsync`, after the audited write has already
committed. At that point cancelling the audit write would leave exactly the hole this design exists to
prevent: a committed business change with no audit record. Since the work being audited is already durable,
the audit write is seen through regardless of the caller's token.

A consequence worth knowing: audit persistence can outlive a cancelled request by the duration of one write.
Interceptor failures are caught and logged at Warning, and never propagate into your `SaveChangesAsync`
call.

---

## 17. Upgrading: The Audit Index Migrations

This section only matters if you already have an `audit.AuditTransactions` table with data in it and you are
taking a newer version of `RzR.DataVigil.Storage.EfSqlServer` or `RzR.DataVigil.Storage.EfPostgreSql`. A new
installation just runs the migrations against an empty table and can stop reading here.

### 17.1 What the two migrations do

Each relational provider ships two new migrations, applied in this order:

1. `WidenAuditTransactionQueryIndexes` - creates the replacement indexes.
2. `DropLegacyAuditTransactionIndexes` - drops the ones they replace.

Every secondary index on `AuditTransactions` becomes a composite ending in `Id`:

| Legacy index | Replacement |
|--------------|-------------|
| `IX_AuditTransactions_Timestamp` | `IX_AuditTransactions_Timestamp_Id` |
| `IX_AuditTransactions_UserId` | `IX_AuditTransactions_UserId_Timestamp_Id` |
| `IX_AuditTransactions_CorrelationId` | `IX_AuditTransactions_CorrelationId_Timestamp_Id` |
| `IX_AuditTransactions_GdprState_Timestamp` | `IX_AuditTransactions_GdprState_Timestamp_Id` |

The reason is the ordering contract in [8.4](#84-if-you-implement-iauditstore-yourself): results come back
`Timestamp` descending, then `Id` descending. Without `Id` in the index the tie-break forced a sort on every
filtered page.

The split into two migrations is what makes the upgrade survivable: the replacements are in place and
serving queries before anything is dropped, so there is no window where a filter has no index behind it.

### 17.2 Before you run them

**Run migrations from exactly one process.** EF Core 5 takes no migration lock. Two instances starting at
the same time both see the same pending migrations and both start applying them. The DDL is written to be
idempotent, so this does not corrupt anything, but two simultaneous index builds against a production-sized
audit table is not a situation you want to be in. Migrate from a single instance, a release job, or a
deployment step - not from every replica's startup path.

**Check free space.** Both the old and the new indexes exist at the same time between the two migrations, so
you need at least the current total index size of `AuditTransactions` available on top of what you already
use.

**Expect slower audit inserts during the window.** With both sets present, every insert maintains eight
secondary indexes instead of four. The window lasts from the first migration until the drop completes. If
you apply both in one startup it is roughly the duration of the index builds; if you deliberately stage
them, it is however long you leave between them.

**Budget for a long build.** `MigrateAuditSqlServerDb()` and `MigrateAuditPostgreSqlDb()` raise the command
timeout to six hours for the migration scope, because the provider default of thirty seconds will abort a
build on a large table. If you run the migrations some other way, raise the timeout yourself.

### 17.3 SQL Server: online or offline depends on your edition

The migration decides at runtime, from `SERVERPROPERTY('EngineEdition')`:

| Edition | `EngineEdition` | Index build |
|---------|-----------------|-------------|
| Enterprise (and Developer/Evaluation) | 3 | `ONLINE = ON` |
| Azure SQL Database | 5 | `ONLINE = ON` |
| Azure SQL Managed Instance | 8 | `ONLINE = ON` |
| Standard, Web, Express | everything else | `ONLINE = OFF` |

> **WARNING - on Standard, Web or Express the build is offline and blocks audit writes.**
>
> An offline index build holds a lock for the duration. Audit inserts against `AuditTransactions` wait
> behind it, and since audit persistence happens after your business write has committed, a long enough
> build shows up as slow requests, not as failed ones. Apply these two migrations in a maintenance window on
> those editions.

### 17.4 PostgreSQL: do not wrap the script in a transaction

Both migrations use `CREATE INDEX CONCURRENTLY` and `DROP INDEX CONCURRENTLY`, which PostgreSQL refuses to
run inside a transaction block. The migrations declare `suppressTransaction: true` so that `Database.Migrate()`
executes them outside one.

```bash
# WRONG - -1 wraps the whole script in a single transaction and the CONCURRENTLY statements fail.
psql -1 -f audit-migration.sql
```

Use `MigrateAuditPostgreSqlDb()` (which calls `Database.Migrate()`) and let EF Core honour the suppression.
If you must run a generated script, run it without any wrapping transaction - no `-1`, no
`--single-transaction`, and no `BEGIN` of your own.

**If a concurrent build fails, PostgreSQL leaves an INVALID index behind**, and `CREATE INDEX CONCURRENTLY IF
NOT EXISTS` will then skip it forever, because something with that name exists. `DropLegacyAuditTransactionIndexes`
checks for exactly this before it drops anything: if any replacement index is missing or marked invalid it
raises an error naming them and leaves the legacy indexes in place. Recover with
`REINDEX INDEX CONCURRENTLY`, or `DROP INDEX CONCURRENTLY` and re-apply `WidenAuditTransactionQueryIndexes`,
then run the drop migration again.

### 17.5 Pre-creating the indexes by hand

Both migrations are idempotent - SQL Server guards each statement with a `sys.indexes` existence check,
PostgreSQL uses `IF NOT EXISTS` / `IF EXISTS`. A large installation can therefore create the four
replacement indexes manually, in its own maintenance window and at its own pace, and then let
`WidenAuditTransactionQueryIndexes` run as a no-op that only records itself in the migrations history table.

The index **names must match the table above exactly**. The existence checks match on name, so an index with
the right columns and a different name is invisible to them: the migration will build a duplicate, and the
PostgreSQL drop guard will still consider the replacement missing.

Both migrations implement `Down()`, so a rollback restores the legacy indexes.

### 17.6 PostgreSQL 13 is the tested minimum

The lowest PostgreSQL major version this library is tested against is **13**. Nothing blocks an older server
by default; after `Migrate()` succeeds, `MigrateAuditPostgreSqlDb()` reads the server version and logs the
result - a Warning naming the version when it is below 13, Debug when it is not, and Debug when the version
could not be read at all.

To make it a hard startup failure instead:

```csharp
builder.Services.AddAuditTrail(options =>
{
    options.Storage.UsePostgreSql(builder.Configuration.GetConnectionString("AuditDb"));

    // Refuse to start against an untested PostgreSQL major version.
    options.Storage.ThrowOnUnsupportedPostgreSqlVersion = true;
});
```

The check runs **before** `Migrate()`, so a rejected server is left completely untouched - no migration is
applied and the audit database is never left half-migrated. It throws `NotSupportedException`. A server
whose version cannot be read is not rejected.

---

## 18. Package Reference Summary

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

`AddAuditTrail` still has to run at some point before the host resolves services - it creates the options object (and the DI builder) that other registrations read from. But `AddAuditTrailEfCore()`, `AddAuditTrailAspNetCore()`, `AddAuditRetentionService()`, and the storage provider methods (`AddAuditTrailFileStorage()`, `AddAuditTrailSqlServer()`, `AddAuditTrailPostgreSqlServer()`, `AddAuditTrailMongoDb()`) do not touch the options object at the point they're called - each only registers concrete types, or a factory delegate that resolves `AuditTrailOptions`/`StorageOptions` from DI when the service is first constructed, not when the extension method runs. So calling any of them before `AddAuditTrail()` does not fail; it only matters that `AddAuditTrail()` has run by the time the host actually builds and resolves services, which is virtually always the case during startup configuration.

Beyond that, resolver registration order no longer matters. `IAuditUserResolver` and `IAuditCorrelationProvider` now resolve by explicit precedence - an explicitly configured resolver (`options.UseUserResolver<T>()`) wins over the ASP.NET Core HTTP-based resolver, which wins over the built-in default - regardless of which order you call `AddAuditTrail()` and `AddAuditTrailAspNetCore()` in. Previously, calling `AddAuditTrailAspNetCore()` before `AddAuditTrail()` could silently leave a web app on the non-HTTP `DefaultUserResolver`, with no error. That's fixed. If an ASP.NET Core host still ends up on the non-HTTP resolver (for example, `AddAuditTrailAspNetCore()` was never called), a startup diagnostic now logs a warning so it doesn't go unnoticed.

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

This sequence is still a reasonable default to follow - it's just no longer load-bearing for which resolver you end up with.
