// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfPostgreSql
//  Author           : RzR
//  Created On       : 2026-08-29 08:10
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-29 08:10
// ***********************************************************************
//  <copyright file="AuditPostgreSqlServerVersionDiagnostic.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using RzR.DataVigil.Core.Options;

#endregion

namespace RzR.DataVigil.Storage.EfPostgreSql.Diagnostics
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Reports whether the PostgreSQL server backing the audit database is a version this library is
    ///     tested against.
    /// </summary>
    /// =================================================================================================
    public static class AuditPostgreSqlServerVersionDiagnostic
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the lowest PostgreSQL major version this library is tested against.
        /// </summary>
        /// =================================================================================================
        private const int MinimumTestedMajorVersion = 13;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Throws when the connected PostgreSQL server is older than the minimum tested major version.
        ///     Intended to run BEFORE any migration is applied, so that a consumer who opted in to strict
        ///     version checking fails fast instead of leaving the audit database half-migrated.
        /// </summary>
        /// <param name="context">The audit context whose connection is inspected.</param>
        /// <exception cref="NotSupportedException">
        ///     Thrown when the server's major version is below the minimum tested major version.
        /// </exception>
        /// =================================================================================================
        public static void ThrowIfServerVersionUnsupported(AuditPostgreSqlDbContext context)
        {
            if (context == null)
                return;

            var version = TryReadServerVersion(context);

            if (version == null || version.Major >= MinimumTestedMajorVersion)
                return;

            throw new NotSupportedException(
                $"The audit PostgreSQL server reports major version {version.Major}, " +
                $"below the minimum tested version {MinimumTestedMajorVersion}. " +
                "No migration has been applied. Upgrade the server, or clear " +
                $"{nameof(StorageOptions.ThrowOnUnsupportedPostgreSqlVersion)} " +
                "to downgrade this to a startup warning.");
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Logs whether the connected PostgreSQL server is a tested version. Intended to run as the
        ///     final step of the migration routine, after <c>Migrate()</c> has succeeded.
        /// </summary>
        /// <param name="context">The audit context whose connection is inspected.</param>
        /// <param name="loggerFactory">
        ///     The logger factory used to create the diagnostic logger. A null factory makes this a no-op.
        /// </param>
        /// =================================================================================================
        public static void LogServerVersionCompatibility(
            AuditPostgreSqlDbContext context,
            ILoggerFactory loggerFactory)
        {
            try
            {
                if (context == null || loggerFactory == null)
                    return;

                var logger = loggerFactory.CreateLogger(typeof(AuditPostgreSqlServerVersionDiagnostic).FullName);
                var version = TryReadServerVersion(context);

                if (version == null)
                {
                    logger.LogDebug(
                        "The audit PostgreSQL server version could not be determined. Skipping the version advisory.");

                    return;
                }

                if (version.Major < MinimumTestedMajorVersion)
                    logger.LogWarning(
                        "The audit PostgreSQL server reports major version {ServerMajorVersion}, below the minimum tested version {MinimumMajorVersion}. Audit queries remain index-backed, but this configuration is untested. Set {OptionName} to fail startup instead of warning.",
                        version.Major,
                        MinimumTestedMajorVersion,
                        nameof(StorageOptions.ThrowOnUnsupportedPostgreSqlVersion));
                else
                    logger.LogDebug(
                        "The audit PostgreSQL server reports major version {ServerMajorVersion}, at or above the minimum tested version {MinimumMajorVersion}.",
                        version.Major,
                        MinimumTestedMajorVersion);
            }
            catch (Exception ex)
            {
                TryLogDebug(loggerFactory, ex);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Reads the server version from the context's own connection, opening it only if it is not
        ///     already open and closing it again only in that case, so an ambient connection owned by the
        ///     caller is left exactly as it was found.
        /// </summary>
        /// <param name="context">The audit context whose connection is inspected.</param>
        /// <returns>
        ///     The server version, or null when it cannot be read.
        /// </returns>
        /// =================================================================================================
        private static Version TryReadServerVersion(AuditPostgreSqlDbContext context)
        {
            var openedHere = false;

            try
            {
                if (!(context.Database.GetDbConnection() is NpgsqlConnection connection))
                    return null;

                if (connection.State != ConnectionState.Open)
                {
                    context.Database.OpenConnection();
                    openedHere = true;
                }

                return connection.PostgreSqlVersion;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (openedHere)
                    TryCloseConnection(context);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Closes a connection this diagnostic opened, swallowing any failure so that cleanup can
        ///     never mask or replace the caller's outcome.
        /// </summary>
        /// <param name="context">The audit context whose connection is closed.</param>
        /// =================================================================================================
        private static void TryCloseConnection(AuditPostgreSqlDbContext context)
        {
            try
            {
                context.Database.CloseConnection();
            }
            catch
            {
                /* ignored */
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Reports a diagnostic failure at debug level, swallowing any failure raised by the logging
        ///     itself so that this advisory can never surface an error of its own.
        /// </summary>
        /// <param name="loggerFactory">The logger factory used to create the diagnostic logger.</param>
        /// <param name="exception">The exception to report.</param>
        /// =================================================================================================
        private static void TryLogDebug(ILoggerFactory loggerFactory, Exception exception)
        {
            try
            {
                loggerFactory
                    ?.CreateLogger(typeof(AuditPostgreSqlServerVersionDiagnostic).FullName)
                    .LogDebug(exception, "The audit PostgreSQL server version advisory could not be evaluated.");
            }
            catch
            {
                /* ignored */
            }
        }
    }
}
