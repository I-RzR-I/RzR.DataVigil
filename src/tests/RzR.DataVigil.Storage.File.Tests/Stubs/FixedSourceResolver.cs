using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Storage.File.Tests.Stubs
{
    internal class FixedSourceResolver : IAuditSourceResolver
    {
        public string SourceToReturn { get; set; } = "Tests";

        public IResult<string> Resolve() => Result<string>.Success(SourceToReturn);
    }
}
