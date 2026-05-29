using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace SampleWorkerService.Resolvers
{
    /// <summary>
    ///     Custom source resolver that identifies this application in audit logs.
    /// </summary>
    public class WorkerSourceResolver : IAuditSourceResolver
    {
        public IResult<string> Resolve()
        {
            return Result<string>.Success("SampleWorkerService");
        }
    }
}
