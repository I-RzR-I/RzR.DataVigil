using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.TestSupport;
using RzR.ResultMessage.Abstractions;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;

namespace RzR.DataVigil.Storage.EfSqlServer.Tests
{
    [TestClass]
    public class SqlServerAuditStorePagingTests : AuditStorePagingContract
    {
        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

        private AuditSqlServerDbContext _dbContext;
        private RecordingLogger<SqlServerAuditStore> _logger;
        private SqlServerAuditStore _store;

        [TestInitialize]
        public void Setup()
        {
            var dbName = "AuditPagingTestDb_" + Guid.NewGuid().ToString("N");
            var options = new DbContextOptionsBuilder<AuditSqlServerDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            var storageOptions = new StorageOptions { Schema = "audit" };
            _dbContext = new AuditSqlServerDbContext(options, storageOptions);
            _logger = new RecordingLogger<SqlServerAuditStore>();
            _store = new SqlServerAuditStore(_dbContext, _logger, new GdprProcessor(new GdprPolicyRegistry()));
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext?.Dispose();
        }

        private static AuditTransaction Transaction(DateTimeOffset timestamp, string userId)
        {
            return BuildTransaction(
                userId: userId,
                timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() });
        }

        protected override async Task SeedAsync(int count)
        {
            for (var i = 0; i < count; i++)
                _dbContext.AuditTransactions.Add(Transaction(BaseTime.AddSeconds(i), "u" + i));

            await _dbContext.SaveChangesAsync();
        }

        protected override Task<IResult<IEnumerable<AuditTransaction>>> ExecuteQueryAsync(
            AuditTransactionQuery query)
            => _store.QueryAsync(query, new GdprRetrievalContext());

        [TestMethod]
        public async Task QueryAsync_WithADisposedContext_Fails()
        {
            await SeedAsync(3);
            _dbContext.Dispose();

            var result = await _store.QueryAsync(new AuditTransactionQuery { Take = 10 }, new GdprRetrievalContext());

            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        public async Task QueryAsync_WithTakeOfZero_SucceedsThoughTheContextIsDisposed()
        {
            await SeedAsync(3);
            _dbContext.Dispose();

            var result = await _store.QueryAsync(new AuditTransactionQuery { Take = 0 }, new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithTakeOfZero_DoesNotWarnAboutACappedPageSize()
        {
            await SeedAsync(3);

            await _store.QueryAsync(new AuditTransactionQuery { Take = 0 }, new GdprRetrievalContext());

            Assert.IsFalse(_logger.Entries.Any(x => x.Level == LogLevel.Warning));
        }

        [TestMethod]
        public async Task QueryAsync_WithNegativeTake_TracesTheNormalizationAtDebugAndDoesNotWarn()
        {
            await SeedAsync(12);

            await _store.QueryAsync(new AuditTransactionQuery { Take = -1 }, new GdprRetrievalContext());

            Assert.AreEqual(1, _logger.Entries.Count(x => x.Level == LogLevel.Debug));
            Assert.IsFalse(_logger.Entries.Any(x => x.Level == LogLevel.Warning));
        }

        [TestMethod]
        public async Task QueryAsync_WithTakeAboveTheMaximum_ReturnsAtMostTheMaximumPageSize()
        {
            await SeedAsync(AuditQueryLimits.MaxTake + 1);

            var result = await _store.QueryAsync(new AuditTransactionQuery { Take = AuditQueryLimits.MaxTake + 1 },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AuditQueryLimits.MaxTake, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithTakeAboveTheMaximum_WarnsThatThePageSizeWasCapped()
        {
            await SeedAsync(1);

            await _store.QueryAsync(new AuditTransactionQuery { Take = AuditQueryLimits.MaxTake + 1 },
                new GdprRetrievalContext());

            var warnings = _logger.Entries.Where(x => x.Level == LogLevel.Warning).ToList();
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0].Message, (AuditQueryLimits.MaxTake + 1).ToString());
            StringAssert.Contains(warnings[0].Message, AuditQueryLimits.MaxTake.ToString());
        }

        [TestMethod]
        public async Task QueryAsync_WithTakeExactlyAtTheMaximum_IsNotCappedAndDoesNotWarn()
        {
            await SeedAsync(AuditQueryLimits.MaxTake);

            var result = await _store.QueryAsync(new AuditTransactionQuery { Take = AuditQueryLimits.MaxTake },
                new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(AuditQueryLimits.MaxTake, result.Response.Count());
            Assert.IsFalse(_logger.Entries.Any(x => x.Level == LogLevel.Warning));
        }

        [TestMethod]
        public async Task QueryAsync_WithADefaultQuery_LogsNothing()
        {
            await SeedAsync(12);

            await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.AreEqual(0, _logger.Entries.Count);
        }
    }
}
