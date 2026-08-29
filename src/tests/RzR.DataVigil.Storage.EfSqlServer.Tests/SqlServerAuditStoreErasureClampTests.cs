using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.TestSupport;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;

namespace RzR.DataVigil.Storage.EfSqlServer.Tests
{
    [TestClass]
    public class SqlServerAuditStoreErasureClampTests : AuditStoreErasureClampContract
    {
        private AuditSqlServerDbContext _dbContext;
        private SqlServerAuditStore _store;

        [TestInitialize]
        public void Setup()
        {
            var dbName = "AuditErasureClampTestDb_" + Guid.NewGuid().ToString("N");
            var options = new DbContextOptionsBuilder<AuditSqlServerDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            var storageOptions = new StorageOptions { Schema = "audit" };
            _dbContext = new AuditSqlServerDbContext(options, storageOptions);
            _store = new SqlServerAuditStore(_dbContext, new RecordingLogger<SqlServerAuditStore>(),
                new GdprProcessor(new GdprPolicyRegistry()));
        }

        [TestCleanup]
        public void Cleanup()
        {
            _dbContext?.Dispose();
        }

        protected override async Task<AuditTransaction> SaveEraseAndReadBackAsync(string storedUserId,
            string erasureRequestUserId)
        {
            await _store.SaveAsync(BuildTransaction(
                userId: storedUserId,
                userName: "Target User",
                ipAddress: "10.0.0.1",
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await _store.AnonymizeByUserAsync(erasureRequestUserId);
            Assert.IsTrue(result.IsSuccess);

            return await _dbContext.AuditTransactions.FirstAsync();
        }
    }
}
