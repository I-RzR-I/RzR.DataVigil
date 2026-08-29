using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal sealed class CustomUserResolver : IAuditUserResolver
    {
        public IResult<AuditUserInfo> Resolve() => Result<AuditUserInfo>.Success();
    }
}
