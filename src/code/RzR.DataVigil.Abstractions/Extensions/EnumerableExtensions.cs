// ***********************************************************************
//  Assembly         : RzR.DataVigil.Abstractions
//  Author           : RzR
//  Created On       : 2026-04-11 02:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-11 03:22
// ***********************************************************************
//  <copyright file="EnumerableExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Collections.Generic;
using System.Linq;

#endregion

namespace RzR.DataVigil.Abstractions.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Internal extension methods for <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// =================================================================================================
    internal static class EnumerableExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Returns <c>true</c> if the enumerable is not null and contains at least one element.
        /// </summary>
        /// <typeparam name="T">Generic type parameter.</typeparam>
        /// <param name="enumerable">The enumerable to act on.</param>
        /// <returns>
        ///     True if not null or empty enumerable, false if not.
        /// </returns>
        /// =================================================================================================
        public static bool IsNotNullOrEmptyEnumerable<T>(this IEnumerable<T> enumerable)
        {
            return enumerable != null && enumerable.Any();
        }
    }
}