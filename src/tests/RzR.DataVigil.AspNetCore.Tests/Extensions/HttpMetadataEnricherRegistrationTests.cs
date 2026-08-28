using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.AspNetCore.Enrichers;
using RzR.DataVigil.AspNetCore.Extensions;
using RzR.DataVigil.AspNetCore.Tests.Stubs;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Pipeline;
using static RzR.DataVigil.AspNetCore.Tests.Stubs.HttpEnricherTestData;

namespace RzR.DataVigil.AspNetCore.Tests.Extensions
{
    [TestClass]
    public class HttpMetadataEnricherRegistrationTests
    {
        [TestMethod]
        public async Task ContainerResolvedPipeline_RunsTheRegisteredEnricher_StampingMethodAndRoute()
        {
            var accessor = new StubHttpContextAccessor
            {
                HttpContext = ContextWithMethod("POST")
            };
            var store = new StubAuditStore();

            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddSingleton<IAuditStore>(store);
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore(_ => "/orders/{id}");

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var pipeline = scope.ServiceProvider.GetRequiredService<AuditPipeline>();
            var transaction = BuildTransaction();

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, store.SaveCallCount);
            Assert.IsTrue(
                transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpMethod));
            Assert.AreEqual("POST", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
            Assert.AreEqual("/orders/{id}", transaction.Metadata[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public void AuditPipeline_ConstructorsFormAStrictPrefixExtensionChain()
        {
            var arities = typeof(AuditPipeline)
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Select(c => c.GetParameters().Length)
                .OrderBy(n => n)
                .ToArray();

            CollectionAssert.AreEqual(new[] { 5, 6, 7 }, arities);
        }

        [TestMethod]
        public async Task ContainerResolvedPipeline_RouteAccessorThrows_StampsTheMethodAndStillPersists()
        {
            var accessor = new StubHttpContextAccessor
            {
                HttpContext = ContextWithMethod("PATCH")
            };
            var store = new StubAuditStore();

            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddSingleton<IAuditStore>(store);
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore(_ => throw new InvalidOperationException("routing blew up"));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var transaction = BuildTransaction();
            var result = await scope.ServiceProvider.GetRequiredService<AuditPipeline>()
                .ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, store.SaveCallCount);
            Assert.AreEqual("PATCH", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [TestMethod]
        public async Task WithoutAddAuditTrailAspNetCore_NoEnricherResolves_AndNoHttpKeysAreStamped()
        {
            var store = new StubAuditStore();

            var services = new ServiceCollection();
            services.AddSingleton<IAuditStore>(store);
            services.AddAuditTrail(_ => { });

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var enrichers = scope.ServiceProvider.GetServices<IAuditMetadataEnricher>().ToList();
            Assert.AreEqual(0, enrichers.Count);

            var pipeline = scope.ServiceProvider.GetRequiredService<AuditPipeline>();
            var transaction = BuildTransaction();

            var result = await pipeline.ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, store.SaveCallCount);
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpMethod));
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [TestMethod]
        public void AspNetCoreRegisteredWithoutAddAuditTrail_EnricherStillResolvesAndRuns()
        {
            var accessor = new StubHttpContextAccessor
            {
                HttpContext = ContextWithMethod("GET")
            };

            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddAuditTrailAspNetCore(_ => "/ping");

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var enricher = scope.ServiceProvider.GetRequiredService<IAuditMetadataEnricher>();
            var result = enricher.Enrich();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Response.Count());
        }

        [TestMethod]
        public async Task ContainerResolvedPipeline_ScopeUserSetOnTheContainersScopeContext_StampsNoHttpKeys()
        {
            var accessor = new StubHttpContextAccessor
            {
                HttpContext = ContextWithMethod("POST")
            };
            var store = new StubAuditStore();

            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddSingleton<IAuditStore>(store);
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore(_ => "/orders/{id}");

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<IAuditScopeContext>()
                .SetUser(new AuditUserInfo
                {
                    UserId = "nightly-job", 
                    UserName = "Nightly job"
                });

            var transaction = BuildTransaction();
            var result = await scope.ServiceProvider.GetRequiredService<AuditPipeline>()
                .ProcessAsync(transaction);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, store.SaveCallCount);
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpMethod));
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpRoute));
            Assert.AreEqual("nightly-job", transaction.UserId);
        }

        [TestMethod]
        public void AddAuditTrailAspNetCore_CalledTwice_RegistersExactlyOneMetadataEnricher()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(new StubHttpContextAccessor());
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore();
            services.AddAuditTrailAspNetCore();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var enrichers = scope.ServiceProvider.GetServices<IAuditMetadataEnricher>().ToList();

            Assert.AreEqual(1, enrichers.Count);
            Assert.IsInstanceOfType(enrichers[0], typeof(HttpOperationMetadataEnricher));
        }

        [TestMethod]
        public void AddAuditTrailAspNetCore_EnricherIsRegisteredAsScoped()
        {
            var services = new ServiceCollection();
            services.AddAuditTrailAspNetCore();

            var descriptor = services.Single(d => d.ServiceType == typeof(IAuditMetadataEnricher));

            Assert.AreEqual(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [TestMethod]
        public void AddAuditTrailAspNetCore_ContainerBuildsAndPassesScopeValidation()
        {
            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<IHttpContextAccessor>(new StubHttpContextAccessor());
            services.AddSingleton<IAuditStore>(new StubAuditStore());
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore(_ => "/orders/{id}");

            using var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            Assert.IsNotNull(provider);
        }

        [TestMethod]
        public async Task RouteOverloadCalledBeforeParameterless_KeepsTheRouteAccessor()
        {
            var transaction = await RunPipelineAsync(
                services =>
                {
                    services.AddAuditTrailAspNetCore(_ => "/orders/{id}");
                    services.AddAuditTrailAspNetCore();
                });

            Assert.AreEqual("/orders/{id}", transaction.Metadata[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public async Task ParameterlessCalledBeforeRouteOverload_StillStampsTheRoute()
        {
            var transaction = await RunPipelineAsync(
                services =>
                {
                    services.AddAuditTrailAspNetCore();
                    services.AddAuditTrailAspNetCore(_ => "/orders/{id}");
                });

            Assert.AreEqual("POST", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
            Assert.AreEqual("/orders/{id}", transaction.Metadata[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public async Task BothOverloadsPassAccessors_LastNonNullWins()
        {
            var transaction = await RunPipelineAsync(
                services =>
                {
                    services.AddAuditTrailAspNetCore(_ => "/a");
                    services.AddAuditTrailAspNetCore(_ => "/b");
                });

            Assert.AreEqual("/b", transaction.Metadata[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public async Task ParameterlessOnly_StampsMethodButNoRouteKey()
        {
            var transaction = await RunPipelineAsync(
                services => services.AddAuditTrailAspNetCore());

            Assert.AreEqual("POST", transaction.Metadata[AuditMetadataKeys.HttpMethod]);
            Assert.IsFalse(transaction.Metadata.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [TestMethod]
        public void AddAuditTrailAspNetCore_CalledThreeTimes_RegistersTheRouteHolderExactlyOnce()
        {
            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<IHttpContextAccessor>(new StubHttpContextAccessor());
            services.AddSingleton<IAuditStore>(new StubAuditStore());
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore(_ => "/a");
            services.AddAuditTrailAspNetCore();
            services.AddAuditTrailAspNetCore(_ => "/b");

            var holderRegistrations = services.Count(
                d => d.ServiceType.Assembly == typeof(HttpOperationMetadataEnricher).Assembly
                     && !d.ServiceType.IsPublic);

            Assert.AreEqual(1, holderRegistrations);
            Assert.AreEqual(
                1, services.Count(d => d.ServiceType == typeof(IAuditMetadataEnricher)));

            using var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            Assert.IsNotNull(provider);
        }

        private static async Task<AuditTransaction> RunPipelineAsync(
            Action<IServiceCollection> register)
        {
            var accessor = new StubHttpContextAccessor
            {
                HttpContext = ContextWithMethod("POST")
            };

            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddSingleton<IAuditStore>(new StubAuditStore());
            services.AddAuditTrail(_ => { });
            register(services);

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var pipeline = scope.ServiceProvider.GetRequiredService<AuditPipeline>();
            var transaction = BuildTransaction();

            var result = await pipeline.ProcessAsync(transaction);
            Assert.IsTrue(result.IsSuccess);

            return transaction;
        }
    }
}
