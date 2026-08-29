using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Storage.File.Tests.Stubs
{
    internal class AnonymousUserResolver : IAuditUserResolver
    {
        public IResult<AuditUserInfo> Resolve() => Result<AuditUserInfo>.Success();
    }
}
