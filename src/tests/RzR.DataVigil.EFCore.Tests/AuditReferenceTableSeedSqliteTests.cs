#region U S I N G

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Entries.Referees;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Helpers;

#endregion

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class AuditReferenceTableSeedSqliteTests
    {
        private SqliteConnection _connection;
        private DbContextOptions<ReferenceDataTestDbContext> _dbOptions;
        private ReferenceDataTestDbContext _context;

        [TestInitialize]
        public void Setup()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _dbOptions = new DbContextOptionsBuilder<ReferenceDataTestDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ReferenceDataTestDbContext(_dbOptions);
            _context.Database.EnsureCreated();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context?.Dispose();
            _connection?.Dispose();
        }

        [TestMethod]
        public void CreatedDatabase_EveryReferenceTable_HoldsOneRowPerEnumMemberWithMatchingIdAndName()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var rows = ReadRows(table);

                foreach (var member in AuditReferenceTableCatalog.EnumMembersOf(table.EnumType))
                {
                    var matches = rows.Where(r => r.Id == member.Value).ToList();

                    Assert.AreEqual(1, matches.Count,
                        $"{table.TableName}: the created database holds {matches.Count} row(s) for "
                        + $"{table.EnumType.Name}.{member.Name} (= {member.Value}); exactly one seed row was "
                        + "expected.");

                    Assert.AreEqual(member.Name, matches[0].Name,
                        $"{table.TableName}: the persisted row with Id {member.Value} came back as "
                        + $"'{matches[0].Name}' rather than '{member.Name}'.");
                }
            }
        }

        [TestMethod]
        public void CreatedDatabase_EveryReferenceTable_HoldsExactlyAsManyRowsAsTheEnumHasMembers()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var expected = AuditReferenceTableCatalog.EnumMembersOf(table.EnumType).Count;
                var actual = ReadRows(table).Count;

                Assert.AreEqual(expected, actual,
                    $"{table.TableName}: the created database holds {actual} row(s) but "
                    + $"{table.EnumType.Name} declares {expected} member(s).");
            }
        }

        [TestMethod]
        public void CreatedDatabase_EveryReferenceRow_KeepsItsNameAndDescription()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                foreach (var row in ReadRows(table))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Name),
                        $"{table.TableName}: the row with Id {row.Id} came back with a null or blank Name.");

                    Assert.IsFalse(string.IsNullOrWhiteSpace(row.Description),
                        $"{table.TableName}: the row '{row.Name}' (Id {row.Id}) came back with no Description. "
                        + "The whole purpose of the lookup table is human-readable analysis of audit data.");
                }
            }
        }

        [TestMethod]
        public void CreatedDatabase_EveryZeroValuedEnumMember_IsReadableBack()
        {
            var zeroValued = AuditReferenceTableCatalog.All()
                .Select(t => new
                {
                    Table = t,
                    Member = AuditReferenceTableCatalog.EnumMembersOf(t.EnumType)
                        .FirstOrDefault(m => m.Value == 0)
                })
                .Where(x => x.Member != null)
                .ToList();

            Assert.IsTrue(zeroValued.Count > 0,
                "No reference enum declares a zero-valued member any more, so this test no longer verifies "
                + "anything. Either a member was renumbered, or the zero-key risk has genuinely gone away and "
                + "this test should be removed deliberately rather than left passing on an empty set.");

            foreach (var x in zeroValued)
            {
                var rows = ReadRows(x.Table).Where(r => r.Id == 0).ToList();

                Assert.AreEqual(1, rows.Count,
                    $"{x.Table.TableName}: the seed row for {x.Table.EnumType.Name}.{x.Member.Name} (Id 0) did "
                    + "not come back from the database. A zero-valued primary key was dropped or rejected on "
                    + "insert.");

                Assert.AreEqual(x.Member.Name, rows[0].Name,
                    $"{x.Table.TableName}: the Id 0 row round-tripped as '{rows[0].Name}' rather than "
                    + $"'{x.Member.Name}'.");
            }
        }

        [TestMethod]
        public async Task CreatedDatabase_InsertingASecondRowWithAnExistingName_IsRejected()
        {
            _context.RefAuditActions.Add(new RefAuditAction
            {
                Id = 9999,
                Name = nameof(AuditAction.Create),
                Description = "Duplicate of the seeded Create row."
            });

            await Assert.ThrowsExceptionAsync<DbUpdateException>(
                () => _context.SaveChangesAsync(),
                "RefAuditActions accepted a second row named 'Create'. The unique index on Name is declared in "
                + "the configuration but did not reach the created schema.");
        }

        [TestMethod]
        public async Task AuditTransaction_IsStillPersisted_WhenEveryReferenceTableHasBeenDropped()
        {
            foreach (var table in AuditReferenceTableCatalog.All())
            {
                var tableName = _context.Model.FindEntityType(table.EntityType).GetTableName();

                await _context.Database.ExecuteSqlRawAsync("DROP TABLE \"" + tableName + "\";");
            }

            var transaction = new AuditTransaction
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                UserId = "user-1",
                GdprState = GdprStorageState.Original
            };

            _context.AuditTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            using var verify = new ReferenceDataTestDbContext(_dbOptions);

            Assert.AreEqual(1, await verify.AuditTransactions.CountAsync(),
                "Writing an audit transaction failed once the reference tables were absent. The reference "
                + "tables are optional - a consumer who never applies the AddAuditReferenceTables migration "
                + "must keep working - so nothing on the write path may depend on them.");
        }

        private List<RefRow> ReadRows(ReferenceTable table)
        {
            if (table.EntityType == typeof(RefAuditAction))
                return _context.RefAuditActions.AsNoTracking().ToList()
                    .Select(r => new RefRow(r.Id, r.Name, r.Description)).ToList();

            if (table.EntityType == typeof(RefAuditUserSource))
                return _context.RefAuditUserSources.AsNoTracking().ToList()
                    .Select(r => new RefRow(r.Id, r.Name, r.Description)).ToList();

            if (table.EntityType == typeof(RefGdprFieldAction))
                return _context.RefGdprFieldActions.AsNoTracking().ToList()
                    .Select(r => new RefRow(r.Id, r.Name, r.Description)).ToList();

            if (table.EntityType == typeof(RefGdprStorageState))
                return _context.RefGdprStorageStates.AsNoTracking().ToList()
                    .Select(r => new RefRow(r.Id, r.Name, r.Description)).ToList();

            throw new NotSupportedException(
                $"No database reader is wired up for reference entity '{table.EntityType.Name}'. Add one so "
                + "the new reference table is covered by these round-trip tests.");
        }
    }
}
