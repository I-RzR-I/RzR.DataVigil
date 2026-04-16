// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfMongoDb
//  Author           : RzR
//  Created On       : 2026-04-15 11:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 12:04
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Options;
using RzR.DataVigil.Core.Pipeline;
using RzR.DataVigil.Storage.EfMongoDb.Interceptors;

#endregion

namespace RzR.DataVigil.Storage.EfMongoDb.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for registering and configuring the MongoDB audit storage provider.
    /// </summary>
    /// =================================================================================================
    public static class StorageOptionsExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Configures MongoDB as the backing store for audit data.
        ///     Supply the MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).
        /// </summary>
        /// <param name="options">
        ///     The <see cref="StorageOptions" /> instance being configured.
        /// </param>
        /// <param name="connectionString">
        ///     The MongoDB connection string for the audit database.
        /// </param>
        /// <param name="databaseName">
        ///     The MongoDB database name for audit collections.
        /// </param>
        /// <returns>
        ///     The same <see cref="StorageOptions" /> instance, for fluent chaining.
        /// </returns>
        /// =================================================================================================
        public static StorageOptions UseMongoDb(
            this StorageOptions options,
            string connectionString,
            string databaseName)
        {
            options.ConnectionString = connectionString;
            options.DatabaseName = databaseName;

            return options;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers the <see cref="MongoDbAuditStore" /> and
        ///     <see cref="AuditMongoDbContext" /> in the dependency-injection container.
        ///     Call this method after <c>AddAuditTrail()</c> with <c>UseMongoDb()</c> configured.
        /// </summary>
        /// <param name="services">
        ///     The <see cref="IServiceCollection" /> to register services into.
        /// </param>
        /// <returns>
        ///     The same <see cref="IServiceCollection" /> instance, for fluent chaining.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditTrailMongoDb(this IServiceCollection services)
        {
            services.AddDbContext<AuditMongoDbContext>((sp, opts) =>
            {
                var storageOptions = sp.GetRequiredService<StorageOptions>();

                if (string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
                    throw new InvalidOperationException(
                        "Audit MongoDB connection string is required. " +
                        "Configure it via UseMongoDb() in AddAuditTrail().");

                if (string.IsNullOrWhiteSpace(storageOptions.DatabaseName))
                    throw new InvalidOperationException(
                        "Audit MongoDB database name is required. " +
                        "Configure it via UseMongoDb() in AddAuditTrail().");

                opts.UseMongoDB(storageOptions.ConnectionString, storageOptions.DatabaseName);
            });

            services.AddScoped<IAuditStore, MongoDbAuditStore>();
            services.AddScoped<AuditReadCollector>();
            services.AddScoped<AuditMaterializationInterceptor>();

            return services;
        }
    }
}