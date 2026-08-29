using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Stubs.Retention
{
    internal class ThrowingAuditStore : IAuditStore
    {
        public int PurgeCallCount { get; private set; }

        public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
            => Task.FromResult<IResult>(Result.Failure("Not implemented"));

        public Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(
            AuditTransactionQuery filters,
            GdprRetrievalContext gdprRetrievalContext = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IResult<IEnumerable<AuditTransaction>>>(Result<IEnumerable<AuditTransaction>>.Failure("Not implemented"));

        public Task<IResult> AnonymizeByUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IResult>(Result.Failure("Not implemented"));

        public Task<IResult> PurgeBeforeAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
        {
            PurgeCallCount++;
            throw new InvalidOperationException("Simulated purge failure");
        }
    }
}
