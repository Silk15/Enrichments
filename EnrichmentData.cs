using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Enrichments.Core;
using Newtonsoft.Json;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Video;

namespace Enrichments
{
    [Serializable]
    public class EnrichmentData : CustomData
    {
        public static readonly string[] IgnoredItemIds = new string[] { "Yamato", "Splinter", "ArgentBlade" };

        public int tier;
        public int cost;
        public bool showInCore;
        public bool allowRefund;
        public bool passDataToHolsteredItems;
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
        public FilterLogic itemFilter;
        public List<string> itemIds;
        public FilterLogic categoryFilter;
        public List<string> itemCategories;
        public FilterLogic typeFilter;
        public List<ItemData.Type> itemTypes;

        public override int GetCurrentVersion() => 0;

        [NonSerialized]
        public SkillTreeData primarySkillTree;
        public SkillTreeData secondarySkillTree;

        [NonSerialized]
        public VideoClip video;

        [NonSerialized]
        private int videoCount;

        [NonSerialized]
        private Item item;

        [NonSerialized]
        private ContentCustomEnrichment contentCustomEnrichment;

        [JsonIgnore]
        public Item Item
        {
            get
            {
                if (item != null) return item;
                foreach (var enrichments in EnrichmentManager.Enrichments)
                    if (enrichments.Value.Contains(this))
                        item = enrichments.Key;
                return item;
            }
        }

        [JsonIgnore]
        public ContentCustomEnrichment ContentCustomEnrichment
        {
            get
            {
                if (contentCustomEnrichment != null) return contentCustomEnrichment;
                if (Item.TryGetCustomData(out contentCustomEnrichment)) return contentCustomEnrichment;
                return null;
            }
        }

        /// <summary>
        /// Determines whether the specified <paramref name="item"/> can be enriched with this data type based on category, type, and id.
        /// </summary>
        /// <param name="item">The item to run these conditions on.</param>
        /// <returns>
        /// <see langword="true"/> if the item passes all filter checks; otherwise, <see langword="false"/>.
        /// </returns>
        public virtual bool IsAllowedOnItem(Item item)
        {
            bool categoryAllowed = itemCategories.IsNullOrEmpty() || (categoryFilter == FilterLogic.NoneExcept && itemCategories.Contains(item.data.category)) || (categoryFilter == FilterLogic.AnyExcept && !itemCategories.Contains(item.data.category));
            bool typeAllowed = itemTypes.IsNullOrEmpty() || (typeFilter == FilterLogic.NoneExcept && itemTypes.Contains(item.data.type)) || (typeFilter == FilterLogic.AnyExcept && !itemTypes.Contains(item.data.type));
            bool itemAllowed = itemIds.IsNullOrEmpty() || (itemFilter == FilterLogic.NoneExcept && itemIds.Contains(item.data.id)) || (itemFilter == FilterLogic.AnyExcept && !itemIds.Contains(item.data.id));
            return typeAllowed && itemAllowed && categoryAllowed;
        }

        public override void OnCatalogRefresh()
        {
            base.OnCatalogRefresh();
            secondarySkillTree = Catalog.GetData<SkillTreeData>(secondarySkillTreeId);
            primarySkillTree = Catalog.GetData<SkillTreeData>(primarySkillTreeId);
            foreach (ItemData itemData in Catalog.GetDataList<ItemData>().Where(i => i.TryGetModule(out ItemModuleCrystal _) && !IgnoredItemIds.Contains(i.id)))
                if (!itemData.TryGetModule(out ItemModuleEnrichmentCore _))
                    itemData.modules.Add(new ItemModuleEnrichmentCore());
        }

        public override IEnumerator LoadAddressableAssetsCoroutine()
        {
            yield return GameManager.local.StartCoroutine(EnrichmentOrb.TryGeneratePools(25));
        }

        /// <summary>
        /// Asynchronously loads (or reuses) the enrichment's associated video clip.
        /// Increments an internal reference count so the clip is only released when all users call <see cref="ReleaseVideo"/>.
        /// </summary>
        /// <param name="onVideoLoaded">Callback invoked when the video is available.</param>
        public void GetVideo(Action<VideoClip> onVideoLoaded)
        {
            if (video != null)
            {
                ++videoCount;
                onVideoLoaded(video);
            }

            Catalog.LoadAssetAsync(videoAddress, (Action<VideoClip>)(clip =>
            {
                if (clip == null) return;
                ++videoCount;
                video = clip;
                onVideoLoaded(video);
            }), "Enrichment: " + id + " loading video");
        }

