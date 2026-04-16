// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-11 02:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 18:02
// ***********************************************************************
//  <copyright file="AuditEntryConfiguration.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.EFCore.Configuration
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     EF Core Fluent API configuration for the AuditEntry entity.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{RzR.DataVigil.Abstractions.Models.Entries.AuditEntry}"/>
    /// =================================================================================================
    public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the schema.
        /// </summary>
        /// =================================================================================================
        private readonly string _schema;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditEntryConfiguration"/> class.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// =================================================================================================
        public AuditEntryConfiguration(string schema)
        {
            _schema = schema;
        }

        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<AuditEntry> builder)
        {
            builder.ToTable("AuditEntries", _schema);

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.TransactionId).IsRequired();
            builder.Property(e => e.Action).IsRequired();

            builder.Property(e => e.EntityName).HasMaxLength(256);
            builder.Property(e => e.EntityId).HasMaxLength(256);
            builder.Property(e => e.EntityTypeName).HasMaxLength(512);

            builder.HasMany(e => e.Properties)
                .WithOne()
                .HasForeignKey("AuditEntryId")
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(e => e.TransactionId);
            builder.HasIndex(e => e.EntityName);
        }
    }
}