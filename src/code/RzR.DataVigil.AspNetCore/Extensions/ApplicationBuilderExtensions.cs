// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-04-15 18:04
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-04-15 18:04
// ***********************************************************************
//  <copyright file="ApplicationBuilderExtensions.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RzR.DataVigil.Core.Pipeline;
using RzR.Extensions.Domain.Primitives;

#endregion

namespace RzR.DataVigil.AspNetCore.Extensions
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Extension methods for the ASP.NET Core application builder.
    /// </summary>
    /// =================================================================================================
    public static class ApplicationBuilderExtensions
    {
        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Adds middleware that flushes collected Read audit entries at the end of each request.
        ///     This must be registered in the pipeline for Read audit interception to work
        ///     with non-relational providers (e.g. MongoDB, Cosmos).
        ///     <para>
        ///     Place this call after <c>UseRouting()</c> and before <c>MapControllers()</c>,
        ///     or after <c>MapControllers()</c> — the middleware flushes after downstream middleware
        ///     completes.
        ///     </para>
        /// </summary>
        /// <param name="app">The application builder to act on.</param>
        /// <returns>The same application builder, for fluent chaining.</returns>
        /// =================================================================================================
        public static IApplicationBuilder UseAuditReadFlush(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                await next();

                var collector = context.RequestServices.GetService<AuditReadCollector>();
                if (collector.IsNotNull() && collector.HasEntries.IsTrue())
                {
                    await collector.FlushAsync(context.RequestAborted);
                }
            });
        }
    }
}
