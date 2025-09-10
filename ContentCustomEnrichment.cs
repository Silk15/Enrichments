using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ThunderRoad;

namespace Enrichments
{
    public class ContentCustomEnrichment : ContentCustomData
    {
        public int MaxEnrichments { get; set; } = 4;
        public List<string> Enrichments { get; set; } = new();

        [JsonIgnore]
        public EnrichmentData[] Data => Enrichments.AsDataArray<EnrichmentData>();

        public ContentCustomEnrichment() { }

        public ContentCustomEnrichment(List<string> enrichments) => Enrichments = enrichments;

        /// <summary>
        /// Used to modify the maximum amount of enrichments an item is allowed to have. 
        /// </summary>
        /// <param name="maxEnrichments"></param>
        public void SetMaxEnrichments(int maxEnrichments) => MaxEnrichments = maxEnrichments;

        /// <summary>
        /// Used to save an enrichment to an item, fails if this data is at max capacity.
        /// </summary>
        /// <param name="id"></param>
        public void Add(string id)
        {
            if (!Enrichments.Contains(id) && Enrichments.Count < MaxEnrichments) Enrichments.Add(id);
        }

        /// <summary>
        /// Used to remove an enrichment from an item, fails if this item has no enrichments, or the specified id is not present.
        /// </summary>
        /// <param name="id"></param>
        public void Remove(string id)
        {
            if (Enrichments.Contains(id) && Enrichments.Count > 0) Enrichments.Remove(id);
        }
        
        /// <summary>
        /// Clears the enrichments from this item.
        /// </summary>
        public void Clear() => Enrichments.Clear();

        /// <summary>
        /// Whether this item has an enrichment matching the given id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool Has(string id) => Enrichments.Contains(id);
    }
}