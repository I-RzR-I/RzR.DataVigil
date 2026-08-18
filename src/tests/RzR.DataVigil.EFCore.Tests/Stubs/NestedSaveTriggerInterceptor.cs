using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal sealed class NestedSaveTriggerInterceptor : SaveChangesInterceptor
    {
        private readonly DbContext _innerContext;
        private bool _triggered;

        public NestedSaveTriggerInterceptor(DbContext innerContext)
        {
            _innerContext = innerContext;
        }

        public DbContext OuterContext { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_triggered && ReferenceEquals(eventData.Context, OuterContext))
            {
                _triggered = true;
                await _innerContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
        }
    }
}
