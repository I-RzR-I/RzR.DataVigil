using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Stubs
{
    internal class StubCorrelationProvider : IAuditCorrelationProvider
    {
        public string CorrelationId { get; set; } = "corr-001";
        public string TraceId { get; set; } = "trace-001";

        public IResult<string> GetCorrelationId() => Result<string>.Success(CorrelationId);
        public IResult<string> GetTraceId() => Result<string>.Success(TraceId);
    }
}
