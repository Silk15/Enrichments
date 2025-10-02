using ThunderRoad;
using ThunderRoad.DebugViz;
using UnityEngine;

namespace Enrichments;

/// <summary>
/// Ensures holstered items derive the parent item's enrichments, for quivers and other utilities.
/// </summary>
public class EnrichmentHolder : ThunderBehaviour
{
    public EnrichmentData linkedData;
    public string enrichmentId;
    public Holder linkedHolder;

    public override ManagedLoops EnabledManagedLoops => ManagedLoops.Update;

    public void Load(string enrichmentId, EnrichmentData linkedData, Holder linkedHolder)
    {
        this.enrichmentId = enrichmentId;
        this.linkedHolder = linkedHolder;
        this.linkedData = linkedData;
        
        foreach (Item item in linkedHolder.items)
            EnrichmentManager.AddEnrichment(item, enrichmentId, false); 
        
        linkedHolder.Snapped += OnSnapped;
        linkedHolder.UnSnapped += OnUnSnapped;
    }

    public void Unload()
    {
        foreach (Item item in linkedHolder.items)
            EnrichmentManager.RemoveEnrichment(item, enrichmentId, false); 
        linkedHolder.Snapped -= OnSnapped;
        linkedHolder.UnSnapped -= OnUnSnapped;
        linkedHolder = null;
    }

    private void OnSnapped(Item item)
    {
        if (!EnrichmentManager.HasEnrichment(item, enrichmentId)) EnrichmentManager.AddEnrichment(item, enrichmentId, false);
        VizManager.ClearViz(this, $"enrichmentholder{item.data.id}{linkedHolder.items.IndexOf(item)}");
        linkedData.OnHolderSnapped(item, linkedHolder);
    }

    private void OnUnSnapped(Item item) => linkedData.OnHolderUnSnapped(item, linkedHolder);

    protected override void ManagedUpdate()
    {
        base.ManagedUpdate();
        foreach (Item item in linkedHolder.items)
            if (item.holder != linkedHolder)
                VizManager.AddOrUpdateViz(this, $"enrichmentholder{item.data.id}{linkedHolder.items.IndexOf(item)}", Color.blue, VizManager.VizType.Lines, new[] { item.transform.position, linkedHolder.transform.position });
    }
}