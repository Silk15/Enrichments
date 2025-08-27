using System.Collections.Generic;
using ThunderRoad;

namespace Enrichments
{
    public class ContentCustomEnrichment : ContentCustomData
    {
        public int MaxEnrichments { get; set; } = 4;
        public List<string> Enrichments { get; set; } = new();
        
        public ContentCustomEnrichment() { }

        public ContentCustomEnrichment(List<string> enrichments) => this.Enrichments = enrichments;
        
        public void SetMaxEnrichments(int maxEnrichments) => this.MaxEnrichments = maxEnrichments;

        public void Add(string id)
        {
            if (!Enrichments.Contains(id) && Enrichments.Count < MaxEnrichments) Enrichments.Add(id);
        }

        public void Remove(string id)
        {
            if (Enrichments.Contains(id)) Enrichments.Remove(id);
        }

        public void Clear() => Enrichments.Clear();
        
        public bool Has(string id) => Enrichments.Contains(id);
    }
}