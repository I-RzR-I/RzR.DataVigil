using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Tests.Models;
using RzR.DataVigil.Core.Tests.Resolvers;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditTrailOptionsTests
    {
        [TestMethod]
        public void Exclude_AddsTypeToGlobalExclusions()
        {
            var options = new AuditTrailOptions();

            options.Exclude<DummyEntity>();

            Assert.IsTrue(options.GlobalExclusions.Contains(typeof(DummyEntity)));
        }

        [TestMethod]
        public void Exclude_Fluent_ReturnsSameInstance()
        {
            var options = new AuditTrailOptions();

            var result = options.Exclude<DummyEntity>();

            Assert.AreSame(options, result);
        }

        [TestMethod]
        public void Exclude_MultipleTypes_AllPresent()
        {
            var options = new AuditTrailOptions();

            options.Exclude<DummyEntity>().Exclude<AnotherEntity>();

            Assert.IsTrue(options.GlobalExclusions.Contains(typeof(DummyEntity)));
            Assert.IsTrue(options.GlobalExclusions.Contains(typeof(AnotherEntity)));
            Assert.AreEqual(2, options.GlobalExclusions.Count);
        }

        [TestMethod]
        public void Exclude_DuplicateType_DoesNotDuplicate()
        {
            var options = new AuditTrailOptions();

            options.Exclude<DummyEntity>().Exclude<DummyEntity>();

            Assert.AreEqual(1, options.GlobalExclusions.Count);
        }

        [TestMethod]
        public void UseUserResolver_SetsUserResolverType()
        {
            var options = new AuditTrailOptions();

            var result = options.UseUserResolver<CustomUserResolver>();

            Assert.AreSame(options, result);
            var field = typeof(AuditTrailOptions).GetProperty("UserResolverType",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(CustomUserResolver), field.GetValue(options));
        }

        [TestMethod]
        public void UseSourceResolver_SetsSourceResolverType()
        {
            var options = new AuditTrailOptions();

            options.UseSourceResolver<CustomSourceResolver>();

            var field = typeof(AuditTrailOptions).GetProperty("SourceResolverType",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(CustomSourceResolver), field.GetValue(options));
        }

        [TestMethod]
        public void DefaultOptions_HaveEmptyGlobalExclusions()
        {
            var options = new AuditTrailOptions();

            Assert.AreEqual(0, options.GlobalExclusions.Count);
        }

        [TestMethod]
        public void DefaultOptions_SubOptionsAreNotNull()
        {
            var options = new AuditTrailOptions();

            Assert.IsNotNull(options.EfCore);
            Assert.IsNotNull(options.Storage);
            Assert.IsNotNull(options.Gdpr);
        }
    }
}
