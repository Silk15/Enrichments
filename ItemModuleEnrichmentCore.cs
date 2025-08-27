using System;
using ThunderRoad;

namespace Enrichments
{
    public class ItemModuleEnrichmentCore : ItemModule
    {
        public const string lineRendererAddress = "Silk.Prefab.Enrichments.Line";
        public string loopEffectId = "EnrichmentCoreLoop";
        public string connectEffectId = "EnrichmentCoreConnect";
        public string disconnectEffectId = "EnrichmentCoreDisconnect";

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
            item.gameObject.GetOrAddComponent<UIEnrichmentCore>().Init(item, this);
        }
    }
}