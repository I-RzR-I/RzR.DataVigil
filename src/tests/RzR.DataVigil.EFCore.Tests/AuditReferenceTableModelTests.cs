#region U S I N G

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Entries.Referees;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Helpers;

#endregion

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class AuditReferenceTableModelTests
    {
        private ReferenceDataTestDbContext _context;

        private IModel DesignTimeModel
            => _context.GetService<IDesignTimeModel>().Model;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ReferenceDataTestDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;

            _context = new ReferenceDataTestDbContext(options);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context?.Dispose();
        }

        [TestMethod]
        public void Model_EveryReferenceEntity_IsMapped()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                Assert.IsNotNull(DesignTimeModel.FindEntityType(table.EntityType),
                    $"{table.EntityType.Name} is not mapped. The DbSet or the ApplyConfiguration call for "
                    + $"{table.TableName} is missing from AuditDbContextBase.");
            }
        }

        [TestMethod]
        public void Model_EveryReferenceEntity_IsMappedToItsExpectedTableName()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var actual = DesignTimeModel.FindEntityType(table.EntityType).GetTableName();

                Assert.AreEqual(table.TableName, actual,
                    $"{table.EntityType.Name} maps to table '{actual}'. Renaming a shipped reference table is "
                    + "a breaking change for any consumer already querying it.");
            }
        }

        [TestMethod]
        public void SeedData_ForEveryEnumMember_DeclaresExactlyOneRowWithTheEnumValueAsIdAndTheMemberNameAsName()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var rows = SeedRowsOf(table);

                foreach (var member in AuditReferenceTableCatalog.EnumMembersOf(table.EnumType))
                {
                    var matches = rows.Where(r => r.Id == member.Value).ToList();

                    Assert.AreEqual(1, matches.Count,
                        $"{table.TableName}: expected exactly one seed row for {table.EnumType.Name}."
                        + $"{member.Name} (= {member.Value}) but found {matches.Count}. An enum member was most "
                        + "likely added without adding the matching HasData row.");

                    Assert.AreEqual(member.Name, matches[0].Name,
                        $"{table.TableName}: the seed row with Id {member.Value} is named '{matches[0].Name}' "
                        + $"but the enum member with that value is '{member.Name}'. The lookup table would "
                        + "mislabel every audit row that carries this value.");
                }
            }
        }

        [TestMethod]
        public void SeedData_DeclaresNoRowWhoseIdIsNotAnEnumMember()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var validIds = AuditReferenceTableCatalog.EnumMembersOf(table.EnumType)
                    .Select(m => m.Value)
                    .ToList();

                var orphans = SeedRowsOf(table).Where(r => !validIds.Contains(r.Id)).ToList();

                Assert.AreEqual(0, orphans.Count,
                    $"{table.TableName}: found seed row(s) with Id(s) "
                    + $"[{string.Join(", ", orphans.Select(o => o.Id))}] that no {table.EnumType.Name} member "
                    + "maps to. A member was most likely removed or renumbered without updating the seed.");
            }
        }

        [TestMethod]
        public void SeedData_RowCount_EqualsTheNumberOfEnumMembers()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var expected = AuditReferenceTableCatalog.EnumMembersOf(table.EnumType).Count;
                var actual = SeedRowsOf(table).Count;

                Assert.AreEqual(expected, actual,
                    $"{table.TableName}: the seed declares {actual} row(s) but {table.EnumType.Name} declares "
                    + $"{expected} member(s).");
            }
        }

        [TestMethod]
        public void SeedData_EveryRow_HasANonEmptyName()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                foreach (var row in SeedRowsOf(table))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Name),
                        $"{table.TableName}: the seed row with Id {row.Id} has a null or blank Name, so it "
                        + "cannot be joined to or displayed.");
                }
            }
        }

        [TestMethod]
        public void SeedData_EveryRow_HasANonEmptyDescription()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                foreach (var row in SeedRowsOf(table))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Description),
                        $"{table.TableName}: the seed row '{row.Name}' (Id {row.Id}) has no Description. The "
                        + "whole purpose of the lookup table is human-readable analysis of audit data.");
                }
            }
        }

        [TestMethod]
        public void SeedData_NamesWithinEachTable_AreUnique()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var duplicates = SeedRowsOf(table)
                    .GroupBy(r => r.Name, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                Assert.AreEqual(0, duplicates.Count,
                    $"{table.TableName}: duplicate Name value(s) [{string.Join(", ", duplicates)}] in the seed "
                    + "data. The configuration declares a unique index on Name, so this seed could not be "
                    + "applied to a real database.");
            }
        }

        [TestMethod]
        public void SeedData_EveryRowName_FitsTheConfiguredColumnLength()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var maxLength = DesignTimeModel
                    .FindEntityType(table.EntityType)
                    .FindProperty(nameof(RefAuditAction.Name))
                    .GetMaxLength();

                Assert.IsNotNull(maxLength, $"{table.TableName}: Name has no configured max length.");

                foreach (var row in SeedRowsOf(table))
                {
                    Assert.IsNotNull(row.Name, $"{table.TableName}: the seed row with Id {row.Id} has no Name.");

                    Assert.IsTrue(row.Name.Length <= maxLength.Value,
                        $"{table.TableName}: the seed Name '{row.Name}' is {row.Name.Length} characters but the "
                        + $"column allows {maxLength.Value}, so the migration would be rejected.");
                }
            }
        }

        [TestMethod]
        public void SeedData_EveryRowDescription_FitsTheConfiguredColumnLength()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var maxLength = DesignTimeModel
                    .FindEntityType(table.EntityType)
                    .FindProperty(nameof(RefAuditAction.Description))
                    .GetMaxLength();

                Assert.IsNotNull(maxLength, $"{table.TableName}: Description has no configured max length.");

                foreach (var row in SeedRowsOf(table))
                {
                    Assert.IsNotNull(row.Description,
                        $"{table.TableName}: the seed row '{row.Name}' (Id {row.Id}) has no Description.");

                    Assert.IsTrue(row.Description.Length <= maxLength.Value,
                        $"{table.TableName}: the seed Description for '{row.Name}' is {row.Description.Length} "
                        + $"characters but the column allows {maxLength.Value}, so the migration would be "
                        + "rejected.");
                }
            }
        }

        [TestMethod]
        public void Model_EveryReferenceTablePrimaryKey_IsTheSingleIdColumn()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var key = DesignTimeModel.FindEntityType(table.EntityType).FindPrimaryKey();

                Assert.IsNotNull(key, $"{table.TableName}: no primary key is declared.");

                Assert.AreEqual(1, key.Properties.Count,
                    $"{table.TableName}: the primary key spans {key.Properties.Count} column(s); it is expected "
                    + "to be the single Id column carrying the enum value.");

                Assert.AreEqual(nameof(RefAuditAction.Id), key.Properties[0].Name,
                    $"{table.TableName}: the primary key column is '{key.Properties[0].Name}' rather than "
                    + "'Id'.");
            }
        }

        [TestMethod]
        public void Model_EveryReferenceTablePrimaryKey_IsNeverStoreGenerated()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var idProperty = DesignTimeModel
                    .FindEntityType(table.EntityType)
                    .FindProperty(nameof(RefAuditAction.Id));

                Assert.IsNotNull(idProperty, $"{table.TableName}: the model has no Id property.");

                Assert.AreEqual(ValueGenerated.Never, idProperty.ValueGenerated,
                    $"{table.TableName}: Id is configured as ValueGenerated.{idProperty.ValueGenerated} instead "
                    + "of ValueGenerated.Never. The column must carry the enum value verbatim - if EF treats it "
                    + "as store-generated, SQL Server emits IDENTITY(1,1), which cannot hold a zero-valued enum "
                    + "member, and PostgreSQL grows a stray sequence.");
            }
        }

        [TestMethod]
        public void Model_EveryReferenceTableSeedingAZeroId_HasANonGeneratedKey()
        {
            var zeroValued = AuditReferenceTableCatalog.All()
                .Where(t => AuditReferenceTableCatalog.EnumMembersOf(t.EnumType).Any(m => m.Value == 0))
                .ToList();

            Assert.IsTrue(zeroValued.Count > 0,
                "No reference enum declares a zero-valued member any more, so this test no longer verifies "
                + "anything. Either a member was renumbered, or the zero-key risk has genuinely gone away and "
                + "this test should be removed deliberately rather than left passing on an empty set.");

            foreach (var table in zeroValued)
            {
                var idProperty = DesignTimeModel
                    .FindEntityType(table.EntityType)
                    .FindProperty(nameof(RefAuditAction.Id));

                Assert.AreEqual(ValueGenerated.Never, idProperty.ValueGenerated,
                    $"{table.TableName} seeds Id 0 but its key is ValueGenerated.{idProperty.ValueGenerated}. "
                    + "On SQL Server that becomes IDENTITY(1,1), which rejects the zero row outright.");

                Assert.IsTrue(SeedRowsOf(table).Any(r => r.Id == 0),
                    $"{table.TableName}: the zero-valued enum member has no seed row.");
            }
        }

        [TestMethod]
        public void Model_EveryReferenceTable_DeclaresAUniqueIndexOnName()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var nameIndex = DesignTimeModel
                    .FindEntityType(table.EntityType)
                    .GetIndexes()
                    .SingleOrDefault(i => i.Properties.Count == 1
                                          && i.Properties[0].Name == nameof(RefAuditAction.Name));

                Assert.IsNotNull(nameIndex, $"{table.TableName}: no single-column index on Name is declared.");

                Assert.IsTrue(nameIndex.IsUnique,
                    $"{table.TableName}: the index on Name exists but is not unique, so duplicate names could "
                    + "be inserted and joins against the lookup table would fan out.");
            }
        }

        [TestMethod]
        public void Model_EveryReferenceTableName_IsRequired()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var nameProperty = DesignTimeModel
                    .FindEntityType(table.EntityType)
                    .FindProperty(nameof(RefAuditAction.Name));

                Assert.IsFalse(nameProperty.IsNullable,
                    $"{table.TableName}: Name is nullable, so a row with no human-readable label could be "
                    + "inserted.");
            }
        }

        [TestMethod]
        public void Model_EveryEnumColumnPersistedInTheAuditSchema_HasAReferenceTableCoveringEveryValueItCanHold()
        {
            var persistedEnumColumns = new[]
            {
                new { Entity = typeof(AuditEntry), Property = nameof(AuditEntry.Action) },
                new { Entity = typeof(AuditTransaction), Property = nameof(AuditTransaction.GdprState) }
            };

            foreach (var column in persistedEnumColumns)
            {
                var property = DesignTimeModel.FindEntityType(column.Entity).FindProperty(column.Property);

                Assert.IsNotNull(property,
                    $"{column.Entity.Name}.{column.Property} is not mapped any more.");

                var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

                Assert.IsTrue(enumType.IsEnum,
                    $"{column.Entity.Name}.{column.Property} is stored as {property.ClrType.Name}, which is not "
                    + "an enum, so the reference table that describes it no longer applies.");

                var table = AuditReferenceTableCatalog.All().SingleOrDefault(t => t.EnumType == enumType);

                Assert.IsNotNull(table,
                    $"{column.Entity.Name}.{column.Property} persists {enumType.Name} values but no reference "
                    + "table describes that enum, so the stored numbers cannot be resolved to names.");

                var seededIds = SeedRowsOf(table).Select(r => r.Id).ToList();

                foreach (var member in AuditReferenceTableCatalog.EnumMembersOf(enumType))
                {
                    Assert.IsTrue(seededIds.Contains(member.Value),
                        $"{column.Entity.Name}.{column.Property} can hold {enumType.Name}.{member.Name} "
                        + $"(= {member.Value}) but {table.TableName} has no row for it. Joining audit data to "
                        + "the lookup table would drop or fail to label those rows.");
                }
            }
        }

        private List<RefRow> SeedRowsOf(ReferenceTable table)
        {
            var entityType = DesignTimeModel.FindEntityType(table.EntityType);

            Assert.IsNotNull(entityType, $"{table.EntityType.Name} is not mapped in the model.");

            return entityType.GetSeedData()
                .Select(d => new RefRow(
                    Convert.ToInt32(d[nameof(RefAuditAction.Id)]),
                    (string)d[nameof(RefAuditAction.Name)],
                    (string)d[nameof(RefAuditAction.Description)]))
                .ToList();
        }
    }
}
