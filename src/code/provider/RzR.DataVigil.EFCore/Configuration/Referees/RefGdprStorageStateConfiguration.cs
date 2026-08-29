// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-08-27 10:00
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-27 16:13
// ***********************************************************************
//  <copyright file="RefGdprStorageStateConfiguration.cs" company="RzR SOFT & TECH">
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
    ///     EF Core Fluent API configuration for the RefGdprStorageState reference entity.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{RzR.DataVigil.Abstractions.Models.Referees.RefGdprStorageState}"/>
    /// =================================================================================================
    public class RefGdprStorageStateConfiguration : IEntityTypeConfiguration<RefGdprStorageState>
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the schema.
        /// </summary>
        /// =================================================================================================
        private readonly string _schema;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="RefGdprStorageStateConfiguration"/> class.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// =================================================================================================
        public RefGdprStorageStateConfiguration(string schema)
        {
            _schema = schema;
        }

        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<RefGdprStorageState> builder)
        {
            builder.ToTable("RefGdprStorageStates", _schema);

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.Name).IsRequired().HasMaxLength(64);
            builder.Property(e => e.Description).HasMaxLength(512);

            builder.HasIndex(e => e.Name).IsUnique();

            builder.HasData(
                new RefGdprStorageState
                {
                    Id = (int)GdprStorageState.Original,
                    Name = nameof(GdprStorageState.Original),
                    Description = "Data is stored unmodified."
                },
                new RefGdprStorageState
                {
                    Id = (int)GdprStorageState.PartiallyProcessed,
                    Name = nameof(GdprStorageState.PartiallyProcessed),
                    Description = "Some fields have been masked, hashed or otherwise processed."
                },
                new RefGdprStorageState
                {
                    Id = (int)GdprStorageState.FullyAnonymized,
                    Name = nameof(GdprStorageState.FullyAnonymized),
                    Description = "All sensitive data has been fully anonymized."
                },
                new RefGdprStorageState
                {
                    Id = (int)GdprStorageState.Erased,
                    Name = nameof(GdprStorageState.Erased),
                    Description = "Data has been erased under right-to-erasure."
                });
        }
    }
}
