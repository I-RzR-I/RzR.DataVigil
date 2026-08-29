// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-08-29 14:20
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 14:20
// ***********************************************************************
//  <copyright file="AuditColumnValue.cs" company="RzR SOFT &amp; TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

namespace RzR.DataVigil.Core.Helpers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     The single definition of how a value is shortened to fit an audit storage column.
    /// </summary>
    /// =================================================================================================
    public static class AuditColumnValue
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Shortens a value to at most <paramref name="max"/> characters without ever splitting a
        ///     surrogate pair, and returns it unchanged when it already fits.
        /// </summary>
        /// <remarks>
        ///     Cutting between the halves of a surrogate pair would leave a lone high surrogate, which is
        ///     not encodable as valid UTF-8. On PostgreSQL that either fails the insert or is silently
        ///     replaced with U+FFFD - reintroducing the very failure this shortening exists to prevent.
        /// </remarks>
        /// <param name="value">The value to shorten, may be null.</param>
        /// <param name="max">The column length in characters.</param>
        /// <returns>
        ///     The value as it can appear in storage.
        /// </returns>
        /// =================================================================================================
        public static string TruncateToColumnLength(string value, int max)
        {
            if (max <= 0 || string.IsNullOrEmpty(value) || value.Length <= max)
                return value;

            var cut = max;
            if (char.IsHighSurrogate(value[cut - 1]))
                cut--;

            return value.Substring(0, cut);
        }
    }
}
