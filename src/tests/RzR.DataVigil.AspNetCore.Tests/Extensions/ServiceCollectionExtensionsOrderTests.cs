using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.AspNetCore.Extensions;
using RzR.DataVigil.AspNetCore.Resolvers;
using RzR.DataVigil.Core.Extensions;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Resolvers;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.AspNetCore.Tests.Extensions
{
    internal sealed class CustomUserResolver : IAuditUserResolver
    {
        /// <inheritdoc/>
        public IResult<AuditUserInfo> Resolve() => Result<AuditUserInfo>.Success();
    }

    internal sealed class CustomCorrelationProvider : IAuditCorrelationProvider
    {
        /// <inheritdoc/>
        public IResult<string> GetCorrelationId() => Result<string>.Success(null);

        /// <inheritdoc/>
        public IResult<string> GetTraceId() => Result<string>.Success(null);
    }

    [TestClass]
    public class ServiceCollectionExtensionsOrderTests
    {
        private static IServiceCollection NewServices() => new ServiceCollection();

        [TestMethod]
        public void AspNetCoreAfterCore_NoCustom_ResolvesAspNetCoreUserResolver()
        {
            var services = NewServices();
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore();

            using var scope = services.BuildServiceProvider().CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IAuditUserResolver>();

            Assert.IsInstanceOfType(resolver, typeof(AspNetCoreUserResolver));
        }

        [TestMethod]
        public void AspNetCoreBeforeCore_NoCustom_ResolvesAspNetCoreUserResolver()
        {
            var services = NewServices();
            services.AddAuditTrailAspNetCore();
            services.AddAuditTrail(_ => { });

            using var scope = services.BuildServiceProvider().CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IAuditUserResolver>();

            Assert.IsInstanceOfType(resolver, typeof(AspNetCoreUserResolver));
        }

        [TestMethod]
        public void AspNetCoreAfterCore_CustomResolver_ResolvesCustom()
        {
            var services = NewServices();
            services.AddAuditTrail(o => o.UseUserResolver<CustomUserResolver>());
            services.AddAuditTrailAspNetCore();

            using var scope = services.BuildServiceProvider().CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IAuditUserResolver>();

            Assert.IsInstanceOfType(resolver, typeof(CustomUserResolver));
        }

        [TestMethod]
        public void AspNetCoreBeforeCore_CustomResolver_ResolvesCustom()
        {
            var services = NewServices();
            services.AddAuditTrailAspNetCore();
            services.AddAuditTrail(o => o.UseUserResolver<CustomUserResolver>());

            using var scope = services.BuildServiceProvider().CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IAuditUserResolver>();

            Assert.IsInstanceOfType(resolver, typeof(CustomUserResolver));
        }

        [TestMethod]
        public void CorrelationAspNetCoreAfterCore_NoCustom_ResolvesAspNetCoreCorrelationProvider()
        {
            var services = NewServices();
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore();

            using var scope = services.BuildServiceProvider().CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IAuditCorrelationProvider>();

            Assert.IsInstanceOfType(provider, typeof(AspNetCoreCorrelationProvider));
        }

        [TestMethod]
        public void CorrelationAspNetCoreBeforeCore_NoCustom_ResolvesAspNetCoreCorrelationProvider()
        {
            var services = NewServices();
            services.AddAuditTrailAspNetCore();
            services.AddAuditTrail(_ => { });

            using var scope = services.BuildServiceProvider().CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IAuditCorrelationProvider>();

            Assert.IsInstanceOfType(provider, typeof(AspNetCoreCorrelationProvider));
        }

        [TestMethod]
        public void CorrelationAspNetCoreAfterCore_HostRegistered_ResolvesHostProvider()
        {
            var services = NewServices();
            services.AddScoped<IAuditCorrelationProvider, CustomCorrelationProvider>();
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore();

            using var scope = services.BuildServiceProvider().CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IAuditCorrelationProvider>();

            Assert.IsInstanceOfType(provider, typeof(CustomCorrelationProvider));
        }

        [TestMethod]
        public void CorrelationAspNetCoreBeforeCore_HostRegistered_ResolvesHostProvider()
        {
            var services = NewServices();
            services.AddScoped<IAuditCorrelationProvider, CustomCorrelationProvider>();
            services.AddAuditTrailAspNetCore();
            services.AddAuditTrail(_ => { });

            using var scope = services.BuildServiceProvider().CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IAuditCorrelationProvider>();

            Assert.IsInstanceOfType(provider, typeof(CustomCorrelationProvider));
        }

        [TestMethod]
        public void CoreOnly_ResolvesDefaultUserResolver()
        {
            var services = NewServices();
            services.AddAuditTrail(_ => { });

            using var scope = services.BuildServiceProvider().CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IAuditUserResolver>();

            Assert.IsInstanceOfType(resolver, typeof(DefaultUserResolver));
        }

        [TestMethod]
        public void AddAuditTrailAspNetCore_CalledTwice_RegistersOneHostedService()
        {
            var services = NewServices();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore();
            services.AddAuditTrailAspNetCore();

            var provider = services.BuildServiceProvider();
            var hostedServices = provider.GetServices<IHostedService>()
                .Where(s => s.GetType().Name == "AuditIdentityResolutionDiagnostic")
                .ToList();

            Assert.AreEqual(1, hostedServices.Count);
        }

        [TestMethod]
        public void HostRegisteredResolverBeforeBoth_IsNotDisplaced()
        {
            var services = NewServices();
            services.AddScoped<IAuditUserResolver, CustomUserResolver>();
            services.AddAuditTrail(_ => { });
            services.AddAuditTrailAspNetCore();

            using var scope = services.BuildServiceProvider().CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IAuditUserResolver>();

            Assert.IsInstanceOfType(resolver, typeof(CustomUserResolver));
        }

        [TestMethod]
        public void UserResolver_IsScoped()
        {
            var services = NewServices();
            services.AddAuditTrailAspNetCore();
            services.AddAuditTrail(_ => { });

            var descriptor = services.Last(d => d.ServiceType == typeof(IAuditUserResolver));

            Assert.AreEqual(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [TestMethod]
        public void SourceResolver_Default_IsSingleton()
        {
            var services = NewServices();
            services.AddAuditTrail(_ => { });

            var descriptor = services.Last(d => d.ServiceType == typeof(IAuditSourceResolver));

            Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [TestMethod]
        public void SourceResolver_ConfiguredCustom_DefaultsToScoped_PreservingExistingBehaviour()
        {
            var services = NewServices();
            services.AddAuditTrail(o => o.UseSourceResolver<CustomSourceResolver>());

            var descriptor = services.Last(d => d.ServiceType == typeof(IAuditSourceResolver));

            Assert.AreEqual(typeof(CustomSourceResolver), descriptor.ImplementationType);
            Assert.AreEqual(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [TestMethod]
        public void SourceResolver_ConfiguredCustom_ExplicitSingleton_IsRegisteredAsSingleton()
        {
            var services = NewServices();
            services.AddAuditTrail(
                o => o.UseSourceResolver<CustomSourceResolver>(ServiceLifetime.Singleton));

            var descriptor = services.Last(d => d.ServiceType == typeof(IAuditSourceResolver));

            Assert.AreEqual(typeof(CustomSourceResolver), descriptor.ImplementationType);
            Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [TestMethod]
        public void SourceResolver_ConfiguredCustom_ExplicitSingleton_ResolvesFromRootProvider_WithScopeValidation()
        {
            // The point of the opt-in: a singleton consumer can resolve IAuditSourceResolver without
            // tripping ServiceProviderOptions.ValidateScopes.
            var services = NewServices();
            services.AddAuditTrail(
                o => o.UseSourceResolver<CustomSourceResolver>(ServiceLifetime.Singleton));

            var provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true });

            var resolved = provider.GetRequiredService<IAuditSourceResolver>();

            Assert.IsInstanceOfType(resolved, typeof(CustomSourceResolver));
        }

        private sealed class CustomSourceResolver : IAuditSourceResolver
        {
            public IResult<string> Resolve() => Result<string>.Success("custom-source");
        }
    }
}
