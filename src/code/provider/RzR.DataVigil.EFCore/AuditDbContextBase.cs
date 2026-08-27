// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-11 02:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-08-27 16:13
// ***********************************************************************
//  <copyright file="AuditDbContextBase.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.EntityFrameworkCore;
using RzR.DataVigil.Abstractions.Models.Entries;
using RzR.DataVigil.Abstractions.Models.Entries.Referees;
using RzR.DataVigil.EFCore.Configuration;
using RzR.DataVigil.EFCore.Configuration.Referees;

#endregion

namespace RzR.DataVigil.EFCore
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Base DbContext for audit storage. Configures AuditEntries and AuditEntryProperties tables
    ///     under a configurable schema.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.DbContext"/>
    /// =================================================================================================
    public abstract class AuditDbContextBase : DbContext
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the schema.
        /// </summary>
        /// =================================================================================================
        private readonly string _schema;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditDbContextBase"/> class.
        /// </summary>
        /// <param name="options">Options for controlling the operation.</param>
        /// <param name="schema">(Optional) The schema.</param>
        /// =================================================================================================
        protected AuditDbContextBase(DbContextOptions options, string schema = "audit")
            : base(options)
        {
            _schema = schema ?? "audit";
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the audit transactions.
        /// </summary>
        /// <value>
        ///     The audit transactions.
        /// </value>
        /// =================================================================================================
        public DbSet<AuditTransaction> AuditTransactions { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the audit entries.
        /// </summary>
        /// <value>
        ///     The audit entries.
        /// </value>
        /// =================================================================================================
        public DbSet<AuditEntry> AuditEntries { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the audit entry properties.
        /// </summary>
        /// <value>
        ///     The audit entry properties.
        /// </value>
        /// =================================================================================================
        public DbSet<AuditEntryProperty> AuditEntryProperties { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the audit action reference rows.
        /// </summary>
        /// <value>
        ///     The audit action reference rows.
        /// </value>
        /// =================================================================================================
        public DbSet<RefAuditAction> RefAuditActions { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the audit user source reference rows.
        /// </summary>
        /// <value>
        ///     The audit user source reference rows.
        /// </value>
        /// =================================================================================================
        public DbSet<RefAuditUserSource> RefAuditUserSources { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the GDPR field action reference rows.
        /// </summary>
        /// <value>
        ///     The GDPR field action reference rows.
        /// </value>
        /// =================================================================================================
        public DbSet<RefGdprFieldAction> RefGdprFieldActions { get; set; }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the GDPR storage state reference rows.
        /// </summary>
        /// <value>
        ///     The GDPR storage state reference rows.
        /// </value>
        /// =================================================================================================
        public DbSet<RefGdprStorageState> RefGdprStorageStates { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(_schema);

            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new AuditTransactionConfiguration(_schema));
            modelBuilder.ApplyConfiguration(new AuditEntryConfiguration(_schema));
            modelBuilder.ApplyConfiguration(new AuditEntryPropertyConfiguration(_schema));

            modelBuilder.ApplyConfiguration(new RefAuditActionConfiguration(_schema));
            modelBuilder.ApplyConfiguration(new RefAuditUserSourceConfiguration(_schema));
            modelBuilder.ApplyConfiguration(new RefGdprFieldActionConfiguration(_schema));
            modelBuilder.ApplyConfiguration(new RefGdprStorageStateConfiguration(_schema));
        }
    }
}