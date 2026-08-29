using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.TestSupport;
using RzR.ResultMessage.Abstractions;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;

namespace RzR.DataVigil.Storage.File.Tests
{
    [TestClass]
    public class FileAuditStorePagingTests : AuditStorePagingContract
    {
        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

        private string _testDirectory;
        private FileAuditStore _store;

        [TestInitialize]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(),
                "FileAuditStorePagingTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);

            var options = new StorageOptions { FilePath = _testDirectory };
            _store = new FileAuditStore(options, new GdprProcessor(new GdprPolicyRegistry()));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }

        private static AuditTransaction Transaction(DateTimeOffset timestamp, string userId)
        {
            return BuildTransaction(
                userId: userId,
                timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() });
        }

        private void CorruptTheStoreFile()
        {
            System.IO.File.WriteAllText(Path.Combine(_testDirectory, "audit-2026-07-01.json"),
                "{ this is not a serialized audit page");
        }

        protected override async Task SeedAsync(int count)
        {
            for (var i = 0; i < count; i++)
                await _store.SaveAsync(Transaction(BaseTime.AddMinutes(i), "u" + i));
        }

        protected override Task<IResult<IEnumerable<AuditTransaction>>> ExecuteQueryAsync(
            AuditTransactionQuery query)
            => _store.QueryAsync(query);

        [TestMethod]
        public async Task QueryAsync_WithAnUnreadableStoreFile_Fails()
        {
            await SeedAsync(3);
            CorruptTheStoreFile();

            var result = await _store.QueryAsync(new AuditTransactionQuery { Take = 10 });

            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        public async Task QueryAsync_WithTakeOfZero_SucceedsThoughTheStoreFileIsUnreadable()
        {
            await SeedAsync(3);
            CorruptTheStoreFile();

            var result = await _store.QueryAsync(new AuditTransactionQuery { Take = 0 });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Response.Count());
        }
    }
}
