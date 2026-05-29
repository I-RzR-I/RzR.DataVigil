using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

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
