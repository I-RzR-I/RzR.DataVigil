using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RzR.DataVigil.Abstractions.Constants;
using RzR.DataVigil.Abstractions.Enums;
using RzR.DataVigil.Abstractions.Models.Identity;
using RzR.DataVigil.AspNetCore.Enrichers;
using RzR.DataVigil.AspNetCore.Tests.Stubs;
using RzR.ResultMessage.Abstractions;
using static RzR.DataVigil.AspNetCore.Tests.Stubs.HttpEnricherTestData;

namespace RzR.DataVigil.AspNetCore.Tests.Enrichers
{
    [TestClass]
    public class HttpOperationMetadataEnricherTests
    {
        private StubHttpContextAccessor _accessor;

        [TestInitialize]
        public void Init() => _accessor = new StubHttpContextAccessor();

        [TestMethod]
        public void Enrich_NoHttpContext_ReturnsSuccessWithNoPairs()
        {
            _accessor.HttpContext = null;
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => "/orders/{id}");

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(0, stamped.Count);
        }

        [TestMethod]
        public void Enrich_NullAccessor_ReturnsSuccessWithNoPairs()
        {
            var enricher = new HttpOperationMetadataEnricher(null, _ => "/orders/{id}");

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(0, stamped.Count);
        }

        [TestMethod]
        public void Enrich_ContextWithNeitherMethodNorRoute_ReturnsSuccessWithNoPairs()
        {
            _accessor.HttpContext = new DefaultHttpContext();
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => null);

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(0, stamped.Count);
        }

        [TestMethod]
        public void Enrich_MethodAndRouteAvailable_StampsExactlyTheTwoReservedKeys()
        {
            _accessor.HttpContext = ContextWithMethod("POST");
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => "/orders/{id}");

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(2, stamped.Count);
            Assert.AreEqual("POST", stamped[AuditMetadataKeys.HttpMethod]);
            Assert.AreEqual("/orders/{id}", stamped[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public void Enrich_ReservedKeyNames_AreTheDocumentedConstants()
        {
            Assert.AreEqual("__datavigil.http.method", AuditMetadataKeys.HttpMethod);
            Assert.AreEqual("__datavigil.http.route", AuditMetadataKeys.HttpRoute);
        }

        [TestMethod]
        public void Enrich_RouteTemplateIsPassedThroughUnaltered_NotTheRawPath()
        {
            _accessor.HttpContext = ContextWithMethod("GET");
            _accessor.HttpContext.Request.Path = "/orders/42";
            _accessor.HttpContext.Request.QueryString = new QueryString("?email=a@b.com");

            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => "/orders/{id}");

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual("/orders/{id}", stamped[AuditMetadataKeys.HttpRoute]);
            Assert.IsFalse(stamped.Values.Any(v => v.Contains("42")));
            Assert.IsFalse(stamped.Values.Any(v => v.Contains("@")));
        }

        [TestMethod]
        public void Enrich_RouteAccessorIsNull_StampsMethodOnly()
        {
            _accessor.HttpContext = ContextWithMethod("DELETE");
            var enricher = new HttpOperationMetadataEnricher(_accessor, null);

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual("DELETE", stamped[AuditMetadataKeys.HttpMethod]);
            Assert.IsFalse(stamped.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [TestMethod]
        public void Enrich_RouteAccessorThrows_StampsMethodOnly()
        {
            _accessor.HttpContext = ContextWithMethod("PUT");
            var enricher = new HttpOperationMetadataEnricher(
                _accessor,
                _ => throw new InvalidOperationException("routing blew up"));

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual("PUT", stamped[AuditMetadataKeys.HttpMethod]);
            Assert.IsFalse(stamped.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [TestMethod]
        public void Enrich_RouteAccessorReturnsNull_StampsMethodOnly()
        {
            _accessor.HttpContext = ContextWithMethod("GET");
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => null);

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual("GET", stamped[AuditMetadataKeys.HttpMethod]);
            Assert.IsFalse(stamped.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        public void Enrich_RouteAccessorReturnsBlank_StampsMethodOnly(string blank)
        {
            _accessor.HttpContext = ContextWithMethod("GET");
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => blank);

            var stamped = Stamped(enricher.Enrich());

            Assert.IsFalse(stamped.ContainsKey(AuditMetadataKeys.HttpRoute));
        }

        [TestMethod]
        public void Enrich_RouteAccessorReceivesTheAmbientContext()
        {
            var context = ContextWithMethod("GET");
            _accessor.HttpContext = context;

            HttpContext seen = null;
            var enricher = new HttpOperationMetadataEnricher(
                _accessor,
                ctx =>
                {
                    seen = ctx;

                    return "/x";
                });

            enricher.Enrich();

            Assert.AreSame(context, seen);
        }

        [DataTestMethod]
        [DataRow("M-SEARCH")]
        [DataRow("GET ")]
        [DataRow(" GET")]
        [DataRow("GE7")]
        [DataRow("GET\r\nX-Injected: 1")]
        [DataRow("<script>")]
        [DataRow("PROPFIND;DROP")]
        public void Enrich_MethodIsNotAllAsciiLetters_OmitsTheMethodKeyEntirely(string method)
        {
            _accessor.HttpContext = ContextWithMethod(method);
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => "/orders/{id}");

            var stamped = Stamped(enricher.Enrich());

            Assert.IsFalse(stamped.ContainsKey(AuditMetadataKeys.HttpMethod));
            Assert.AreEqual("/orders/{id}", stamped[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public void Enrich_MethodContainsANonAsciiUnicodeLetter_OmitsTheMethodKey()
        {
            var method = new string(new[] { 'M', (char)0x00C9, 'T', 'H', 'O', 'D', 'E' });
            _accessor.HttpContext = ContextWithMethod(method);
            var enricher = new HttpOperationMetadataEnricher(_accessor, null);

            var stamped = Stamped(enricher.Enrich());

            Assert.IsFalse(stamped.ContainsKey(AuditMetadataKeys.HttpMethod));
        }

        [DataTestMethod]
        [DataRow("GET")]
        [DataRow("POST")]
        [DataRow("PUT")]
        [DataRow("PATCH")]
        [DataRow("DELETE")]
        [DataRow("HEAD")]
        [DataRow("OPTIONS")]
        [DataRow("PROPFIND")]
        [DataRow("get")]
        public void Enrich_MethodIsAllAsciiLetters_IsStampedVerbatim(string method)
        {
            _accessor.HttpContext = ContextWithMethod(method);
            var enricher = new HttpOperationMetadataEnricher(_accessor, null);

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(method, stamped[AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public void Enrich_MethodOfExactlyTwentyFourLetters_IsAccepted()
        {
            var method = new string('A', 24);
            _accessor.HttpContext = ContextWithMethod(method);
            var enricher = new HttpOperationMetadataEnricher(_accessor, null);

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(method, stamped[AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public void Enrich_MethodOfTwentyFiveLetters_IsOmittedRatherThanTruncated()
        {
            var method = new string('A', 25);
            _accessor.HttpContext = ContextWithMethod(method);
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => "/orders/{id}");

            var stamped = Stamped(enricher.Enrich());

            Assert.IsFalse(stamped.ContainsKey(AuditMetadataKeys.HttpMethod));
            Assert.AreEqual("/orders/{id}", stamped[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public void Enrich_AbsurdlyLongClientMethod_IsNotStoredAtAnyLength()
        {
            _accessor.HttpContext = ContextWithMethod(new string('B', 100_000));
            var enricher = new HttpOperationMetadataEnricher(_accessor, null);

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(0, stamped.Count);
        }

        [TestMethod]
        public void Enrich_RouteTemplateOfExactlyFiveHundredAndTwelve_IsKeptWhole()
        {
            var template = new string('r', 512);
            _accessor.HttpContext = ContextWithMethod("GET");
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => template);

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(512, stamped[AuditMetadataKeys.HttpRoute].Length);
            Assert.AreEqual(template, stamped[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public void Enrich_RouteTemplateLongerThanFiveHundredAndTwelve_IsTruncatedToTheCapNotDropped()
        {
            var template = new string('r', 600);
            _accessor.HttpContext = ContextWithMethod("GET");
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => template);

            var stamped = Stamped(enricher.Enrich());

            Assert.IsTrue(stamped.ContainsKey(AuditMetadataKeys.HttpRoute));
            Assert.AreEqual(512, stamped[AuditMetadataKeys.HttpRoute].Length);
            Assert.AreEqual(template.Substring(0, 512), stamped[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public void Enrich_RouteTemplateWithSurroundingWhitespace_IsTrimmedBeforeTheCapIsApplied()
        {
            _accessor.HttpContext = ContextWithMethod("GET");
            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => "  /orders/{id}  ");

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual("/orders/{id}", stamped[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public void Enrich_ScopeContextCarriesAUser_StampsNothingEvenWithAnAmbientRequest()
        {
            _accessor.HttpContext = ContextWithMethod("POST");
            var scope = new StubAuditScopeContext
            {
                CurrentUser = new AuditUserInfo
                {
                    UserId = "worker-1",
                    UserName = "Nightly job",
                    Source = AuditUserSource.ScopeContext
                }
            };

            var enricher = new HttpOperationMetadataEnricher(_accessor, _ => "/orders/{id}", scope);

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual(0, stamped.Count);
        }

        [TestMethod]
        public void Enrich_ScopeContextPresentButCarriesNoUser_StillStamps()
        {
            _accessor.HttpContext = ContextWithMethod("POST");
            var enricher = new HttpOperationMetadataEnricher(
                _accessor, _ => "/orders/{id}", new StubAuditScopeContext());

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual("POST", stamped[AuditMetadataKeys.HttpMethod]);
            Assert.AreEqual("/orders/{id}", stamped[AuditMetadataKeys.HttpRoute]);
        }

        [TestMethod]
        public void Enrich_ScopeContextResolutionFails_StillStamps()
        {
            _accessor.HttpContext = ContextWithMethod("POST");
            var enricher = new HttpOperationMetadataEnricher(
                _accessor,
                _ => "/orders/{id}",
                new StubAuditScopeContext { GetCurrentUserShouldFail = true });

            var stamped = Stamped(enricher.Enrich());

            Assert.AreEqual("POST", stamped[AuditMetadataKeys.HttpMethod]);
        }

        [TestMethod]
        public void Enrich_ScopeContextThrows_ReturnsAFailedResultRatherThanPropagating()
        {
            _accessor.HttpContext = ContextWithMethod("POST");
            var enricher = new HttpOperationMetadataEnricher(
                _accessor,
                _ => "/orders/{id}",
                new StubAuditScopeContext { GetCurrentUserShouldThrow = true });

            var result = enricher.Enrich();

            Assert.IsNotNull(result);
            Assert.IsFalse(result.IsSuccess);
        }

        private static IDictionary<string, string> Stamped(
            IResult<IEnumerable<KeyValuePair<string, string>>> result)
        {
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Response);

            var stamped = new Dictionary<string, string>();
            foreach (var pair in result.Response)
                stamped[pair.Key] = pair.Value;

            return stamped;
        }
    }
}
