// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-08-28 20:40
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 00:15
// ***********************************************************************
//  <copyright file="CorrelationIdValidator.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Core.Enums;
using RzR.Extensions.Domain.Text;

#endregion

namespace RzR.DataVigil.Core.Helpers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Shared validation for every value that can reach the audit correlation id column, whatever
    ///     source supplied it: the audit scope, an HTTP request header, or the request trace identifier.
    /// </summary>
    /// =================================================================================================
    internal static class CorrelationIdValidator
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Accepts a candidate correlation id only when it is short and simple enough to be safe to
        ///     store. A rejected candidate is never truncated and never throws, so the caller falls
        ///     through to the next source and an audit record is always written.
        /// </summary>
        /// <param name="candidate">The raw candidate value, possibly null.</param>
        /// <param name="value">[out] The accepted, trimmed value, or null when the candidate is rejected.</param>
        /// <returns>
        ///     True when the candidate is an acceptable correlation id.
        /// </returns>
        /// =================================================================================================
        internal static bool TryAccept(string candidate, out string value)
            => TryAccept(candidate, out value, out _, out _, out _);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Accepts a candidate correlation id and, when it is rejected, reports why. The caller needs
        ///     the reason to distinguish a value that was never supplied - the ordinary case in a host
        ///     that does not set one - from a value that was supplied and then refused, which is the only
        ///     case worth reporting to an operator.
        /// </summary>
        /// <param name="candidate">The raw candidate value, possibly null.</param>
        /// <param name="value">[out] The accepted, trimmed value, or null when the candidate is rejected.</param>
        /// <param name="reason">[out] Why the candidate was rejected, or <c>Accepted</c>.</param>
        /// <param name="invalidIndex">
        ///     [out] The zero-based index of the first disallowed character, or -1 for every other reason.
        /// </param>
        /// <param name="candidateLength">[out] The trimmed length of the candidate, or 0 when it is null.</param>
        /// <returns>
        ///     True when the candidate is an acceptable correlation id.
        /// </returns>
        /// =================================================================================================
        internal static bool TryAccept(string candidate, out string value,
            out CorrelationIdRejection reason, out int invalidIndex, out int candidateLength)
        {
            var trimmed = candidate?.Trim();

            value = null;
            invalidIndex = -1;
            candidateLength = trimmed?.Length ?? 0;

            if (trimmed.IsMissing())
            {
                reason = CorrelationIdRejection.Missing;

                return false;
            }

            if (candidateLength > AuditColumnLengths.CorrelationId)
            {
                reason = CorrelationIdRejection.TooLong;

                return false;
            }

            for (var index = 0; index < trimmed!.Length; index++)
            {
                if (IsAllowedCorrelationChar(trimmed[index])) continue;

                invalidIndex = index;
                reason = CorrelationIdRejection.DisallowedCharacter;

                return false;
            }

            value = trimmed;
            reason = CorrelationIdRejection.Accepted;

            return true;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Allowed correlation-id characters: <c>[A-Za-z0-9._:-]</c>. The charset is deliberately
        ///     ASCII-only, which is what makes the length limit mean the same thing for a UTF-16 column
        ///     and for a byte-counted one.
        /// </summary>
        /// <param name="c">The character to test.</param>
        /// <returns>
        ///     True if the character is allowed.
        /// </returns>
        /// =================================================================================================
        private static bool IsAllowedCorrelationChar(char c)
            => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
               || c == '.' || c == '_' || c == ':' || c == '-';
    }
}
