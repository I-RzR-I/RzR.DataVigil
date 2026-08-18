using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Storage.File.Tests.Helpers;
using static RzR.DataVigil.Storage.File.Tests.Helpers.AuditTestDataBuilder;

namespace RzR.DataVigil.Storage.File.Tests
{
    [TestClass]
    public class FileAuditStoreTests
    {
        private string _testDirectory;

        [TestInitialize]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "FileAuditStoreTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }

        private FileAuditStore CreateStore(string path = null, GdprProcessor gdprProcessor = null)
        {
            var options = new StorageOptions { FilePath = path ?? _testDirectory };
            return new FileAuditStore(options, gdprProcessor ?? new GdprProcessor(new GdprPolicyRegistry()));
        }

        [TestMethod]
        public async Task SaveAsync_WithNullTransaction_ReturnsSuccess()
        {
            var store = CreateStore();

            var result = await store.SaveAsync(null);

            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task SaveAsync_WithValidTransaction_CreatesJsonFile()
        {
            var store = CreateStore();
            var today = DateTimeOffset.UtcNow;
            var txn = BuildTransaction(timestamp: today, entries: new List<AuditEntry> { BuildEntry() });

            var result = await store.SaveAsync(txn);

            Assert.IsTrue(result.IsSuccess);
            var expectedFile = Path.Combine(_testDirectory, $"audit-{today.UtcDateTime:yyyy-MM-dd}.json");
            Assert.IsTrue(System.IO.File.Exists(expectedFile), "Expected JSON file was not created.");
        }

        [TestMethod]
        public async Task SaveAsync_WithValidTransaction_SerializesCorrectly()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);
            var txn = BuildTransaction(userId: "u99", userName: "Alice", timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry(entityId: "7") });

            await store.SaveAsync(txn);

            var filePath = Path.Combine(_testDirectory, "audit-2025-06-15.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);

            Assert.AreEqual(1, saved.Count);
            Assert.AreEqual("u99", saved[0].UserId);
            Assert.AreEqual("Alice", saved[0].UserName);
            Assert.AreEqual(1, saved[0].Entries.Count);
        }

