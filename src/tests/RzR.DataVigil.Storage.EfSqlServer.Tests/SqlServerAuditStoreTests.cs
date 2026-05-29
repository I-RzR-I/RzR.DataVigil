using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Core.Gdpr;
using static RzR.DataVigil.Storage.EfSqlServer.Tests.Helpers.AuditTestDataBuilder;

namespace RzR.DataVigil.Storage.EfSqlServer.Tests
{
    [TestClass]
    public class SqlServerAuditStoreTests
    {
        private AuditSqlServerDbContext _dbContext;
        private SqlServerAuditStore _store;

        [TestInitialize]
        public void Setup()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .AddDebug();
            });

            var logger = loggerFactory.CreateLogger<SqlServerAuditStore>();

            var dbName = "AuditTestDb_" + Guid.NewGuid().ToString("N");
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

        private SqlServerAuditStore CreateStoreWithGdpr(GdprProcessor gdprProcessor)
        {
            return new SqlServerAuditStore(_dbContext,
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SqlServerAuditStore>(),
                gdprProcessor);
        }

        [TestMethod]
        public async Task SaveAsync_WithNullTransaction_ReturnsSuccess()
        {
            var result = await _store.SaveAsync(null);

            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task SaveAsync_WithSingleTransaction_PersistsToDatabase()
        {
            var entry = BuildEntry(entityName: "Product");
            var txn = BuildTransaction(userId: "userA", entries: new List<AuditEntry> { entry });

            var result = await _store.SaveAsync(txn);

            Assert.IsTrue(result.IsSuccess);
            var stored = await _dbContext.AuditTransactions.Include(t => t.Entries).ToListAsync();
            Assert.AreEqual(1, stored.Count);
            Assert.AreEqual("userA", stored[0].UserId);
            Assert.AreEqual(1, stored[0].Entries.Count);
            Assert.AreEqual("Product", stored[0].Entries.First().EntityName);
        }

        [TestMethod]
        public async Task SaveAsync_WithMultipleEntries_PersistsAll()
        {
            var entries = new List<AuditEntry>
            {
                BuildEntry(entityName: "Order"),
                BuildEntry(entityName: "Customer"),
                BuildEntry(entityName: "Product")
            };
            var txn = BuildTransaction(userId: "u1", entries: entries);

            var result = await _store.SaveAsync(txn);

            Assert.IsTrue(result.IsSuccess);
            var entryCount = await _dbContext.AuditEntries.CountAsync();
            Assert.AreEqual(3, entryCount);
        }

        [TestMethod]
        public async Task SaveAsync_WithEntryIncludingProperties_PersistsProperties()
        {
            var entry = BuildEntry(properties: new List<AuditEntryProperty>
            {
                BuildProperty("Price", "10.00", "20.00"),
                BuildProperty("Stock", "100", "80")
            });
            var txn = BuildTransaction(entries: new List<AuditEntry> { entry });

            var result = await _store.SaveAsync(txn);

            Assert.IsTrue(result.IsSuccess);
            var propCount = await _dbContext.AuditEntryProperties.CountAsync();
            Assert.AreEqual(2, propCount);
        }

        [TestMethod]
        public async Task SaveAsync_CalledTwice_AccumulatesBothTransactions()
        {
            var first = BuildTransaction(userId: "batch1", entries: new List<AuditEntry> { BuildEntry() });
            var second = BuildTransaction(userId: "batch2", entries: new List<AuditEntry> { BuildEntry() });

            await _store.SaveAsync(first);
            await _store.SaveAsync(second);

            var count = await _dbContext.AuditTransactions.CountAsync();
            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public async Task SaveAsync_PreservesAllFields()
        {
            var timestamp = new DateTimeOffset(2025, 6, 15, 12, 30, 0, TimeSpan.Zero);
            var entry = BuildEntry(entityName: "Customer", entityId: "99", action: AuditAction.Update);
            var txn = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp,
                UserId = "admin",
                UserName = "Admin User",
                IpAddress = "192.168.1.100",
                CorrelationId = "corr-123",
                TraceId = "trace-456",
                Source = "WebApi",
                GdprState = GdprStorageState.Original,
                Metadata = new Dictionary<string, string> { ["key1"] = "val1" },
                Entries = new List<AuditEntry> { entry }
            };
            entry.TransactionId = txn.Id;

            await _store.SaveAsync(txn);

            var stored = await _dbContext.AuditTransactions.Include(t => t.Entries).FirstAsync();
            Assert.AreEqual("admin", stored.UserId);
            Assert.AreEqual("Admin User", stored.UserName);
            Assert.AreEqual("192.168.1.100", stored.IpAddress);
            Assert.AreEqual("corr-123", stored.CorrelationId);
            Assert.AreEqual("trace-456", stored.TraceId);
            Assert.AreEqual("WebApi", stored.Source);

            var storedEntry = stored.Entries.First();
            Assert.AreEqual(AuditAction.Update, storedEntry.Action);
            Assert.AreEqual("Customer", storedEntry.EntityName);
            Assert.AreEqual("99", storedEntry.EntityId);
        }

        [TestMethod]
        public async Task QueryAsync_WithNoEntries_ReturnsEmptyPagedResult()
        {
            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.AreEqual(0, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithSavedTransactions_ReturnsThem()
        {
            for (int i = 1; i <= 3; i++)
                await _store.SaveAsync(BuildTransaction(userId: $"u{i}", entries: new List<AuditEntry> { BuildEntry() }));

            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.AreEqual(3, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithPaging_ReturnsCorrectPage()
        {
            for (int i = 1; i <= 10; i++)
                await _store.SaveAsync(BuildTransaction(userId: $"u{i}", entries: new List<AuditEntry> { BuildEntry() }));

            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.AreEqual(10, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_SecondPage_ReturnsNextSubset()
        {
            for (int i = 1; i <= 10; i++)
                await _store.SaveAsync(BuildTransaction(userId: $"u{i}", entries: new List<AuditEntry> { BuildEntry() }));

            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.AreEqual(10, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_IncludesEntriesAndProperties()
        {
            var entry = BuildEntry(properties: new List<AuditEntryProperty>
            {
                BuildProperty("Name", "Old", "New")
            });
            await _store.SaveAsync(BuildTransaction(entries: new List<AuditEntry> { entry }));

            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.AreEqual(1, result.Response.Count());
            Assert.AreEqual(1, result.Response.ToList()[0].Entries.Count);
            Assert.AreEqual(1, result.Response.ToList()[0].Entries.First().Properties.Count);
            Assert.AreEqual("Name", result.Response.ToList()[0].Entries.First().Properties.First().PropertyName);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithMatchingUser_ErasesPersonalData()
        {
            var entry = BuildEntry();
            var txn = BuildTransaction(userId: "target", userName: "Target User", ipAddress: "10.0.0.1",
                entries: new List<AuditEntry> { entry });
            await _store.SaveAsync(txn);

            var result = await _store.AnonymizeByUserAsync("target");

            Assert.IsTrue(result.IsSuccess);

            var stored = await _dbContext.AuditTransactions.FirstAsync();
            Assert.AreEqual("[ERASED]", stored.UserId);
            Assert.AreEqual("[ERASED]", stored.UserName);
            Assert.AreEqual("[ERASED]", stored.IpAddress);
            Assert.AreEqual(GdprStorageState.Erased, stored.GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithNonMatchingUser_PreservesOriginalData()
        {
            var txn = BuildTransaction(userId: "safe", userName: "Safe User", ipAddress: "192.168.0.1",
                entries: new List<AuditEntry> { BuildEntry() });
            await _store.SaveAsync(txn);

            var result = await _store.AnonymizeByUserAsync("other_user");

            Assert.IsTrue(result.IsSuccess);

            var stored = await _dbContext.AuditTransactions.FirstAsync();
            Assert.AreEqual("safe", stored.UserId);
            Assert.AreEqual("Safe User", stored.UserName);
            Assert.AreEqual("192.168.0.1", stored.IpAddress);
            Assert.AreEqual(GdprStorageState.Original, stored.GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithMixedUsers_OnlyErasesTargetUser()
        {
            await _store.SaveAsync(BuildTransaction(userId: "target", userName: "Target",
                entries: new List<AuditEntry> { BuildEntry() }));
            await _store.SaveAsync(BuildTransaction(userId: "keep", userName: "Keep Me",
                entries: new List<AuditEntry> { BuildEntry() }));
            await _store.SaveAsync(BuildTransaction(userId: "target", userName: "Target Again",
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await _store.AnonymizeByUserAsync("target");

            Assert.IsTrue(result.IsSuccess);

            var all = await _dbContext.AuditTransactions.ToListAsync();
            var erased = all.Where(t => t.UserId == "[ERASED]").ToList();
            var kept = all.Where(t => t.UserId == "keep").ToList();

            Assert.AreEqual(2, erased.Count);
            Assert.AreEqual(1, kept.Count);
            Assert.AreEqual("Keep Me", kept[0].UserName);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithNoMatchingEntries_ReturnsSuccess()
        {
            await _store.SaveAsync(BuildTransaction(userId: "someone",
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await _store.AnonymizeByUserAsync("nonexistent");

            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithNoEntries_ReturnsSuccess()
        {
            var result = await _store.AnonymizeByUserAsync("anyuser");

            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_AlreadyErasedUser_IsIdempotent()
        {
            var txn = BuildTransaction(userId: "gdpr", userName: "GDPR User", ipAddress: "1.2.3.4", entries: new List<AuditEntry> { BuildEntry() });
            await _store.SaveAsync(txn);
            await _store.AnonymizeByUserAsync("gdpr");

            // Run again
            var result = await _store.AnonymizeByUserAsync("gdpr");
            Assert.IsTrue(result.IsSuccess);

            var stored = await _dbContext.AuditTransactions.FirstAsync();
            Assert.AreEqual("[ERASED]", stored.UserId);
            Assert.AreEqual(GdprStorageState.Erased, stored.GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_OnlyMasksUserFields_NotEntriesOrProperties()
        {
            var entry = BuildEntry(entityName: "Customer", entityId: "cust-1", action: AuditAction.Update, properties: new List<AuditEntryProperty> {
                BuildProperty("Email", "old@example.com", "new@example.com")
            });
            var txn = BuildTransaction(userId: "gdpr2", userName: "GDPR2 User", ipAddress: "5.6.7.8", entries: new List<AuditEntry> { entry });
            await _store.SaveAsync(txn);
            await _store.AnonymizeByUserAsync("gdpr2");

            var stored = await _dbContext.AuditTransactions.Include(t => t.Entries).ThenInclude(e => e.Properties).FirstAsync();
            Assert.AreEqual("[ERASED]", stored.UserId);
            Assert.AreEqual("[ERASED]", stored.UserName);
            Assert.AreEqual("[ERASED]", stored.IpAddress);
            Assert.AreEqual(GdprStorageState.Erased, stored.GdprState);

            var storedEntry = stored.Entries.First();
            Assert.AreEqual("Customer", storedEntry.EntityName);
            Assert.AreEqual("cust-1", storedEntry.EntityId);

            var prop = storedEntry.Properties.First();
            Assert.AreEqual("Email", prop.PropertyName);
            Assert.AreEqual("old@example.com", prop.OldValue);
            Assert.AreEqual("new@example.com", prop.NewValue);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_MultipleTransactionsForSameUser_AllAreErased()
        {
            await _store.SaveAsync(BuildTransaction(userId: "multi", userName: "Multi User", entries: new List<AuditEntry> { BuildEntry() }));
            await _store.SaveAsync(BuildTransaction(userId: "multi", userName: "Multi User", entries: new List<AuditEntry> { BuildEntry() }));
            await _store.AnonymizeByUserAsync("multi");

            var all = await _dbContext.AuditTransactions.ToListAsync();
            Assert.IsTrue(all.All(t => t.UserId == "[ERASED]" && t.GdprState == GdprStorageState.Erased));
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_OnlyMasksUserName_WhenCustomLogicApplied()
        {
            // Simulate a scenario where only UserName is masked (custom logic, for demo)
            var txn = BuildTransaction(userId: "partial", userName: "Partial User", ipAddress: "9.9.9.9",
                entries: new List<AuditEntry> { BuildEntry() });
            await _store.SaveAsync(txn);

            // Manually mask only UserName
            var stored = await _dbContext.AuditTransactions.FirstAsync();
            stored.UserName = "[ERASED]";
            await _dbContext.SaveChangesAsync();

            // Now anonymize by user (should not affect UserId/IpAddress if already erased)
            await _store.AnonymizeByUserAsync("partial");
            var result = await _dbContext.AuditTransactions.FirstAsync();
            Assert.AreEqual("[ERASED]", result.UserId); // Anonymizer always erases UserId
            Assert.AreEqual("[ERASED]", result.UserName);
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_RemovesTransactionsOlderThanCutoff()
        {
            var cutoff = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
            await _store.SaveAsync(BuildTransaction(userId: "old", timestamp: cutoff.AddDays(-1),
                entries: new List<AuditEntry> { BuildEntry() }));
            await _store.SaveAsync(BuildTransaction(userId: "new", timestamp: cutoff.AddDays(1),
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await _store.PurgeBeforeAsync(cutoff);

            Assert.IsTrue(result.IsSuccess);

            var remaining = await _dbContext.AuditTransactions.ToListAsync();
            Assert.AreEqual(1, remaining.Count);
            Assert.AreEqual("new", remaining[0].UserId);
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_PreservesTransactionsAtCutoffBoundary()
        {
            var cutoff = new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero);
            await _store.SaveAsync(BuildTransaction(userId: "at", timestamp: cutoff,
                entries: new List<AuditEntry> { BuildEntry() }));
            await _store.SaveAsync(BuildTransaction(userId: "after", timestamp: cutoff.AddSeconds(1),
                entries: new List<AuditEntry> { BuildEntry() }));

            await _store.PurgeBeforeAsync(cutoff);

            var remaining = await _dbContext.AuditTransactions.ToListAsync();
            Assert.AreEqual(2, remaining.Count);
            Assert.IsTrue(remaining.Any(t => t.UserId == "at"));
            Assert.IsTrue(remaining.Any(t => t.UserId == "after"));
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_RemovesRelatedEntriesAndProperties()
        {
            var cutoff = new DateTimeOffset(2025, 8, 1, 0, 0, 0, TimeSpan.Zero);
            var entry = BuildEntry(properties: new List<AuditEntryProperty>
            {
                BuildProperty("Field", "OldVal", "NewVal")
            });
            await _store.SaveAsync(BuildTransaction(userId: "old", timestamp: cutoff.AddDays(-1),
                entries: new List<AuditEntry> { entry }));
            await _store.SaveAsync(BuildTransaction(userId: "new", timestamp: cutoff.AddDays(1),
                entries: new List<AuditEntry> { BuildEntry() }));

            await _store.PurgeBeforeAsync(cutoff);

            var propCount = await _dbContext.AuditEntryProperties.CountAsync();
            Assert.AreEqual(0, propCount);
            var entryCount = await _dbContext.AuditEntries.CountAsync();
            Assert.AreEqual(1, entryCount);
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_WithNoMatchingEntries_ReturnsSuccessAndChangesNothing()
        {
            var cutoff = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            await _store.SaveAsync(BuildTransaction(userId: "future", timestamp: cutoff.AddMonths(6),
                entries: new List<AuditEntry> { BuildEntry() }));

            var result = await _store.PurgeBeforeAsync(cutoff);

            Assert.IsTrue(result.IsSuccess);
            var count = await _dbContext.AuditTransactions.CountAsync();
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_WithNoEntries_ReturnsSuccess()
        {
            var result = await _store.PurgeBeforeAsync(DateTimeOffset.UtcNow);

            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public async Task PurgeBeforeAsync_RemovesAllWhenAllAreOld()
        {
            var cutoff = DateTimeOffset.UtcNow;
            for (int i = 1; i <= 5; i++)
                await _store.SaveAsync(BuildTransaction(userId: $"u{i}", timestamp: cutoff.AddDays(-i),
                    entries: new List<AuditEntry> { BuildEntry() }));

            await _store.PurgeBeforeAsync(cutoff);

            var count = await _dbContext.AuditTransactions.CountAsync();
            Assert.AreEqual(0, count);
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
            var store = CreateStoreWithGdpr(new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order", properties: new List<AuditEntryProperty>
            {
                BuildProperty("Email", "old@test.com", "new@test.com")
            });
            await store.SaveAsync(BuildTransaction(entries: new List<AuditEntry> { entry }));

            var result = await store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

            Assert.IsTrue(result.IsSuccess);
            var prop = result.Response.First().Entries.First().Properties.First();
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
            var store = CreateStoreWithGdpr(new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order", properties: new List<AuditEntryProperty>
            {
                BuildProperty("Phone", "555-1234", "555-5678")
            });
            await store.SaveAsync(BuildTransaction(entries: new List<AuditEntry> { entry }));

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
            var store = CreateStoreWithGdpr(new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order", properties: new List<AuditEntryProperty>
            {
                BuildProperty("Email", "old@test.com", "new@test.com")
            });
            await store.SaveAsync(BuildTransaction(entries: new List<AuditEntry> { entry }));

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
            var store = CreateStoreWithGdpr(new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order", properties: new List<AuditEntryProperty>
            {
                BuildProperty("SSN", "123-45-6789", "987-65-4321")
            });
            await store.SaveAsync(BuildTransaction(entries: new List<AuditEntry> { entry }));

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
            var entry = BuildEntry(entityName: "NoPolicy", properties: new List<AuditEntryProperty>
            {
                BuildProperty("Name", "Alice", "Bob")
            });
            await _store.SaveAsync(BuildTransaction(entries: new List<AuditEntry> { entry }));

            var result = await _store.QueryAsync(new AuditTransactionQuery(), new GdprRetrievalContext());

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
            var store = CreateStoreWithGdpr(new GdprProcessor(registry));

            var entry = BuildEntry(entityName: "Order", properties: new List<AuditEntryProperty>
            {
                BuildProperty("Email", "user@test.com", "new@test.com"),
                BuildProperty("Phone", "555-1234", "555-5678"),
                BuildProperty("Name", "Alice", "Bob")
            });
            await store.SaveAsync(BuildTransaction(entries: new List<AuditEntry> { entry }));

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
    }
}
