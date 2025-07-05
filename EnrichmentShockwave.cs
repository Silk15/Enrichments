using ThunderRoad;
using UnityEngine;

namespace Enrichments;

public class EnrichmentShockwave : EnrichmentData
{
    public override void OnEnrichmentLoaded(Item item)
    {
        base.OnEnrichmentLoaded(item);
        Debug.Log("Loaded");
    }

    public override void OnEnrichmentUnloaded(Item item)
    {
        base.OnEnrichmentUnloaded(item);
        Debug.Log("Unloaded");
    }

    public override void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemImbued(item, imbue, spellCastCharge);
        Debug.Log("Imbued");
    }

    public override void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
    {
        base.OnItemUnimbued(item, imbue, spellCastCharge);
        Debug.Log("Unimbued");
    }
}