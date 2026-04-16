using System.Collections.Generic;
using RzR.DataVigil.Core.Gdpr;

namespace RzR.DataVigil.Core.Tests.Helpers
{
    internal static class GdprPolicyRegistryHelper
    {
        internal static GdprPolicyRegistry CreateRegistry(string entityName, EntityGdprPolicy policy)
        {
            var registry = new GdprPolicyRegistry();
            var field = typeof(GdprPolicyRegistry).GetField("_policiesByName",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var dict = (IDictionary<string, EntityGdprPolicy>)field.GetValue(registry);
            dict[entityName] = policy;

            return registry;
        }
    }
}
