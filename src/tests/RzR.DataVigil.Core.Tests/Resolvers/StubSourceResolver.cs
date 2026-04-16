using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Services;

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
