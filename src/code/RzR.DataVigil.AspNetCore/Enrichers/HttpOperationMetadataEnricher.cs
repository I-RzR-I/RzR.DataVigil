// ***********************************************************************
//  Assembly         : RzR.DataVigil.AspNetCore
//  Author           : RzR
//  Created On       : 2026-08-27 14:32
//
//  Last Modified By : RzR
//  Last Modified On : 2026-08-28 20:40
// ***********************************************************************
//  <copyright file="HttpOperationMetadataEnricher.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
//
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Services;
using RzR.Extensions.Domain.Primitives;
using RzR.Extensions.Domain.Text;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;
using RzR.ResultMessage.Extensions.Result;

#endregion

namespace RzR.DataVigil.AspNetCore.Enrichers
{
    /// -------------------------------------------------------------------------------------------------
    /// <summary>
    ///     Records which HTTP operation a data change arrived through, as the request method and the
    ///     matched route template, under the <c>__datavigil.http.*</c> keys.
    /// </summary>
    /// <seealso cref="T:RzR.DataVigil.Abstractions.Services.IAuditMetadataEnricher"/>
    /// =================================================================================================
    public class HttpOperationMetadataEnricher : IAuditMetadataEnricher
    {
        private const int MaxMethodLength = 24;
        private const int MaxRouteTemplateLength = 512;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     The payload for every case in which nothing can be stamped, which is the normal answer in a
        ///     worker or console host rather than a degradation. Shared rather than rebuilt so the
        ///     non-HTTP path carries no per-call payload allocation.
        /// </summary>
        /// =================================================================================================
        private static readonly KeyValuePair<string, string>[] NoMetadata = [];

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) the HTTP context accessor.
        /// </summary>
        /// =================================================================================================
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Immutable) supplies the matched route template for a context. Provided by the consumer so
        ///     that this assembly does not have to reference the routing packages, whose surface differs
        ///     between ASP.NET Core versions. Null when the consumer did not supply one, in which case
        ///     only the method is stamped.
        /// </summary>
        /// =================================================================================================
        private readonly Func<HttpContext, string> _routeTemplateAccessor;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Optional scope override. Null when the enricher is constructed directly.
        /// </summary>
        /// =================================================================================================
        private readonly IAuditScopeContext _scopeContext;

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="HttpOperationMetadataEnricher"/> class with no
        ///     scope context.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="routeTemplateAccessor">
        ///     Returns the matched route template for a context, or null when none matched. May be null,
        ///     in which case the route key is never stamped.
        /// </param>
        /// =================================================================================================
        public HttpOperationMetadataEnricher(
            IHttpContextAccessor httpContextAccessor,
            Func<HttpContext, string> routeTemplateAccessor)
            : this(httpContextAccessor, routeTemplateAccessor, null)
        {
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Initializes a new instance of the <see cref="HttpOperationMetadataEnricher"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="routeTemplateAccessor">
        ///     Returns the matched route template for a context, or null when none matched. May be null,
        ///     in which case the route key is never stamped.
        /// </param>
        /// <param name="scopeContext">
        ///     The scope context whose presence of a user marks the work as no longer belonging to the
        ///     ambient request.
        /// </param>
        /// =================================================================================================
        public HttpOperationMetadataEnricher(IHttpContextAccessor httpContextAccessor,
            Func<HttpContext, string> routeTemplateAccessor, IAuditScopeContext scopeContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _routeTemplateAccessor = routeTemplateAccessor;
            _scopeContext = scopeContext;
        }

        /// <inheritdoc/>
        public IResult<IEnumerable<KeyValuePair<string, string>>> Enrich()
        {
            try
            {
                var scopedUser = _scopeContext?.GetCurrentUser();
                if (scopedUser.IsNotNull() && scopedUser!.IsSuccess && scopedUser.Response.IsNotNull())
                    return Success(NoMetadata);

                var httpContext = _httpContextAccessor?.HttpContext;
                if (httpContext.IsNull())
                    return Success(NoMetadata);

                var hasMethod = TryGetMethod(httpContext!.Request?.Method, out var method);

                var hasRoute = TryGetRouteTemplate(httpContext, out var routeTemplate);

                var count = (hasMethod ? 1 : 0) + (hasRoute ? 1 : 0);
                if (count == 0)
                    return Success(NoMetadata);

                var pairs = new KeyValuePair<string, string>[count];
                var index = 0;

                if (hasMethod)
                    pairs[index++] = new KeyValuePair<string, string>(AuditMetadataKeys.HttpMethod, method);

                if (hasRoute)
                    pairs[index] = new KeyValuePair<string, string>(AuditMetadataKeys.HttpRoute, routeTemplate);

                return Success(pairs);
            }
            catch (Exception e)
            {
                return Result<IEnumerable<KeyValuePair<string, string>>>
                    .Failure(e.Message)
                    .WithError(e);
            }
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Wraps a payload in a successful result.
        /// </summary>
        /// <param name="pairs">The pairs to carry.</param>
        /// <returns>
        ///     A successful IResult carrying the pairs.
        /// </returns>
        /// =================================================================================================
        private static IResult<IEnumerable<KeyValuePair<string, string>>> Success(
            IEnumerable<KeyValuePair<string, string>> pairs)
            => Result<IEnumerable<KeyValuePair<string, string>>>.Success(pairs);

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Accepts the request method only when it is short and simple enough to be safe to store.
        ///     The method is a client-supplied token and an extension verb may be arbitrarily long, so a
        ///     rejected value is omitted rather than stored or allowed to fail the audit.
        /// </summary>
        /// <param name="candidate">The raw request method.</param>
        /// <param name="value">[out] The accepted method, or null.</param>
        /// <returns>
        ///     True if the method is acceptable.
        /// </returns>
        /// =================================================================================================
        private static bool TryGetMethod(string candidate, out string value)
        {
            value = candidate.IsPresent()
                    && candidate.Length <= MaxMethodLength
                    && IsAllLetters(candidate)
                ? candidate
                : null;

            return value.IsNotNull();
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Tests that every character is an ASCII letter, <c>[A-Za-z]</c>.
        /// </summary>
        /// <param name="candidate">The string to test, never null or empty.</param>
        /// <returns>
        ///     True if every character is an ASCII letter.
        /// </returns>
        /// =================================================================================================
        private static bool IsAllLetters(string candidate)
        {
            for (var i = 0; i < candidate.Length; i++)
            {
                var c = candidate[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    continue;

                return false;
            }

            return true;
        }

        /// -------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Asks the consumer-supplied accessor for the matched route template.
        /// </summary>
        /// <param name="httpContext">The current context.</param>
        /// <param name="value">[out] The accepted template, or null.</param>
        /// <returns>
        ///     True if a template was resolved.
        /// </returns>
        /// =================================================================================================
        private bool TryGetRouteTemplate(HttpContext httpContext, out string value)
        {
            value = null;

            if (_routeTemplateAccessor.IsNull())
                return false;

            string candidate;
            try
            {
                candidate = _routeTemplateAccessor(httpContext);
            }
            catch
            {
                return false;
            }

            if (candidate.IsMissing())
                return false;

            candidate = candidate.Trim();
            value = candidate.Length > MaxRouteTemplateLength
                ? candidate.Substring(0, MaxRouteTemplateLength)
                : candidate;

            return true;
        }
    }
}
