// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 08:04
// 
//  Last Modified By : RzR
//  Last Modified On : 20-05-2026 23:08
//  ***********************************************************************
//  <copyright file="GdprRegistryFactory.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System.Collections.Generic;
using System.Reflection;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Core.Gdpr;

#endregion

namespace RzR.DataVigil.Benchmarks.Helpers
{
    internal static class GdprRegistryFactory
    {
        internal static GdprPolicyRegistry CreateEmpty()
        {
            return new GdprPolicyRegistry();
        }

        internal static GdprPolicyRegistry CreateWithStorageRules(string entityName,
            IEnumerable<FieldGdprRule> storageRules)
        {
            var registry = new GdprPolicyRegistry();
            var policy = new EntityGdprPolicy
            {
                StorageRules = storageRules
            };

            InjectPolicy(registry, entityName, policy);

            return registry;
        }

        internal static GdprPolicyRegistry CreateWithRetrievalRules(string entityName,
            IEnumerable<FieldGdprRule> retrievalRules)
        {
            var registry = new GdprPolicyRegistry();
            var policy = new EntityGdprPolicy
            {
                RetrievalRules = retrievalRules
            };

            InjectPolicy(registry, entityName, policy);

            return registry;
        }

        internal static GdprPolicyRegistry CreateOrderStorageRegistry()
        {
            return CreateWithStorageRules("Order", new[]
            {
                new FieldGdprRule
                {
                    FieldName = "CustomerEmail",
                    Action = GdprFieldAction.Mask
                },
                new FieldGdprRule
                {
                    FieldName = "CustomerPhone",
                    Action = GdprFieldAction.Mask
                }
            });
        }

        internal static GdprPolicyRegistry CreateOrderRetrievalRegistry()
        {
            return CreateWithRetrievalRules("Order", new[]
            {
                new FieldGdprRule
                {
                    FieldName = "CustomerEmail",
                    Action = GdprFieldAction.Mask,
                    AllowedRoles = new[] { "Admin" }
                },
                new FieldGdprRule
                {
                    FieldName = "CustomerPhone",
                    Action = GdprFieldAction.Anonymize,
                    AllowedClaims = new Dictionary<string, string> { ["gdpr"] = "full" }
                }
            });
        }

        internal static GdprPolicyRegistry CreateMixedStorageRegistry(string entityName, int fieldCount)
        {
            var rules = new List<FieldGdprRule>();
            for (var i = 0; i < fieldCount; i++)
            {
                var action = (GdprFieldAction)(i % 4); // Exclude, Mask, Anonymize, Hash
                rules.Add(new FieldGdprRule
                {
                    FieldName = $"Property{i}",
                    Action = action
                });
            }

            return CreateWithStorageRules(entityName, rules);
        }

        private static void InjectPolicy(GdprPolicyRegistry registry, string entityName, EntityGdprPolicy policy)
        {
            var field = typeof(GdprPolicyRegistry).GetField("_policiesByName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = (IDictionary<string, EntityGdprPolicy>)field.GetValue(registry);
            dict[entityName] = policy;
        }
    }
}