// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 08:04
// 
//  Last Modified By : RzR
//  Last Modified On : 16-05-2026 19:52
//  ***********************************************************************
//  <copyright file="GdprRetrievalBenchmarks.cs" company="RzR SOFT & TECH">
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
    public class GdprRetrievalBenchmarks
    {
        private GdprRetrievalContext _adminContext;
        private GdprRetrievalContext _noAccessContext;
        private GdprProcessor _processor;

        [Params(5, 10, 20)] 
        public int PropertyCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var rules = new List<FieldGdprRule>();
            for (var i = 0; i < PropertyCount; i++)
                rules.Add(new FieldGdprRule
                {
                    FieldName = $"Property{i}",
                    Action = i % 2 == 0 ? GdprFieldAction.Mask : GdprFieldAction.Anonymize,
                    AllowedRoles = new[] { "Admin" }
                });

            _processor = new GdprProcessor(
                GdprRegistryFactory.CreateWithRetrievalRules("Entity0", rules));

            _adminContext = new GdprRetrievalContext
            {
                UserRoles = new[] { "Admin" },
                UserClaims = new Dictionary<string, string>()
            };

            _noAccessContext = new GdprRetrievalContext
            {
                UserRoles = new[] { "Viewer" },
                UserClaims = new Dictionary<string, string>()
            };
        }

        [Benchmark(Description = "Retrieval: Admin (bypass all masks)")]
        public AuditEntry RetrievalAdmin()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);

            return _processor.ApplyRetrievalPolicies(entry, _adminContext);
        }

        [Benchmark(Description = "Retrieval: No access (mask all)")]
        public AuditEntry RetrievalNoAccess()
        {
            var entry = AuditDataFactory.CreateEntry("Entity0", PropertyCount);

            return _processor.ApplyRetrievalPolicies(entry, _noAccessContext);
        }

        [Benchmark(Description = "Retrieval: Order entity (Admin)")]
        public AuditEntry RetrievalOrderAdmin()
        {
            var processor = new GdprProcessor(GdprRegistryFactory.CreateOrderRetrievalRegistry());
            var txn = AuditDataFactory.CreateOrderTransaction();
            var entry = ((List<AuditEntry>)txn.Entries)[0];
            return processor.ApplyRetrievalPolicies(entry, _adminContext);
        }

        [Benchmark(Description = "Retrieval: Order entity (no access)")]
        public AuditEntry RetrievalOrderNoAccess()
        {
            var processor = new GdprProcessor(GdprRegistryFactory.CreateOrderRetrievalRegistry());
            var txn = AuditDataFactory.CreateOrderTransaction();
            var entry = ((List<AuditEntry>)txn.Entries)[0];

            return processor.ApplyRetrievalPolicies(entry, _noAccessContext);
        }
    }
}