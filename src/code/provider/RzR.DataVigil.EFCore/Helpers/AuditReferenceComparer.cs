using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RzR.DataVigil.EFCore.Helpers;

/// -------------------------------------------------------------------------------------------------
/// <summary>
///     Compares entities by reference, so an entity overriding <c>Equals</c> cannot make two
///     distinct tracked instances look like one.
/// </summary>
/// =================================================================================================
internal sealed class AuditReferenceComparer : IEqualityComparer<object>
{
    public static readonly AuditReferenceComparer Instance = new AuditReferenceComparer();

    /// <inheritdoc />
    public new bool Equals(object x, object y) => ReferenceEquals(x, y);

    /// <inheritdoc />
    public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}