using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal sealed class FailureWithResponseScopeContext : IAuditScopeContext
    {
        public IResult SetUser(AuditUserInfo user) => Result.Success();

        public IResult<AuditUserInfo> GetCurrentUser()
        {
            var failure = Result<AuditUserInfo>.Failure("scope lookup failed");
            failure.Response = new AuditUserInfo { UserId = "should-not-be-used" };

            return failure;
        }

        public void Dispose()
        {
        }

        public IResult SetCorrelationId(string correlationId) => Result.Success();

        public IResult<string> GetCurrentCorrelationId() => Result<string>.Success(null);
    }
}
