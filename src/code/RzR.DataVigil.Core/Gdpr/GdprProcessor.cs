// ***********************************************************************
//  Assembly         : RzR.DataVigil.Core
//  Author           : RzR
//  Created On       : 2026-04-10 22:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 19:15
// ***********************************************************************
//  <copyright file="GdprProcessor.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RzR.DataVigil.Core.Extensions;
using RzR.Extensions.Domain.Collections;
using RzR.Extensions.Domain.Primitives;
using RzR.Extensions.Domain.Text;

#endregion

namespace RzR.DataVigil.Core.Gdpr
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Applies GDPR policies to audit entries (both at storage and retrieval time).
    /// </summary>
    /// =================================================================================================
    public sealed class GdprProcessor
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the registry.
        /// </summary>
        /// =================================================================================================
        private readonly GdprPolicyRegistry _registry;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="GdprProcessor"/> class.
        /// </summary>
        /// <param name="registry">The registry.</param>
        /// =================================================================================================
        public GdprProcessor(GdprPolicyRegistry registry)
        {
            _registry = registry;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Apply storage GDPR policies before persisting the audit entry.
        ///     Returns (<paramref name="entry"/>, applied, fullyAnonymized) where:
        ///     <c>applied</c> is true when at least one GDPR rule was executed,
        ///     <c>fullyAnonymized</c> is true when every applied rule was
        ///     <see cref="GdprFieldAction.Anonymize"/> or <see cref="GdprFieldAction.Exclude"/>
        ///     (i.e. no identifiable data remains in the processed fields).
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <returns>
        ///     A Tuple.
        /// </returns>
        /// =================================================================================================
        public (AuditEntry entry, bool applied, bool fullyAnonymized) ApplyStoragePolicies(AuditEntry entry)
        {
            if (!_registry.TryGetPolicyByName(entry.EntityName, out var policy)) 
                return (entry, false, false);

            if (policy.StorageRules.IsNullOrEmptyEnumerable()) 
                return (entry, false, false);

            var hasProcessed = false;
            var allAnonymized = true;

            foreach (var property in entry.Properties.NotNull())
            {
                var rule = FindRule(policy.StorageRules, property.PropertyName);
                if (rule.IsNull())
                    continue;

                hasProcessed = true;

                if (rule.Action != GdprFieldAction.Anonymize && rule.Action != GdprFieldAction.Exclude)
                    allAnonymized = false;

                switch (rule.Action)
                {
                    case GdprFieldAction.Exclude:
                        property.OldValue = null;
                        property.NewValue = null;
                        break;

                    case GdprFieldAction.Mask:
                        property.OldValue = MaskValue(property.OldValue);
                        property.NewValue = MaskValue(property.NewValue);
                        break;

                    case GdprFieldAction.Anonymize:
                        property.OldValue = property.OldValue.AsAnonymizedIfPresent();
                        property.NewValue = property.NewValue.AsAnonymizedIfPresent();
                        break;

                    case GdprFieldAction.Hash:
                        property.OldValue = HashValue(property.OldValue);
                        property.NewValue = HashValue(property.NewValue);
                        break;

                    case GdprFieldAction.Custom:
                        if (rule.CustomTransformer.IsNotNull())
                        {
                            property.OldValue = property.OldValue.IsNotNull()
                                ? rule.CustomTransformer(property.OldValue)
                                : null;
                            property.NewValue = property.NewValue.IsNotNull()
                                ? rule.CustomTransformer(property.NewValue)
                                : null;
                        }

                        break;
                }
            }

            // Remove excluded properties entirely
            var excluded = new HashSet<string>(
                policy.StorageRules
                    .Where(r => r.Action == GdprFieldAction.Exclude)
                    .Select(r => r.FieldName));

            if (excluded.Count > 0)
            {
                var filtered = new List<AuditEntryProperty>();
                foreach (var prop in entry.Properties)
                {
                    if (excluded.Contains(prop.PropertyName).IsFalse())
                        filtered.Add(prop);
                }

                entry.Properties = filtered;
            }

            return (entry, hasProcessed, hasProcessed && allAnonymized);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Apply retrieval GDPR policies before returning audit data to the caller.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="context">The context.</param>
        /// <returns>
        ///     An AuditEntry.
        /// </returns>
        /// =================================================================================================
        public AuditEntry ApplyRetrievalPolicies(AuditEntry entry, GdprRetrievalContext context)
        {
            if (!_registry.TryGetPolicyByName(entry.EntityName, out var policy))
                return entry;

            if (policy.RetrievalRules.IsNullOrEmptyEnumerable())
                return entry;

            foreach (var property in entry.Properties.NotNull())
            {
                var rule = FindRule(policy.RetrievalRules, property.PropertyName);
                if (rule.IsNull())
                    continue;

                // If user has access, skip masking
                if (context.CanAccess(rule))
                    continue;

                switch (rule.Action)
                {
                    case GdprFieldAction.Mask:
                        property.OldValue = MaskValue(property.OldValue);
                        property.NewValue = MaskValue(property.NewValue);
                        break;

                    case GdprFieldAction.Anonymize:
                        property.OldValue = property.OldValue.AsAnonymizedIfPresent();
                        property.NewValue = property.NewValue.AsAnonymizedIfPresent();
                        break;
                }
            }

            return entry;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Searches for the first rule.
        /// </summary>
        /// <param name="rules">The rules.</param>
        /// <param name="fieldName">Name of the field.</param>
        /// <returns>
        ///     The found rule.
        /// </returns>
        /// =================================================================================================
        private static FieldGdprRule FindRule(IEnumerable<FieldGdprRule> rules, string fieldName)
        {
            foreach (var rule in rules)
            {
                if (string.Equals(rule.FieldName, fieldName, StringComparison.Ordinal))
                    return rule;
            }

            return null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Mask value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        ///     A string.
        /// </returns>
        /// =================================================================================================
        private static string MaskValue(string value)
        {
            if (value.IsNullOrEmpty())
                return value;

            if (value.Length <= 2)
                return "***";

            // Show first and last char, mask the rest
            return value[0] + new string('*', Math.Max(value.Length - 2, 3)) + value[value.Length - 1];
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Hash value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        ///     A string.
        /// </returns>
        /// =================================================================================================
        private static string HashValue(string value)
        {
            if (value.IsNull())
                return null;

            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++) 
                    sb.Append(bytes[i].ToString("x2"));

                return sb.ToString();
            }
        }
    }
}