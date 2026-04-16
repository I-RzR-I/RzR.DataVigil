using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.EFCore.Extensions;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Entities;
using RzR.DataVigil.EFCore.Tests.Stubs;

namespace RzR.DataVigil.EFCore.Tests
{
    /// <summary>
    ///     End-to-end tests simulating a console application (non-web) scenario:
    ///     full DI registration, no ASP.NET Core, IAuditScopeContext for user identity.
    /// </summary>
    [TestClass]
    public class ConsoleAppAuditTests
    {
        private ServiceProvider _sp;
        private InMemoryAuditStore _auditStore;
        private string _dbName;

        [TestInitialize]
        public void Setup()
        {
            _dbName = Guid.NewGuid().ToString();
            _auditStore = new InMemoryAuditStore();

            var services = new ServiceCollection();

            // Logging (required by interceptors)
            services.AddLogging();

            // Register audit trail the same way a console app would
            services.AddAuditTrail(opts =>
            {
                opts.EfCore.Intercept<AuditableTestDbContext>();
            });

            // Register EF Core audit interceptors
            services.AddAuditTrailEfCore();

            // Register the in-memory audit store
            services.AddSingleton<IAuditStore>(_auditStore);

            // Register DbContext with audit interceptors wired via DI
            services.AddDbContext<AuditableTestDbContext>((sp, opts) =>
            {
                opts.UseInMemoryDatabase(_dbName);
                opts.AddAuditInterceptors(sp);
            });

            _sp = services.BuildServiceProvider();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _sp?.Dispose();
        }

        [TestMethod]
        public async Task ConsoleApp_ScopeContextUser_EnrichedOnTransaction()
        {
            using var scope = _sp.CreateScope();
            var scopeCtx = scope.ServiceProvider.GetRequiredService<IAuditScopeContext>();
            scopeCtx.SetUser(new AuditUserInfo
            {
                UserId = "console-user-42",
                UserName = "BatchRunner",
                IpAddress = "10.0.0.1"
            });

            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Alice", Total = 100m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _auditStore.Transactions.Count);
            var tx = _auditStore.Transactions[0];
            Assert.AreEqual("console-user-42", tx.UserId);
            Assert.AreEqual("BatchRunner", tx.UserName);
            Assert.AreEqual("10.0.0.1", tx.IpAddress);
        }

        [TestMethod]
        public async Task ConsoleApp_NoScopeUser_TransactionHasNullUserId()
        {
            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Bob", Total = 50m, Quantity = 2 });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _auditStore.Transactions.Count);
            var tx = _auditStore.Transactions[0];
            Assert.IsNull(tx.UserId);
        }

        [TestMethod]
        public async Task ConsoleApp_ChangeUserBetweenSaves_EachTransactionGetsCorrectUser()
        {
            using var scope = _sp.CreateScope();
            var scopeCtx = scope.ServiceProvider.GetRequiredService<IAuditScopeContext>();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();

            // First save as user A
            scopeCtx.SetUser(new AuditUserInfo { UserId = "user-A", UserName = "Alice" });
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Order1", Total = 10m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            // Second save as user B
            scopeCtx.SetUser(new AuditUserInfo { UserId = "user-B", UserName = "Bob" });
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Order2", Total = 20m, Quantity = 2 });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(2, _auditStore.Transactions.Count);
            Assert.AreEqual("user-A", _auditStore.Transactions[0].UserId);
            Assert.AreEqual("user-B", _auditStore.Transactions[1].UserId);
        }

        [TestMethod]
        public async Task ConsoleApp_DefaultSource_IsUnknown()
        {
            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Carol", Total = 75m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            var tx = _auditStore.Transactions.Single();
            Assert.AreEqual("Unknown", tx.Source);
        }

        [TestMethod]
        public async Task ConsoleApp_CustomSourceResolver_OverridesDefault()
        {
            var store = new InMemoryAuditStore();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuditTrail(opts =>
            {
                opts.EfCore.Intercept<AuditableTestDbContext>();
                opts.UseSourceResolver<ConsoleSourceResolver>();
            });
            services.AddAuditTrailEfCore();
            services.AddSingleton<IAuditStore>(store);

            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<AuditableTestDbContext>((sp, opts) =>
            {
                opts.UseInMemoryDatabase(dbName);
                opts.AddAuditInterceptors(sp);
            });

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Dave", Total = 200m, Quantity = 5 });
            await ctx.SaveChangesAsync();

            var tx = store.Transactions.Single();
            Assert.AreEqual("ConsoleApp", tx.Source);
        }

