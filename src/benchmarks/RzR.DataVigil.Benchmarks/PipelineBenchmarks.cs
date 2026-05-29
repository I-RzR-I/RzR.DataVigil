// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 08:04
// 
//  Last Modified By : RzR
//  Last Modified On : 16-05-2026 19:52
//  ***********************************************************************
//  <copyright file="PipelineBenchmarks.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RzR.DataVigil.Benchmarks.Helpers;
using RzR.DataVigil.Benchmarks.Providers;
using RzR.DataVigil.Benchmarks.Resolvers;
using RzR.DataVigil.Benchmarks.Stores;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Pipeline;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 10)]
    public class PipelineBenchmarks
    {
        private AuditPipeline _pipelineNoGdpr;
        private AuditPipeline _pipelineWithGdpr;

        [Params(1, 5, 20)]
        public int EntryCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var userResolver = new StubUserResolver();
            var sourceResolver = new StubSourceResolver();
            var correlationProvider = new StubCorrelationProvider();

            var emptyProcessor = new GdprProcessor(GdprRegistryFactory.CreateEmpty());
            var gdprProcessor = new GdprProcessor(
                GdprRegistryFactory.CreateMixedStorageRegistry("Entity0", 10));

            var store = new NoOpAuditStore();

            _pipelineNoGdpr = new AuditPipeline(
                userResolver, sourceResolver, correlationProvider, emptyProcessor, store);

            _pipelineWithGdpr = new AuditPipeline(
                userResolver, sourceResolver, correlationProvider, gdprProcessor, store);
        }

        [Benchmark(Description = "Pipeline: no GDPR")]
        public async Task<IResult> ProcessNoGdpr()
        {
            var txn = AuditDataFactory.CreateTransaction(EntryCount, 10);

            return await _pipelineNoGdpr.ProcessAsync(txn);
        }

        [Benchmark(Description = "Pipeline: with GDPR (mixed actions)")]
        public async Task<IResult> ProcessWithGdpr()
        {
            var txn = AuditDataFactory.CreateTransaction(EntryCount, 10);

            return await _pipelineWithGdpr.ProcessAsync(txn);
        }

        [Benchmark(Description = "Pipeline: Order entity (realistic)")]
        public async Task<IResult> ProcessOrderRealistic()
        {
            var pipeline = new AuditPipeline(
                new StubUserResolver(),
                new StubSourceResolver(),
                new StubCorrelationProvider(),
                new GdprProcessor(GdprRegistryFactory.CreateOrderStorageRegistry()),
                new NoOpAuditStore());

            var txn = AuditDataFactory.CreateOrderTransaction();

            return await pipeline.ProcessAsync(txn);
        }
    }
}