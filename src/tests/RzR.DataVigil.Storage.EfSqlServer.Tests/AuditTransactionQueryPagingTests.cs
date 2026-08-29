using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Extensions;
using RzR.DataVigil.Abstractions.Models.Query;

namespace RzR.DataVigil.Storage.EfSqlServer.Tests
{
    [TestClass]
    public class AuditTransactionQueryPagingTests
    {
        [TestMethod]
        public void GetEffectivePaging_WithADefaultQuery_LeavesTheRequestedPagingUntouched()
        {
            new AuditTransactionQuery().GetEffectivePaging(out var skip, out var take,
                out var pagingWasNormalized, out var takeWasCapped);

            Assert.AreEqual(0, skip);
            Assert.AreEqual(AuditQueryLimits.DefaultTake, take);
            Assert.IsFalse(pagingWasNormalized);
            Assert.IsFalse(takeWasCapped);
        }

        [TestMethod]
        public void GetEffectivePaging_WithTakeOneBelowTheMaximum_IsNotCapped()
        {
            new AuditTransactionQuery { Take = AuditQueryLimits.MaxTake - 1 }.GetEffectivePaging(
                out _, out var take, out _, out var takeWasCapped);

            Assert.AreEqual(AuditQueryLimits.MaxTake - 1, take);
            Assert.IsFalse(takeWasCapped);
        }

        [TestMethod]
        public void GetEffectivePaging_WithTakeExactlyAtTheMaximum_IsNotCapped()
        {
            new AuditTransactionQuery { Take = AuditQueryLimits.MaxTake }.GetEffectivePaging(
                out _, out var take, out _, out var takeWasCapped);

            Assert.AreEqual(AuditQueryLimits.MaxTake, take);
            Assert.IsFalse(takeWasCapped);
        }

        [TestMethod]
        public void GetEffectivePaging_WithTakeOneAboveTheMaximum_IsCappedToTheMaximum()
        {
            new AuditTransactionQuery { Take = AuditQueryLimits.MaxTake + 1 }.GetEffectivePaging(
                out _, out var take, out var pagingWasNormalized, out var takeWasCapped);

            Assert.AreEqual(AuditQueryLimits.MaxTake, take);
            Assert.IsTrue(takeWasCapped);
            Assert.IsFalse(pagingWasNormalized);
        }

        [TestMethod]
        public void GetEffectivePaging_WithTakeOfZero_StaysZeroAndIsNotRaisedToTheDefault()
        {
            new AuditTransactionQuery { Take = 0 }.GetEffectivePaging(
                out _, out var take, out var pagingWasNormalized, out var takeWasCapped);

            Assert.AreEqual(0, take);
            Assert.IsFalse(pagingWasNormalized);
            Assert.IsFalse(takeWasCapped);
        }

        [TestMethod]
        public void GetEffectivePaging_WithTakeOfMinusOne_BecomesTheDefaultPageSize()
        {
            new AuditTransactionQuery { Take = -1 }.GetEffectivePaging(
                out _, out var take, out var pagingWasNormalized, out var takeWasCapped);

            Assert.AreEqual(AuditQueryLimits.DefaultTake, take);
            Assert.IsTrue(pagingWasNormalized);
            Assert.IsFalse(takeWasCapped);
        }

        [TestMethod]
        public void GetEffectivePaging_WithNegativeSkip_BecomesTheMinimumOffset()
        {
            new AuditTransactionQuery { Skip = -5 }.GetEffectivePaging(
                out var skip, out _, out var pagingWasNormalized, out _);

            Assert.AreEqual(AuditQueryLimits.MinSkip, skip);
            Assert.IsTrue(pagingWasNormalized);
        }

        [TestMethod]
        public void GetEffectivePaging_WithSkipOfZero_IsNotReportedAsNormalized()
        {
            new AuditTransactionQuery { Skip = 0 }.GetEffectivePaging(
                out var skip, out _, out var pagingWasNormalized, out _);

            Assert.AreEqual(0, skip);
            Assert.IsFalse(pagingWasNormalized);
        }

        [TestMethod]
        public void GetEffectivePaging_WithAPositiveSkip_IsPassedThroughUnchanged()
        {
            new AuditTransactionQuery { Skip = 25 }.GetEffectivePaging(
                out var skip, out _, out var pagingWasNormalized, out _);

            Assert.AreEqual(25, skip);
            Assert.IsFalse(pagingWasNormalized);
        }

        [TestMethod]
        public void GetEffectivePaging_WithNegativeSkipAndOverMaximumTake_ReportsBothOutcomes()
        {
            new AuditTransactionQuery { Skip = -5, Take = AuditQueryLimits.MaxTake + 1 }.GetEffectivePaging(
                out var skip, out var take, out var pagingWasNormalized, out var takeWasCapped);

            Assert.AreEqual(AuditQueryLimits.MinSkip, skip);
            Assert.AreEqual(AuditQueryLimits.MaxTake, take);
            Assert.IsTrue(pagingWasNormalized);
            Assert.IsTrue(takeWasCapped);
        }

        [TestMethod]
        public void GetEffectivePaging_DoesNotMutateTheRequestedPagingOnTheQuery()
        {
            var filters = new AuditTransactionQuery { Skip = -5, Take = AuditQueryLimits.MaxTake + 1 };

            filters.GetEffectivePaging(out _, out _, out _, out _);

            Assert.AreEqual(-5, filters.Skip);
            Assert.AreEqual(AuditQueryLimits.MaxTake + 1, filters.Take);
        }

        [TestMethod]
        public void GetEffectivePaging_WithANullQuery_ThrowsArgumentNullException()
        {
            AuditTransactionQuery filters = null;

            Assert.ThrowsException<ArgumentNullException>(() =>
                filters.GetEffectivePaging(out _, out _, out _, out _));
        }
    }
}
