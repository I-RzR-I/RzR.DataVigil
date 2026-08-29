#region U S A G E S

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.TestSupport
{
    public abstract class AuditStoreErasureClampContract
    {
        protected abstract Task<AuditTransaction> SaveEraseAndReadBackAsync(string storedUserId,
            string erasureRequestUserId);

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithAnIdLongerThanTheColumn_ErasesTheRecordStoredUnderTheTruncatedId()
        {
            var storedUserId = new string('a', AuditColumnLengths.UserId);
            var erasureRequestUserId = storedUserId + new string('b', 44);

            var stored = await SaveEraseAndReadBackAsync(storedUserId, erasureRequestUserId);

            Assert.AreEqual("[ERASED]", stored.UserId);
            Assert.AreEqual("[ERASED]", stored.UserName);
            Assert.AreEqual("[ERASED]", stored.IpAddress);
            Assert.AreEqual(GdprStorageState.Erased, stored.GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithAnIdCutMidSurrogatePair_ErasesTheRecordStoredUnderTheShorterId()
        {
            var storedUserId = new string('a', AuditColumnLengths.UserId - 1);
            var astralCharacter = char.ConvertFromUtf32(0x1F600);
            var erasureRequestUserId = storedUserId + astralCharacter + new string('b', 43);

            Assert.AreEqual(300, erasureRequestUserId.Length);
            Assert.IsTrue(char.IsHighSurrogate(erasureRequestUserId[AuditColumnLengths.UserId - 1]));

            var stored = await SaveEraseAndReadBackAsync(storedUserId, erasureRequestUserId);

            Assert.AreEqual("[ERASED]", stored.UserId);
            Assert.AreEqual(GdprStorageState.Erased, stored.GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithAnIdExactlyAtTheColumnLength_StillErasesTheRecord()
        {
            var userId = new string('a', AuditColumnLengths.UserId);

            var stored = await SaveEraseAndReadBackAsync(userId, userId);

            Assert.AreEqual("[ERASED]", stored.UserId);
            Assert.AreEqual(GdprStorageState.Erased, stored.GdprState);
        }

        [TestMethod]
        public async Task AnonymizeByUserAsync_WithAnOverLongIdThatDoesNotSharePrefix_LeavesTheRecordIntact()
        {
            var storedUserId = new string('a', AuditColumnLengths.UserId);
            var erasureRequestUserId = new string('c', 300);

            var stored = await SaveEraseAndReadBackAsync(storedUserId, erasureRequestUserId);

            Assert.AreEqual(storedUserId, stored.UserId);
            Assert.AreEqual(GdprStorageState.Original, stored.GdprState);
        }
    }
}
