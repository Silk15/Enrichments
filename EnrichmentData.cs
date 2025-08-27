using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public VideoClip video;

        [NonSerialized]
        private int videoCount;

        public bool IsAllowedOnItem(Item item) => (allowedTypes.IsNullOrEmpty() || allowedTypes.Contains(item.data.type)) && (allowedItemIds.IsNullOrEmpty() || allowedItemIds.Contains(item.data.id)) && (allowedCategories.IsNullOrEmpty() || allowedCategories.Contains(item.data.category));

        public override void OnCatalogRefresh()
        {
            base.OnCatalogRefresh();
            secondarySkillTree = Catalog.GetData<SkillTreeData>(secondarySkillTreeId);
            primarySkillTree = Catalog.GetData<SkillTreeData>(primarySkillTreeId);
            foreach (ItemData itemData in Catalog.GetDataList<ItemData>().Where(i => i.TryGetModule(out ItemModuleCrystal _)))
                if (!itemData.TryGetModule(out ItemModuleEnrichmentCore _)) itemData.modules.Add(new ItemModuleEnrichmentCore());
        }

        public override IEnumerator LoadAddressableAssetsCoroutine()
        {
            yield return GameManager.local.StartCoroutine(EnrichmentOrb.TryGeneratePools(50));
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

        /// <summary>
        /// Called once when this enrichment is loaded onto an item.
        /// </summary>
        /// <param name="item"></param>
        public virtual void OnEnrichmentLoaded(Item item) => item.mainCollisionHandler.OnCollisionStartEvent += OnItemCollide;
        
        /// <summary>
        /// Called once when every enrichment has loaded on an item as a late refresh. Good for enrichments that rely on each other.
        /// </summary>
        /// <param name="enrichments"></param>
        public virtual void OnLateEnrichmentsLoaded(List<EnrichmentData> enrichments) { }

        /// <summary>
        /// Called whenever the enriched item collides with a surface.
        /// </summary>
        /// <param name="collisionInstance"></param>
        public virtual void OnItemCollide(CollisionInstance collisionInstance) { }

        /// <summary>
        /// Called once when this enrichment is unloaded from an item.
        /// </summary>
        /// <param name="item"></param>

        public virtual void OnEnrichmentUnloaded(Item item) => item.mainCollisionHandler.OnCollisionStartEvent -= OnItemCollide;
        
        /// <summary>
        /// Called whenever the enriched item is imbued.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="imbue"></param>
        /// <param name="spellCastCharge"></param>
        
        public virtual void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge) { }
        
        /// <summary>
        /// Called whenever the enriched item is unimbued.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="imbue"></param>
        /// <param name="spellCastCharge"></param>
        
        public virtual void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge) { }

        public string GetName() => LocalizationManager.Instance.TryGetLocalization("Enrichments", displayName);

        public string GetDescription() => LocalizationManager.Instance.TryGetLocalization("Enrichments", description);
    }
}