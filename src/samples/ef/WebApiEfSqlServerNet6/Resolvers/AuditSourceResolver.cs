using AggregatedGenericResultMessage;
using AggregatedGenericResultMessage.Abstractions;
using RzR.DataVigil.Abstractions.Services;

namespace WebApiEfSqlServerNet6.Resolvers
{
    public class AuditSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve()
        {
            return Result<string>.Success("WebApiNet6");
        }
    }
}
