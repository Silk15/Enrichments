using System;
using System.Linq;
using Newtonsoft.Json;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Video;

namespace Enrichments
{
    [Serializable]
    public class EnrichmentData : CustomData
    {
        public int tier;
        public int cost;
        public bool showInCore;
        public bool allowRefund;
        public string shardId;
        public string orbEffectId;
        public string displayName;
        public string description;
        public string videoAddress;
        public string uiPrefabAddress;
        public string buttonEnabledIconAddress;
        public string buttonDisabledIconAddress;
        public string orbIconAddress;
        public string skillTreeId;
        
        [NonSerialized]
        public SkillTreeData skillTree;

        [NonSerialized]
        public EffectData orbEffectData;

        [NonSerialized]
        public VideoClip video;

        [NonSerialized]
        public Sprite orbIcon;
        private int videoCount;
        
        public override void OnCatalogRefresh()
        {
            base.OnCatalogRefresh();
            skillTree = Catalog.GetData<SkillTreeData>(skillTreeId);
            orbEffectData = Catalog.GetData<EffectData>(orbEffectId);
            foreach (ItemData itemData in Catalog.GetDataList<ItemData>().Where(i => i.TryGetModule(out ItemModuleCrystal _)))
                if (!itemData.TryGetModule(out ItemModuleEnrichmentCore _))
                    itemData.modules.Add(new ItemModuleEnrichmentCore());
        }

        public void GetVideo(Action<VideoClip> onVideoLoaded)
        {
            if (video !=null)
            {
                ++videoCount;
                onVideoLoaded(video);
            }
            Catalog.LoadAssetAsync(videoAddress, (Action<VideoClip>) (clip =>
            {
                if (clip == null) return;
                ++videoCount;
                video = clip;
                onVideoLoaded(video);
            }), "Enrichment: " + id + " loading video");
        }

        public void ReleaseVideo()
        {
            --videoCount;
            if (videoCount > 0) return;
            VideoClip video = this.video;
            this.video = null;
            if (!(video != null)) return;
            Catalog.ReleaseAsset(video);
        }

        public void GetOrbIcon(Action<Sprite> callback) => Catalog.LoadAssetAsync(orbIconAddress, callback, id);

        public void GetButtonIcon(bool enabled, Action<Sprite> callback)
        {
            string address = enabled ? buttonEnabledIconAddress : buttonDisabledIconAddress;
            Catalog.LoadAssetAsync(address, callback, id);
        }

        public virtual void OnEnrichmentLoaded(Item item) { }

        public virtual void OnEnrichmentUnloaded(Item item) { }
        
        public virtual void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge) { }
        
        public virtual void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge) { }

        public string GetName() => LocalizationManager.Instance.TryGetLocalization("Enrichments", displayName);

        public string GetDescription() => LocalizationManager.Instance.TryGetLocalization("Enrichments", description);
    }
}