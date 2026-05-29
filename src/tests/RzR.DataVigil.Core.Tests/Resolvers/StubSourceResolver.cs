using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Resolvers
{
    internal class StubSourceResolver : IAuditSourceResolver
    {
        public string SourceToReturn { get; set; } = "TestHost";

        public IResult<string> Resolve()
        {
            return Result<string>.Success(SourceToReturn);
        }
    }
}