        [TestMethod]
        public async Task ConsoleApp_NoActivity_CorrelationIdIsNull()
        {
            Activity.Current = null;

            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            ctx.Orders.Add(new AuditableOrder { CustomerName = "NoActivity", Total = 1m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            var tx = _auditStore.Transactions.Single();
            Assert.IsNull(tx.CorrelationId);
        }

        [TestMethod]
        public async Task ConsoleApp_WithActivity_CorrelationIdPopulated()
        {
            var source = new ActivitySource("Test.ConsoleApp");
            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            using var activity = source.StartActivity("TestOperation");
            Assert.IsNotNull(Activity.Current);

            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            ctx.Orders.Add(new AuditableOrder { CustomerName = "WithActivity", Total = 1m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            var tx = _auditStore.Transactions.Single();
            Assert.IsNotNull(tx.CorrelationId);
            Assert.IsNotNull(tx.TraceId);
        }

        [TestMethod]
        public async Task ConsoleApp_Create_AuditedViaDI()
        {
            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Eve", Total = 10m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _auditStore.Transactions.Count);
            var entry = _auditStore.Transactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Create, entry.Action);
            Assert.AreEqual("AuditableOrder", entry.EntityName);
        }

        [TestMethod]
        public async Task ConsoleApp_Update_AuditedViaDI()
        {
            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            var order = new AuditableOrder { CustomerName = "Frank", Total = 20m, Quantity = 2 };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            _auditStore.Transactions.Clear();

            order.Total = 25m;
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _auditStore.Transactions.Count);
            var entry = _auditStore.Transactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Update, entry.Action);
        }

        [TestMethod]
        public async Task ConsoleApp_Delete_AuditedViaDI()
        {
            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            var order = new AuditableOrder { CustomerName = "Grace", Total = 30m, Quantity = 3 };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            _auditStore.Transactions.Clear();

            ctx.Orders.Remove(order);
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _auditStore.Transactions.Count);
            var entry = _auditStore.Transactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Delete, entry.Action);
        }

        [TestMethod]
        public async Task ConsoleApp_FullCrudLifecycle_AllAudited()
        {
            using var scope = _sp.CreateScope();
            var scopeCtx = scope.ServiceProvider.GetRequiredService<IAuditScopeContext>();
            scopeCtx.SetUser(new AuditUserInfo { UserId = "console-admin", UserName = "Admin" });

            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();

            var order = new AuditableOrder { CustomerName = "Lifecycle", Total = 100m, Quantity = 5 };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();

            order.Total = 150m;
            await ctx.SaveChangesAsync();

            ctx.Orders.Remove(order);
            await ctx.SaveChangesAsync();

            Assert.AreEqual(3, _auditStore.Transactions.Count);

            Assert.AreEqual(AuditAction.Create, _auditStore.Transactions[0].Entries.Single().Action);
            Assert.AreEqual("console-admin", _auditStore.Transactions[0].UserId);
            Assert.AreEqual("Unknown", _auditStore.Transactions[0].Source);

            Assert.AreEqual(AuditAction.Update, _auditStore.Transactions[1].Entries.Single().Action);
            Assert.AreEqual("console-admin", _auditStore.Transactions[1].UserId);

            Assert.AreEqual(AuditAction.Delete, _auditStore.Transactions[2].Entries.Single().Action);
            Assert.AreEqual("console-admin", _auditStore.Transactions[2].UserId);
        }

        [TestMethod]
        public async Task ConsoleApp_SeparateScopes_IndependentUserContext()
        {
            using (var scope1 = _sp.CreateScope())
            {
                var scopeCtx1 = scope1.ServiceProvider.GetRequiredService<IAuditScopeContext>();
                scopeCtx1.SetUser(new AuditUserInfo { UserId = "user-A", UserName = "ScopeA" });

                var ctx1 = scope1.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
                ctx1.Orders.Add(new AuditableOrder { CustomerName = "Scope1", Total = 1m, Quantity = 1 });
                await ctx1.SaveChangesAsync();
            }

            using (var scope2 = _sp.CreateScope())
            {
                var scopeCtx2 = scope2.ServiceProvider.GetRequiredService<IAuditScopeContext>();
                scopeCtx2.SetUser(new AuditUserInfo { UserId = "user-B", UserName = "ScopeB" });

                var ctx2 = scope2.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
                ctx2.Orders.Add(new AuditableOrder { CustomerName = "Scope2", Total = 2m, Quantity = 2 });
                await ctx2.SaveChangesAsync();
            }

            Assert.AreEqual(2, _auditStore.Transactions.Count);
            Assert.AreEqual("user-A", _auditStore.Transactions[0].UserId);
            Assert.AreEqual("user-B", _auditStore.Transactions[1].UserId);
        }

