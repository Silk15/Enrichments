using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Serialization;
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
        public string primarySkillTreeId;
        public string secondarySkillTreeId;
        public List<string> allowedCategories;
        public List<string> allowedItemIds;
        public List<ItemData.Type> allowedTypes;
        
        [NonSerialized]
        public SkillTreeData primarySkillTree;
        public SkillTreeData secondarySkillTree;

        [NonSerialized]
        public EffectData orbEffectData;

        [NonSerialized]
        public VideoClip video;

        [NonSerialized]
        public Sprite orbIcon;
        private int videoCount;

        public bool IsAllowedOnItem(Item item)
        {
            if (allowedTypes.IsNullOrEmpty() && allowedItemIds.IsNullOrEmpty() && allowedCategories.IsNullOrEmpty()) return true;
            if (!allowedTypes.IsNullOrEmpty() && allowedTypes.Contains(item.data.type)) return true;
            if (!allowedItemIds.IsNullOrEmpty() && allowedItemIds.Contains(item.data.id)) return true;
            if (!allowedCategories.IsNullOrEmpty() && allowedCategories.Contains(item.data.category)) return true;
            return false;
        }

        public override void OnCatalogRefresh()
        {
            base.OnCatalogRefresh();
            secondarySkillTree = Catalog.GetData<SkillTreeData>(secondarySkillTreeId);
            primarySkillTree = Catalog.GetData<SkillTreeData>(primarySkillTreeId);
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
        
        public virtual void OnLateEnrichmentsLoaded(List<EnrichmentData> enrichments) { }

        public virtual void OnEnrichmentUnloaded(Item item) { }
        
        public virtual void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge) { }
        
        public virtual void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge) { }

        public string GetName() => LocalizationManager.Instance.TryGetLocalization("Enrichments", displayName);

        public string GetDescription() => LocalizationManager.Instance.TryGetLocalization("Enrichments", description);
    }
}