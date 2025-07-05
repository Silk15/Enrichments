using System;
using System.Collections.Generic;
using ThunderRoad;

namespace Enrichments
{
    public class ContentCustomEnrichment : ContentCustomData
    {
        public List<string> enrichments = new();
        
        public ContentCustomEnrichment() { }

        public ContentCustomEnrichment(List<string> enrichments) => this.enrichments = enrichments;

        public void Add(string id)
        {
            if (!enrichments.Contains(id) && enrichments.Count < 4) enrichments.Add(id);
        }

        public void Remove(string id)
        {
            if (enrichments.Contains(id)) enrichments.Remove(id);
        }

        public void Clear() => enrichments.Clear();
        
        public bool Has(string id) => enrichments.Contains(id);
    }
}