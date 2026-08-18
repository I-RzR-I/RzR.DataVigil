using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Storage.File.Tests.Helpers
{
    internal class AnonymousUserResolver : IAuditUserResolver
    {
        public IResult<AuditUserInfo> Resolve() => Result<AuditUserInfo>.Success();
    }

    internal class FixedSourceResolver : IAuditSourceResolver
    {
        public string SourceToReturn { get; set; } = "Tests";

        public IResult<string> Resolve() => Result<string>.Success(SourceToReturn);
    }

    internal class FixedCorrelationProvider : IAuditCorrelationProvider
    {
        public string CorrelationId { get; set; } = "corr-1";
        public string TraceId { get; set; } = "trace-1";

        public IResult<string> GetCorrelationId() => Result<string>.Success(CorrelationId);
        public IResult<string> GetTraceId() => Result<string>.Success(TraceId);
    }
}
