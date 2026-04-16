// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-14 13:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 18:08
// ***********************************************************************
//  <copyright file="AuditTransactionConfiguration.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.EFCore.Configuration
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     EF Core Fluent API configuration for the AuditTransaction entity.
    /// </summary>
    /// <seealso cref="T:Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{RzR.DataVigil.Abstractions.Models.Entries.AuditTransaction}"/>
    /// =================================================================================================
    public class AuditTransactionConfiguration : IEntityTypeConfiguration<AuditTransaction>
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the schema.
        /// </summary>
        /// =================================================================================================
        private readonly string _schema;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="AuditTransactionConfiguration"/> class.
        /// </summary>
        /// <param name="schema">The schema.</param>
        /// =================================================================================================
        public AuditTransactionConfiguration(string schema)
        {
            _schema = schema;
        }

        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<AuditTransaction> builder)
        {
            builder.ToTable("AuditTransactions", _schema);

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.Timestamp).IsRequired();
            builder.Property(t => t.UserId).HasMaxLength(256);
            builder.Property(t => t.UserName).HasMaxLength(256);
            builder.Property(t => t.IpAddress).HasMaxLength(64);
            builder.Property(t => t.CorrelationId).HasMaxLength(256);
            builder.Property(t => t.TraceId).HasMaxLength(256);
            builder.Property(t => t.Source).HasMaxLength(512);
            builder.Property(t => t.GdprState).IsRequired();

            var metadataConverter = new ValueConverter<IDictionary<string, string>, string>(
                v => v != null && v.Count > 0
                    ? JsonSerializer.Serialize(v, (JsonSerializerOptions)null)
                    : null,
                v => !string.IsNullOrEmpty(v)
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null)
                    : new Dictionary<string, string>());

            var metadataComparer = new ValueComparer<IDictionary<string, string>>(
                (left, right) => DictionariesAreEqual(left, right),
                d => GetDictionaryHashCode(d),
                d => d == null ? (IDictionary<string, string>)null : new Dictionary<string, string>(d));

            builder.Property(t => t.Metadata)
                .HasConversion(metadataConverter, metadataComparer);

            builder.HasMany(t => t.Entries)
                .WithOne()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(t => t.Timestamp);
            builder.HasIndex(t => t.UserId);
            builder.HasIndex(t => t.CorrelationId);
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Dictionaries are equal.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        ///     True if it succeeds, false if it fails.
        /// </returns>
        /// =================================================================================================
        private static bool DictionariesAreEqual(
            IDictionary<string, string> left,
            IDictionary<string, string> right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            if (left.Count != right.Count)
                return false;

            foreach (var kvp in left)
            {
                if (!right.TryGetValue(kvp.Key, out var rightValue))
                    return false;

                if (kvp.Value != rightValue)
                    return false;
            }

            return true;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets dictionary hash code.
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <returns>
        ///     The dictionary hash code.
        /// </returns>
        /// =================================================================================================
        private static int GetDictionaryHashCode(IDictionary<string, string> dictionary)
        {
            if (dictionary == null)
                return 0;

            var hashCode = 0;

            foreach (var kvp in dictionary) 
                hashCode = HashCode.Combine(hashCode, kvp.Key, kvp.Value);

            return hashCode;
        }
    }
}