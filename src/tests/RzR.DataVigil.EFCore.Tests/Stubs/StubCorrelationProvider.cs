using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal class StubCorrelationProvider : IAuditCorrelationProvider
    {
        public IResult<string> GetCorrelationId() => Result<string>.Success("corr-1");

        public IResult<string> GetTraceId() => Result<string>.Success("trace-1");
    }
}
