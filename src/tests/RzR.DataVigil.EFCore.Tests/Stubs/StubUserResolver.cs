using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

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
}
