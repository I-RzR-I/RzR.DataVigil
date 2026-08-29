using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Stubs
{
    internal sealed class DeferredThrowingEnricher : IAuditMetadataEnricher
    {
        public IResult<IEnumerable<KeyValuePair<string, string>>> Enrich()
            => Result<IEnumerable<KeyValuePair<string, string>>>.Success(Lazy());

        private static IEnumerable<KeyValuePair<string, string>> Lazy()
        {
            yield return new KeyValuePair<string, string>("k.before", "v");
            throw new InvalidOperationException("thrown during enumeration");
        }
    }
}
