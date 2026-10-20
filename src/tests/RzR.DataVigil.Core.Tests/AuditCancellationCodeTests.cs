using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Extensions;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Core.Tests.Resolvers;
using RzR.DataVigil.Core.Tests.Stubs;
using RzR.ResultMessage;
using RzR.ResultMessage.Models;
using static RzR.DataVigil.TestSupport.AuditTestDataBuilder;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class AuditCancellationCodeTests
    {
        private const string OtherCode = "DATAVIGIL.OTHER";

        [TestMethod]
        public async Task ProcessAsync_CallersTokenCanceled_ExactlyOneMessageCarriesTheCancelCode()
        {
            var pipeline = new AuditPipeline(
                new StubUserResolver
                {
                    UserToReturn = new AuditUserInfo
                    {
                        UserId = "user-1",
                        UserName = "Alice",
                        Source = AuditUserSource.HttpContext
                    }
                },
                new StubSourceResolver { SourceToReturn = "WebApi" },
                new StubCorrelationProvider { CorrelationId = "c-1", TraceId = "t-1" },
                new GdprProcessor(new GdprPolicyRegistry()),
                new CancelingAuditStore());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await pipeline.ProcessAsync(BuildTransaction(BuildPipelineEntry()), cts.Token);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Messages);
            Assert.AreEqual(1,
                result.Messages.Count(m => m != null && m.Code == AuditResultCodes.OperationCanceled),
                "The producer must put the cancel code on Code exactly once; the attached exception "
                + "message carries no code.");
        }

        [TestMethod]
        public void IsAuditCanceled_MessagesEmpty_IsFalse()
        {
            var result = Result.Failure(OtherCode, "x");
            result.Messages.Clear();
            Assert.AreEqual(0, result.Messages.Count, "Precondition: the collection is empty.");

            var canceled = result.IsAuditCanceled();

            Assert.IsFalse(canceled);
        }

        [TestMethod]
        public void IsAuditCanceled_MessagesNull_IsFalse()
        {
            var result = Result.Success();
            result.Messages = null;
            Assert.IsNull(result.Messages, "Precondition: the collection itself is null.");

            var canceled = result.IsAuditCanceled();

            Assert.IsFalse(canceled);
        }

        [TestMethod]
        public void IsAuditCanceled_OnlyANullElement_IsFalse()
        {
            var result = Result.Success();
            result.Messages.Add(null);
            Assert.AreEqual(1, result.Messages.Count, "Precondition: the single element is null.");

            var canceled = result.IsAuditCanceled();

            Assert.IsFalse(canceled);
        }

        [TestMethod]
        public void IsAuditCanceled_NullElementBeforeTheCancelMessage_IsTrue()
        {
            var result = Result.Success();
            result.Messages.Add(null);
            result.Messages.Add(new MessageModel(AuditResultCodes.OperationCanceled, "c"));

            var canceled = result.IsAuditCanceled();

            Assert.IsTrue(canceled, "A null element must be skipped, not end the scan.");
        }

        [TestMethod]
        public void IsAuditCanceled_CancelMessageIsNotFirst_IsTrue()
        {
            var result = Result.Failure(OtherCode, "x");
            result.Messages.Add(new MessageModel(AuditResultCodes.OperationCanceled, "c"));
            Assert.AreEqual(OtherCode, result.Messages.First().Code,
                "Precondition: the cancel message is not the first one.");

            var canceled = result.IsAuditCanceled();

            Assert.IsTrue(canceled);
        }

        [TestMethod]
        public void IsAuditCanceled_CodeDiffersOnlyInCase_IsFalse()
        {
            const string lowerCaseCode = "datavigil.audit.canceled";
            var result = Result.Failure(lowerCaseCode, "c");
            Assert.AreEqual(lowerCaseCode, result.Messages.Single().Code,
                "Precondition: the message carries the case variant on Code.");

            var canceled = result.IsAuditCanceled();

            Assert.IsFalse(canceled, "The comparison is ordinal, so case matters.");
        }

        [TestMethod]
        public void IsAuditCanceled_CodeHasSurroundingWhitespace_IsFalse()
        {
            const string paddedCode = " DATAVIGIL.AUDIT.CANCELED ";
            var result = Result.Failure(paddedCode, "c");
            Assert.AreEqual(paddedCode, result.Messages.Single().Code,
                "Precondition: the message carries the padded variant on Code.");

            var canceled = result.IsAuditCanceled();

            Assert.IsFalse(canceled, "The comparison is ordinal and does not trim.");
        }

        [TestMethod]
        public void IsAuditCanceled_GenericFailureWithCancelCode_IsTrue()
        {
            var result = Result<string>.Failure(AuditResultCodes.OperationCanceled, "c");

            var canceled = result.IsAuditCanceled();

            Assert.IsTrue(canceled);
        }

        [TestMethod]
        public void IsAuditCanceled_MessageBuiltWithNamedCodeArgument_IsTrue()
        {
            var result = Result.Success();
            result.Messages.Add(new MessageModel(code: AuditResultCodes.OperationCanceled, message: "c"));

            var canceled = result.IsAuditCanceled();

            Assert.IsTrue(canceled);
        }
    }
}
