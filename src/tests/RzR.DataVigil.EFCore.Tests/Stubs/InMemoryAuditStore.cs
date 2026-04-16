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

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal class InMemoryAuditStore : IAuditStore
    {
        public List<AuditTransaction> Transactions { get; } = new List<AuditTransaction>();

        public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
        {
            Transactions.Add(transaction);
            return Task.FromResult<IResult>(Result.Success());
        }

        public Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(
            AuditTransactionQuery filters,
            GdprRetrievalContext gdprRetrievalContext = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IResult> AnonymizeByUserAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IResult> PurgeBeforeAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
