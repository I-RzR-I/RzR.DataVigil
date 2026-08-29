using System;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal sealed class ThrowingScopeContext : IAuditScopeContext
    {
        public int GetCorrelationIdCallCount { get; private set; }

        public IResult SetUser(AuditUserInfo user) => Result.Success();

        public IResult<AuditUserInfo> GetCurrentUser() => Result<AuditUserInfo>.Success(null);

        public IResult SetCorrelationId(string correlationId) => Result.Success();

        public IResult<string> GetCurrentCorrelationId()
        {
            GetCorrelationIdCallCount++;

            throw new ObjectDisposedException(nameof(ThrowingScopeContext));
        }

        public void Dispose()
        {
        }
    }
}
