// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfSqlServer
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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Options;
using System;
using System.Reflection;

#endregion

namespace RzR.DataVigil.Storage.EfSqlServer.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for configuring SQL Server as the audit storage provider.
    /// </summary>
    /// =================================================================================================
    public static class StorageOptionsExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Configures SQL Server as the audit store backend.
        ///     Pass the same connection string as your application for same-database storage,
        ///     or a different one for a separate dedicated audit database.
        /// </summary>
        /// <param name="options">The storage options to configure.</param>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <returns>
        ///     The <see cref="StorageOptions"/> instance for fluent chaining.
        /// </returns>
        /// =================================================================================================
        public static StorageOptions UseSqlServer(
            this StorageOptions options,
            string connectionString)
        {
            options.ConnectionString = connectionString;

            return options;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers the SQL Server audit store (<see cref="SqlServerAuditStore"/>) and its
        ///     <see cref="AuditSqlServerDbContext"/> in the DI container.
        ///     Call after <c>AddAuditTrail()</c> with <c>UseSqlServer()</c> configured.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>
        ///     The <see cref="IServiceCollection"/> for fluent chaining.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditTrailSqlServer(this IServiceCollection services)
        {
            services.AddDbContext<AuditSqlServerDbContext>((sp, opts) =>
            {
                var storageOptions = sp.GetRequiredService<StorageOptions>();

                if (string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
                    throw new InvalidOperationException(
                        "Audit SQL Server connection string is required. " +
                        "Use the same connection string as your app for same-DB, " +
                        "or a different one for a separate audit database.");

                opts.UseSqlServer(storageOptions.ConnectionString, options =>
                {
                    options.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                    options.MigrationsHistoryTable($"__{nameof(AuditSqlServerDbContext)}", storageOptions.Schema);
                });
            });

            services.AddScoped<IAuditStore, SqlServerAuditStore>();

            return services;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Applies pending EF Core migrations for the SQL Server audit database.
        ///     Call this method at application startup after the host has been built.
        ///     <code>
        ///     var host = builder.Build();
        ///     host.Services.MigrateAuditSqlServerDb();
        ///     host.Run();
        ///     </code>
        /// </summary>
        /// <param name="serviceProvider">
        ///     The application's root <see cref="IServiceProvider"/> (e.g. <c>app.ApplicationServices</c>
        ///     or <c>host.Services</c>).
        /// </param>
        /// =================================================================================================
        public static void MigrateAuditSqlServerDb(this IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var auditDb = scope.ServiceProvider.GetRequiredService<AuditSqlServerDbContext>();
                auditDb.Database.Migrate();
            }
        }
    }
}
