// ***********************************************************************
//  Assembly         : RzR.DataVigil.Storage.EfMongoDb
//  Author           : RzR
//  Created On       : 2026-04-15 18:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 18:04
// ***********************************************************************
//  <copyright file="DbContextOptionsBuilderExtensions.cs" company="RzR SOFT & TECH">
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
using RzR.DataVigil.Storage.EfMongoDb.Interceptors;

#endregion

namespace RzR.DataVigil.Storage.EfMongoDb.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for adding MongoDB-specific audit interceptors to DbContext options.
    /// </summary>
    /// =================================================================================================
    public static class DbContextOptionsBuilderExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Adds the audit materialization interceptor that automatically captures Read operations
        ///     for non-relational providers (MongoDB). Call this alongside
        ///     <c>AddAuditInterceptors(sp)</c> when configuring a non-relational DbContext.
        /// </summary>
        /// <param name="optionsBuilder">The options builder to act on.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>
        ///     A DbContextOptionsBuilder.
        /// </returns>
        /// =================================================================================================
        public static DbContextOptionsBuilder AddAuditReadInterceptor(
            this DbContextOptionsBuilder optionsBuilder,
            IServiceProvider serviceProvider)
        {
            var interceptor = serviceProvider.GetRequiredService<AuditMaterializationInterceptor>();

            optionsBuilder.AddInterceptors(interceptor);

            return optionsBuilder;
        }
    }
}
