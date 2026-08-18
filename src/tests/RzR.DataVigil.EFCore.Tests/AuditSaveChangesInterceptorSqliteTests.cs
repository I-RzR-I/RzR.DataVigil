// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.EFCore.Tests
//  Author            : RzR
//  Created           : 18-08-2026 22:08
// 
//  Last Modified By : RzR
//  Last Modified On : 19-08-2026 00:37
//  ***********************************************************************
//  <copyright file="AuditSaveChangesInterceptorSqliteTests.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    public class AuditSaveChangesInterceptorSqliteTests
    {
        private SqliteConnection _connection;
        private DbContextOptions<SqliteTestDbContext> _dbOptions;
        private AuditSaveChangesInterceptor _interceptor;
        private StubAuditStore _store;

        [TestInitialize]
        public void Setup()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _store = new StubAuditStore();
            _interceptor = BuildInterceptor<SqliteTestDbContext>(_store);

            _dbOptions = new DbContextOptionsBuilder<SqliteTestDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(_interceptor)
                .Options;

            using var ctx = new SqliteTestDbContext(_dbOptions);
            ctx.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _connection.Dispose();
        }

        private static AuditSaveChangesInterceptor BuildInterceptor<TContext>(StubAuditStore store)
            where TContext : class
        {
            var options = new AuditTrailOptions();
            options.EfCore.Intercept<TContext>();

            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                store);

            var logger = LoggerFactory.Create(_ => { }).CreateLogger<AuditSaveChangesInterceptor>();

            return new AuditSaveChangesInterceptor(options, pipeline, logger);
        }

        [TestMethod]
        public async Task SaveChanges_IdentityKey_AuditEntityIdIsRealKey()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);
            var order = new IdentityKeyedOrder { CustomerName = "Alice", Total = 100m };
            ctx.Orders.Add(order);

            await ctx.SaveChangesAsync();

            Assert.AreNotEqual(0, order.Id);

            var entry = _store.SavedTransactions.Single().Entries.Single();
            Assert.AreEqual(order.Id.ToString(), entry.EntityId);
            Assert.IsFalse(entry.EntityId.StartsWith("-"), "EntityId must not be an EF temporary placeholder");
        }

        [TestMethod]
        public async Task SaveChanges_IdentityKey_IdPropertyRowIsRealKey()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);
            var order = new IdentityKeyedOrder { CustomerName = "Bob", Total = 50m };
            ctx.Orders.Add(order);

            await ctx.SaveChangesAsync();

            var entry = _store.SavedTransactions.Single().Entries.Single();
            var idProperty = entry.Properties.Single(p => p.PropertyName == "Id");
            Assert.AreEqual(order.Id.ToString(), idProperty.NewValue);
        }

        [TestMethod]
        public async Task SaveChanges_GuidKey_EntityIdUnchanged()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var store = new StubAuditStore();
            var interceptor = BuildInterceptor<AuditableTestDbContext>(store);

            var dbOptions = new DbContextOptionsBuilder<AuditableTestDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(interceptor)
                .Options;

            using (var setupCtx = new AuditableTestDbContext(dbOptions))
            {
                setupCtx.Database.EnsureCreated();
            }

            using var ctx = new AuditableTestDbContext(dbOptions);
            var order = new AuditableOrder { CustomerName = "Carol", Total = 10m, Quantity = 1 };
            ctx.Orders.Add(order);

            await ctx.SaveChangesAsync();

            var entry = store.SavedTransactions.Single().Entries.Single();
            Assert.AreEqual(order.Id.ToString(), entry.EntityId);
        }

        [TestMethod]
        public async Task SaveChangesAsync_FailedSave_NoAuditRecordWritten()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);
            ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Duplicate", Total = 1m });
            ctx.Orders.Add(new IdentityKeyedOrder { CustomerName = "Duplicate", Total = 2m });

            await Assert.ThrowsExceptionAsync<DbUpdateException>(() => ctx.SaveChangesAsync());

            Assert.AreEqual(0, _store.SavedTransactions.Count, "A failed save must not write an audit record");
        }

        [TestMethod]
        public async Task SaveChangesAsync_FailedSave_ThenSuccessfulSave_OnlySecondAudited()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);
            var order1 = new IdentityKeyedOrder { CustomerName = "Clash", Total = 1m };
            var order2 = new IdentityKeyedOrder { CustomerName = "Clash", Total = 2m };
            ctx.Orders.AddRange(order1, order2);

            await Assert.ThrowsExceptionAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
            Assert.AreEqual(0, _store.SavedTransactions.Count);

            order2.CustomerName = "NoClash";
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count,
                "Only the second, successful save must be audited; the failed attempt must be discarded");

            var entries = _store.SavedTransactions.Single().Entries.ToList();
            Assert.AreEqual(2, entries.Count);
            Assert.IsTrue(entries.All(e => e.EntityId == order1.Id.ToString() || e.EntityId == order2.Id.ToString()));
        }

        [TestMethod]
        public async Task SaveChanges_NoChangesCollected_DoesNotPersistStalePending()
        {
            var throwingInterceptor = new ThrowingSavingChangesInterceptor();
            var dbOptions = new DbContextOptionsBuilder<SqliteTestDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(_interceptor, throwingInterceptor)
                .Options;

            using var ctx = new SqliteTestDbContext(dbOptions);
            var orphan = new IdentityKeyedOrder { CustomerName = "Orphan", Total = 1m };
            ctx.Orders.Add(orphan);

            throwingInterceptor.ShouldThrow = true;
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
            Assert.AreEqual(0, _store.SavedTransactions.Count);

            ctx.Entry(orphan).State = EntityState.Detached;
            throwingInterceptor.ShouldThrow = false;
            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, _store.SavedTransactions.Count,
                "A no-op SaveChanges must not re-persist a pending transaction orphaned by an earlier aborted save");

            var real = new IdentityKeyedOrder { CustomerName = "Real", Total = 2m };
            ctx.Orders.Add(real);
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);
            var entries = _store.SavedTransactions.Single().Entries.ToList();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(real.Id.ToString(), entries.Single().EntityId);
        }

        [TestMethod]
        public async Task SaveChanges_TwoContextsOneInterceptor_TransactionsDoNotCross()
        {
            using var connection2 = new SqliteConnection("DataSource=:memory:");
            connection2.Open();

            var dbOptions2 = new DbContextOptionsBuilder<SqliteTestDbContext>()
                .UseSqlite(connection2)
                .AddInterceptors(_interceptor)
                .Options;

            using (var setupCtx = new SqliteTestDbContext(dbOptions2))
            {
                setupCtx.Database.EnsureCreated();
            }

            using var ctx2 = new SqliteTestDbContext(dbOptions2);

            var trigger = new NestedSaveTriggerInterceptor(ctx2);
            var dbOptions1 = new DbContextOptionsBuilder<SqliteTestDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(_interceptor, trigger)
                .Options;

            using var ctx1 = new SqliteTestDbContext(dbOptions1);
            trigger.OuterContext = ctx1;

            var order1 = new IdentityKeyedOrder { CustomerName = "Ctx1Order", Total = 1m };
            var order2 = new IdentityKeyedOrder { CustomerName = "Ctx2Order", Total = 2m };
            ctx1.Orders.Add(order1);
            ctx2.Orders.Add(order2);

            await ctx1.SaveChangesAsync();

            Assert.AreEqual(2, _store.SavedTransactions.Count);

            var allEntries = _store.SavedTransactions.SelectMany(t => t.Entries).ToList();
            Assert.AreEqual(2, allEntries.Count);

            var entry1 = allEntries.Single(e =>
                e.Properties.Any(p =>
                    p.PropertyName == nameof(IdentityKeyedOrder.CustomerName) && p.NewValue == order1.CustomerName));
            var entry2 = allEntries.Single(e =>
                e.Properties.Any(p =>
                    p.PropertyName == nameof(IdentityKeyedOrder.CustomerName) && p.NewValue == order2.CustomerName));

            Assert.AreEqual(nameof(IdentityKeyedOrder), entry1.EntityName);
            Assert.AreEqual(order1.Id.ToString(), entry1.EntityId);
            Assert.AreEqual(nameof(IdentityKeyedOrder), entry2.EntityName);
            Assert.AreEqual(order2.Id.ToString(), entry2.EntityId);
        }

        [TestMethod]
        public async Task SaveChanges_ModifiedDependent_TemporaryForeignKeyFromNewPrincipal_AuditRecordHoldsRealForeignKey()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);

            var initialOrder = new IdentityKeyedOrder { CustomerName = "InitialOwner", Total = 10m };
            var line = new OrderLine { Note = "Line1", Order = initialOrder };
            ctx.Orders.Add(initialOrder);
            ctx.OrderLines.Add(line);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            var newOrder = new IdentityKeyedOrder { CustomerName = "NewOwner", Total = 20m };
            ctx.Orders.Add(newOrder);
            line.Order = newOrder;

            await ctx.SaveChangesAsync();

            var lineEntry = _store.SavedTransactions.Single().Entries.Single(e => e.EntityName == nameof(OrderLine));
            var orderIdProperty = lineEntry.Properties.Single(p => p.PropertyName == nameof(OrderLine.OrderId));

            Assert.AreEqual(newOrder.Id.ToString(), orderIdProperty.NewValue);
            Assert.IsFalse(orderIdProperty.NewValue.StartsWith("-"), "OrderId must not be an EF temporary placeholder");
        }

        [TestMethod]
        public void SaveChanges_SyncPath_IdentityKey_AuditEntityIdIsRealKey()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);
            var order = new IdentityKeyedOrder { CustomerName = "SyncDave", Total = 15m };
            ctx.Orders.Add(order);

            ctx.SaveChanges();

            var entry = _store.SavedTransactions.Single().Entries.Single();
            Assert.AreEqual(order.Id.ToString(), entry.EntityId);
        }

        [TestMethod]
        public void SaveChanges_AcceptAllChangesFalse_EntityIdIsRealKey()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);
            var order = new IdentityKeyedOrder { CustomerName = "NoAccept", Total = 25m };
            ctx.Orders.Add(order);

            ctx.SaveChanges(false);

            var entry = _store.SavedTransactions.Single().Entries.Single();
            Assert.IsFalse(entry.EntityId.StartsWith("-"), "EntityId must not be an EF temporary placeholder");
            Assert.IsTrue(int.TryParse(entry.EntityId, out var realId) && realId > 0);

            ctx.ChangeTracker.AcceptAllChanges();
            Assert.AreEqual(realId, order.Id);
        }
    }
}