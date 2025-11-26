using System;
using System.Collections.Generic;
using System.Linq;

namespace Enrichments;

public class EnrichmentArgs : EventArgs
{ 
    public bool BaseResult { get; }
    public List<bool> Overrides { get; } = new();
    
    public bool ResultContains(bool condition) => Overrides.Contains(condition);
    
    public void AddOverride(bool result) => Overrides.Add(result);
    
    public void AddOverrides(List<bool> results) => Overrides.AddRange(results);
    
    public EnrichmentArgs(bool baseResult) => BaseResult = baseResult;
    
    public EnrichmentArgs(bool baseResult, List<bool> overrides)
    {
        BaseResult = baseResult;
        Overrides = overrides;
    }
}