using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Services;

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal class ConsoleSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve() => Result<string>.Success("ConsoleApp");
    }
}
