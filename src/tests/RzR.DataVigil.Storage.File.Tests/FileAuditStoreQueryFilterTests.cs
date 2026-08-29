using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;

namespace RzR.DataVigil.Storage.File.Tests
{
    [TestClass]
    public class FileAuditStoreQueryFilterTests
    {
        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

        private string _testDirectory;

        [TestInitialize]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(),
                "FileAuditStoreQueryFilterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }

        private FileAuditStore CreateStore()
        {
            var options = new StorageOptions { FilePath = _testDirectory };

            return new FileAuditStore(options, new GdprProcessor(new GdprPolicyRegistry()));
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
            var store = CreateStore();
            var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var highId = Guid.Parse("00000000-0000-0000-0000-000000000003");
            var middleId = Guid.Parse("00000000-0000-0000-0000-000000000002");

            await store.SaveAsync(Transaction(BaseTime, id: lowId));
            await store.SaveAsync(Transaction(BaseTime, id: highId));
            await store.SaveAsync(Transaction(BaseTime, id: middleId));

            var result = await store.QueryAsync(new AuditTransactionQuery());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { highId, middleId, lowId },
                result.Response.Select(x => x.Id).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithUserIdFilter_ReturnsOnlyThatUser()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, userId: "alice"));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(1), userId: "bob"));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(2), userId: "alice"));

            var result = await store.QueryAsync(new AuditTransactionQuery { UserId = "alice" });

            Assert.IsTrue(result.IsSuccess);
            var returned = result.Response.ToList();
            Assert.AreEqual(2, returned.Count);
            Assert.IsTrue(returned.All(x => x.UserId == "alice"));
        }

        [TestMethod]
        public async Task QueryAsync_WithCorrelationIdFilter_ReturnsOnlyThatCorrelation()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, correlationId: "corr-a"));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(1), correlationId: "corr-b"));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(2), correlationId: "corr-a"));

            var result = await store.QueryAsync(new AuditTransactionQuery { CorrelationId = "corr-a" });

            Assert.IsTrue(result.IsSuccess);
            var returned = result.Response.ToList();
            Assert.AreEqual(2, returned.Count);
            Assert.IsTrue(returned.All(x => x.CorrelationId == "corr-a"));
        }

        [TestMethod]
        public async Task QueryAsync_WithGdprStateFilter_ReturnsOnlyThatState()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, gdprState: GdprStorageState.Original));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(1), gdprState: GdprStorageState.Erased));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(2), gdprState: GdprStorageState.Erased));

            var result = await store.QueryAsync(new AuditTransactionQuery { GdprState = GdprStorageState.Erased });

            Assert.IsTrue(result.IsSuccess);
            var returned = result.Response.ToList();
            Assert.AreEqual(2, returned.Count);
            Assert.IsTrue(returned.All(x => x.GdprState == GdprStorageState.Erased));
        }

        [TestMethod]
        public async Task QueryAsync_WithFromUtcFilter_ExcludesEarlierTransactions()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, userId: "oldest"));
            await store.SaveAsync(Transaction(BaseTime.AddHours(1), userId: "middle"));
            await store.SaveAsync(Transaction(BaseTime.AddHours(2), userId: "newest"));

            var result = await store.QueryAsync(new AuditTransactionQuery { FromUtc = BaseTime.AddHours(1) });

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "newest", "middle" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithToUtcFilter_ExcludesLaterTransactions()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, userId: "oldest"));
            await store.SaveAsync(Transaction(BaseTime.AddHours(1), userId: "middle"));
            await store.SaveAsync(Transaction(BaseTime.AddHours(2), userId: "newest"));

            var result = await store.QueryAsync(new AuditTransactionQuery { ToUtc = BaseTime.AddHours(2) });

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "middle", "oldest" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_TransactionExactlyAtFromUtc_IsIncluded()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime.AddSeconds(-1), userId: "just_before"));
            await store.SaveAsync(Transaction(BaseTime, userId: "exactly_at"));

            var result = await store.QueryAsync(new AuditTransactionQuery { FromUtc = BaseTime });

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "exactly_at" }, result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_TransactionExactlyAtToUtc_IsExcluded()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime.AddSeconds(-1), userId: "just_before"));
            await store.SaveAsync(Transaction(BaseTime, userId: "exactly_at"));

            var result = await store.QueryAsync(new AuditTransactionQuery { ToUtc = BaseTime });

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "just_before" }, result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithFromUtcAfterToUtc_ReturnsNoRowsAndSucceeds()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, userId: "alice"));
            await store.SaveAsync(Transaction(BaseTime.AddHours(1), userId: "bob"));

            var result = await store.QueryAsync(new AuditTransactionQuery
            {
                FromUtc = BaseTime.AddDays(1),
                ToUtc = BaseTime
            });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithUserIdAndCorrelationIdFilters_ReturnsOnlyTheIntersection()
        {
            var store = CreateStore();
            var intersection = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
            await store.SaveAsync(Transaction(BaseTime, userId: "alice", correlationId: "corr-1", id: intersection));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(1), userId: "alice", correlationId: "corr-2"));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(2), userId: "bob", correlationId: "corr-1"));

            var result = await store.QueryAsync(new AuditTransactionQuery
            {
                UserId = "alice",
                CorrelationId = "corr-1"
            });

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { intersection }, result.Response.Select(x => x.Id).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithWhitespaceOnlyUserIdFilter_IsNotApplied()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, userId: "alice"));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(1), userId: "bob"));

            var result = await store.QueryAsync(new AuditTransactionQuery { UserId = "   " });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithWhitespaceOnlyCorrelationIdFilter_IsNotApplied()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, correlationId: "corr-a"));
            await store.SaveAsync(Transaction(BaseTime.AddMinutes(1), correlationId: "corr-b"));

            var result = await store.QueryAsync(new AuditTransactionQuery { CorrelationId = "   " });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithNoFilterSupplied_ReturnsEveryTransaction()
        {
            var store = CreateStore();
            await store.SaveAsync(Transaction(BaseTime, userId: "alice", correlationId: null,
                gdprState: GdprStorageState.Original));
            await store.SaveAsync(Transaction(BaseTime.AddHours(1), userId: "bob", correlationId: "corr-b",
                gdprState: GdprStorageState.Erased));
            await store.SaveAsync(Transaction(BaseTime.AddHours(2), userId: "carol", correlationId: "corr-c",
                gdprState: GdprStorageState.FullyAnonymized));

            var result = await store.QueryAsync(new AuditTransactionQuery());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "carol", "bob", "alice" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_DefaultQuery_ReturnsTheTenNewestTransactions()
        {
            var store = CreateStore();
            for (var i = 0; i < 12; i++)
                await store.SaveAsync(Transaction(BaseTime.AddMinutes(i), userId: "u" + i));

            var result = await store.QueryAsync(new AuditTransactionQuery());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(
                new[] { "u11", "u10", "u9", "u8", "u7", "u6", "u5", "u4", "u3", "u2" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithFilterMatchingFewerRowsThanTake_FiltersBeforePaging()
        {
            var store = CreateStore();
            for (var i = 0; i < 3; i++)
                await store.SaveAsync(Transaction(BaseTime.AddMinutes(i), userId: "needle"));

            for (var i = 3; i < 15; i++)
                await store.SaveAsync(Transaction(BaseTime.AddMinutes(i), userId: "hay"));

            var result = await store.QueryAsync(new AuditTransactionQuery { UserId = "needle", Skip = 0, Take = 10 });

            Assert.IsTrue(result.IsSuccess);
            var returned = result.Response.ToList();
            Assert.AreEqual(3, returned.Count);
            Assert.IsTrue(returned.All(x => x.UserId == "needle"));
        }
    }
}
