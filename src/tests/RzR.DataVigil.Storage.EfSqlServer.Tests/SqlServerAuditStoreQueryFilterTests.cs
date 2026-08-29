using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;

namespace RzR.DataVigil.Storage.EfSqlServer.Tests
{
    [TestClass]
    public class SqlServerAuditStoreQueryFilterTests
    {
        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

        private AuditSqlServerDbContext _dbContext;
        private SqlServerAuditStore _store;

        [TestInitialize]
        public void Setup()
        {
            var loggerFactory = LoggerFactory.Create(_ => { });
            var logger = loggerFactory.CreateLogger<SqlServerAuditStore>();

            var dbName = "AuditFilterTestDb_" + Guid.NewGuid().ToString("N");
            var options = new DbContextOptionsBuilder<AuditSqlServerDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            var storageOptions = new StorageOptions { Schema = "audit" };
            _dbContext = new AuditSqlServerDbContext(options, storageOptions);
            _store = new SqlServerAuditStore(_dbContext, logger, new GdprProcessor(new GdprPolicyRegistry()));
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext?.Dispose();
        }

        private static AuditTransaction Transaction(
            DateTimeOffset timestamp,
            string userId = "user1",
            string correlationId = null,
            GdprStorageState gdprState = GdprStorageState.Original,
            Guid? id = null)
        {
            return BuildTransaction(
                userId: userId,
                timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() },
                id: id,
                correlationId: correlationId,
                gdprState: gdprState);
        }

