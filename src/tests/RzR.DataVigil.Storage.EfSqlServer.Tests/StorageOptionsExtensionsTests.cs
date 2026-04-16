using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Storage.EfSqlServer.Extensions;

namespace RzR.DataVigil.Storage.EfSqlServer.Tests
{
    [TestClass]
    public class StorageOptionsExtensionsTests
    {
        [TestMethod]
        public void UseSqlServer_SetsConnectionString()
        {
            var options = new StorageOptions();
            const string connStr = "Server=localhost;Database=AuditDb;";

            options.UseSqlServer(connStr);

            Assert.AreEqual(connStr, options.ConnectionString);
        }

        [TestMethod]
        public void UseSqlServer_ReturnsTheSameOptionsInstance()
        {
            var options = new StorageOptions();

            var returned = options.UseSqlServer("Server=localhost;");

            Assert.AreSame(options, returned);
        }

        [TestMethod]
        public void UseSqlServer_WithEmptyString_SetsEmptyConnectionString()
        {
            var options = new StorageOptions { ConnectionString = "existing" };

            options.UseSqlServer(string.Empty);

            Assert.AreEqual(string.Empty, options.ConnectionString);
        }

        [TestMethod]
        public void UseSqlServer_WithNull_SetsNullConnectionString()
        {
            var options = new StorageOptions { ConnectionString = "existing" };

            options.UseSqlServer(null);

            Assert.IsNull(options.ConnectionString);
        }

        [TestMethod]
        public void UseSqlServer_DoesNotAffectOtherOptions()
        {
            var options = new StorageOptions { Schema = "custom", FilePath = "/some/path" };

            options.UseSqlServer("Server=localhost;");

            Assert.AreEqual("custom", options.Schema);
            Assert.AreEqual("/some/path", options.FilePath);
        }

        [TestMethod]
        public void AddAuditTrailSqlServer_RegistersIAuditStoreAsScoped()
        {
            var services = new ServiceCollection();

            services.AddAuditTrailSqlServer();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
            Assert.IsNotNull(descriptor, "IAuditStore should be registered.");
            Assert.AreEqual(ServiceLifetime.Scoped, descriptor.Lifetime);
        }

        [TestMethod]
        public void AddAuditTrailSqlServer_RegistersAuditSqlServerDbContext()
        {
            var services = new ServiceCollection();

            services.AddAuditTrailSqlServer();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AuditSqlServerDbContext));
            Assert.IsNotNull(descriptor, "AuditSqlServerDbContext should be registered.");
        }

        [TestMethod]
        public void AddAuditTrailSqlServer_ReturnsServiceCollectionForChaining()
        {
            var services = new ServiceCollection();

            var returned = services.AddAuditTrailSqlServer();

            Assert.AreSame(services, returned);
        }

        [TestMethod]
        public void AddAuditTrailSqlServer_WhenConnectionStringIsEmpty_ThrowsOnDbContextResolve()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new StorageOptions { ConnectionString = string.Empty });
            services.AddAuditTrailSqlServer();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            Assert.ThrowsException<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<AuditSqlServerDbContext>());
        }

        [TestMethod]
        public void AddAuditTrailSqlServer_WhenConnectionStringIsWhitespace_ThrowsOnDbContextResolve()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new StorageOptions { ConnectionString = "   " });
            services.AddAuditTrailSqlServer();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            Assert.ThrowsException<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<AuditSqlServerDbContext>());
        }
    }
}
