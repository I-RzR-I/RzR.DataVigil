// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 08:04
// 
//  Last Modified By : RzR
//  Last Modified On : 16-05-2026 19:52
//  ***********************************************************************
//  <copyright file="FileStoreQueryBenchmarks.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Benchmarks.Helpers;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Storage.File;

#endregion

namespace RzR.DataVigil.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 2, iterationCount: 5)]
    public class FileStoreQueryBenchmarks
    {
        private string _basePath;
        private FileAuditStore _store;

        [Params(50, 200, 1000)] 
        public int TransactionCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _basePath = Path.Combine(Path.GetTempPath(), $"datavigil-bench-query-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_basePath);

            SeedFile(_basePath, TransactionCount, 2, 5);

            var options = new StorageOptions { FilePath = _basePath };
            var processor = new GdprProcessor(GdprRegistryFactory.CreateEmpty());
            _store = new FileAuditStore(options, processor);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_basePath))
                Directory.Delete(_basePath, true);
        }

        [Benchmark(Description = "Query: first 10 (no GDPR retrieval)")]
        public async Task QueryFirst10()
        {
            await _store.QueryAsync(new AuditTransactionQuery { Skip = 0, Take = 10 });
        }

        [Benchmark(Description = "Query: first 10 with GDPR retrieval context")]
        public async Task QueryFirst10WithGdpr()
        {
            var ctx = new GdprRetrievalContext
            {
                UserRoles = new List<string> { "Admin" },
                UserClaims = new Dictionary<string, string> { { "gdpr", "read" } }
            };

            await _store.QueryAsync(new AuditTransactionQuery { Skip = 0, Take = 10 }, ctx);
        }

        [Benchmark(Description = "Query: page 5 (skip 40, take 10)")]
        public async Task QueryPage5()
        {
            await _store.QueryAsync(new AuditTransactionQuery { Skip = 40, Take = 10 });
        }

        [Benchmark(Description = "AnonymizeByUser: scan all transactions")]
        public async Task AnonymizeByUser()
        {
            // Use a non-existent user so nothing is actually modified (measures scan cost)
            await _store.AnonymizeByUserAsync("non-existent-user-id");
        }

        [Benchmark(Description = "PurgeBefore: scan all transactions")]
        public async Task PurgeBefore()
        {
            // Use a date far in the past so nothing is purged (measures scan cost)
            await _store.PurgeBeforeAsync(DateTimeOffset.MinValue);
        }

        private static void SeedFile(string dir, int txnCount, int entriesPerTxn, int propsPerEntry)
        {
            var list = AuditDataFactory.CreateTransactionBatch(txnCount, entriesPerTxn, propsPerEntry);
            var dateKey = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
            var filePath = Path.Combine(dir, $"audit-{dateKey}.json");
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
    }
}