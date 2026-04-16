using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace RzR.DataVigil.AspNetCore.Tests.Helpers
{
    internal static class HttpContextHelper
    {
        internal static IHttpContextAccessor CreateAccessor(HttpContext ctx)
        {
            var accessor = new HttpContextAccessor { HttpContext = ctx };

            return accessor;
        }

        internal static DefaultHttpContext CreateAuthenticatedContext(
            string userId = "u-1",
            string userName = "Alice",
            IEnumerable<string> roles = null,
            IDictionary<string, string> extraClaims = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName)
            };

            if (roles != null)
                foreach (var role in roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));

            if (extraClaims != null)
                foreach (var kvp in extraClaims)
                    claims.Add(new Claim(kvp.Key, kvp.Value));

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var context = new DefaultHttpContext();
            context.User = principal;

            return context;
        }

        internal static IHttpContextAccessor CreateAccessorWithHeaders(
            string correlationId = null,
            string requestId = null)
        {
            var context = new DefaultHttpContext();
            if (correlationId != null)
                context.Request.Headers["X-Correlation-Id"] = correlationId;

            if (requestId != null)
                context.Request.Headers["X-Request-Id"] = requestId;

            return new HttpContextAccessor { HttpContext = context };
        }
    }
}
