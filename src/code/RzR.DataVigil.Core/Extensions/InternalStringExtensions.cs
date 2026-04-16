// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-14 19:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:26
// ***********************************************************************
//  <copyright file="InternalStringExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using DomainCommonExtensions.DataTypeExtensions;

#endregion

namespace RzR.DataVigil.Core.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Internal string extension methods for GDPR field-value transformations
    ///     (anonymization and erasure markers).
    /// </summary>
    /// =================================================================================================
    public static class InternalStringExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Replaces the string value with the <c>[ANONYMIZED]</c> marker,
        ///     regardless of the original content.
        /// </summary>
        /// <param name="source">The original string value.</param>
        /// <returns>
        ///     The literal string <c>"[ANONYMIZED]"</c>.
        /// </returns>
        /// =================================================================================================
        public static string AsAnonymized(this string source)
        {
            return "[ANONYMIZED]";
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Replaces the string value with the <c>[ANONYMIZED]</c> marker when the value
        ///     is non-null and non-empty; returns <see langword="null"/> otherwise.
        /// </summary>
        /// <param name="source">The original string value (may be <see langword="null"/>).</param>
        /// <returns>
        ///     <c>"[ANONYMIZED]"</c> if <paramref name="source"/> is present; <see langword="null"/> otherwise.
        /// </returns>
        /// =================================================================================================
        public static string AsAnonymizedIfPresent(this string source)
        {
            return source.IsPresent() ? "[ANONYMIZED]" : null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Replaces the string value with the <c>[ERASED]</c> marker,
        ///     regardless of the original content.
        /// </summary>
        /// <param name="source">The original string value.</param>
        /// <returns>
        ///     The literal string <c>"[ERASED]"</c>.
        /// </returns>
        /// =================================================================================================
        public static string AsErased(this string source)
        {
            return "[ERASED]";
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Replaces the string value with the <c>[ERASED]</c> marker when the value
        ///     is non-null and non-empty; returns <see langword="null"/> otherwise.
        /// </summary>
        /// <param name="source">The original string value (may be <see langword="null"/>).</param>
        /// <returns>
        ///     <c>"[ERASED]"</c> if <paramref name="source"/> is present; <see langword="null"/> otherwise.
        /// </returns>
        /// =================================================================================================
        public static string AsErasedIfPresent(this string source)
        {
            return source.IsPresent() ? "[ERASED]" : null;
        }
    }
}