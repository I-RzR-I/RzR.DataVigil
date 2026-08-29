using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Stubs
{
    internal class NullReturningScopeContext : IAuditScopeContext
    {
        public IResult SetUser(AuditUserInfo user) => Result.Success();

        public IResult<AuditUserInfo> GetCurrentUser() => null;

        public void Dispose()
        {
        }

        public IResult SetCorrelationId(string correlationId) => Result.Success();

        public IResult<string> GetCurrentCorrelationId() => Result<string>.Success(null);
    }
}
