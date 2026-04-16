using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;

namespace RzR.DataVigil.Core.Tests.Resolvers
{
    internal class StubUserResolver : IAuditUserResolver
    {
        public AuditUserInfo UserToReturn { get; set; }

        public IResult<AuditUserInfo> Resolve()
        {
            return Result<AuditUserInfo>.Success(UserToReturn);
        }
    }
}
