using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RzR.DataVigil.EFCore.Tests.Stubs
{
    internal sealed class ThrowingSavingChangesInterceptor : SaveChangesInterceptor
    {
        public bool ShouldThrow { get; set; }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (ShouldThrow)
                throw new InvalidOperationException("Simulated failure from SavingChanges.");

            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
                throw new InvalidOperationException("Simulated failure from SavingChangesAsync.");

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
