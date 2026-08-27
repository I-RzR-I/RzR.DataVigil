// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-08-27 10:00
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-27 16:13
// ***********************************************************************
//  <copyright file="RefAuditActionConfiguration.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Entries.Referees;


#endregion

namespace RzR.DataVigil.EFCore.Configuration.Referees
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     EF Core Fluent API configuration for the RefAuditAction reference entity.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{RzR.DataVigil.Abstractions.Models.Referees.RefAuditAction}"/>
    /// =================================================================================================
    public class RefAuditActionConfiguration : IEntityTypeConfiguration<RefAuditAction>
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the schema.
        /// </summary>
        /// =================================================================================================
        private readonly string _schema;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="RefAuditActionConfiguration"/> class.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// =================================================================================================
        public RefAuditActionConfiguration(string schema)
        {
            _schema = schema;
        }

        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<RefAuditAction> builder)
        {
            builder.ToTable("RefAuditActions", _schema);

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.Name).IsRequired().HasMaxLength(64);
            builder.Property(e => e.Description).HasMaxLength(512);

            builder.HasIndex(e => e.Name).IsUnique();

            builder.HasData(
                new RefAuditAction
                {
                    Id = (int)AuditAction.Create,
                    Name = nameof(AuditAction.Create),
                    Description = "A new entity was inserted."
                },
                new RefAuditAction
                {
                    Id = (int)AuditAction.Read,
                    Name = nameof(AuditAction.Read),
                    Description = "An existing entity was read."
                },
                new RefAuditAction
                {
                    Id = (int)AuditAction.Update,
                    Name = nameof(AuditAction.Update),
                    Description = "An existing entity was modified."
                },
                new RefAuditAction
                {
                    Id = (int)AuditAction.Delete,
                    Name = nameof(AuditAction.Delete),
                    Description = "An existing entity was deleted."
                });
        }
    }
}
