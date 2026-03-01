using System;
using ThunderRoad;
using TriInspector;

namespace Enrichments
{
    public class ItemModuleEnrichmentCore : ItemModule
    {
        public const string lineRendererAddress = "Silk.Prefab.Enrichments.Line";
        
        [NonSerialized]
        public EffectData loopEffectData;
        [Dropdown(nameof(GetAllEffectID))]
        public string loopEffectId = "EnrichmentCoreLoop";
        
        [NonSerialized]
        public EffectData connectEffectData;
        
        [Dropdown(nameof(GetAllEffectID))]
        public string connectEffectId = "EnrichmentCoreConnect";
        
        [NonSerialized]
        public EffectData disconnectEffectData;
        [Dropdown(nameof(GetAllEffectID))]
        public string disconnectEffectId = "EnrichmentCoreDisconnect";
        
        #if !SDK
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
        #endif

        public TriDropdownList<string> GetAllEffectID() => Catalog.GetDropdownAllID(Category.Effect);
    }
}