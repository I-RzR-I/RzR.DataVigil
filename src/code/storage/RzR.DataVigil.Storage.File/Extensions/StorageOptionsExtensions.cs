// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.File
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 20:48
// ***********************************************************************
//  <copyright file="StorageOptionsExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.Extensions.DependencyInjection;
using RzR.DataVigil.Abstractions.Services;
using RzR.DataVigil.Core.Gdpr;
using RzR.DataVigil.Core.Options;

#endregion

namespace RzR.DataVigil.Storage.File.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for configuring file-based storage.
    /// </summary>
    /// =================================================================================================
    public static class StorageOptionsExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Use the file system as the audit store.
        /// </summary>
        /// <param name="options">The options to act on.</param>
        /// <param name="directoryPath">(Optional) Full pathname of the directory file.</param>
        /// <returns>
        ///     The StorageOptions.
        /// </returns>
        /// =================================================================================================
        public static StorageOptions UseFile(
            this StorageOptions options,
            string directoryPath = null)
        {
            options.FilePath = directoryPath;

            return options;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Registers the file audit store in DI.
        /// </summary>
        /// <param name="services">The services to act on.</param>
        /// <returns>
        ///     An IServiceCollection.
        /// </returns>
        /// =================================================================================================
        public static IServiceCollection AddAuditTrailFileStorage(this IServiceCollection services)
        {
            services.AddSingleton<IAuditStore>(sp =>
            {
                var options = sp.GetRequiredService<StorageOptions>();
                var gdprProcessor = sp.GetRequiredService<GdprProcessor>();

                return new FileAuditStore(options, gdprProcessor);
            });

            return services;
        }
    }
}