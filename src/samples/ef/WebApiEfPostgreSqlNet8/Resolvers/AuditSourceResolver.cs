using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace WebApiEfPostgreSqlNet8.Resolvers
{
    public class AuditSourceResolver : IAuditSourceResolver
    {
        /// <inheritdoc />
        public IResult<string> Resolve()
        {
            return Result<string>.Success("WebApiNet8");
        }
    }
}
