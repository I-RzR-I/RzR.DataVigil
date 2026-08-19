using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace RzR.DataVigil.EFCore.Extensions;

/// <summary>
///     A database context entity extensions.
/// </summary>
internal static class DbContextEntityExtensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Determines whether the context still holds unsaved work.
    /// </summary>
    /// <param name="context">The audited context.</param>
    /// <returns>True when at least one tracked entry is added, modified or deleted.</returns>
    /// =================================================================================================
    internal static bool HasPendingWork(this DbContext context)
        => context.ChangeTracker.Entries().Any(entry => entry.State.IsPending());

    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Determines whether an entry state represents work that a save would send to the database.
    /// </summary>
    /// <param name="state">The entry state.</param>
    /// <returns>True when the state is added, modified or deleted.</returns>
    /// =================================================================================================
    internal static bool IsPending(this EntityState state)
        => state == EntityState.Added || state == EntityState.Modified || state == EntityState.Deleted;
}