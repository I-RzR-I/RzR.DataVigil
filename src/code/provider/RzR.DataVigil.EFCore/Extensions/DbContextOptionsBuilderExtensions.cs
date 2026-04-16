// ***********************************************************************
//  Assembly         : RzR.DataVigil.EFCore
//  Author           : RzR
//  Created On       : 2026-04-10 23:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-14 18:09
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
using RzR.DataVigil.EFCore.Interceptors;

#endregion

namespace RzR.DataVigil.EFCore.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for adding audit interceptors to DbContext.
    /// </summary>
    /// =================================================================================================
    public static class DbContextOptionsBuilderExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Adds audit trail interceptors to the DbContext options.
        /// </summary>
        /// <param name="optionsBuilder">The optionsBuilder to act on.</param>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>
        ///     A DbContextOptionsBuilder.
        /// </returns>
        /// =================================================================================================
        public static DbContextOptionsBuilder AddAuditInterceptors(
            this DbContextOptionsBuilder optionsBuilder,
            IServiceProvider serviceProvider)
        {
            var saveInterceptor = serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>();
            var commandInterceptor = serviceProvider.GetRequiredService<AuditCommandInterceptor>();

            optionsBuilder.AddInterceptors(saveInterceptor, commandInterceptor);

            return optionsBuilder;
        }
    }
}