        /// <summary>
        /// Releases a reference to the video clip associated with this enrichment. 
        /// The clip is only unloaded when all users have released it, to avoid recycling it while still in use.
        /// </summary>
        public void ReleaseVideo()
        {
            --videoCount;
            if (videoCount > 0) return;
            VideoClip video = this.video;
            this.video = null;
            if (!(video != null)) return;
            Catalog.ReleaseAsset(video);
        }

        /// <summary>
        /// Asynchronously loads the sprite used for the enrichment orb icon from addressables.
        /// </summary>
        public void GetOrbIcon(Action<Sprite> callback) => Catalog.LoadAssetAsync(orbIconAddress, callback, id);

        /// <summary>
        /// Asynchronously loads the button icon sprite (enabled/disabled button on the UI) from addressables.
        /// </summary>
        public void GetButtonIcon(bool enabled, Action<Sprite> callback)
        {
            string address = enabled ? buttonEnabledIconAddress : buttonDisabledIconAddress;
            Catalog.LoadAssetAsync(address, callback, id);
        }

        /// <summary>
        /// Called once when this enrichment is loaded onto an item.
        /// </summary>
        /// <param name="item">The item enrichments were loaded onto.</param>
        public virtual void OnEnrichmentLoaded(Item item)
        {
            item.mainCollisionHandler.OnCollisionStartEvent += OnItemCollide;
            if (passDataToHolsteredItems)
                foreach (Holder holder in item.childHolders)
                {
                    Debug.Log($"[Enrichment {id} Data Passing] Unloaded holder: {holder.name} with {holder.items.Count} item(s):\n- " + string.Join("\n- ", holder.items.Select(i => i.data.id)));
                    holder.gameObject.GetOrAddComponent<EnrichmentHolder>().Load(id, this, holder);
                }
        }

        /// <summary>
        /// Called once when this enrichment is unloaded from an item.
        /// </summary>
        /// <param name="item">The item enrichments were unloaded from.</param>
        public virtual void OnEnrichmentUnloaded(Item item)
        {
            item.mainCollisionHandler.OnCollisionStartEvent -= OnItemCollide;
            if (passDataToHolsteredItems)
                foreach (Holder holder in item.childHolders)
                    if (holder.TryGetComponent(out EnrichmentHolder enrichmentHolder))
                    {
                        Debug.Log($"[Enrichment {id} Data Passing] Unloaded holder: {holder.name} with {holder.items.Count} item(s):\n- " + string.Join("\n- ", holder.items.Select(i => i.data.id)));
                        enrichmentHolder.Unload();
                    }
        }

        /// <summary>
        /// Invoked after all enrichments have been loaded onto an item. 
        /// Useful for enrichments that depend on other enrichments being present.
        /// </summary>
        /// <param name="enrichments">A list of all enrichments currently applied to the item.</param>
        public virtual void OnLateEnrichmentsLoaded(List<EnrichmentData> enrichments)
        {
        }

        /// <summary>
        /// Called whenever the enriched item collides with a surface.
        /// </summary>
        /// <param name="collisionInstance">The collisionInstance that occured at the point of contact.</param>
        public virtual void OnItemCollide(CollisionInstance collisionInstance)
        {
        }

        /// <summary>
        /// Called whenever the enriched item is imbued.
        /// </summary>
        public virtual void OnItemImbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
        {
        }

        /// <summary>
        /// Called whenever the enriched item is unimbued.
        /// </summary>
        public virtual void OnItemUnimbued(Item item, Imbue imbue, SpellCastCharge spellCastCharge)
        {
        }

        /// <summary>
        /// Called whenever an item is placed into a holder parented to *this* item, as long as <see cref="passDataToHolsteredItems"/> is enabled.
        /// </summary>
        /// <param name="item">The item snapped.</param>
        /// <param name="holder">The holder the item was snapped into.</param>
        public virtual void OnHolderSnapped(Item item, Holder holder)
        {
        }

        /// <summary>
        /// Called whenever an item is removed from a holder parented to *this* item, as long as <see cref="passDataToHolsteredItems"/> is enabled.
        /// </summary>
        /// <param name="item">The item unsnapped.</param>
        /// <param name="holder">The holder the item was removed from.</param>
        public virtual void OnHolderUnSnapped(Item item, Holder holder)
        {
        }

        /// <summary>
        /// Returns a localized version of the enrichment's name.
        /// </summary>
        /// <returns></returns>
        public string GetName() => LocalizationManager.Instance.TryGetLocalization("Enrichments", displayName);

        /// <summary>
        /// Returns a localized version of the enrichment's description.
        /// </summary>
        /// <returns></returns>
        public string GetDescription() => LocalizationManager.Instance.TryGetLocalization("Enrichments", description);
    }
}