#region U S A G E S

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.TestSupport
{
    public abstract class AuditStorePagingContract
    {
        protected abstract Task SeedAsync(int count);

        protected abstract Task<IResult<IEnumerable<AuditTransaction>>> ExecuteQueryAsync(
            AuditTransactionQuery query);

        [TestMethod]
        public async Task QueryAsync_WithTakeOfZero_ReturnsAnEmptySuccessfulResultThoughRecordsExist()
        {
            await SeedAsync(3);

            var result = await ExecuteQueryAsync(new AuditTransactionQuery { Take = 0 });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithNegativeTake_FallsBackToTheDefaultPageSize()
        {
            await SeedAsync(12);

            var result = await ExecuteQueryAsync(new AuditTransactionQuery { Take = -1 });

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(10, result.Response.Count());
        }

        [TestMethod]
        public async Task QueryAsync_WithNegativeSkip_ReturnsTheFirstPage()
        {
            await SeedAsync(12);

            var result = await ExecuteQueryAsync(new AuditTransactionQuery { Skip = -5, Take = 10 });

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(
                new[] { "u11", "u10", "u9", "u8", "u7", "u6", "u5", "u4", "u3", "u2" },
                result.Response.Select(x => x.UserId).ToArray());
        }

        [TestMethod]
        public async Task QueryAsync_WithADefaultQuery_StillReturnsTheSameTenNewestRecords()
        {
            await SeedAsync(12);

            var result = await ExecuteQueryAsync(new AuditTransactionQuery());

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(
                new[] { "u11", "u10", "u9", "u8", "u7", "u6", "u5", "u4", "u3", "u2" },
                result.Response.Select(x => x.UserId).ToArray());
        }
    }
}
