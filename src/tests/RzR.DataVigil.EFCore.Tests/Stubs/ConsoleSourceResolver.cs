using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal class ConsoleSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve() => Result<string>.Success("ConsoleApp");
    }
}
