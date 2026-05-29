// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 15-05-2026 23:05
// 
//  Last Modified By : RzR
//  Last Modified On : 16-05-2026 19:52
//  ***********************************************************************
//  <copyright file="FileStoreBulkPerformanceBenchmarks.cs" company="RzR SOFT & TECH">
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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Benchmarks.Helpers;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Storage.File;

#endregion

namespace RzR.DataVigil.Benchmarks
{
    [MemoryDiagnoser]
    [SimpleJob(1, 0, 1)]
    public class FileStoreBulkPerformanceBenchmarks
    {
        private const int EntriesPerTransaction = 1;
        private const int PropertiesPerEntry = 5;

        private string _basePath;
        private GdprProcessor _emptyProcessor;
        private string _iterationPath;
        private List<AuditTransaction> _preparedTransactions;
        private FileAuditStore _store;

        [Params(1000, 10000)] 
        public int RecordCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _basePath = Path.Combine(Path.GetTempPath(), $"datavigil-bench-bulk-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_basePath);
            _emptyProcessor = new GdprProcessor(GdprRegistryFactory.CreateEmpty());
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_basePath))
                Directory.Delete(_basePath, true);
        }

        [IterationSetup(Target = nameof(StorePreparedBatch))]
        public void SetupStorePreparedBatch()
        {
            _preparedTransactions = AuditDataFactory.CreateTransactionBatch(
                RecordCount,
                EntriesPerTransaction,
                PropertiesPerEntry);

            CreateIterationStore(nameof(StorePreparedBatch));
        }

        [IterationCleanup(Target = nameof(StorePreparedBatch))]
        public void CleanupStorePreparedBatch()
        {
            CleanupIterationStore();
            _preparedTransactions = null;
        }

        [IterationSetup(Target = nameof(PrepareAndStoreBatch))]
        public void SetupPrepareAndStoreBatch()
        {
            CreateIterationStore(nameof(PrepareAndStoreBatch));
        }

        [IterationCleanup(Target = nameof(PrepareAndStoreBatch))]
        public void CleanupPrepareAndStoreBatch()
        {
            CleanupIterationStore();
        }

        [Benchmark(Description = "Prepare only: build N transactions")]
        public int PrepareBatchOnly()
        {
            var batch = AuditDataFactory.CreateTransactionBatch(
                RecordCount,
                EntriesPerTransaction,
                PropertiesPerEntry);

            return batch.Count;
        }

        [InvocationCount(1)]
        [Benchmark(Description = "Store only: persist pre-generated N transactions")]
        public async Task<int> StorePreparedBatch()
        {
            return await SaveBatchAsync(_store, _preparedTransactions).ConfigureAwait(false);
        }

        [InvocationCount(1)]
        [Benchmark(Description = "Prepare + Store: build and persist N transactions")]
        public async Task<int> PrepareAndStoreBatch()
        {
            var batch = AuditDataFactory.CreateTransactionBatch(
                RecordCount,
                EntriesPerTransaction,
                PropertiesPerEntry);

            return await SaveBatchAsync(_store, batch).ConfigureAwait(false);
        }

        private void CreateIterationStore(string scenario)
        {
            _iterationPath = Path.Combine(_basePath, $"{scenario}-{RecordCount}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_iterationPath);
            var options = new StorageOptions { FilePath = _iterationPath };
            _store = new FileAuditStore(options, _emptyProcessor);
        }

        private void CleanupIterationStore()
        {
            if (Directory.Exists(_iterationPath))
                Directory.Delete(_iterationPath, true);

            _iterationPath = null;
            _store = null;
        }

        private static async Task<int> SaveBatchAsync(FileAuditStore store, IList<AuditTransaction> batch)
        {
            var saved = 0;

            foreach (var transaction in batch)
            {
                var result = await store.SaveAsync(transaction).ConfigureAwait(false);
                if (!result.IsSuccess)
                    throw new InvalidOperationException("Unable to persist benchmark transaction.");

                saved++;
            }

            return saved;
        }
    }

    [MemoryDiagnoser]
    [BenchmarkCategory("Long")]
    [SimpleJob(1, 0, 1)]
    public class FileStoreBulkPerformanceLongBenchmarks
    {
        private const int EntriesPerTransaction = 1;
        private const int PropertiesPerEntry = 5;

        private string _basePath;
        private GdprProcessor _emptyProcessor;
        private string _iterationPath;
        private List<AuditTransaction> _preparedTransactions;
        private FileAuditStore _store;

        [Params(50000, 100000)] 
        public int RecordCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _basePath = Path.Combine(Path.GetTempPath(), $"datavigil-bench-bulk-long-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_basePath);
            _emptyProcessor = new GdprProcessor(GdprRegistryFactory.CreateEmpty());
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_basePath))
                Directory.Delete(_basePath, true);
        }

        [IterationSetup(Target = nameof(StorePreparedBatch))]
        public void SetupStorePreparedBatch()
        {
            _preparedTransactions = AuditDataFactory.CreateTransactionBatch(
                RecordCount,
                EntriesPerTransaction,
                PropertiesPerEntry);

            CreateIterationStore(nameof(StorePreparedBatch));
        }

        [IterationCleanup(Target = nameof(StorePreparedBatch))]
        public void CleanupStorePreparedBatch()
        {
            CleanupIterationStore();
            _preparedTransactions = null;
        }

        [IterationSetup(Target = nameof(PrepareAndStoreBatch))]
        public void SetupPrepareAndStoreBatch()
        {
            CreateIterationStore(nameof(PrepareAndStoreBatch));
        }

        [IterationCleanup(Target = nameof(PrepareAndStoreBatch))]
        public void CleanupPrepareAndStoreBatch()
        {
            CleanupIterationStore();
        }

        [Benchmark(Description = "Prepare only [LONG]: build N transactions")]
        public int PrepareBatchOnly()
        {
            var batch = AuditDataFactory.CreateTransactionBatch(
                RecordCount,
                EntriesPerTransaction,
                PropertiesPerEntry);

            return batch.Count;
        }

        [InvocationCount(1)]
        [Benchmark(Description = "Store only [LONG]: persist pre-generated N transactions")]
        public async Task<int> StorePreparedBatch()
        {
            return await SaveBatchAsync(_store, _preparedTransactions).ConfigureAwait(false);
        }

        [InvocationCount(1)]
        [Benchmark(Description = "Prepare + Store [LONG]: build and persist N transactions")]
        public async Task<int> PrepareAndStoreBatch()
        {
            var batch = AuditDataFactory.CreateTransactionBatch(
                RecordCount,
                EntriesPerTransaction,
                PropertiesPerEntry);

            return await SaveBatchAsync(_store, batch).ConfigureAwait(false);
        }

        private void CreateIterationStore(string scenario)
        {
            _iterationPath = Path.Combine(_basePath, $"{scenario}-{RecordCount}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_iterationPath);
            var options = new StorageOptions { FilePath = _iterationPath };
            _store = new FileAuditStore(options, _emptyProcessor);
        }

        private void CleanupIterationStore()
        {
            if (Directory.Exists(_iterationPath))
                Directory.Delete(_iterationPath, true);

            _iterationPath = null;
            _store = null;
        }

        private static async Task<int> SaveBatchAsync(FileAuditStore store, IList<AuditTransaction> batch)
        {
            var saved = 0;

            foreach (var transaction in batch)
            {
                var result = await store.SaveAsync(transaction).ConfigureAwait(false);
                if (!result.IsSuccess)
                    throw new InvalidOperationException("Unable to persist benchmark transaction.");

                saved++;
            }

            return saved;
        }
    }

    [MemoryDiagnoser]
    [SimpleJob(1, 0, 1)]
    public class FileStoreOneSecondThroughputBenchmarks
    {
        private const int SeedBatchSize = 2000;
        private const int EntriesPerTransaction = 1;
        private const int PropertiesPerEntry = 5;

        private string _basePath;
        private GdprProcessor _emptyProcessor;
        private string _iterationPath;
        private List<AuditTransaction> _seedBatch;
        private FileAuditStore _store;

        [GlobalSetup]
        public void Setup()
        {
            _basePath = Path.Combine(Path.GetTempPath(), $"datavigil-bench-throughput-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_basePath);

            _emptyProcessor = new GdprProcessor(GdprRegistryFactory.CreateEmpty());
            _seedBatch = AuditDataFactory.CreateTransactionBatch(
                SeedBatchSize,
                EntriesPerTransaction,
                PropertiesPerEntry);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_basePath))
                Directory.Delete(_basePath, true);
        }

        [IterationSetup]
        public void SetupIteration()
        {
            _iterationPath = Path.Combine(_basePath, $"iteration-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_iterationPath);

            var options = new StorageOptions { FilePath = _iterationPath };
            _store = new FileAuditStore(options, _emptyProcessor);
        }

        [IterationCleanup]
        public void CleanupIteration()
        {
            if (Directory.Exists(_iterationPath))
                Directory.Delete(_iterationPath, true);

            _iterationPath = null;
            _store = null;
        }

        [Benchmark(Description = "Store throughput: transactions persisted in 1 second")]
        public async Task<int> StoreThroughputOneSecond()
        {
            var stopwatch = Stopwatch.StartNew();
            var storedCount = 0;
            var index = 0;

            while (stopwatch.Elapsed < TimeSpan.FromSeconds(1))
            {
                var transaction = _seedBatch[index];
                var result = await _store.SaveAsync(transaction).ConfigureAwait(false);
                if (!result.IsSuccess)
                    throw new InvalidOperationException("Unable to persist benchmark transaction.");

                storedCount++;
                index++;

                if (index == _seedBatch.Count)
                    index = 0;
            }

            return storedCount;
        }
    }
}