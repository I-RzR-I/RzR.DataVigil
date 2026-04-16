// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfMongoDb
//  Author           : RzR
//  Created On       : 2026-04-15 11:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 16:04
// ***********************************************************************
//  <copyright file="AuditMongoDbContext.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.Storage.EfMongoDb
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     MongoDB-specific audit DbContext.
    ///     Does NOT inherit <c>AuditDbContextBase</c> because the base class applies
    ///     relational-only configuration (ToTable, HasDefaultSchema, FK relationships,
    ///     shadow properties, ValueConverters) that is incompatible with the MongoDB
    ///     EF Core provider. Instead, maps audit entities as a single MongoDB collection
    ///     with embedded documents for entries and properties — the natural document
    ///     model for audit data.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.DbContext" />
    /// =================================================================================================
    public class AuditMongoDbContext : DbContext
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditMongoDbContext" /> class.
        /// </summary>
        /// <param name="options">The MongoDB DbContext options.</param>
        /// =================================================================================================
        public AuditMongoDbContext(DbContextOptions<AuditMongoDbContext> options)
            : base(options)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the audit transactions collection.
        /// </summary>
        /// <value>
        ///     The audit transactions.
        /// </value>
        /// =================================================================================================
        public DbSet<AuditTransaction> AuditTransactions { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AuditTransaction>(builder =>
            {
                builder.ToCollection("audit_transactions");
                builder.HasKey(t => t.Id);

                builder.OwnsMany(t => t.Entries, entry =>
                {
                    entry.OwnsMany(e => e.Properties);
                });
            });
        }
    }
}