#region U S I N G

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.EFCore.Interceptors;
using RzR.DataVigil.EFCore.Tests.Data;
using RzR.DataVigil.EFCore.Tests.Entities;
using RzR.DataVigil.EFCore.Tests.Stubs;

#endregion

namespace RzR.DataVigil.EFCore.Tests
{
    [TestClass]
    public class ReadAuditFormatDiagnosticTests
    {
        private SqliteConnection _connection;
        private DbContextOptions<SqliteTestDbContext> _dbOptions;
        private StubAuditStore _store;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Setup()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _store = new StubAuditStore();

            var options = new AuditTrailOptions();
            options.EfCore.Intercept<SqliteTestDbContext>();
            options.EfCore.IncludeReads();
            options.EfCore.IncludeReadProperties();

            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(new GdprPolicyRegistry()),
                _store);

            var factory = LoggerFactory.Create(_ => { });

            _dbOptions = new DbContextOptionsBuilder<SqliteTestDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(new AuditSaveChangesInterceptor(options, pipeline,
                    factory.CreateLogger<AuditSaveChangesInterceptor>()))
                .AddInterceptors(new AuditCommandInterceptor(options, pipeline,
                    factory.CreateLogger<AuditCommandInterceptor>()))
                .Options;

            using var seed = new SqliteTestDbContext(_dbOptions);
            seed.Database.EnsureCreated();
            seed.Orders.Add(new IdentityKeyedOrder { CustomerName = "Beta", Total = 120.50m });
            seed.Orders.Add(new IdentityKeyedOrder { CustomerName = "Leon Viewer", Total = 10m });
            seed.SaveChanges();

            _store.SavedTransactions.Clear();
        }

        [TestCleanup]
        public void Cleanup() => _connection.Dispose();

        [TestMethod]
        public async Task ReadShapes_AreRecorded_InAFormatThatIdentifiesEntityRowAndColumns()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);

            await RunAsync("ToListAsync (all rows)",
                () => ctx.Orders.AsNoTracking().ToListAsync());

            await RunAsync("FirstOrDefaultAsync by key (inline constant)",
                () => ctx.Orders.AsNoTracking().FirstOrDefaultAsync(order => order.Id == 1));

            var wantedId = 1;
            await RunAsync("FirstOrDefaultAsync by key (parameterised)",
                () => ctx.Orders.AsNoTracking().FirstOrDefaultAsync(order => order.Id == wantedId));

            await RunAsync("FindAsync by key",
                async () => await ctx.Orders.FindAsync(2));

            await RunAsync("Where + projection",
                () => ctx.Orders.AsNoTracking()
                    .Where(order => order.Total > 50m)
                    .Select(order => new { order.CustomerName })
                    .ToListAsync());

            await RunAsync("CountAsync (aggregate)",
                () => ctx.Orders.AsNoTracking().CountAsync());

            await RunAsync("Join across two tables",
                () => ctx.OrderLines.AsNoTracking().Include(line => line.Order).ToListAsync());

            Assert.IsTrue(_store.SavedTransactions.Count > 0, "No Read audit transaction was recorded at all.");
        }

        [TestMethod]
        public void KeyLookups_ShowTheSqlThatEntityIdExtractionHasToMatch()
        {
            using var ctx = new SqliteTestDbContext(_dbOptions);

            var wantedId = 1;

            TestContext.WriteLine("===== inline constant =====");
            TestContext.WriteLine(ctx.Orders.AsNoTracking().Where(order => order.Id == 1).ToQueryString());

            TestContext.WriteLine("===== parameterised =====");
            TestContext.WriteLine(ctx.Orders.AsNoTracking().Where(order => order.Id == wantedId).ToQueryString());

            TestContext.WriteLine("===== string key predicate =====");
            TestContext.WriteLine(ctx.Orders.AsNoTracking()
                .Where(order => order.CustomerName == "Beta").ToQueryString());
        }

        private async Task RunAsync<T>(string label, System.Func<Task<T>> query)
        {
            var before = _store.SavedTransactions.Count;

            await query();

            TestContext.WriteLine($"===== {label} =====");

            var produced = _store.SavedTransactions.Skip(before).ToList();
            if (produced.Count == 0)
            {
                TestContext.WriteLine("  (no audit transaction recorded)");
                TestContext.WriteLine(string.Empty);

                return;
            }

            foreach (var transaction in produced)
                Dump(transaction);

            TestContext.WriteLine(string.Empty);
        }

        private void Dump(AuditTransaction transaction)
        {
            TestContext.WriteLine($"  Source={transaction.Source} UserId={transaction.UserId} " +
                                  $"Metadata=[{string.Join(", ", transaction.Metadata.Select(pair => $"{pair.Key}={pair.Value}"))}]");

            foreach (var entry in transaction.Entries)
            {
                TestContext.WriteLine(
                    $"  Action={entry.Action} EntityName='{entry.EntityName}' EntityId='{entry.EntityId}' " +
                    $"EntityTypeName='{entry.EntityTypeName}'");

                if (entry.Properties is null || entry.Properties.Count == 0)
                {
                    TestContext.WriteLine("      (no properties)");

                    continue;
                }

                foreach (var property in entry.Properties)
                {
                    TestContext.WriteLine(
                        $"      {property.PropertyName} ({property.PropertyType}) old='{property.OldValue}' new='{property.NewValue}'");
                }
            }
        }
    }
}
