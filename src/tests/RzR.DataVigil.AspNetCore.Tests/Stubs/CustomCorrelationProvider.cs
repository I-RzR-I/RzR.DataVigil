using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal sealed class CustomCorrelationProvider : IAuditCorrelationProvider
    {
        public IResult<string> GetCorrelationId() => Result<string>.Success(null);

        public IResult<string> GetTraceId() => Result<string>.Success(null);
    }
}
