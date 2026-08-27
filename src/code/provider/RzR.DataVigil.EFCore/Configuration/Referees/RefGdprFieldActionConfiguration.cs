// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-08-27 10:00
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-27 16:13
// ***********************************************************************
//  <copyright file="RefGdprFieldActionConfiguration.cs" company="RzR SOFT & TECH">
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
    ///     EF Core Fluent API configuration for the RefGdprFieldAction reference entity.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{RzR.DataVigil.Abstractions.Models.Referees.RefGdprFieldAction}"/>
    /// =================================================================================================
    public class RefGdprFieldActionConfiguration : IEntityTypeConfiguration<RefGdprFieldAction>
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the schema.
        /// </summary>
        /// =================================================================================================
        private readonly string _schema;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="RefGdprFieldActionConfiguration"/> class.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// =================================================================================================
        public RefGdprFieldActionConfiguration(string schema)
        {
            _schema = schema;
        }

        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<RefGdprFieldAction> builder)
        {
            builder.ToTable("RefGdprFieldActions", _schema);

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.Name).IsRequired().HasMaxLength(64);
            builder.Property(e => e.Description).HasMaxLength(512);

            builder.HasIndex(e => e.Name).IsUnique();

            builder.HasData(
                new RefGdprFieldAction
                {
                    Id = (int)GdprFieldAction.Exclude,
                    Name = nameof(GdprFieldAction.Exclude),
                    Description = "Field is not stored or displayed at all."
                },
                new RefGdprFieldAction
                {
                    Id = (int)GdprFieldAction.Mask,
                    Name = nameof(GdprFieldAction.Mask),
                    Description = "Field is partially hidden, for example j***@mail.com."
                },
                new RefGdprFieldAction
                {
                    Id = (int)GdprFieldAction.Anonymize,
                    Name = nameof(GdprFieldAction.Anonymize),
                    Description = "Field is fully replaced with an anonymized placeholder."
                },
                new RefGdprFieldAction
                {
                    Id = (int)GdprFieldAction.Hash,
                    Name = nameof(GdprFieldAction.Hash),
                    Description = "Field is replaced with a SHA-256 hash for pseudonymization."
                },
                new RefGdprFieldAction
                {
                    Id = (int)GdprFieldAction.Custom,
                    Name = nameof(GdprFieldAction.Custom),
                    Description = "Field is transformed by a custom delegate."
                });
        }
    }
}
