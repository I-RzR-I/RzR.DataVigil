// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-14 18:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 18:55
// ***********************************************************************
//  <copyright file="AuditReadInterceptorHelper.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.RegularExpressions;
using DomainCommonExtensions.ArraysExtensions;
using DomainCommonExtensions.DataTypeExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RzR.DataVigil.Abstractions.Models.Entries;

#endregion

namespace RzR.DataVigil.EFCore.Helpers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Parses EF Core-generated SQL statements to extract table names, primary-key values,
    ///     and selected columns for Read audit tracking.
    ///     Supports FROM/JOIN clauses with double-quoted, bracket-quoted, and unquoted identifiers.
    /// </summary>
    /// =================================================================================================
    internal static class AuditReadInterceptorHelper
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable)
        ///     Regex to extract table references from EF-generated SQL statements.
        ///     Covers FROM and JOIN clauses with double-quote, bracket, or unquoted identifiers.
        ///     Patterns matched:
        ///       FROM "schema"."Table"   /  FROM [schema].[Table]   /  FROM schema.Table
        ///       JOIN "schema"."Table"   /  JOIN [schema].[Table]   /  JOIN schema.Table
        ///       FROM "Table"            /  FROM [Table]            /  FROM Table
        /// </summary>
        /// =================================================================================================
        private static readonly Regex TableNamePattern = new Regex(
            @"(?:FROM|JOIN)\s+(?:""(\w+)""|(\w+)|\[(\w+)\])\.(?:""(\w+)""|(\w+)|\[(\w+)\])|(?:FROM|JOIN)\s+(?:""(\w+)""|(\w+)|\[(\w+)\])(?=\s|$|;)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable)
        ///     Regex to extract the primary key filter value from EF-generated WHERE clauses.
        ///     Matches directly after WHERE (non-greedy, single-line segment) for a column named "Id"
        ///     in double-quote, bracket, or unquoted form.
        ///     Patterns matched:
        ///       WHERE "t"."Id" = @__id_0   /  WHERE [t].[Id] = @p0   /  WHERE t.Id = @p0
        /// </summary>
        /// =================================================================================================
        private static readonly Regex WhereIdPattern = new Regex(
            @"WHERE\s+(?:""?\w+""?|[\w\[\]]+)\.(?:""Id""|Id|\[Id\])\s*=\s*(@[\w]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable)
        ///     Regex to extract selected column names from EF-generated SELECT statements.
        ///     Handles double-quote, bracket, or unquoted identifiers.
        ///     Patterns matched:
        ///       "t"."ColumnName"  /  [t].[ColumnName]  /  t.ColumnName
        /// </summary>
        /// =================================================================================================
        private static readonly Regex ColumnPattern = new Regex(
            @"(?:""?\w+""?|\[\w+\])\.(?:""(\w+)""|(\w+)|\[(\w+)\])",
            RegexOptions.Compiled);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Extract the entity ID value from the SQL WHERE clause by resolving the parameter.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <param name="parameters">Options for controlling the operation.</param>
        /// <returns>
        ///     The extracted entity identifier.
        /// </returns>
        /// =================================================================================================
        internal static string ExtractEntityId(string sql, DbParameterCollection parameters)
        {
            var match = WhereIdPattern.Match(sql);
            if (!match.Success)
                return null;

            var paramName = match.Groups[1].Value;

            for (var i = 0; i < parameters.Count; i++)
                if (string.Equals(parameters[i].ParameterName, paramName, StringComparison.OrdinalIgnoreCase))
                    return parameters[i].Value?.ToString();

            return null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Extracts column names from the SELECT clause (everything between SELECT and FROM).
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <returns>
        ///     A HashSet&lt;string&gt;
        /// </returns>
        /// =================================================================================================
        internal static HashSet<string> ParseSelectedColumns(string sql)
        {
            var fromIndex = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
            if (fromIndex < 0)
                return null;

            var selectClause = sql.Substring(0, fromIndex);
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in ColumnPattern.Matches(selectClause))
            {
                // Groups: 1 = double-quoted, 2 = unquoted, 3 = bracket-quoted
                var col = match.Groups[1].Success ? match.Groups[1].Value
                    : match.Groups[2].Success ? match.Groups[2].Value
                    : match.Groups[3].Value;

                columns.Add(col);
            }

            return columns.Count > 0 ? columns : null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Builds AuditEntryProperty records for each selected column that maps to an entity
        ///     property. When <paramref name="includeValues"/> is <c>true</c>, resolves parameter
        ///     values from the command and stores them in <see cref="AuditEntryProperty.NewValue"/>.
        /// </summary>
        /// <param name="entityType">Type of the entity.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        /// <param name="auditEntry">The audit entry.</param>
        /// <param name="parameters">The SQL command parameters (may be <c>null</c>).</param>
        /// <param name="includeValues">Whether to populate property values from command parameters.</param>
        /// =================================================================================================
        internal static void BuildReadProperties(
            IEntityType entityType,
            HashSet<string> selectedColumns,
            AuditEntry auditEntry,
            DbParameterCollection parameters = null,
            bool includeValues = false)
        {
            foreach (var property in entityType.GetProperties())
            {
                var columnName = PropertyMetadataHelper.GetColumnName(property);
                if (columnName.IsMissing())
                    continue;

                var propertyName = PropertyMetadataHelper.GetName(property);

                // Match by column name or CLR property name
                if (!selectedColumns.Contains(columnName) && !selectedColumns.Contains(propertyName))
                    continue;

                string resolvedValue = null;
                if (includeValues && parameters != null)
                    resolvedValue = ResolveParameterValue(parameters, propertyName, columnName);

                auditEntry.Properties.Add(new AuditEntryProperty
                {
                    PropertyName = propertyName,
                    PropertyType = PropertyMetadataHelper.GetCleanTypeName(PropertyMetadataHelper.GetClrType(property)),
                    OldValue = null,
                    NewValue = resolvedValue
                });
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Attempts to find a SQL parameter whose name contains the property or column name
        ///     and returns its value as a string.
        /// </summary>
        /// <param name="parameters">The SQL command parameters.</param>
        /// <param name="propertyName">CLR property name to match.</param>
        /// <param name="columnName">Database column name to match.</param>
        /// <returns>
        ///     The parameter value as a string, or <c>null</c> if no match is found.
        /// </returns>
        /// =================================================================================================
        private static string ResolveParameterValue(
            DbParameterCollection parameters,
            string propertyName,
            string columnName)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                var paramName = parameters[i].ParameterName;
                if (paramName.IsPresent()
                    && (paramName.IndexOf(propertyName, StringComparison.OrdinalIgnoreCase) >= 0
                        || paramName.IndexOf(columnName, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return parameters[i].Value?.ToString();
                }
            }

            return null;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Extracts (schema, table) pairs from a SQL SELECT statement. Schema may be null if not
        ///     qualified.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <returns>
        ///     A IReadOnlyList&lt;(string Schema,string Table)&gt;
        /// </returns>
        /// =================================================================================================
        internal static IReadOnlyList<(string Schema, string Table)> ParseTableNames(string sql)
        {
            var results = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var match in TableNamePattern.Matches(sql).NotNull())
            {
                string schema, table;

                if (match.Groups[4].Success || match.Groups[5].Success || match.Groups[6].Success)
                {
                    // schema.table form — groups 1-3 = schema, groups 4-6 = table
                    schema = match.Groups[1].Success ? match.Groups[1].Value
                        : match.Groups[2].Success ? match.Groups[2].Value
                        : match.Groups[3].Value;

                    table = match.Groups[4].Success ? match.Groups[4].Value
                        : match.Groups[5].Success ? match.Groups[5].Value
                        : match.Groups[6].Value;
                }
                else
                {
                    // table-only form — groups 7-9
                    schema = null;
                    table = match.Groups[7].Success ? match.Groups[7].Value
                        : match.Groups[8].Success ? match.Groups[8].Value
                        : match.Groups[9].Value;
                }

                var key = schema.IsPresent() ? $"{schema}.{table}" : table;
                if (seen.Add(key))
                    results.Add((schema, table));
            }

            return results;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Resolves the EF Core IEntityType from a table name by checking the model metadata.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="table">The table.</param>
        /// <returns>
        ///     An IEntityType.
        /// </returns>
        /// =================================================================================================
        internal static IEntityType ResolveEntityType(
            DbContext context, string schema, string table)
        {
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var tableName = PropertyMetadataHelper.GetTableName(entityType);
                var tableSchema = PropertyMetadataHelper.GetSchema(entityType);

                if (string.Equals(tableName, table, StringComparison.OrdinalIgnoreCase))
                {
                    // If schema was parsed, match it too
                    if (schema.IsPresent())
                    {
                        if (string.Equals(tableSchema, schema, StringComparison.OrdinalIgnoreCase))
                            return entityType;
                    }
                    else
                    {
                        return entityType;
                    }
                }
            }

            return null;
        }
    }
}