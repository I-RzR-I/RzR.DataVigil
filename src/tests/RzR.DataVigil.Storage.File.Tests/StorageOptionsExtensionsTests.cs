using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Storage.File.Extensions;
using System.Linq;
using RzR.DataVigil.Core.Options;

namespace RzR.DataVigil.Storage.File.Tests
{
    [TestClass]
    public class StorageOptionsExtensionsTests
    {
        [TestMethod]
        public void UseFile_WithSpecifiedPath_SetsFilePath()
        {
            var options = new StorageOptions();

            options.UseFile("/tmp/audit-logs");

            Assert.AreEqual("/tmp/audit-logs", options.FilePath);
        }

        [TestMethod]
        public void UseFile_WithNullPath_SetsNullFilePath()
        {
            var options = new StorageOptions { FilePath = "existing-path" };

            options.UseFile(null);

            Assert.IsNull(options.FilePath);
        }

        [TestMethod]
        public void UseFile_ReturnsTheSameOptionsInstance()
        {
            var options = new StorageOptions();

            var returned = options.UseFile("/any/path");

            Assert.AreSame(options, returned);
        }

        [TestMethod]
        public void UseFile_WithEmptyString_SetsEmptyFilePath()
        {
            var options = new StorageOptions();

            options.UseFile(string.Empty);

            Assert.AreEqual(string.Empty, options.FilePath);
        }

        [TestMethod]
        public void AddAuditTrailFileStorage_RegistersFileAuditStoreAsSingleton()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new StorageOptions { FilePath = System.IO.Path.GetTempPath() });
            services.AddSingleton(new GdprPolicyRegistry());
            services.AddSingleton<GdprProcessor>();

            services.AddAuditTrailFileStorage();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAuditStore));
            Assert.IsNotNull(descriptor, "IAuditStore should be registered.");
            Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [TestMethod]
        public void AddAuditTrailFileStorage_ResolvedInstance_IsFileAuditStore()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new StorageOptions { FilePath = System.IO.Path.GetTempPath() });
            services.AddSingleton(new GdprPolicyRegistry());
            services.AddSingleton<GdprProcessor>();
            services.AddAuditTrailFileStorage();

            using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<IAuditStore>();

            Assert.IsInstanceOfType(store, typeof(FileAuditStore));
        }

        [TestMethod]
        public void AddAuditTrailFileStorage_ReturnsServiceCollectionForChaining()
        {
            var services = new ServiceCollection();

            var returned = services.AddAuditTrailFileStorage();

            Assert.AreSame(services, returned);
        }
    }
}
