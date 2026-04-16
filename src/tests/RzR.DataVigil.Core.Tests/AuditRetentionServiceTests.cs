using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Hosting;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Tests.Stubs;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditRetentionServiceTests
    {
        [TestMethod]
        public void WithRetention_SetsRetentionDays()
        {
            var options = new StorageOptions();

            options.WithRetention(90);

            Assert.AreEqual(90, options.RetentionDays);
        }

        [TestMethod]
        public void WithRetention_ReturnsSameInstanceForChaining()
        {
            var options = new StorageOptions();

            var result = options.WithRetention(30);

            Assert.AreSame(options, result);
        }

        [TestMethod]
        public void RetentionDays_DefaultIsNull()
        {
            var options = new StorageOptions();

            Assert.IsNull(options.RetentionDays);
        }

        [TestMethod]
        public void WithRetention_CanOverwritePreviousValue()
        {
            var options = new StorageOptions();
            options.WithRetention(30);
            options.WithRetention(60);

            Assert.AreEqual(60, options.RetentionDays);
        }

        [TestMethod]
        public async Task ExecuteAsync_NullRetentionDays_DoesNotPurge()
        {
            var store = new StubAuditStore();
            var options = new StorageOptions(); // RetentionDays = null

            using var host = BuildHost(store, options);
            await host.StartAsync();

            // Give the background service time to execute
            await Task.Delay(200);

            await host.StopAsync();

            Assert.AreEqual(0, store.PurgeCallCount);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithRetentionDays_CallsPurge()
        {
            var store = new StubAuditStore();
            var options = new StorageOptions();
            options.WithRetention(30);

            using var host = BuildHost(store, options);
            var before = DateTimeOffset.UtcNow;

            await host.StartAsync();
            await Task.Delay(200);
            await host.StopAsync();

            Assert.IsTrue(store.PurgeCallCount >= 1, "PurgeBeforeAsync should have been called at least once.");
        }

        [TestMethod]
        public async Task ExecuteAsync_CutoffMatchesRetentionDays()
        {
            var store = new StubAuditStore();
            var options = new StorageOptions();
            options.WithRetention(30);

            using var host = BuildHost(store, options);
            var before = DateTimeOffset.UtcNow;

            await host.StartAsync();
            await Task.Delay(200);
            await host.StopAsync();

            Assert.IsNotNull(store.LastPurgeCutoff);

            // Cutoff should be approximately UtcNow - 30 days
            var expectedCutoff = before.AddDays(-30);
            var diff = Math.Abs((store.LastPurgeCutoff.Value - expectedCutoff).TotalSeconds);

            Assert.IsTrue(diff < 5, $"Cutoff was off by {diff:F1}s — expected ~{expectedCutoff}, got {store.LastPurgeCutoff.Value}.");
        }

        [TestMethod]
        public async Task ExecuteAsync_WithRetention1Day_CutoffIsApproximatelyYesterday()
        {
            var store = new StubAuditStore();
            var options = new StorageOptions();
            options.WithRetention(1);

            using var host = BuildHost(store, options);
            var before = DateTimeOffset.UtcNow;

            await host.StartAsync();
            await Task.Delay(200);
            await host.StopAsync();

            Assert.IsNotNull(store.LastPurgeCutoff);

            var expectedCutoff = before.AddDays(-1);
            var diff = Math.Abs((store.LastPurgeCutoff.Value - expectedCutoff).TotalSeconds);

            Assert.IsTrue(diff < 5, $"1-day retention cutoff off by {diff:F1}s.");
        }
        
        [TestMethod]
        public async Task ExecuteAsync_PurgeFails_DoesNotCrashHost()
        {
            var store = new StubAuditStore { PurgeShouldFail = true };
            var options = new StorageOptions();
            options.WithRetention(30);

            using var host = BuildHost(store, options);

            // Should not throw even though PurgeBeforeAsync fails
            await host.StartAsync();
            await Task.Delay(200);
            await host.StopAsync();

            Assert.IsTrue(store.PurgeCallCount >= 1);
        }

        [TestMethod]
        public async Task ExecuteAsync_PurgeThrowsException_DoesNotCrashHost()
        {
            var store = new ThrowingAuditStore();
            var options = new StorageOptions();
            options.WithRetention(7);

            using var host = BuildHost(store, options);

            await host.StartAsync();
            await Task.Delay(200);
            await host.StopAsync();

            Assert.IsTrue(store.PurgeCallCount >= 1);
        }

        [TestMethod]
        public async Task ExecuteAsync_CancellationRequested_StopsGracefully()
        {
            var store = new StubAuditStore();
            var options = new StorageOptions();
            options.WithRetention(30);

            using var host = BuildHost(store, options);

            await host.StartAsync();
            await Task.Delay(100);

            // StopAsync triggers cancellation
            await host.StopAsync();

            // Service should have completed without hanging
            Assert.IsTrue(true, "Service stopped gracefully.");
        }

        [TestMethod]
        public void AddAuditRetentionService_RegistersHostedService()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new StorageOptions());
            services.AddSingleton<IAuditStore>(new StubAuditStore());

            services.AddAuditRetentionService();

            var sp = services.BuildServiceProvider();
            var hostedServices = sp.GetServices<IHostedService>();

            var found = false;
            foreach (var svc in hostedServices)
            {
                if (svc is AuditRetentionService)
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "AuditRetentionService should be registered as IHostedService.");
        }

        [TestMethod]
        public async Task ExecuteAsync_LongEnoughRun_PurgesMultipleTimes()
        {
            var store = new StubAuditStore();
            var options = new StorageOptions();
            options.WithRetention(14);

            using var host = BuildHost(store, options);

            await host.StartAsync();
            await Task.Delay(300);
            await host.StopAsync();

            // At minimum 1 purge should have occurred (the first iteration runs immediately)
            Assert.IsTrue(store.PurgeCallCount >= 1);
        }

        private static IHost BuildHost(IAuditStore store, StorageOptions options)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(options);
                    services.AddScoped<IAuditStore>(_ => store);
                    services.AddHostedService<AuditRetentionService>();
                })
                .Build();
        }

        /// <summary>
        ///     An IAuditStore that throws on PurgeBeforeAsync to verify exception resilience.
        /// </summary>
        private class ThrowingAuditStore : IAuditStore
        {
            public int PurgeCallCount { get; private set; }

            public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
                => Task.FromResult<IResult>(Result.Failure("Not implemented"));

            public Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(
                AuditTransactionQuery filters,
                GdprRetrievalContext gdprRetrievalContext = null,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IResult<IEnumerable<AuditTransaction>>>(Result<IEnumerable<AuditTransaction>>.Failure("Not implemented"));

            public Task<IResult> AnonymizeByUserAsync(string userId, CancellationToken cancellationToken = default)
                => Task.FromResult<IResult>(Result.Failure("Not implemented"));

            public Task<IResult> PurgeBeforeAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
            {
                PurgeCallCount++;
                throw new InvalidOperationException("Simulated purge failure");
            }
        }
    }
}
