// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 08:04
// 
//  Last Modified By : RzR
//  Last Modified On : 16-05-2026 19:52
//  ***********************************************************************
//  <copyright file="FileStoreWriteBenchmarks.cs" company="RzR SOFT & TECH">
//      Copyright (c) RzR. All rights reserved.
//  </copyright>
//  <contact>
//      https://iamrzr.dev/contact
//  </contact>
//  <summary></summary>
//  ***********************************************************************

#region U S I N G

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RzR.DataVigil.Benchmarks.Helpers;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Storage.File;

#endregion

namespace RzR.DataVigil.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 2, iterationCount: 5)]
    public class FileStoreWriteBenchmarks
    {
        private string _basePath;
        private GdprProcessor _emptyProcessor;
        private StorageOptions _options;
        private GdprProcessor _orderProcessor;

        [GlobalSetup]
        public void Setup()
        {
            _basePath = Path.Combine(Path.GetTempPath(), $"datavigil-bench-write-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_basePath);

            _options = new StorageOptions { FilePath = _basePath };
            _emptyProcessor = new GdprProcessor(GdprRegistryFactory.CreateEmpty());
            _orderProcessor = new GdprProcessor(GdprRegistryFactory.CreateOrderStorageRegistry());
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_basePath))
                Directory.Delete(_basePath, true);
        }

        [Benchmark(Description = "Write: single transaction (5 props)")]
        public async Task WriteSingleTransaction()
        {
            var dir = Path.Combine(_basePath, $"single-{Guid.NewGuid():N}");
            var opts = new StorageOptions { FilePath = dir };
            var store = new FileAuditStore(opts, _emptyProcessor);
            var txn = AuditDataFactory.CreateTransaction(1, 5);
            await store.SaveAsync(txn);
        }

        [Benchmark(Description = "Write: 10 transactions sequentially")]
        public async Task Write10Transactions()
        {
            var dir = Path.Combine(_basePath, $"ten-{Guid.NewGuid():N}");
            var opts = new StorageOptions { FilePath = dir };
            var store = new FileAuditStore(opts, _emptyProcessor);

            for (var i = 0; i < 10; i++)
            {
                var txn = AuditDataFactory.CreateTransaction(1, 5);
                await store.SaveAsync(txn);
            }
        }

        [Benchmark(Description = "Write: append to file with 100 existing txns")]
        public async Task WriteAppendTo100()
        {
            var dir = Path.Combine(_basePath, $"append100-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            SeedFile(dir, 100, 1, 5);

            var opts = new StorageOptions { FilePath = dir };
            var store = new FileAuditStore(opts, _emptyProcessor);
            var txn = AuditDataFactory.CreateTransaction(1, 5);
            await store.SaveAsync(txn);
        }

        [Benchmark(Description = "Write: append to file with 1000 existing txns")]
        public async Task WriteAppendTo1000()
        {
            var dir = Path.Combine(_basePath, $"append1k-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            SeedFile(dir, 1000, 1, 5);

            var opts = new StorageOptions { FilePath = dir };
            var store = new FileAuditStore(opts, _emptyProcessor);
            var txn = AuditDataFactory.CreateTransaction(1, 5);
            await store.SaveAsync(txn);
        }

        [Benchmark(Description = "Write: Order entity with GDPR masking")]
        public async Task WriteOrderWithGdpr()
        {
            var dir = Path.Combine(_basePath, $"order-{Guid.NewGuid():N}");
            var opts = new StorageOptions { FilePath = dir };
            var store = new FileAuditStore(opts, _orderProcessor);
            var txn = AuditDataFactory.CreateOrderTransaction();
            await store.SaveAsync(txn);
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