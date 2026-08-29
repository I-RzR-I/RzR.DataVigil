using Microsoft.AspNetCore.Http;

namespace RzR.DataVigil.AspNetCore.Tests.Stubs
{
    internal sealed class StubHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext HttpContext { get; set; }
    }
}