        [TestMethod]
        public async Task QueryAsync_TransactionsSharingOneTimestamp_AreOrderedByIdDescending()
        {
            var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var highId = Guid.Parse("00000000-0000-0000-0000-000000000003");
            var middleId = Guid.Parse("00000000-0000-0000-0000-000000000002");

            await _store.SaveAsync(Transaction(BaseTime, id: lowId));
            await _store.SaveAsync(Transaction(BaseTime, id: highId));
            await _store.SaveAsync(Transaction(BaseTime, id: middleId));

            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { highId, middleId, lowId },
                result.Response.Select(x => x.Id).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithUserIdFilter_ReturnsOnlyThatUser()
        {
            await _store.SaveAsync(Transaction(BaseTime, userId: "alice"));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(1), userId: "bob"));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(2), userId: "alice"));

            var result = await _store.QueryAsync(new AuditTransactionQuery { UserId = "alice" },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var returned = result.Response.ToList();
            Assert.AreEqual(2, returned.Count);
            Assert.IsTrue(returned.All(x => x.UserId == "alice"));
        }

        [TestMethod]
        public async Task QueryAsync_WithCorrelationIdFilter_ReturnsOnlyThatCorrelation()
        {
            await _store.SaveAsync(Transaction(BaseTime, correlationId: "corr-a"));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(1), correlationId: "corr-b"));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(2), correlationId: "corr-a"));

            var result = await _store.QueryAsync(new AuditTransactionQuery { CorrelationId = "corr-a" },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var returned = result.Response.ToList();
            Assert.AreEqual(2, returned.Count);
            Assert.IsTrue(returned.All(x => x.CorrelationId == "corr-a"));
        }

        [TestMethod]
        public async Task QueryAsync_WithGdprStateFilter_ReturnsOnlyThatState()
        {
            await _store.SaveAsync(Transaction(BaseTime, gdprState: GdprStorageState.Original));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(1), gdprState: GdprStorageState.Erased));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(2), gdprState: GdprStorageState.Erased));

            var result = await _store.QueryAsync(new AuditTransactionQuery { GdprState = GdprStorageState.Erased },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var returned = result.Response.ToList();
            Assert.AreEqual(2, returned.Count);
            Assert.IsTrue(returned.All(x => x.GdprState == GdprStorageState.Erased));
        }

        [TestMethod]
        public async Task QueryAsync_WithFromUtcFilter_ExcludesEarlierTransactions()
        {
            await _store.SaveAsync(Transaction(BaseTime, userId: "oldest"));
            await _store.SaveAsync(Transaction(BaseTime.AddHours(1), userId: "middle"));
            await _store.SaveAsync(Transaction(BaseTime.AddHours(2), userId: "newest"));

            var result = await _store.QueryAsync(new AuditTransactionQuery { FromUtc = BaseTime.AddHours(1) },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "newest", "middle" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithToUtcFilter_ExcludesLaterTransactions()
        {
            await _store.SaveAsync(Transaction(BaseTime, userId: "oldest"));
            await _store.SaveAsync(Transaction(BaseTime.AddHours(1), userId: "middle"));
            await _store.SaveAsync(Transaction(BaseTime.AddHours(2), userId: "newest"));

            var result = await _store.QueryAsync(new AuditTransactionQuery { ToUtc = BaseTime.AddHours(2) },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "middle", "oldest" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_TransactionExactlyAtFromUtc_IsIncluded()
        {
            await _store.SaveAsync(Transaction(BaseTime.AddSeconds(-1), userId: "just_before"));
            await _store.SaveAsync(Transaction(BaseTime, userId: "exactly_at"));

            var result = await _store.QueryAsync(new AuditTransactionQuery { FromUtc = BaseTime },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "exactly_at" }, result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_TransactionExactlyAtToUtc_IsExcluded()
        {
            await _store.SaveAsync(Transaction(BaseTime.AddSeconds(-1), userId: "just_before"));
            await _store.SaveAsync(Transaction(BaseTime, userId: "exactly_at"));

            var result = await _store.QueryAsync(new AuditTransactionQuery { ToUtc = BaseTime },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "just_before" }, result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithFromUtcAfterToUtc_ReturnsNoRowsAndSucceeds()
        {
            await _store.SaveAsync(Transaction(BaseTime, userId: "alice"));
            await _store.SaveAsync(Transaction(BaseTime.AddHours(1), userId: "bob"));

            var result = await _store.QueryAsync(new AuditTransactionQuery
            {
                FromUtc = BaseTime.AddDays(1),
                ToUtc = BaseTime
            }, new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithUserIdAndCorrelationIdFilters_ReturnsOnlyTheIntersection()
        {
            var intersection = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
            await _store.SaveAsync(Transaction(BaseTime, userId: "alice", correlationId: "corr-1", id: intersection));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(1), userId: "alice", correlationId: "corr-2"));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(2), userId: "bob", correlationId: "corr-1"));

            var result = await _store.QueryAsync(new AuditTransactionQuery
            {
                UserId = "alice",
                CorrelationId = "corr-1"
            }, new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { intersection }, result.Response.Select(x => x.Id).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithWhitespaceOnlyUserIdFilter_IsNotApplied()
        {
            await _store.SaveAsync(Transaction(BaseTime, userId: "alice"));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(1), userId: "bob"));

            var result = await _store.QueryAsync(new AuditTransactionQuery { UserId = "   " },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithWhitespaceOnlyCorrelationIdFilter_IsNotApplied()
        {
            await _store.SaveAsync(Transaction(BaseTime, correlationId: "corr-a"));
            await _store.SaveAsync(Transaction(BaseTime.AddMinutes(1), correlationId: "corr-b"));

            var result = await _store.QueryAsync(new AuditTransactionQuery { CorrelationId = "   " },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithNoFilterSupplied_ReturnsEveryTransaction()
        {
            await _store.SaveAsync(Transaction(BaseTime, userId: "alice", correlationId: null,
                gdprState: GdprStorageState.Original));
            await _store.SaveAsync(Transaction(BaseTime.AddHours(1), userId: "bob", correlationId: "corr-b",
                gdprState: GdprStorageState.Erased));
            await _store.SaveAsync(Transaction(BaseTime.AddHours(2), userId: "carol", correlationId: "corr-c",
                gdprState: GdprStorageState.FullyAnonymized));

            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "carol", "bob", "alice" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_DefaultQuery_ReturnsTheTenNewestTransactions()
        {
            for (var i = 0; i < 12; i++)
                await _store.SaveAsync(Transaction(BaseTime.AddMinutes(i), userId: "u" + i));

            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(
                new[] { "u11", "u10", "u9", "u8", "u7", "u6", "u5", "u4", "u3", "u2" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithFilterMatchingFewerRowsThanTake_FiltersBeforePaging()
        {
            for (var i = 0; i < 3; i++)
                await _store.SaveAsync(Transaction(BaseTime.AddMinutes(i), userId: "needle"));

            for (var i = 3; i < 15; i++)
                await _store.SaveAsync(Transaction(BaseTime.AddMinutes(i), userId: "hay"));

            var result = await _store.QueryAsync(
                new AuditTransactionQuery { UserId = "needle", Skip = 0, Take = 10 },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var returned = result.Response.ToList();
            Assert.AreEqual(3, returned.Count);
            Assert.IsTrue(returned.All(x => x.UserId == "needle"));
        }
    }
}
