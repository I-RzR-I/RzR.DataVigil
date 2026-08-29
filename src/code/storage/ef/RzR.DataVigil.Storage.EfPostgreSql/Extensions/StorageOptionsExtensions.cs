// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfPostgreSql
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 12:00
// ***********************************************************************
//  <copyright file="StorageOptionsExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Storage.EfPostgreSql.Diagnostics;

#endregion

namespace RzR.DataVigil.Storage.EfPostgreSql.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for registering and configuring the PostgreSQL audit storage provider.
    /// </summary>
    /// =================================================================================================
    public static class StorageOptionsExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the command timeout, in seconds, applied to the scope that runs migrations.
        ///     Six hours. Index builds against a production-sized audit table run for minutes to hours,
        ///     while the provider default is thirty seconds, and a cancelled build is worse than a slow
        ///     one: it leaves an index the migration's own existence guard will skip forever.
        /// </summary>
        /// =================================================================================================
        private const int MigrationCommandTimeoutSeconds = 6 * 60 * 60;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Configures PostgreSQL as the backing store for audit data.
        ///     Supply the same connection string as your application for same-database storage,
        ///     or a different one to store audit records in a separate database.
        /// </summary>
        /// <param name="options">
        ///     The <see cref="StorageOptions"/> instance being configured.
        /// </param>
        /// <param name="connectionString">
        ///     The PostgreSQL connection string for the audit database.
        /// </param>
        /// <returns>
        ///     The same <see cref="StorageOptions"/> instance, for fluent chaining.
        /// </returns>
        /// =================================================================================================
        public static StorageOptions UsePostgreSql(this StorageOptions options,
            string connectionString)
        {
            options.ConnectionString = connectionString;

            return options;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers the <see cref="PostgreSqlAuditStore"/> and
        ///     <see cref="AuditPostgreSqlDbContext"/> in the dependency-injection container.
        ///     Call this method after <c>AddAuditTrail()</c> with <c>UsePostgreSql()</c> configured.
        /// </summary>
        /// <param name="services">
        ///     The <see cref="IServiceCollection"/> to register services into.
        /// </param>
        /// <returns>
        ///     The same <see cref="IServiceCollection"/> instance, for fluent chaining.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditTrailPostgreSqlServer(this IServiceCollection services)
        {
            services.AddDbContext<AuditPostgreSqlDbContext>((sp, opts) =>
            {
                var storageOptions = sp.GetRequiredService<StorageOptions>();

                if (string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
                    throw new InvalidOperationException(
                        "Audit PostgreSql connection string is required. " +
                        "Use the same connection string as your app for same-DB, " +
                        "or a different one for a separate audit database.");

                opts.UseNpgsql(storageOptions.ConnectionString, options =>
                {
                    options.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                    options.MigrationsHistoryTable($"__{nameof(AuditPostgreSqlDbContext)}", storageOptions.Schema);
                });
            });

            services.AddScoped<IAuditStore, PostgreSqlAuditStore>();

            return services;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Applies pending EF Core migrations for the PostgreSQL audit database.
        ///     Call this method at application startup after the host has been built.
        ///     <code>
        ///     var host = builder.Build();
        ///     host.Services.MigrateAuditPostgreSqlDb();
        ///     host.Run();
        ///     </code>
        /// </summary>
        /// <param name="serviceProvider">
        ///     The application's root <see cref="IServiceProvider"/> (e.g. <c>app.ApplicationServices</c>
        ///     or <c>host.Services</c>).
        /// </param>
        /// <exception cref="NotSupportedException">
        ///     Thrown only when <see cref="StorageOptions.ThrowOnUnsupportedPostgreSqlVersion"/> is set and
        ///     the server's major version is below the minimum tested version.
        /// </exception>
        /// =================================================================================================
        public static void MigrateAuditPostgreSqlDb(this IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var auditDb = scope.ServiceProvider.GetRequiredService<AuditPostgreSqlDbContext>();
                var storageOptions = scope.ServiceProvider.GetRequiredService<StorageOptions>();

                if (storageOptions.ThrowOnUnsupportedPostgreSqlVersion)
                    AuditPostgreSqlServerVersionDiagnostic.ThrowIfServerVersionUnsupported(auditDb);

                auditDb.Database.SetCommandTimeout(MigrationCommandTimeoutSeconds);

                auditDb.Database.Migrate();

                AuditPostgreSqlServerVersionDiagnostic.LogServerVersionCompatibility(
                    auditDb,
                    scope.ServiceProvider.GetService<ILoggerFactory>());
            }
        }
    }
}
