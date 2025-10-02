using System;
using UnityEngine;

namespace Enrichments.Core;

[Serializable]
public abstract class VersionUpdater
{
    public int sourceVersion;
    public int targetVersion;
    public bool autoUpdate;
    
    public virtual bool Allowed(ContentCustomEnrichment contentCustomEnrichment) => contentCustomEnrichment.Version == sourceVersion && EnrichmentManager.Version == targetVersion;

    public virtual void Update(string itemId, ContentCustomEnrichment contentCustomEnrichment)
    {
        if (autoUpdate)
        {
            Debug.Log($"[Enrichment Version Control] [{GetType().Name}] Updating {itemId} from {sourceVersion} to {targetVersion}.");
            contentCustomEnrichment.Version = EnrichmentManager.Version;
        }
    }
}