        [TestMethod]
        public async Task ConsoleApp_ScopeDisposed_UserDoesNotLeakToNextScope()
        {
            using (var scope1 = _sp.CreateScope())
            {
                var scopeCtx = scope1.ServiceProvider.GetRequiredService<IAuditScopeContext>();
                scopeCtx.SetUser(new AuditUserInfo { UserId = "leaky-user" });

                var ctx1 = scope1.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
                ctx1.Orders.Add(new AuditableOrder { CustomerName = "S1", Total = 1m, Quantity = 1 });
                await ctx1.SaveChangesAsync();
            }

            using (var scope2 = _sp.CreateScope())
            {
                var ctx2 = scope2.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
                ctx2.Orders.Add(new AuditableOrder { CustomerName = "S2", Total = 2m, Quantity = 1 });
                await ctx2.SaveChangesAsync();
            }

            Assert.AreEqual(2, _auditStore.Transactions.Count);
            Assert.AreEqual("leaky-user", _auditStore.Transactions[0].UserId);
            Assert.IsNull(_auditStore.Transactions[1].UserId, "User from previous scope should not leak");
        }

        [TestMethod]
        public async Task ConsoleApp_MultipleSavesInOneScope_AllAudited()
        {
            using var scope = _sp.CreateScope();
            var scopeCtx = scope.ServiceProvider.GetRequiredService<IAuditScopeContext>();
            scopeCtx.SetUser(new AuditUserInfo { UserId = "batch-user", UserName = "Batch" });

            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();

            ctx.Orders.Add(new AuditableOrder { CustomerName = "Batch1", Total = 10m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            ctx.Orders.Add(new AuditableOrder { CustomerName = "Batch2", Total = 20m, Quantity = 2 });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(2, _auditStore.Transactions.Count);
            Assert.IsTrue(_auditStore.Transactions.All(t => t.UserId == "batch-user"));
        }

        [TestMethod]
        public async Task ConsoleApp_ManualPipeline_WithoutEfCore_StoresTransaction()
        {
            using var scope = _sp.CreateScope();
            var scopeCtx = scope.ServiceProvider.GetRequiredService<IAuditScopeContext>();
            scopeCtx.SetUser(new AuditUserInfo { UserId = "etl-job", UserName = "ETL" });

            var pipeline = scope.ServiceProvider.GetRequiredService<AuditPipeline>();

            var tx = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Entries = new[]
                {
                    new AuditEntry
                    {
                        Id = Guid.NewGuid(),
                        Action = AuditAction.Create,
                        EntityName = "ImportRecord",
                        EntityTypeName = "MyApp.ImportRecord",
                        EntityId = "42"
                    }
                }
            };

            var result = await pipeline.ProcessAsync(tx);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, _auditStore.Transactions.Count);

            var saved = _auditStore.Transactions.Single();
            Assert.AreEqual("etl-job", saved.UserId);
            Assert.AreEqual("ETL", saved.UserName);
            Assert.AreEqual("Unknown", saved.Source);
            Assert.AreEqual("ImportRecord", saved.Entries.Single().EntityName);
        }

        [TestMethod]
        public async Task ConsoleApp_ManualPipeline_AnonymousUser_Succeeds()
        {
            using var scope = _sp.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<AuditPipeline>();

            var tx = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                Entries = new[]
                {
                    new AuditEntry
                    {
                        Id = Guid.NewGuid(),
                        Action = AuditAction.Update,
                        EntityName = "Config",
                        EntityTypeName = "MyApp.Config",
                        EntityId = "1"
                    }
                }
            };

            var result = await pipeline.ProcessAsync(tx);

            Assert.IsTrue(result.IsSuccess);
            var saved = _auditStore.Transactions.Single();
            Assert.IsNull(saved.UserId);
            Assert.IsNull(saved.UserName);
        }

        [TestMethod]
        public void ConsoleApp_SyncSaveChanges_AuditedViaDI()
        {
            using var scope = _sp.CreateScope();
            var scopeCtx = scope.ServiceProvider.GetRequiredService<IAuditScopeContext>();
            scopeCtx.SetUser(new AuditUserInfo { UserId = "sync-user" });

            var ctx = scope.ServiceProvider.GetRequiredService<AuditableTestDbContext>();
            ctx.Orders.Add(new AuditableOrder { CustomerName = "SyncOp", Total = 5m, Quantity = 1 });
            ctx.SaveChanges();

            Assert.AreEqual(1, _auditStore.Transactions.Count);
            Assert.AreEqual("sync-user", _auditStore.Transactions[0].UserId);
            Assert.AreEqual(AuditAction.Create, _auditStore.Transactions[0].Entries.Single().Action);
        }
    }
}
