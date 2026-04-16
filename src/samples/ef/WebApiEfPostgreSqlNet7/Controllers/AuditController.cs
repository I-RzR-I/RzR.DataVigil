using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;

namespace WebApiEfPostgreSqlNet7.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditController : ControllerBase
    {
        private readonly IAuditStore _auditStore;

        public AuditController(IAuditStore auditStore)
        {
            _auditStore = auditStore;
        }

        /// <summary>
        /// Query audit log entries with paging/filtering/sorting.
        /// </summary>
        [HttpPost("query")]
        public async Task<IActionResult> Query(CancellationToken cancellationToken)
        {
            var context = new RzR.DataVigil.Abstractions.Models.Gdpr.GdprRetrievalContext
            {
                UserRoles = User != null
                    ? new System.Security.Claims.ClaimsPrincipal(User).Claims
                        .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                        .Select(c => c.Value)
                    : new string[0],
                UserClaims = User?.Claims?.ToDictionary(c => c.Type, c => c.Value)
                    ?? new System.Collections.Generic.Dictionary<string, string>()
            };
            var result = await _auditStore.QueryAsync(new AuditTransactionQuery(), context, cancellationToken);

            return Ok(result.Response);
        }
    }
}
