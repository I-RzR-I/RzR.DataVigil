using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Core.Resolvers;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class DefaultCorrelationProviderTests
    {
        [TestMethod]
        public void GetCorrelationId_NoActivity_ReturnsNull()
        {
            Activity.Current = null;
            var provider = new DefaultCorrelationProvider();

            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNull(result.Response);
        }

        [TestMethod]
        public void GetTraceId_NoActivity_ReturnsDefault()
        {
            Activity.Current = null;
            var provider = new DefaultCorrelationProvider();

            var result = provider.GetTraceId();

            Assert.IsTrue(result.IsSuccess);
        }

        [TestMethod]
        public void GetCorrelationId_WithActivity_ReturnsActivityId()
        {
            var source = new ActivitySource("TestSource");
            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            using var activity = source.StartActivity("Test");
            Assert.IsNotNull(Activity.Current, "Activity.Current should be set");

            var provider = new DefaultCorrelationProvider();
            var result = provider.GetCorrelationId();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
            Assert.AreEqual(Activity.Current.Id, result.Response);
        }

        [TestMethod]
        public void GetTraceId_WithActivity_ReturnsTraceId()
        {
            var source = new ActivitySource("TestSource2");
            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);

            using var activity = source.StartActivity("Test2");
            Assert.IsNotNull(Activity.Current);

            var provider = new DefaultCorrelationProvider();
            var result = provider.GetTraceId();

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);
        }
    }
}
