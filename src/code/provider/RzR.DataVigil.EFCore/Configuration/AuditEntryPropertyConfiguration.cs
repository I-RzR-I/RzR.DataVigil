// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-11 02:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 18:03
// ***********************************************************************
//  <copyright file="AuditEntryPropertyConfiguration.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.EFCore.Configuration
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     EF Core Fluent API configuration for the AuditEntryProperty entity. 
    ///     Uses shadow properties for Id (PK) and AuditEntryId (FK)
    ///     to keep the Abstractions model free of persistence concerns.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{RzR.DataVigil.Abstractions.Models.Entries.AuditEntryProperty}"/>
    /// =================================================================================================
    public class AuditEntryPropertyConfiguration : IEntityTypeConfiguration<AuditEntryProperty>
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the schema.
        /// </summary>
        /// =================================================================================================
        private readonly string _schema;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditEntryPropertyConfiguration"/> class.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// =================================================================================================
        public AuditEntryPropertyConfiguration(string schema)
        {
            _schema = schema;
        }

        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<AuditEntryProperty> builder)
        {
            builder.ToTable("AuditEntryProperties", _schema);

            // Shadow PK
            builder.Property<Guid>("Id");
            builder.HasKey("Id");
            builder.Property<Guid>("Id").ValueGeneratedOnAdd();

            // Shadow FK — set by EF via relationship on AuditEntry
            builder.Property<Guid>("AuditEntryId");

            builder.Property(e => e.PropertyName).HasMaxLength(256);
            builder.Property(e => e.PropertyType).HasMaxLength(256);
            builder.Property(e => e.OldValue); //.HasColumnType("nvarchar(max)");
            builder.Property(e => e.NewValue); //.HasColumnType("nvarchar(max)");

            builder.HasIndex("AuditEntryId");
        }
    }
}