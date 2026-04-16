using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.EFCore.Interceptors;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Entities;
using RzR.DataVigil.EFCore.Tests.Stubs;

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class AuditSaveChangesInterceptorTests
    {
        private StubAuditStore _store;
        private AuditSaveChangesInterceptor _interceptor;
        private DbContextOptions<AuditableTestDbContext> _dbOptions;

        [TestInitialize]
        public void Setup()
        {
            _store = new StubAuditStore();

            var options = new AuditTrailOptions();
            options.EfCore.Intercept<AuditableTestDbContext>();

            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                _store);

            var logger = LoggerFactory.Create(_ => { }).CreateLogger<AuditSaveChangesInterceptor>();

            _interceptor = new AuditSaveChangesInterceptor(options, pipeline, logger);

            _dbOptions = new DbContextOptionsBuilder<AuditableTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(_interceptor)
                .Options;
        }

        [TestMethod]
        public async Task SaveChangesAsync_AddEntity_DetectsCreate()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Orders.Add(new AuditableOrder
            {
                CustomerName = "Alice",
                Total = 100m,
                Quantity = 2
            });

            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);

            var entry = _store.SavedTransactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Create, entry.Action);
            Assert.AreEqual("AuditableOrder", entry.EntityName);
        }

        [TestMethod]
        public void SaveChanges_AddEntity_DetectsCreate()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Orders.Add(new AuditableOrder
            {
                CustomerName = "Bob",
                Total = 50m,
                Quantity = 1
            });

            ctx.SaveChanges();

            Assert.AreEqual(1, _store.SavedTransactions.Count);

            var entry = _store.SavedTransactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Create, entry.Action);
        }

        [TestMethod]
        public async Task SaveChangesAsync_CreateEntry_OldValuesAreNull()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Orders.Add(new AuditableOrder
            {
                CustomerName = "Carol",
                Total = 75m,
                Quantity = 3
            });

            await ctx.SaveChangesAsync();

            var entry = _store.SavedTransactions[0].Entries.Single();
            foreach (var prop in entry.Properties)
            {
                Assert.IsNull(prop.OldValue, $"OldValue should be null for Create, property: {prop.PropertyName}");
            }
        }

        [TestMethod]
        public async Task SaveChangesAsync_ModifyEntity_DetectsUpdate()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var order = new AuditableOrder
            {
                CustomerName = "Dave",
                Total = 200m,
                Quantity = 5
            };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            order.Total = 250m;
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);

            var entry = _store.SavedTransactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Update, entry.Action);
            Assert.AreEqual("AuditableOrder", entry.EntityName);
        }

        [TestMethod]
        public async Task SaveChangesAsync_UpdateEntry_TracksOldAndNewValues()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var order = new AuditableOrder
            {
                CustomerName = "Eve",
                Total = 100m,
                Quantity = 1
            };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            order.CustomerName = "Eve Updated";
            await ctx.SaveChangesAsync();

            var entry = _store.SavedTransactions[0].Entries.Single();
            var nameProp = entry.Properties.FirstOrDefault(p => p.PropertyName == "CustomerName");
            Assert.IsNotNull(nameProp);
            Assert.AreEqual("Eve", nameProp.OldValue);
            Assert.AreEqual("Eve Updated", nameProp.NewValue);
        }
        
        [TestMethod]
        public async Task SaveChangesAsync_RemoveEntity_DetectsDelete()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var order = new AuditableOrder
            {
                CustomerName = "Frank",
                Total = 300m,
                Quantity = 10
            };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            ctx.Orders.Remove(order);
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);

            var entry = _store.SavedTransactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Delete, entry.Action);
            Assert.AreEqual("AuditableOrder", entry.EntityName);
        }

        [TestMethod]
        public async Task SaveChangesAsync_DeleteEntry_NewValuesAreNull()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var order = new AuditableOrder
            {
                CustomerName = "Grace",
                Total = 400m,
                Quantity = 7
            };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            ctx.Orders.Remove(order);
            await ctx.SaveChangesAsync();

            var entry = _store.SavedTransactions[0].Entries.Single();
            foreach (var prop in entry.Properties)
            {
                Assert.IsNull(prop.NewValue, $"NewValue should be null for Delete, property: {prop.PropertyName}");
            }
        }

        [TestMethod]
        public async Task SaveChangesAsync_MixedCUD_DetectsAllActions()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);

            var toUpdate = new AuditableOrder { CustomerName = "Update-Me", Total = 10m, Quantity = 1 };
            var toDelete = new AuditableOrder { CustomerName = "Delete-Me", Total = 20m, Quantity = 2 };
            ctx.Orders.AddRange(toUpdate, toDelete);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            // Create
            ctx.Orders.Add(new AuditableOrder { CustomerName = "New-One", Total = 30m, Quantity = 3 });
            // Update
            toUpdate.Total = 99m;
            // Delete
            ctx.Orders.Remove(toDelete);

            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);

            var entries = _store.SavedTransactions[0].Entries.ToList();
            Assert.AreEqual(3, entries.Count);

            CollectionAssert.AreEquivalent(
                new[] { AuditAction.Create, AuditAction.Update, AuditAction.Delete },
                entries.Select(e => e.Action).ToArray());
        }

        [TestMethod]
        public async Task SaveChangesAsync_UnchangedEntity_NotAudited()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var order = new AuditableOrder { CustomerName = "Unchanged", Total = 10m, Quantity = 1 };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            // SaveChanges with no modifications
            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, _store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task SaveChangesAsync_GloballyExcludedEntity_NotAudited()
        {
            var store = new StubAuditStore();

            var opts = new AuditTrailOptions();
            opts.EfCore.Intercept<AuditableTestDbContext>();
            opts.GlobalExclusions.Add(typeof(AuditableOrder));

            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                store);

            var logger = LoggerFactory.Create(_ => { }).CreateLogger<AuditSaveChangesInterceptor>();
            var interceptor = new AuditSaveChangesInterceptor(opts, pipeline, logger);

            var dbOpts = new DbContextOptionsBuilder<AuditableTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;

            using var ctx = new AuditableTestDbContext(dbOpts);
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Excluded", Total = 1m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task SaveChangesAsync_TransactionHasTimestampAndId()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Meta", Total = 5m, Quantity = 1 });

            await ctx.SaveChangesAsync();

            var tx = _store.SavedTransactions.Single();
            Assert.AreNotEqual(Guid.Empty, tx.Id);
            Assert.IsTrue(tx.Timestamp > DateTimeOffset.MinValue);
        }

        [TestMethod]
        public async Task SaveChangesAsync_EntriesShareTransactionId()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Orders.Add(new AuditableOrder { CustomerName = "A", Total = 1m, Quantity = 1 });
            ctx.Orders.Add(new AuditableOrder { CustomerName = "B", Total = 2m, Quantity = 2 });

            await ctx.SaveChangesAsync();

            var tx = _store.SavedTransactions.Single();
            Assert.IsTrue(tx.Entries.Count >= 2);
            foreach (var entry in tx.Entries)
            {
                Assert.AreEqual(tx.Id, entry.TransactionId);
            }
        }

        [TestMethod]
        public async Task ShouldAudit_CreateAllowed_EntityIsAudited()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Products.Add(new SelectiveAuditProduct
            {
                Name = "Widget",
                Price = 9.99m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Create }
            });

            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);
            var entry = _store.SavedTransactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Create, entry.Action);
            Assert.AreEqual("SelectiveAuditProduct", entry.EntityName);
        }

        [TestMethod]
        public async Task ShouldAudit_CreateDenied_EntityIsSkipped()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Products.Add(new SelectiveAuditProduct
            {
                Name = "Gadget",
                Price = 19.99m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Update, AuditAction.Delete }
            });

            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, _store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task ShouldAudit_UpdateAllowed_EntityIsAudited()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var product = new SelectiveAuditProduct
            {
                Name = "Gizmo",
                Price = 5m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Create, AuditAction.Update }
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            product.Price = 7.5m;
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);
            var entry = _store.SavedTransactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Update, entry.Action);
        }

        [TestMethod]
        public async Task ShouldAudit_UpdateDenied_EntityIsSkipped()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var product = new SelectiveAuditProduct
            {
                Name = "Doohickey",
                Price = 3m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Create, AuditAction.Delete }
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            product.Price = 4m;
            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, _store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task ShouldAudit_DeleteAllowed_EntityIsAudited()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var product = new SelectiveAuditProduct
            {
                Name = "Thingamajig",
                Price = 12m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Create, AuditAction.Delete }
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            ctx.Products.Remove(product);
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);
            var entry = _store.SavedTransactions[0].Entries.Single();
            Assert.AreEqual(AuditAction.Delete, entry.Action);
        }

        [TestMethod]
        public async Task ShouldAudit_DeleteDenied_EntityIsSkipped()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var product = new SelectiveAuditProduct
            {
                Name = "Whatchamacallit",
                Price = 15m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Create, AuditAction.Update }
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            ctx.Products.Remove(product);
            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, _store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task ShouldAudit_NoActionsAllowed_AllOperationsSkipped()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var product = new SelectiveAuditProduct
            {
                Name = "Silent",
                Price = 1m,
                AllowedActions = new HashSet<AuditAction>()
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, _store.SavedTransactions.Count, "Create should be skipped");

            product.Price = 2m;
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, _store.SavedTransactions.Count, "Update should be skipped");

            ctx.Products.Remove(product);
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, _store.SavedTransactions.Count, "Delete should be skipped");
        }

        [TestMethod]
        public async Task ShouldAudit_AllActionsAllowed_AllOperationsAudited()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var product = new SelectiveAuditProduct
            {
                Name = "FullAudit",
                Price = 10m,
                AllowedActions = new HashSet<AuditAction>
                    { AuditAction.Create, AuditAction.Update, AuditAction.Delete }
            };

            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            Assert.AreEqual(1, _store.SavedTransactions.Count);
            Assert.AreEqual(AuditAction.Create, _store.SavedTransactions[0].Entries.Single().Action);
            _store.SavedTransactions.Clear();

            product.Price = 20m;
            await ctx.SaveChangesAsync();
            Assert.AreEqual(1, _store.SavedTransactions.Count);
            Assert.AreEqual(AuditAction.Update, _store.SavedTransactions[0].Entries.Single().Action);
            _store.SavedTransactions.Clear();

            ctx.Products.Remove(product);
            await ctx.SaveChangesAsync();
            Assert.AreEqual(1, _store.SavedTransactions.Count);
            Assert.AreEqual(AuditAction.Delete, _store.SavedTransactions[0].Entries.Single().Action);
        }

        [TestMethod]
        public async Task ShouldAudit_MixedEntities_OnlyAllowedAreAudited()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);

            // IAuditable order (always audited for CUD) + IAuditableEntity product (Create denied)
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Mixed", Total = 1m, Quantity = 1 });
            ctx.Products.Add(new SelectiveAuditProduct
            {
                Name = "Denied",
                Price = 5m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Update }
            });

            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);
            var entries = _store.SavedTransactions[0].Entries.ToList();

            // Only the Order should be audited; the Product's Create is denied
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("AuditableOrder", entries[0].EntityName);
        }

        [TestMethod]
        public void ShouldAudit_SyncPath_Respected()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Products.Add(new SelectiveAuditProduct
            {
                Name = "SyncDenied",
                Price = 2m,
                AllowedActions = new HashSet<AuditAction>()
            });

            ctx.SaveChanges();

            Assert.AreEqual(0, _store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task Exclusion_NonIAuditableEntity_NeverAudited()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Logs.Add(new NonAuditableLog { Message = "boot" });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, _store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task Exclusion_NonIAuditable_UpdateAndDelete_NeverAudited()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var log = new NonAuditableLog { Message = "initial" };
            ctx.Logs.Add(log);
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, _store.SavedTransactions.Count);

            log.Message = "changed";
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, _store.SavedTransactions.Count);

            ctx.Logs.Remove(log);
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, _store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task Exclusion_NonIAuditable_MixedWithAuditable_OnlyAuditableTracked()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Logs.Add(new NonAuditableLog { Message = "ignored" });
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Tracked", Total = 1m, Quantity = 1 });

            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, _store.SavedTransactions.Count);
            var entries = _store.SavedTransactions[0].Entries.ToList();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("AuditableOrder", entries[0].EntityName);
        }

        [TestMethod]
        public async Task Exclusion_GlobalExclusion_EntitySkippedOnCreate()
        {
            var store = new StubAuditStore();
            var opts = new AuditTrailOptions();
            opts.EfCore.Intercept<AuditableTestDbContext>();
            opts.GlobalExclusions.Add(typeof(AuditableOrder));

            var (dbOpts, _) = BuildInterceptorAndDbOptions<AuditableTestDbContext>(opts, store);

            using var ctx = new AuditableTestDbContext(dbOpts);
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Excluded", Total = 1m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task Exclusion_GlobalExclusion_EntitySkippedOnUpdateAndDelete()
        {
            var store = new StubAuditStore();
            var opts = new AuditTrailOptions();
            opts.EfCore.Intercept<AuditableTestDbContext>();
            opts.GlobalExclusions.Add(typeof(AuditableOrder));

            var (dbOpts, _) = BuildInterceptorAndDbOptions<AuditableTestDbContext>(opts, store);

            using var ctx = new AuditableTestDbContext(dbOpts);
            // Add is excluded too, but we need an entity in the tracker
            var order = new AuditableOrder { CustomerName = "Seed", Total = 10m, Quantity = 1 };
            ctx.Orders.Add(order);
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, store.SavedTransactions.Count, "Create should be excluded");

            order.Total = 99m;
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, store.SavedTransactions.Count, "Update should be excluded");

            ctx.Orders.Remove(order);
            await ctx.SaveChangesAsync();
            Assert.AreEqual(0, store.SavedTransactions.Count, "Delete should be excluded");
        }

        [TestMethod]
        public async Task Exclusion_GlobalExclusion_OtherEntitiesStillAudited()
        {
            var store = new StubAuditStore();
            var opts = new AuditTrailOptions();
            opts.EfCore.Intercept<AuditableTestDbContext>();
            opts.GlobalExclusions.Add(typeof(AuditableOrder));

            var (dbOpts, _) = BuildInterceptorAndDbOptions<AuditableTestDbContext>(opts, store);

            using var ctx = new AuditableTestDbContext(dbOpts);
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Excluded", Total = 1m, Quantity = 1 });
            ctx.Products.Add(new SelectiveAuditProduct
            {
                Name = "Included",
                Price = 5m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Create }
            });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, store.SavedTransactions.Count);
            var entries = store.SavedTransactions[0].Entries.ToList();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("SelectiveAuditProduct", entries[0].EntityName);
        }

        [TestMethod]
        public async Task Exclusion_ContextLevel_ExcludedEntitySkipped()
        {
            var store = new StubAuditStore();
            var opts = new AuditTrailOptions();
            opts.EfCore.Intercept<ContextExcludingOrderDbContext>();

            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                store);
            var logger = LoggerFactory.Create(_ => { }).CreateLogger<AuditSaveChangesInterceptor>();
            var interceptor = new AuditSaveChangesInterceptor(opts, pipeline, logger);

            var dbOpts = new DbContextOptionsBuilder<ContextExcludingOrderDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;

            using var ctx = new ContextExcludingOrderDbContext(dbOpts);
            ctx.Orders.Add(new AuditableOrder { CustomerName = "CtxExcluded", Total = 1m, Quantity = 1 });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(0, store.SavedTransactions.Count);
        }

        [TestMethod]
        public async Task Exclusion_ContextLevel_NonExcludedEntitiesStillAudited()
        {
            var store = new StubAuditStore();
            var opts = new AuditTrailOptions();
            opts.EfCore.Intercept<ContextExcludingOrderDbContext>();

            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                store);
            var logger = LoggerFactory.Create(_ => { }).CreateLogger<AuditSaveChangesInterceptor>();
            var interceptor = new AuditSaveChangesInterceptor(opts, pipeline, logger);

            var dbOpts = new DbContextOptionsBuilder<ContextExcludingOrderDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;

            using var ctx = new ContextExcludingOrderDbContext(dbOpts);
            ctx.Orders.Add(new AuditableOrder { CustomerName = "Excluded", Total = 1m, Quantity = 1 });
            ctx.Products.Add(new SelectiveAuditProduct
            {
                Name = "Audited",
                Price = 3m,
                AllowedActions = new HashSet<AuditAction> { AuditAction.Create }
            });
            await ctx.SaveChangesAsync();

            Assert.AreEqual(1, store.SavedTransactions.Count);
            var entries = store.SavedTransactions[0].Entries.ToList();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("SelectiveAuditProduct", entries[0].EntityName);
        }

        // ───────── Excluded fields / properties ─────────

        [TestMethod]
        public async Task ExcludedFields_Create_ExcludedPropertyOmitted()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.FieldExclusions.Add(new FieldExclusionEntity
            {
                PublicNote = "visible",
                SecretNote = "hidden",
                InternalCode = "IC-01",
                ExcludedFieldNames = new HashSet<string> { "SecretNote" }
            });

            await ctx.SaveChangesAsync();

            var entry = _store.SavedTransactions[0].Entries.Single();
            var propNames = entry.Properties.Select(p => p.PropertyName).ToList();
            Assert.IsTrue(propNames.Contains("PublicNote"));
            Assert.IsFalse(propNames.Contains("SecretNote"), "SecretNote should be excluded");
            Assert.IsTrue(propNames.Contains("InternalCode"));
        }

        [TestMethod]
        public async Task ExcludedFields_Update_ExcludedPropertyOmitted()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var entity = new FieldExclusionEntity
            {
                PublicNote = "old-public",
                SecretNote = "old-secret",
                InternalCode = "IC-01",
                ExcludedFieldNames = new HashSet<string> { "SecretNote" }
            };
            ctx.FieldExclusions.Add(entity);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            entity.PublicNote = "new-public";
            entity.SecretNote = "new-secret";
            await ctx.SaveChangesAsync();

            var auditEntry = _store.SavedTransactions[0].Entries.Single();
            var propNames = auditEntry.Properties.Select(p => p.PropertyName).ToList();
            Assert.IsTrue(propNames.Contains("PublicNote"));
            Assert.IsFalse(propNames.Contains("SecretNote"), "SecretNote should be excluded from update");
        }

        [TestMethod]
        public async Task ExcludedFields_Delete_ExcludedPropertyOmitted()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            var entity = new FieldExclusionEntity
            {
                PublicNote = "to-delete",
                SecretNote = "secret",
                InternalCode = "IC-99",
                ExcludedFieldNames = new HashSet<string> { "SecretNote" }
            };
            ctx.FieldExclusions.Add(entity);
            await ctx.SaveChangesAsync();
            _store.SavedTransactions.Clear();

            ctx.FieldExclusions.Remove(entity);
            await ctx.SaveChangesAsync();

            var auditEntry = _store.SavedTransactions[0].Entries.Single();
            var propNames = auditEntry.Properties.Select(p => p.PropertyName).ToList();
            Assert.IsTrue(propNames.Contains("PublicNote"));
            Assert.IsFalse(propNames.Contains("SecretNote"), "SecretNote should be excluded from delete");
        }

        [TestMethod]
        public async Task ExcludedFields_MultipleFieldsExcluded()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.FieldExclusions.Add(new FieldExclusionEntity
            {
                PublicNote = "keep",
                SecretNote = "hide1",
                InternalCode = "hide2",
                ExcludedFieldNames = new HashSet<string> { "SecretNote", "InternalCode" }
            });

            await ctx.SaveChangesAsync();

            var entry = _store.SavedTransactions[0].Entries.Single();
            var propNames = entry.Properties.Select(p => p.PropertyName).ToList();
            Assert.IsTrue(propNames.Contains("PublicNote"));
            Assert.IsFalse(propNames.Contains("SecretNote"));
            Assert.IsFalse(propNames.Contains("InternalCode"));
        }

        [TestMethod]
        public async Task ExcludedFields_NoFieldsExcluded_AllPropertiesPresent()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.FieldExclusions.Add(new FieldExclusionEntity
            {
                PublicNote = "a",
                SecretNote = "b",
                InternalCode = "c",
                ExcludedFieldNames = new HashSet<string>()
            });

            await ctx.SaveChangesAsync();

            var entry = _store.SavedTransactions[0].Entries.Single();
            var propNames = entry.Properties.Select(p => p.PropertyName).ToList();
            Assert.IsTrue(propNames.Contains("PublicNote"));
            Assert.IsTrue(propNames.Contains("SecretNote"));
            Assert.IsTrue(propNames.Contains("InternalCode"));
        }

        [TestMethod]
        public async Task ExcludedFields_PlainIAuditableEntity_NoFieldsExcluded()
        {
            using var ctx = new AuditableTestDbContext(_dbOptions);
            ctx.Orders.Add(new AuditableOrder
            {
                CustomerName = "Full",
                Total = 50m,
                Quantity = 3
            });

            await ctx.SaveChangesAsync();

            var entry = _store.SavedTransactions[0].Entries.Single();
            var propNames = entry.Properties.Select(p => p.PropertyName).ToList();
            Assert.IsTrue(propNames.Contains("CustomerName"));
            Assert.IsTrue(propNames.Contains("Total"));
            Assert.IsTrue(propNames.Contains("Quantity"));
        }

        // ───────── Helper ─────────

        private (DbContextOptions<T> dbOptions, AuditSaveChangesInterceptor interceptor)
            BuildInterceptorAndDbOptions<T>(AuditTrailOptions opts, StubAuditStore store)
            where T : DbContext
        {
            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                store);

            var logger = LoggerFactory.Create(_ => { }).CreateLogger<AuditSaveChangesInterceptor>();
            var interceptor = new AuditSaveChangesInterceptor(opts, pipeline, logger);

            var dbOpts = new DbContextOptionsBuilder<T>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;

            return (dbOpts, interceptor);
        }
    }
}
