using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;

namespace RzR.DataVigil.Core.Tests.Stubs
{
    internal class StubAuditStore : IAuditStore
    {
        public AuditTransaction LastSaved { get; private set; }
        public int SaveCallCount { get; private set; }
        public bool ShouldFail { get; set; }

        public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            LastSaved = transaction;

            if (ShouldFail)
                return Task.FromResult<IResult>(Result.Failure("Store failed"));

            return Task.FromResult<IResult>(Result.Success());
        }

        public Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(
            AuditTransactionQuery filters,
            GdprRetrievalContext gdprRetrievalContext = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IResult> AnonymizeByUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public int PurgeCallCount { get; private set; }
        public DateTimeOffset? LastPurgeCutoff { get; private set; }
        public bool PurgeShouldFail { get; set; }

        public Task<IResult> PurgeBeforeAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
        {
            PurgeCallCount++;
            LastPurgeCutoff = before;

            if (PurgeShouldFail)
                return Task.FromResult<IResult>(Result.Failure("Purge failed"));

            return Task.FromResult<IResult>(Result.Success());
        }
    }
}
