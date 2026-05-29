// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 08:04
// 
//  Last Modified By : RzR
//  Last Modified On : 16-05-2026 19:52
//  ***********************************************************************
//  <copyright file="GdprProcessorBenchmarks.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Benchmarks.Helpers;
using RzR.DataVigil.Core.Gdpr;

#endregion

namespace RzR.DataVigil.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class GdprProcessorBenchmarks
    {
        private GdprProcessor _anonymizeProcessor;
        private GdprProcessor _customProcessor;
        private GdprProcessor _emptyProcessor;
        private GdprProcessor _excludeProcessor;
        private GdprProcessor _hashProcessor;
        private GdprProcessor _maskProcessor;
        private GdprProcessor _mixedProcessor;
        private GdprProcessor _orderProcessor;

        [Params(5, 10, 20)] 
        public int PropertyCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            // Single-action registries for the generic "Entity0"
            _maskProcessor = new GdprProcessor(
                GdprRegistryFactory.CreateWithStorageRules("Entity0", BuildRules(PropertyCount, GdprFieldAction.Mask)));

            _hashProcessor = new GdprProcessor(
                GdprRegistryFactory.CreateWithStorageRules("Entity0", BuildRules(PropertyCount, GdprFieldAction.Hash)));

            _anonymizeProcessor = new GdprProcessor(
                GdprRegistryFactory.CreateWithStorageRules("Entity0",
                    BuildRules(PropertyCount, GdprFieldAction.Anonymize)));

            _excludeProcessor = new GdprProcessor(
                GdprRegistryFactory.CreateWithStorageRules("Entity0",
                    BuildRules(PropertyCount, GdprFieldAction.Exclude)));

            _customProcessor = new GdprProcessor(
                GdprRegistryFactory.CreateWithStorageRules("Entity0", BuildCustomRules(PropertyCount)));

            // Mixed: cycles through Exclude/Mask/Anonymize/Hash
            _mixedProcessor = new GdprProcessor(
                GdprRegistryFactory.CreateMixedStorageRegistry("Entity0", PropertyCount));

            // No rules registered
            _emptyProcessor = new GdprProcessor(GdprRegistryFactory.CreateEmpty());

            // Order entity (realistic: 2 masked fields out of 5)
            _orderProcessor = new GdprProcessor(GdprRegistryFactory.CreateOrderStorageRegistry());
        }

        [Benchmark(Description = "Mask (all fields)")]
        public AuditEntry ApplyMask()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);
            var (result, _, _) = _maskProcessor.ApplyStoragePolicies(entry);

            return result;
        }

        [Benchmark(Description = "Hash/SHA-256 (all fields)")]
        public AuditEntry ApplyHash()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);
            var (result, _, _) = _hashProcessor.ApplyStoragePolicies(entry);

            return result;
        }

        [Benchmark(Description = "Anonymize (all fields)")]
        public AuditEntry ApplyAnonymize()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);
            var (result, _, _) = _anonymizeProcessor.ApplyStoragePolicies(entry);

            return result;
        }

        [Benchmark(Description = "Exclude (all fields)")]
        public AuditEntry ApplyExclude()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);
            var (result, _, _) = _excludeProcessor.ApplyStoragePolicies(entry);

            return result;
        }

        [Benchmark(Description = "Custom transform (all fields)")]
        public AuditEntry ApplyCustom()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);
            var (result, _, _) = _customProcessor.ApplyStoragePolicies(entry);

            return result;
        }

        [Benchmark(Description = "Mixed actions (Exclude/Mask/Anon/Hash)")]
        public AuditEntry ApplyMixed()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);
            var (result, _, _) = _mixedProcessor.ApplyStoragePolicies(entry);

            return result;
        }

        [Benchmark(Description = "No GDPR rules (passthrough)")]
        public AuditEntry ApplyNone()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);
            var (result, _, _) = _emptyProcessor.ApplyStoragePolicies(entry);

            return result;
        }

        [Benchmark(Description = "Order entity (2/5 fields masked)")]
        public AuditEntry ApplyOrderRealistic()
        {
            var txn = AuditDataFactory.CreateOrderTransaction();
            var entry = ((List<AuditEntry>)txn.Entries)[0];
            // We need to set Properties as List for indexing but the entry already has them
            var (result, _, _) = _orderProcessor.ApplyStoragePolicies(entry);

            return result;
        }

        private static List<FieldGdprRule> BuildRules(int count, GdprFieldAction action)
        {
            var rules = new List<FieldGdprRule>(count);
            for (var i = 0; i < count; i++)
                rules.Add(new FieldGdprRule
                {
                    FieldName = $"Property{i}",
                    Action = action
                });

            return rules;
        }

        private static List<FieldGdprRule> BuildCustomRules(int count)
        {
            var rules = new List<FieldGdprRule>(count);
            for (var i = 0; i < count; i++)
                rules.Add(new FieldGdprRule
                {
                    FieldName = $"Property{i}",
                    Action = GdprFieldAction.Custom,
                    CustomTransformer = val => $"CUSTOM_{val.Length}"
                });

            return rules;
        }
    }
}