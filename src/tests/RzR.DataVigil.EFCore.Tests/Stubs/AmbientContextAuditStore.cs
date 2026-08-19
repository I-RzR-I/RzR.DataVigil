#region U S I N G

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.EFCore.Tests.Entities;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal class AmbientContextAuditStore : IAuditStore
    {
        public const string Topic = "audit.recorded";

        public DbContext Context { get; set; }

        public List<AuditTransaction> SavedTransactions { get; } = new List<AuditTransaction>();

        public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
        {
            SavedTransactions.Add(transaction);

            Context.Set<AuditOutboxRecord>().Add(new AuditOutboxRecord
            {
                Topic = Topic,
                Payload = transaction.Id.ToString()
            });

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
