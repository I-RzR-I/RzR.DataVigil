using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Resolvers
{
    internal class StubUserResolver : IAuditUserResolver
    {
        public AuditUserInfo UserToReturn { get; set; }
        public bool ShouldFail { get; set; }

        public IResult<AuditUserInfo> Resolve()
        {
            return ShouldFail
                ? Result<AuditUserInfo>.Failure()
                : Result<AuditUserInfo>.Success(UserToReturn);
        }
    }
}