        [TestMethod]
        public async Task SaveAsync_CalledTwice_AccumulatesTransactionsInSameFile()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2025, 7, 1, 8, 0, 0, TimeSpan.Zero);
            var txn1 = BuildTransaction(userId: "u1", timestamp: timestamp, entries: new List<AuditEntry> { BuildEntry() });
            var txn2 = BuildTransaction(userId: "u2", timestamp: timestamp, entries: new List<AuditEntry> { BuildEntry() });

            await store.SaveAsync(txn1);
            await store.SaveAsync(txn2);

            var filePath = Path.Combine(_testDirectory, "audit-2025-07-01.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);

            Assert.AreEqual(2, saved.Count);
            Assert.IsTrue(saved.Any(t => t.UserId == "u1"));
            Assert.IsTrue(saved.Any(t => t.UserId == "u2"));
        }

        [TestMethod]
        public async Task SaveAsync_WithDifferentDates_CreatesMultipleFiles()
        {
            var store = CreateStore();
            var day1 = new DateTimeOffset(2025, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var day2 = new DateTimeOffset(2025, 8, 2, 0, 0, 0, TimeSpan.Zero);

            await store.SaveAsync(BuildTransaction(timestamp: day1, entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(timestamp: day2, entries: new List<AuditEntry> { BuildEntry() }));

            Assert.IsTrue(System.IO.File.Exists(Path.Combine(_testDirectory, "audit-2025-08-01.json")));
            Assert.IsTrue(System.IO.File.Exists(Path.Combine(_testDirectory, "audit-2025-08-02.json")));
        }

        [TestMethod]
        public async Task QueryAsync_WhenDirectoryDoesNotExist_ReturnsEmptyPage()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), "no_such_dir_" + Guid.NewGuid().ToString("N"));
            var store = CreateStore(missingPath);

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Response.ToList().Count);
        }

        [TestMethod]
        public async Task QueryAsync_WithExistingTransactions_ReturnsAll()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero);
            for (int i = 0; i < 3; i++)
                await store.SaveAsync(BuildTransaction(userId: $"u{i}", timestamp: timestamp.AddMinutes(i),
                    entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(3, result.Response.ToList().Count);
        }

        [TestMethod]
        public async Task QueryAsync_WithPaging_ReturnsCorrectSubset()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero);
            for (int i = 1; i <= 5; i++)
                await store.SaveAsync(BuildTransaction(userId: $"u{i}", timestamp: timestamp.AddMinutes(i),
                    entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(5, result.Response.ToList().Count);
        }

        [TestMethod]
        public async Task QueryAsync_SecondPage_ReturnsNextSubset()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2025, 10, 10, 0, 0, 0, TimeSpan.Zero);
            for (int i = 1; i <= 5; i++)
                await store.SaveAsync(BuildTransaction(userId: $"u{i}", timestamp: timestamp.AddMinutes(i),
                    entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(5, result.Response.ToList().Count);
        }

        [TestMethod]
        public async Task QueryAsync_WithEqualsFilter_ReturnsMatchingTransactions()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(userId: "match", timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(userId: "other", timestamp: timestamp.AddMinutes(1),
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(userId: "match", timestamp: timestamp.AddMinutes(2),
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Response.ToList().Count >= 2);
            Assert.IsTrue(result.Response.Any(t => t.UserId == "match"));
        }

        [TestMethod]
        public async Task QueryAsync_WithContainsFilter_ReturnsMatchingTransactions()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2025, 11, 5, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(source: "WebApi", timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(source: "Console", timestamp: timestamp.AddMinutes(1),
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(source: "WebApp", timestamp: timestamp.AddMinutes(2),
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Response.ToList().Count >= 2);
        }

        [TestMethod]
        public async Task QueryAsync_WithIsNullFilter_ReturnsTransactionsWithNullProperty()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2025, 11, 10, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(source: "WebApi", timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() }));

            var noSource = BuildTransaction(timestamp: timestamp.AddMinutes(1),
                entries: new List<AuditEntry> { BuildEntry() });
            noSource.Source = null;
            await store.SaveAsync(noSource);

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Response.Any(t => t.Source == null));
        }

        [TestMethod]
        public async Task QueryAsync_DefaultOrder_IsTimestampDescending()
        {
            var store = CreateStore();
            var baseTime = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(userId: "early", timestamp: baseTime,
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(userId: "late", timestamp: baseTime.AddHours(2),
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(userId: "middle", timestamp: baseTime.AddHours(1),
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Response.ToList().Count >= 3);
        }

        [TestMethod]
        public async Task QueryAsync_WithAscendingOrder_ReturnsCorrectlyOrdered()
        {
            var store = CreateStore();
            var baseTime = new DateTimeOffset(2025, 12, 5, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(source: "C_Source", timestamp: baseTime,
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(source: "A_Source", timestamp: baseTime.AddMinutes(1),
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(source: "B_Source", timestamp: baseTime.AddMinutes(2),
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Response.ToList().Count >= 3);
        }

        [TestMethod]
        public async Task QueryAsync_WithDescendingOrder_ReturnsCorrectlyOrdered()
        {
            var store = CreateStore();
            var baseTime = new DateTimeOffset(2025, 12, 6, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(source: "A_Source", timestamp: baseTime,
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(source: "B_Source", timestamp: baseTime.AddMinutes(1),
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(source: "C_Source", timestamp: baseTime.AddMinutes(2),
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.QueryAsync(new AuditTransactionQuery());
            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(result.Response.ToList().Count >= 3);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WhenDirectoryDoesNotExist_ReturnsSuccess()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), "no_such_dir_" + Guid.NewGuid().ToString("N"));
            var store = CreateStore(missingPath);

            var result = await store.AnonymizeByUserAsync("user123");

            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithMatchingUser_ErasesPersonalData()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var txn = BuildTransaction(userId: "target", userName: "Target User", ipAddress: "10.0.0.1",
                timestamp: timestamp, entries: new List<AuditEntry> { BuildEntry() });
            await store.SaveAsync(txn);

            var result = await store.AnonymizeByUserAsync("target");

            Assert.IsTrue(result.IsSuccess);

            var filePath = Path.Combine(_testDirectory, "audit-2026-01-01.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);

            Assert.AreEqual("[ERASED]", saved[0].UserId);
            Assert.AreEqual("[ERASED]", saved[0].UserName);
            Assert.AreEqual("[ERASED]", saved[0].IpAddress);
            Assert.AreEqual(GdprStorageState.Erased, saved[0].GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithNonMatchingUser_DoesNotModifyOtherUsers()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
            var txn = BuildTransaction(userId: "safe_user", userName: "Safe User", ipAddress: "192.168.1.1",
                timestamp: timestamp, entries: new List<AuditEntry> { BuildEntry() });
            await store.SaveAsync(txn);

            await store.AnonymizeByUserAsync("other_user");

            var filePath = Path.Combine(_testDirectory, "audit-2026-01-02.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);

            Assert.AreEqual("safe_user", saved[0].UserId);
            Assert.AreEqual("Safe User", saved[0].UserName);
            Assert.AreEqual("192.168.1.1", saved[0].IpAddress);
            Assert.AreEqual(GdprStorageState.Original, saved[0].GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithMixedUsers_OnlyErasesTargetUser()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(userId: "target", userName: "Target", timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(userId: "keep", userName: "Keep Me", timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() }));

            await store.AnonymizeByUserAsync("target");

            var filePath = Path.Combine(_testDirectory, "audit-2026-01-03.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);

            var targetTxn = saved.First(t => t.UserId == "[ERASED]" || t.UserName == "[ERASED]");
            var keptTxn = saved.First(t => t.UserId == "keep");

            Assert.AreEqual("[ERASED]", targetTxn.UserId);
            Assert.AreEqual("Keep Me", keptTxn.UserName);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_AlreadyErasedUser_IsIdempotent()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
            var txn = BuildTransaction(userId: "gdpr", userName: "GDPR User", ipAddress: "1.2.3.4", timestamp: timestamp, entries: new List<AuditEntry> { BuildEntry() });
            await store.SaveAsync(txn);
            await store.AnonymizeByUserAsync("gdpr");
            // Run again
            var result = await store.AnonymizeByUserAsync("gdpr");
            Assert.IsTrue(result.IsSuccess);
            var filePath = Path.Combine(_testDirectory, "audit-2026-04-01.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);
            Assert.AreEqual("[ERASED]", saved[0].UserId);
            Assert.AreEqual(GdprStorageState.Erased, saved[0].GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_OnlyMasksUserFields_NotEntriesOrProperties()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero);
            var entry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                Action = AuditAction.Update,
                EntityName = "Customer",
                EntityId = "cust-1",
                EntityTypeName = "TestApp.Domain.Customer",
                Properties = new List<AuditEntryProperty> {
                    new AuditEntryProperty {
                        PropertyName = "Email",
                        PropertyType = "System.String",
                        OldValue = "old@example.com",
                        NewValue = "new@example.com"
                    }
                }
            };
            var txn = BuildTransaction(userId: "gdpr2", userName: "GDPR2 User", ipAddress: "5.6.7.8", timestamp: timestamp, entries: new List<AuditEntry> { entry });
            await store.SaveAsync(txn);
            await store.AnonymizeByUserAsync("gdpr2");
            var filePath = Path.Combine(_testDirectory, "audit-2026-04-02.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);
            Assert.AreEqual("[ERASED]", saved[0].UserId);
            Assert.AreEqual("[ERASED]", saved[0].UserName);
            Assert.AreEqual("[ERASED]", saved[0].IpAddress);
            Assert.AreEqual(GdprStorageState.Erased, saved[0].GdprState);
            var storedEntry = saved[0].Entries.First();
            Assert.AreEqual("Customer", storedEntry.EntityName);
            Assert.AreEqual("cust-1", storedEntry.EntityId);
            var prop = storedEntry.Properties.First();
            Assert.AreEqual("Email", prop.PropertyName);
            Assert.AreEqual("old@example.com", prop.OldValue);
            Assert.AreEqual("new@example.com", prop.NewValue);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_MultipleTransactionsForSameUser_AllAreErased_AcrossFiles()
        {
            var store = CreateStore();
            var day1 = new DateTimeOffset(2026, 4, 3, 0, 0, 0, TimeSpan.Zero);
            var day2 = new DateTimeOffset(2026, 4, 4, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(userId: "multi", userName: "Multi User", timestamp: day1, entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(userId: "multi", userName: "Multi User", timestamp: day2, entries: new List<AuditEntry> { BuildEntry() }));
            await store.AnonymizeByUserAsync("multi");
            foreach (var day in new[] { day1, day2 })
            {
                var filePath = Path.Combine(_testDirectory, $"audit-{day.UtcDateTime:yyyy-MM-dd}.json");
                var json = await System.IO.File.ReadAllTextAsync(filePath);
                var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);
                Assert.IsTrue(saved.All(t => t.UserId == "[ERASED]" && t.GdprState == GdprStorageState.Erased));
            }
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_OnlyMasksUserName_WhenCustomLogicApplied()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero);
            var txn = BuildTransaction(userId: "partial", userName: "Partial User", ipAddress: "9.9.9.9",
                timestamp: timestamp, entries: new List<AuditEntry> { BuildEntry() });
            await store.SaveAsync(txn);
            // Manually mask only UserName
            var filePath = Path.Combine(_testDirectory, "audit-2026-04-05.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(json);
            saved[0].UserName = "[ERASED]";
            await System.IO.File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(saved));
            // Now anonymize by user (should not affect UserId/IpAddress if already erased)
            await store.AnonymizeByUserAsync("partial");
            var json2 = await System.IO.File.ReadAllTextAsync(filePath);
            var result = JsonSerializer.Deserialize<List<AuditTransaction>>(json2);
            Assert.AreEqual("[ERASED]", result[0].UserId); // Anonymizer always erases UserId
            Assert.AreEqual("[ERASED]", result[0].UserName);
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_WhenDirectoryDoesNotExist_ReturnsSuccess()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), "no_such_dir_" + Guid.NewGuid().ToString("N"));
            var store = CreateStore(missingPath);

            var result = await store.PurgeBeforeAsync(DateTimeOffset.UtcNow);

            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_RemovesTransactionsOlderThanCutoff()
        {
            var store = CreateStore();
            var cutoff = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: cutoff.AddDays(-1),
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(userId: "new_user", timestamp: cutoff.AddDays(1),
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await store.PurgeBeforeAsync(cutoff);

            Assert.IsTrue(result.IsSuccess);

            var queryResult = (await store.QueryAsync(new AuditTransactionQuery())).Response.ToList();

            Assert.AreEqual(1, queryResult.Count);
            Assert.AreEqual("new_user", queryResult[0].UserId);
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_DeletesFileWhenAllTransactionsAreRemoved()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() }));

            var expectedFile = Path.Combine(_testDirectory, "audit-2026-02-05.json");
            Assert.IsTrue(System.IO.File.Exists(expectedFile), "File should exist before purge.");

            await store.PurgeBeforeAsync(timestamp.AddDays(1));

            Assert.IsFalse(System.IO.File.Exists(expectedFile), "File should be deleted after purge removes all transactions.");
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_PreservesTransactionsAtOrAfterCutoff()
        {
            var store = CreateStore();
            var cutoff = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(userId: "at_cutoff", timestamp: cutoff,
                entries: new List<AuditEntry> { BuildEntry() }));
            await store.SaveAsync(BuildTransaction(userId: "after_cutoff", timestamp: cutoff.AddSeconds(1),
                entries: new List<AuditEntry> { BuildEntry() }));

            await store.PurgeBeforeAsync(cutoff);

            var queryResult = (await store.QueryAsync(new AuditTransactionQuery())).Response.ToList();

            Assert.AreEqual(2, queryResult.Count);
            Assert.IsTrue(queryResult.Any(t => t.UserId == "at_cutoff"));
            Assert.IsTrue(queryResult.Any(t => t.UserId == "after_cutoff"));
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_WithNoMatchingTransactions_DoesNotDeleteFile()
        {
            var store = CreateStore();
            var timestamp = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(userId: "future_user", timestamp: timestamp,
                entries: new List<AuditEntry> { BuildEntry() }));

            var expectedFile = Path.Combine(_testDirectory, "audit-2026-03-10.json");

            await store.PurgeBeforeAsync(timestamp.AddDays(-10));

            Assert.IsTrue(System.IO.File.Exists(expectedFile), "File should still exist when no transactions matched cutoff.");

            var queryResult = (await store.QueryAsync(new AuditTransactionQuery())).Response.ToList();
            Assert.AreEqual(1, queryResult.Count);
        }

        [TestMethod]
        public async Task QueryAsync_WithMaskRetrievalPolicy_MasksValuesForUnauthorizedUser()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "Email",
                        Action = GdprFieldAction.Mask,
                        AllowedRoles = new[] { "Admin" }
                    }
                }
            };
            var registry = CreateRegistryWithPolicy("Order", policy);
            var store = CreateStore(gdprProcessor: new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "Email", PropertyType = "System.String", OldValue = "old@test.com", NewValue = "new@test.com" }
            };
            var timestamp = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry }));

            var result = await store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var prop = result.Response.First().Entries.First().Properties.First();
            Assert.AreNotEqual("old@test.com", prop.OldValue);
            Assert.AreNotEqual("new@test.com", prop.NewValue);
            Assert.IsTrue(prop.OldValue.Contains("*"), "Old value should be masked.");
            Assert.IsTrue(prop.NewValue.Contains("*"), "New value should be masked.");
        }

        [TestMethod]
        public async Task QueryAsync_WithAnonymizeRetrievalPolicy_AnonymizesValues()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "Phone",
                        Action = GdprFieldAction.Anonymize,
                        AllowedRoles = new[] { "Admin" }
                    }
                }
            };
            var registry = CreateRegistryWithPolicy("Order", policy);
            var store = CreateStore(gdprProcessor: new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "Phone", PropertyType = "System.String", OldValue = "555-1234", NewValue = "555-5678" }
            };
            var timestamp = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry }));

            var result = await store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var prop = result.Response.First().Entries.First().Properties.First();
            Assert.AreEqual("[ANONYMIZED]", prop.OldValue);
            Assert.AreEqual("[ANONYMIZED]", prop.NewValue);
        }

        [TestMethod]
        public async Task QueryAsync_WithAuthorizedRole_ReturnsUnmaskedValues()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "Email",
                        Action = GdprFieldAction.Mask,
                        AllowedRoles = new[] { "Admin" }
                    }
                }
            };
            var registry = CreateRegistryWithPolicy("Order", policy);
            var store = CreateStore(gdprProcessor: new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "Email", PropertyType = "System.String", OldValue = "old@test.com", NewValue = "new@test.com" }
            };
            var timestamp = new DateTimeOffset(2026, 5, 3, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry }));

            var context = new GdprRetrievalContext { UserRoles = new[] { "Admin" } };
            var result = await store.QueryAsync(new AuditTransactionQuery(), context);

            Assert.IsTrue(result.IsSuccess);

            var prop = result.Response.First().Entries.First().Properties.First();
            Assert.AreEqual("old@test.com", prop.OldValue);
            Assert.AreEqual("new@test.com", prop.NewValue);
        }

        [TestMethod]
        public async Task QueryAsync_WithAuthorizedClaim_ReturnsUnmaskedValues()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule
                    {
                        FieldName = "SSN",
                        Action = GdprFieldAction.Anonymize,
                        AllowedClaims = new Dictionary<string, string> { ["gdpr_access"] = "full" }
                    }
                }
            };
            var registry = CreateRegistryWithPolicy("Order", policy);
            var store = CreateStore(gdprProcessor: new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "SSN", PropertyType = "System.String", OldValue = "123-45-6789", NewValue = "987-65-4321" }
            };
            var timestamp = new DateTimeOffset(2026, 5, 4, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry }));

            var context = new GdprRetrievalContext { UserClaims = new Dictionary<string, string> { ["gdpr_access"] = "full" } };
            var result = await store.QueryAsync(new AuditTransactionQuery(), context);

            Assert.IsTrue(result.IsSuccess);
            var prop = result.Response.First().Entries.First().Properties.First();
            Assert.AreEqual("123-45-6789", prop.OldValue);
            Assert.AreEqual("987-65-4321", prop.NewValue);
        }

        [TestMethod]
        public async Task QueryAsync_WithNoGdprPolicy_ReturnsOriginalValues()
        {
            var store = CreateStore();

            var entry = BuildEntry(entityName: "NoPolicy");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "Name", PropertyType = "System.String", OldValue = "Alice", NewValue = "Bob" }
            };
            var timestamp = new DateTimeOffset(2026, 5, 5, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry }));

            var result = await store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var prop = result.Response.First().Entries.First().Properties.First();
            Assert.AreEqual("Alice", prop.OldValue);
            Assert.AreEqual("Bob", prop.NewValue);
        }

        [TestMethod]
        public async Task QueryAsync_WithMultipleRetrievalRules_AppliesEachRuleSeparately()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask, AllowedRoles = new[] { "Admin" } },
                    new FieldGdprRule { FieldName = "Phone", Action = GdprFieldAction.Anonymize, AllowedRoles = new[] { "Admin" } }
                }
            };
            var registry = CreateRegistryWithPolicy("Order", policy);
            var store = CreateStore(gdprProcessor: new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "Email", PropertyType = "System.String", OldValue = "user@test.com", NewValue = "new@test.com" },
                new AuditEntryProperty { PropertyName = "Phone", PropertyType = "System.String", OldValue = "555-1234", NewValue = "555-5678" },
                new AuditEntryProperty { PropertyName = "Name", PropertyType = "System.String", OldValue = "Alice", NewValue = "Bob" }
            };
            var timestamp = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry }));

            var result = await store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var props = result.Response.First().Entries.First().Properties.ToList();
            var emailProp = props.First(p => p.PropertyName == "Email");
            var phoneProp = props.First(p => p.PropertyName == "Phone");
            var nameProp = props.First(p => p.PropertyName == "Name");

            Assert.IsTrue(emailProp.OldValue.Contains("*"), "Email should be masked.");
            Assert.AreEqual("[ANONYMIZED]", phoneProp.OldValue, "Phone should be anonymized.");
            Assert.AreEqual("Alice", nameProp.OldValue, "Name (no rule) should be original.");
        }

        [TestMethod]
        public async Task QueryAsync_WithNullGdprRetrievalContext_DefaultsToEmptyContext()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Mask, AllowedRoles = new[] { "Admin" } }
                }
            };
            var registry = CreateRegistryWithPolicy("Order", policy);
            var store = CreateStore(gdprProcessor: new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "Email", PropertyType = "System.String", OldValue = "test@test.com", NewValue = "new@test.com" }
            };
            var timestamp = new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry }));

            var result = await store.QueryAsync(new AuditTransactionQuery());

            Assert.IsTrue(result.IsSuccess);
            var prop = result.Response.First().Entries.First().Properties.First();
            Assert.IsTrue(prop.OldValue.Contains("*"), "Email should be masked when null context is passed.");
        }

        [TestMethod]
        public async Task QueryAsync_GdprRetrievalPolicy_DoesNotAffectStoredData()
        {
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = new[]
                {
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Anonymize, AllowedRoles = new[] { "Admin" } }
                }
            };
            var registry = CreateRegistryWithPolicy("Order", policy);
            var store = CreateStore(gdprProcessor: new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty { PropertyName = "Email", PropertyType = "System.String", OldValue = "original@test.com", NewValue = "changed@test.com" }
            };
            var timestamp = new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
            await store.SaveAsync(BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry }));

            // First query anonymizes on retrieval
            var result1 = await store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());
            Assert.AreEqual("[ANONYMIZED]", result1.Response.First().Entries.First().Properties.First().OldValue);

            // Verify stored data on disk is untouched
            var filePath = Path.Combine(_testDirectory, $"audit-{timestamp.UtcDateTime:yyyy-MM-dd}.json");
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var stored = JsonSerializer.Deserialize<List<AuditTransaction>>(json);
            Assert.AreEqual("original@test.com", stored[0].Entries.First().Properties.First().OldValue);
        }

        [TestMethod]
        public async Task ProcessAsync_ThroughPipeline_WithHashStorageRule_DoesNotDoubleHashValue()
        {
            var registry = CreateRegistryWithPolicy("Order", new EntityGdprPolicy
            {
                StorageRules = new[]
                {
                    new FieldGdprRule { FieldName = "Email", Action = GdprFieldAction.Hash }
                }
            });
            var gdprProcessor = new GdprProcessor(registry);
            var store = CreateStore(gdprProcessor: gdprProcessor);
            var pipeline = new AuditPipeline(
                new AnonymousUserResolver(),
                new FixedSourceResolver(),
                new FixedCorrelationProvider(),
                gdprProcessor,
                store);

            const string originalEmail = "user@example.com";
            var entry = BuildEntry(entityName: "Order");
            entry.Properties = new List<AuditEntryProperty>
            {
                new AuditEntryProperty
                {
                    PropertyName = "Email", PropertyType = "System.String",
                    OldValue = originalEmail, NewValue = originalEmail
                }
            };
            var timestamp = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
            var txn = BuildTransaction(timestamp: timestamp, entries: new List<AuditEntry> { entry });

            var result = await pipeline.ProcessAsync(txn);
            Assert.IsTrue(result.IsSuccess);

            var filePath = Path.Combine(_testDirectory, "audit-2026-06-01.json");
            var savedJson = await System.IO.File.ReadAllTextAsync(filePath);
            var saved = JsonSerializer.Deserialize<List<AuditTransaction>>(savedJson);
            var storedValue = saved[0].Entries.First().Properties.First().OldValue;

            var expectedSingleHash = ComputeSha256Hex(originalEmail);
            var doubleHashed = ComputeSha256Hex(expectedSingleHash);

            Assert.AreEqual(expectedSingleHash, storedValue,
                "FileAuditStore must not re-apply GDPR storage policies already applied by AuditPipeline.");
            Assert.AreNotEqual(doubleHashed, storedValue,
                "Value must not be hashed twice (SHA256(SHA256(x))).");
        }

        private static string ComputeSha256Hex(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                    sb.Append(bytes[i].ToString("x2"));

                return sb.ToString();
            }
        }
    }
}
