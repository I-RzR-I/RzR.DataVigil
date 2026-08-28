using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal sealed class StubHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }

    internal sealed class StubAuditScopeContext : IAuditScopeContext
    {
        public AuditUserInfo CurrentUser { get; set; }

        public bool GetCurrentUserShouldFail { get; set; }

        public bool GetCurrentUserShouldThrow { get; set; }

        public IResult SetUser(AuditUserInfo user)
        {
            CurrentUser = user;

            return Result.Success();
        }

        public IResult<AuditUserInfo> GetCurrentUser()
        {
            if (GetCurrentUserShouldThrow)
                throw new InvalidOperationException("Scope context failed.");

            return GetCurrentUserShouldFail
                ? Result<AuditUserInfo>.Failure()
                : Result<AuditUserInfo>.Success(CurrentUser);
        }

        public IResult SetCorrelationId(string correlationId) => Result.Success();

        public IResult<string> GetCurrentCorrelationId() => Result<string>.Success(null);

        public void Dispose()
        {
        }
    }

    internal sealed class StubAuditStore : IAuditStore
    {
        public AuditTransaction LastSaved { get; private set; }

        public int SaveCallCount { get; private set; }

        public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            LastSaved = transaction;

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

    internal static class HttpEnricherTestData
    {
        internal static AuditTransaction BuildTransaction() => new AuditTransaction
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Entries = new List<AuditEntry>
            {
                new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    EntityName = "Order",
                    EntityId = "1",
                    Action = AuditAction.Update,
                    Properties = new List<AuditEntryProperty>
                    {
                        new AuditEntryProperty
                        {
                            PropertyName = "Name",
                            PropertyType = "System.String",
                            OldValue = "Old",
                            NewValue = "New"
                        }
                    }
                }
            }
        };

        internal static DefaultHttpContext ContextWithMethod(string method)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;

            return context;
        }
    }
}
