// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-08-27 10:00
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-27 16:13
// ***********************************************************************
//  <copyright file="RefAuditUserSourceConfiguration.cs" company="RzR SOFT & TECH">
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
    ///     EF Core Fluent API configuration for the RefAuditUserSource reference entity.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{RzR.DataVigil.Abstractions.Models.Referees.RefAuditUserSource}"/>
    /// =================================================================================================
    public class RefAuditUserSourceConfiguration : IEntityTypeConfiguration<RefAuditUserSource>
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the schema.
        /// </summary>
        /// =================================================================================================
        private readonly string _schema;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="RefAuditUserSourceConfiguration"/> class.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// =================================================================================================
        public RefAuditUserSourceConfiguration(string schema)
        {
            _schema = schema;
        }

        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<RefAuditUserSource> builder)
        {
            builder.ToTable("RefAuditUserSources", _schema);

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.Name).IsRequired().HasMaxLength(64);
            builder.Property(e => e.Description).HasMaxLength(512);

            builder.HasIndex(e => e.Name).IsUnique();

            builder.HasData(
                new RefAuditUserSource
                {
                    Id = (int)AuditUserSource.Unspecified,
                    Name = nameof(AuditUserSource.Unspecified),
                    Description = "Resolver returned a user but did not declare where that identity came from. The recorded actor is real and can be trusted; only its provenance is undeclared. Contrast with Unresolved, where attribution itself failed."
                },
                new RefAuditUserSource
                {
                    Id = (int)AuditUserSource.Unresolved,
                    Name = nameof(AuditUserSource.Unresolved),
                    Description = "No resolver could be consulted or resolution failed; attribution is unknown."
                },
                new RefAuditUserSource
                {
                    Id = (int)AuditUserSource.Anonymous,
                    Name = nameof(AuditUserSource.Anonymous),
                    Description = "Resolution succeeded and there is genuinely no user for this action."
                },
                new RefAuditUserSource
                {
                    Id = (int)AuditUserSource.ScopeContext,
                    Name = nameof(AuditUserSource.ScopeContext),
                    Description = "User taken from an IAuditScopeContext manual override."
                },
                new RefAuditUserSource
                {
                    Id = (int)AuditUserSource.HttpContext,
                    Name = nameof(AuditUserSource.HttpContext),
                    Description = "User extracted from the ASP.NET Core HttpContext authenticated principal."
                },
                new RefAuditUserSource
                {
                    Id = (int)AuditUserSource.ThreadPrincipal,
                    Name = nameof(AuditUserSource.ThreadPrincipal),
                    Description = "User extracted from Thread.CurrentPrincipal."
                });
        }
    }
}
