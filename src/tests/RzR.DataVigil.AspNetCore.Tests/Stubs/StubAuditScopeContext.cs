using System;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
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
}
