using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal class StubUserResolver : IAuditUserResolver
    {
        public IResult<AuditUserInfo> Resolve()
        {
            return Result<AuditUserInfo>.Success(new AuditUserInfo
            {
                UserId = "test-user",
                UserName = "TestUser",
                IpAddress = "127.0.0.1"
            });
        }
    }

    internal class StubSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve() => Result<string>.Success("Tests");
    }

    internal class StubCorrelationProvider : IAuditCorrelationProvider
    {
        public IResult<string> GetCorrelationId() => Result<string>.Success("corr-1");

        public IResult<string> GetTraceId() => Result<string>.Success("trace-1");
    }

    internal class StubAuditStore : IAuditStore
    {
        public List<AuditTransaction> SavedTransactions { get; } = new List<AuditTransaction>();

        public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
        {
            SavedTransactions.Add(transaction);

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
