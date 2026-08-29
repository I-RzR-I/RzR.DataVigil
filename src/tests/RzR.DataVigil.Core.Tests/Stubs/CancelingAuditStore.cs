using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Stubs
{
    internal sealed class CancelingAuditStore : IAuditStore
    {
        public int SaveCallCount { get; private set; }

        public Task<IResult> SaveAsync(AuditTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;

            throw new OperationCanceledException(cancellationToken);
        }

        public Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(
            AuditTransactionQuery filters,
            GdprRetrievalContext gdprRetrievalContext = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IResult> AnonymizeByUserAsync(string userId,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IResult> PurgeBeforeAsync(DateTimeOffset before,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
