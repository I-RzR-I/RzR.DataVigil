using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Services;

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
