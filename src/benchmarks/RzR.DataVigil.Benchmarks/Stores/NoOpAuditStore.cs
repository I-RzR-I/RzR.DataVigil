// ***********************************************************************
//  Assembly          : RzR.DataVigil.RzR.DataVigil.Benchmarks
//  Author            : RzR
//  Created           : 20-04-2026 13:04
// 
//  Last Modified By : RzR
//  Last Modified On : 20-05-2026 23:09
//  ***********************************************************************
//  <copyright file="NoOpAuditStore.cs" company="RzR SOFT & TECH">
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
using System.Threading;
using System.Threading.Tasks;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Gdpr;
using RzR.DataVigil.Abstractions.Models.Query;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

#endregion

namespace RzR.DataVigil.Benchmarks.Stores
{
    public sealed class NoOpAuditStore : IAuditStore
    {
        public Task<IResult> SaveAsync(AuditTransaction transaction, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IResult>(Result.Success());
        }

        public Task<IResult<IEnumerable<AuditTransaction>>> QueryAsync(AuditTransactionQuery filters,
            GdprRetrievalContext gdprRetrievalContext = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IResult<IEnumerable<AuditTransaction>>>(
                Result<IEnumerable<AuditTransaction>>.Success(Array.Empty<AuditTransaction>()));
        }

        public Task<IResult> AnonymizeByUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IResult>(Result.Success());
        }

        public Task<IResult> PurgeBeforeAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IResult>(Result.Success());
        }
    }
}