using System;
using System.Collections.Generic;
using ThunderRoad;

namespace Enrichments
{
    public class ItemModuleEnrichmentCore : ItemModule
    {
        public string loopEffectId = "EnrichmentCoreLoop";
        public string connectEffectId = "EnrichmentCoreConnect";
        public string disconnectEffectId = "EnrichmentCoreDisconnect";
        public string lineRendererAddress = "Silk.Prefab.Enrichments.Line";
        
        [NonSerialized]
        public EffectData loopEffectData;
        
        [NonSerialized]
        public EffectData connectEffectData;
        
        [NonSerialized]
        public EffectData disconnectEffectData;

        public void Load()
        {
            connectEffectData = Catalog.GetData<EffectData>(connectEffectId);
            disconnectEffectData = Catalog.GetData<EffectData>(disconnectEffectId);
            loopEffectData = Catalog.GetData<EffectData>(loopEffectId);
        }
        
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.GetOrAddComponent<EnrichmentCore>().Init(item, this);
        }
    }
}