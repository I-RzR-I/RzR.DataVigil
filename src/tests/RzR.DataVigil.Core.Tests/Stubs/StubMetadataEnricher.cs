using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Services;
using RzR.ResultMessage;
using RzR.ResultMessage.Abstractions;

namespace RzR.DataVigil.Core.Tests.Stubs
{
    public sealed class StubMetadataEnricher : IAuditMetadataEnricher
    {
        internal const string FailedResultDetail = "enricher-authored-failure-detail";

        internal StubMetadataEnricher(params KeyValuePair<string, string>[] pairs)
            => Pairs = pairs ?? [];

        internal KeyValuePair<string, string>[] Pairs { get; set; }

        internal EnrichOutcome Outcome { get; set; } = EnrichOutcome.Pairs;

        internal int EnrichCallCount { get; private set; }

        public IResult<IEnumerable<KeyValuePair<string, string>>> Enrich()
        {
            EnrichCallCount++;

            switch (Outcome)
            {
                case EnrichOutcome.Throw:
                    throw new InvalidOperationException("Enricher failed.");

                case EnrichOutcome.FailedResult:
                    return Result<IEnumerable<KeyValuePair<string, string>>>.Failure(FailedResultDetail);

                case EnrichOutcome.NullResult:
                    return null;

                case EnrichOutcome.SuccessWithNullPayload:
                    return Result<IEnumerable<KeyValuePair<string, string>>>.Success();

                default:
                    return Result<IEnumerable<KeyValuePair<string, string>>>.Success(Pairs);
            }
        }

        internal static KeyValuePair<string, string> Pair(string key, string value)
            => new KeyValuePair<string, string>(key, value);
    }
}
