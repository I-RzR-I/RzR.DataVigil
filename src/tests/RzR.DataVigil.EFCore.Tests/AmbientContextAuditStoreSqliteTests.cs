#region U S I N G

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.EFCore.Interceptors;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Entities;
using RzR.DataVigil.EFCore.Tests.Stubs;

#endregion

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class AmbientContextAuditStoreSqliteTests
    {
        private SqliteConnection _connection;
        private DbContextOptions<SqliteTestDbContext> _dbOptions;

        [TestInitialize]
        public void Setup()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _connection.Dispose();
        }

        [TestMethod]
        public async Task SaveChanges_StoreWritesThroughAuditedContext_RowIsCommitted()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Vitas", Total = 120.50m });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, store.SavedTransactions.Count, "The audit pipeline never reached the store.");

            using var verify = new SqliteTestDbContext(_dbOptions);
            var rows = await verify.AuditOutbox.AsNoTracking().ToListAsync();

            Assert.AreEqual(1, rows.Count,
                "The store appended its row to the audited context but it was never committed, so the audit record is lost.");
            Assert.AreEqual(AmbientContextAuditStore.Topic, rows[0].Topic);
        }

        [TestMethod]
        public async Task SaveChanges_StoreWritesThroughAuditedContext_LeavesNothingPending()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Vera Viewer", Total = 10m });
            await ctx.SaveChangesAsync();

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "The context still holds unsaved changes after SaveChanges returned.");
        }

        [TestMethod]
        public async Task SaveChanges_StoreWritesThroughAuditedContext_DoesNotRecurse()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Giga Operator", Total = 5m });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, store.SavedTransactions.Count);

            using var verify = new SqliteTestDbContext(_dbOptions);
            Assert.AreEqual(1, await verify.AuditOutbox.CountAsync());
        }

        [TestMethod]
        public async Task SaveChanges_StoreWritesThroughAuditedContext_SeesTheRealStoreGeneratedKey()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            var order = new IdentityKeyedOrder { CustomerName = "Alpha Author", Total = 42m };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();

            var entry = store.SavedTransactions.Single().Entries.Single();

            Assert.AreEqual(order.Id.ToString(), entry.EntityId,
                "The audit entry recorded a temporary key instead of the one the database generated.");
        }

        [TestMethod]
        public async Task SaveChanges_InsideACallerTransaction_RollingBackDiscardsBusinessRowAndAuditRow()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            using (var transaction = await ctx.Database.BeginTransactionAsync())
            {
                ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Beta Author", Total = 7m });
                await ctx.SaveChangesAsync();

                await transaction.RollbackAsync();
            }

            using var verify = new SqliteTestDbContext(_dbOptions);

            Assert.AreEqual(0, await verify.Orders.CountAsync());
            Assert.AreEqual(0, await verify.AuditOutbox.CountAsync(),
                "The audit row survived a rollback that discarded the business change it describes.");
        }

        [TestMethod]
        public async Task SaveChanges_InsideACallerTransaction_CommittingPersistsBusinessRowAndAuditRow()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            using (var transaction = await ctx.Database.BeginTransactionAsync())
            {
                ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Delta Author", Total = 9m });
                await ctx.SaveChangesAsync();

                await transaction.CommitAsync();
            }

            using var verify = new SqliteTestDbContext(_dbOptions);

            Assert.AreEqual(1, await verify.Orders.CountAsync());
            Assert.AreEqual(1, await verify.AuditOutbox.CountAsync(),
                "The audit row was not carried by the caller's transaction.");
        }

        [TestMethod]
        public void SaveChanges_AcceptAllChangesFalse_DoesNotReSendTheCallersWrites()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            var order = new IdentityKeyedOrder { CustomerName = "Epsilon Author", Total = 11m };
            ctx.Orders.Add(order);

            ctx.SaveChanges(false);

            ctx.ChangeTracker.AcceptAllChanges();

            using var verify = new SqliteTestDbContext(_dbOptions);

            Assert.AreEqual(1, verify.Orders.Count(), "The caller's row was written more than once.");
        }

        [TestMethod]
        public async Task Update_StoreWritesThroughAuditedContext_RowIsCommitted()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            var order = new IdentityKeyedOrder { CustomerName = "Zeta Author", Total = 1m };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();

            order.Total = 99m;
            await ctx.SaveChangesAsync();

            Assert.AreEqual(2, store.SavedTransactions.Count);
            Assert.AreEqual(AuditAction.Update, store.SavedTransactions[1].Entries.Single().Action);

            using var verify = new SqliteTestDbContext(_dbOptions);
            Assert.AreEqual(2, await verify.AuditOutbox.CountAsync(),
                "The update's audit row was not committed.");
        }

        [TestMethod]
        public async Task Delete_StoreWritesThroughAuditedContext_RowIsCommitted()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store);

            var order = new IdentityKeyedOrder { CustomerName = "Eta Author", Total = 2m };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();

            ctx.Orders.Remove(order);
            await ctx.SaveChangesAsync();

            Assert.AreEqual(2, store.SavedTransactions.Count);
            Assert.AreEqual(AuditAction.Delete, store.SavedTransactions[1].Entries.Single().Action);

            using var verify = new SqliteTestDbContext(_dbOptions);
            Assert.AreEqual(2, await verify.AuditOutbox.CountAsync(),
                "The delete's audit row was not committed.");
        }

        [TestMethod]
        public async Task Read_StoreWritesThroughAuditedContext_IsNotCommitted()
        {
            var store = new AmbientContextAuditStore();
            using var ctx = CreateContext(store, includeReads: true);

            ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Theta Author", Total = 4m });
            await ctx.SaveChangesAsync();

            var rowsAfterCreate = await CountOutboxAsync();
            var transactionsAfterCreate = store.SavedTransactions.Count;

            _ = await ctx.Orders.AsNoTracking().ToListAsync();

            Assert.IsTrue(store.SavedTransactions.Count > transactionsAfterCreate,
                "Read auditing is enabled but no Read transaction reached the store.");
            Assert.AreEqual(AuditAction.Read, store.SavedTransactions[^1].Entries.Single().Action);

            Assert.AreEqual(rowsAfterCreate, await CountOutboxAsync(),
                "A Read audit row was committed. Read support for ambient-context stores now exists, "
                + "so the documented limitation is stale.");
        }

        private async Task<int> CountOutboxAsync()
        {
            using var verify = new SqliteTestDbContext(_dbOptions);

            return await verify.AuditOutbox.CountAsync();
        }

        [TestMethod]
        public async Task SaveChanges_StoreWritesToIndependentStorage_AuditedContextIsUntouched()
        {
            var store = new StubAuditStore();
            using var ctx = CreateContext(store);

            ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Gamma Author", Total = 3m });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, store.SavedTransactions.Count);

            using var verify = new SqliteTestDbContext(_dbOptions);
            Assert.AreEqual(0, await verify.AuditOutbox.CountAsync(),
                "A store writing to independent storage must not cause anything to be written here.");
        }

        private SqliteTestDbContext CreateContext(IAuditStore store, bool includeReads = false)
        {
            var options = new AuditTrailOptions();
            options.EfCore.Intercept<SqliteTestDbContext>();

            if (includeReads)
                options.EfCore.IncludeReads();

            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                store);

            var factory = LoggerFactory.Create(_ => { });

            var builder = new DbContextOptionsBuilder<SqliteTestDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(new AuditSaveChangesInterceptor(options, pipeline,
                    factory.CreateLogger<AuditSaveChangesInterceptor>()));

            if (includeReads)
                builder.AddInterceptors(new AuditCommandInterceptor(options, pipeline,
                    factory.CreateLogger<AuditCommandInterceptor>()));

            _dbOptions = builder.Options;

            var context = new SqliteTestDbContext(_dbOptions);
            context.Database.EnsureCreated();

            if (store is AmbientContextAuditStore ambient)
                ambient.Context = context;

            return context;
        }
    }
}
