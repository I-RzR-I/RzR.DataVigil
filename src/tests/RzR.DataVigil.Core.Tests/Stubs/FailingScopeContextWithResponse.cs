using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.ResultMessage.Extensions.Result;

namespace RzR.DataVigil.Core.Tests.Stubs
{
    internal class FailingScopeContextWithResponse : IAuditScopeContext
    {
        private readonly AuditUserInfo _response;

        public FailingScopeContextWithResponse(AuditUserInfo response)
        {
            _response = response;
        }

        public IResult SetUser(AuditUserInfo user) => Result.Success();

        public IResult<AuditUserInfo> GetCurrentUser()
            => Result<AuditUserInfo>.Success(_response).Validate(_ => false, "forced failure for test");

        public void Dispose()
        {
        }

        public IResult SetCorrelationId(string correlationId) => Result.Success();

        public IResult<string> GetCurrentCorrelationId() => Result<string>.Success(null);
    }
}
