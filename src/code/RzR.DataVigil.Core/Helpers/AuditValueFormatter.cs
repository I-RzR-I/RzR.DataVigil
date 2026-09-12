// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-09-11 21:00
//
//  Last Modified By : RzR
//  Last Modified On : 2026-09-11 21:26
// ***********************************************************************
//  <copyright file="AuditValueFormatter.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.Core.Helpers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Formats a captured property value for the audit trail.
    /// </summary>
    /// =================================================================================================
    public static class AuditValueFormatter
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the maximum number of characters the JSON this formatter generates may occupy
        ///     before it is truncated.
        /// </summary>
        /// =================================================================================================
        public const int MaxJsonValueLength = 8000;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) options for controlling the JSON written for a complex audit value.
        /// </summary>
        /// =================================================================================================
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            MaxDepth = 8,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the CLR types recorded through <c>ToString</c> instead of as JSON.
        /// </summary>
        /// =================================================================================================
        private static readonly HashSet<string> SimpleTypeNames = new(StringComparer.Ordinal)
        {
            "System.String",
            "System.Decimal",
            "System.Guid",
            "System.DateTime",
            "System.DateTimeOffset",
            "System.DateOnly",
            "System.TimeOnly",
            "System.TimeSpan",
            "System.Uri",
            "System.Version"
        };

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Formats one captured value for the audit trail.
        /// </summary>
        /// <param name="value">
        ///     The value to format, may be null. A boxed <see cref="Nullable{T}" /> already
        ///     reports its underlying type through <see cref="object.GetType" />, so no unwrapping is
        ///     required here.
        /// </param>
        /// <returns>
        ///     The recorded value, or <c>null</c> when <paramref name="value" /> is <c>null</c>.
        /// </returns>
        /// =================================================================================================
        public static string Format(object value)
        {
            if (value.IsNull())
                return null;

            var clrType = value.GetType();

            if (clrType.IsEnum)
                return value.ToString();

            if (IsSimpleType(clrType))
                return value.ToString();

            if (clrType == typeof(byte[]))
                return "byte[" + ((byte[])value).Length + "]";

            if (ShouldSerializeAsJson(clrType))
                return FormatJson(value, true);

            return value.ToString();
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Formats one captured entity key value for the audit trail.
        /// </summary>
        /// <param name="value">
        ///     The key value to format, may be null. A boxed <see cref="Nullable{T}" />
        ///     already reports its underlying type through <see cref="object.GetType" />, so no unwrapping is
        ///     required here.
        /// </param>
        /// <returns>
        ///     The recorded key value, or <c>null</c> when <paramref name="value" /> is <c>null</c>.
        /// </returns>
        /// =================================================================================================
        public static string FormatKey(object value)
        {
            if (value.IsNull())
                return null;

            var clrType = value.GetType();

            if (clrType.IsEnum)
                return value.ToString();

            if (clrType == typeof(byte[]))
                return FormatByteArrayAsHex((byte[])value);

            if (IsSimpleType(clrType))
                return value.ToString();

            if (ShouldSerializeAsJson(clrType))
                return FormatJson(value, false);

            return value.ToString();
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Formats a byte array as a lowercase hexadecimal string, with no separators.
        /// </summary>
        /// <param name="bytes">The bytes to format.</param>
        /// <returns>
        ///     A lowercase hex string twice the length of <paramref name="bytes" />.
        /// </returns>
        /// =================================================================================================
        private static string FormatByteArrayAsHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Query if the given type is recorded through <c>ToString</c> rather than as JSON.
        /// </summary>
        /// <param name="clrType">The non-nullable CLR type of the value.</param>
        /// <returns>
        ///     True when the type is simple, false when it is complex.
        /// </returns>
        /// =================================================================================================
        private static bool IsSimpleType(Type clrType)
        {
            return clrType.IsPrimitive || SimpleTypeNames.Contains(clrType.FullName ?? string.Empty);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Query if the given type should be recorded as JSON rather than through <c>ToString</c>.
        /// </summary>
        /// <param name="clrType">The non-nullable CLR type of the value.</param>
        /// <returns>
        ///     True when the type does not override <c>ToString</c>
        /// </returns>
        /// =================================================================================================
        private static bool ShouldSerializeAsJson(Type clrType)
        {
            var declaringType = clrType.GetMethod("ToString", Type.EmptyTypes)?.DeclaringType;

            return declaringType == typeof(object) || declaringType == typeof(ValueType);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Serializes a complex value to JSON, appending a visible, content-addressed marker when
        ///     <paramref name="truncate" /> is set and the result is longer than
        ///     <see cref="MaxJsonValueLength" />, and marking the value as unrecordable when serialization
        ///     fails for any reason.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="truncate">
        ///     True to cut the JSON to <see cref="MaxJsonValueLength" />; false to always
        ///     return the JSON in full, as required for an entity key.
        /// </param>
        /// <returns>
        ///     The recorded value.
        /// </returns>
        /// =================================================================================================
        private static string FormatJson(object value, bool truncate)
        {
            try
            {
                var json = JsonSerializer.Serialize(value, JsonOptions);
                if (truncate.IsFalse() || json.Length <= MaxJsonValueLength)
                    return json;

                var marker = "...[truncated, full length " + json.Length + ", sha256 " + ComputeSha256Prefix(json) +
                             "]";

                return AuditColumnValue.TruncateToColumnLength(json, MaxJsonValueLength - marker.Length) + marker;
            }
            catch (Exception ex)
            {
                return "[unrecordable: " + ex.GetType().Name + "]";
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Computes the first 16 lowercase hex characters of the SHA-256 hash of a string, so that two
        ///     truncated values that differ only past the cut are distinguishable in the audit record.
        /// </summary>
        /// <param name="value">The value to hash.</param>
        /// <returns>
        ///     A 16-character lowercase hex string.
        /// </returns>
        /// =================================================================================================
        private static string ComputeSha256Prefix(string value)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(16);

                for (var i = 0; i < 8; i++)
                    builder.Append(hash[i].ToString("x2"));

                return builder.ToString();
            }
        }
    }
}