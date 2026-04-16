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
using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
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
        e.MaskOnStorage(c => c.Email); // "alice@mail.com" > "a**********m"
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

This registers a background service called `AuditRetentionService`. It wakes up once a day, checks the retention setting, and deletes anything that's too old. If you didn't configure a retention period it just does nothing. And it swallows any exceptions so it won't take down your app if something goes wrong with the cleanup.

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
using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;

public class MyUserResolver : IAuditUserResolver
{
    private readonly ICurrentUserService _currentUser;

    public MyUserResolver(ICurrentUserService currentUser)
        => _currentUser = currentUser;

    public IResult<AuditUserInfo> Resolve()
    {
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
| User          | `IAuditScopeContext` > `Thread.CurrentPrincipal` > anonymous | `HttpContext.User` > falls back to scope context |
| Correlation   | `System.Diagnostics.Activity.Current`                    | `X-Correlation-Id` header > `X-Request-Id` > Activity |
| Source        | Returns `"Unknown"`                                      | Returns `"Unknown"` (override recommended)        |

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

### Registration order matters

The order you call these matters. `AddAuditTrail` has to come first because it creates the options object that all the other registrations rely on. After that, the order of the rest is less critical but sticking to this sequence avoids surprises.

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
