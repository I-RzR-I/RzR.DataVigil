using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Extensions;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions.Models;
using RzR.ResultMessage.Enums;
using RzR.ResultMessage.Models;

namespace RzR.DataVigil.Core.Tests
{
    [TestClass]
    public class MessageCodeKeyDivergenceTests
    {
        private const string OtherCode = "DATAVIGIL.OTHER";

        [TestMethod]
        public void IsAuditCanceled_KeyIsCancelButCodeIsOther_IsFalse()
        {
            var result = ResultCarrying(new DivergentMessage
            {
                Key = AuditResultCodes.OperationCanceled,
                Code = OtherCode
            });

            var canceled = result.IsAuditCanceled();

            Assert.IsFalse(canceled, "Key is obsolete and must not be consulted; only Code classifies.");
        }

        [TestMethod]
        public void IsAuditCanceled_CodeIsCancelButKeyIsOther_IsTrue()
        {
            var result = ResultCarrying(new DivergentMessage
            {
                Code = AuditResultCodes.OperationCanceled,
                Key = OtherCode
            });

            var canceled = result.IsAuditCanceled();

            Assert.IsTrue(canceled, "Code is the v4 contract and must classify regardless of Key.");
        }

        [TestMethod]
        public void IsAuditCanceled_CodeIsCancelAndKeyIsNull_IsTrue()
        {
            var result = ResultCarrying(new DivergentMessage
            {
                Code = AuditResultCodes.OperationCanceled,
                Key = null
            });

            var canceled = result.IsAuditCanceled();

            Assert.IsTrue(canceled);
        }

        [TestMethod]
        public void IsAuditCanceled_KeyIsCancelAndCodeIsNull_IsFalse()
        {
            var result = ResultCarrying(new DivergentMessage
            {
                Key = AuditResultCodes.OperationCanceled,
                Code = null
            });

            var canceled = result.IsAuditCanceled();

            Assert.IsFalse(canceled, "There is deliberately no fallback to the obsolete Key when Code is null.");
        }

        private static Result<object> ResultCarrying(DivergentMessage message)
        {
            var result = Result.Success();
            result.Messages.Add(message);

            return result;
        }

        private sealed class DivergentMessage : IMessageModel
        {
            public string Code { get; set; }

            public string Key { get; set; }

            public MessageDataModel Message { get; set; } = new MessageDataModel();

            public MessageType MessageType { get; set; } = MessageType.Error;

            public string LogTraceId { get; set; }

            public List<RelatedObjectModel> RelatedObjects { get; set; } = new List<RelatedObjectModel>();

            public override string ToString()
                => Code + ": " + Message;
        }
    }
}
