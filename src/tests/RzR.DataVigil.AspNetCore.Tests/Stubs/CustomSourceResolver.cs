using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal sealed class CustomSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve() => Result<string>.Success("custom-source");
    }
}
