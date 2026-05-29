using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace WebApiEfPostgreSqlNet9.Resolvers
{
    public class AuditSourceResolver : IAuditSourceResolver
    {
        /// <inheritdoc />
        public IResult<string> Resolve()
        {
            return Result<string>.Success("WebApiNet9");
        }
    }
}
