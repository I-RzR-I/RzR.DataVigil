using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.TestSupport;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;

namespace RzR.DataVigil.Storage.File.Tests
{
    [TestClass]
    public class FileAuditStoreErasureClampTests : AuditStoreErasureClampContract
    {
        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private string _testDirectory;

        [TestInitialize]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(),
                "FileAuditStoreErasureClampTests_" + Guid.NewGuid().ToString("N"));
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

        protected override async Task<AuditTransaction> SaveEraseAndReadBackAsync(string storedUserId,
            string erasureRequestUserId)
        {
            var store = CreateStore();

            await store.SaveAsync(BuildTransaction(
                userId: storedUserId,
                userName: "Target User",
                ipAddress: "10.0.0.1",
                timestamp: BaseTime,
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.AnonymizeByUserAsync(erasureRequestUserId);
            Assert.IsTrue(result.IsSuccess);

            var json = await System.IO.File.ReadAllTextAsync(Path.Combine(_testDirectory, "audit-2026-01-01.json"));

            return JsonSerializer.Deserialize<List<AuditTransaction>>(json)[0];
        }
    }
}
