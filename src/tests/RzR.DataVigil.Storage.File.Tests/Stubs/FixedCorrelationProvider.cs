using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Storage.File.Tests.Stubs
{
    internal class FixedCorrelationProvider : IAuditCorrelationProvider
    {
        public string CorrelationId { get; set; } = "corr-1";
        public string TraceId { get; set; } = "trace-1";

        public IResult<string> GetCorrelationId() => Result<string>.Success(CorrelationId);
        public IResult<string> GetTraceId() => Result<string>.Success(TraceId);
    }
}
