using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ThunderRoad;
using UnityEngine;

namespace Enrichments;

/// <summary>
/// The central manager for all enrichments, handles the lifecycle of custom enrichments, loading, unloading, saving and event invocation.
/// </summary>
public class EnrichmentManager : ThunderScript
{
    /// <summary>
    /// A collection of every active item with an enrichment.
    /// </summary>
    public static Dictionary<Item, List<EnrichmentData>> Enrichments { get; } = new();

    public override void ScriptEnable()
    {
        base.ScriptEnable();
        new Harmony("com.silk.enrichments").PatchAll();
        Item.OnItemSpawn += OnItemSpawn;
        Item.OnItemDespawn += OnItemDespawn;
        EventManager.onCreatureSpawn += OnCreatureSpawn;
        EventManager.onCreatureDespawn += OnCreatureDespawn;
    }

    public override void ScriptDisable()
    {
        base.ScriptDisable();
        Item.OnItemSpawn -= OnItemSpawn;
        Item.OnItemDespawn -= OnItemDespawn;
        EventManager.onCreatureSpawn -= OnCreatureSpawn;
        EventManager.onCreatureDespawn -= OnCreatureDespawn;
    }

    private void OnCreatureSpawn(Creature creature)
    {
        if (creature && creature.mana)
        {
            if (creature.isPlayer) creature.gameObject.GetOrAddComponent<UIEnrichmentSlotHighlighter>().Load(creature);
            creature.mana.OnImbueLoadEvent += ImbueLoad;
            creature.mana.OnImbueUnloadEvent += ImbueUnload;
        }
    }

    private void OnCreatureDespawn(Creature creature, EventTime eventTime)
    {
        if (eventTime == EventTime.OnEnd) return;
        if (creature.isPlayer && creature.TryGetComponent(out UIEnrichmentSlotHighlighter slotHighlighter)) slotHighlighter.Unload();
        creature.mana.OnImbueLoadEvent -= ImbueLoad;
        creature.mana.OnImbueUnloadEvent -= ImbueUnload;
    }

    private void OnItemSpawn(Item item) => LoadEnrichments(item);

    private void OnItemDespawn(Item item) => UnloadEnrichments(item);
    
    private static void ImbueLoad(SpellCastCharge spellCastCharge, Imbue imbue)
    {
        if (!(imbue?.colliderGroup?.collisionHandler?.Entity is Item item)) return;
        if (Enrichments.ContainsKey(item))
            foreach (EnrichmentData enrichmentData in Enrichments[item])
            {
                if (!item || !imbue || spellCastCharge == null)
                {
                    Debug.LogError("[Enrichments] ImbueUnload has null parameters, OnItemImbued will not be run!");
                    return;
                }
                try
                {
                    enrichmentData.OnItemImbued(item, imbue, spellCastCharge);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Enrichments] Caught exception while loading imbue: {spellCastCharge.id} on item: {item.data.id} at enrichment: {enrichmentData.id}. Skipping. Exception below:");
                    Debug.LogException(e);
                }
            }
    }

    private static void ImbueUnload(SpellCastCharge spellCastCharge, Imbue imbue)
    {
        if (!(imbue?.colliderGroup?.collisionHandler?.Entity is Item item)) return;
        if (Enrichments.ContainsKey(item))
            foreach (EnrichmentData enrichmentData in Enrichments[item])
            {
                if (!item || !imbue || spellCastCharge == null)
                {
                    Debug.LogError("[Enrichments] ImbueUnload has null parameters, OnItemUnimbued will not be run!");
                    return;
                }
                try
                {
                    enrichmentData.OnItemUnimbued(item, imbue, spellCastCharge);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Enrichments] Caught exception while unloading imbue: {spellCastCharge.id} on item: {item.data.id} at enrichment: {enrichmentData.id}. Skipping. Exception below:");
                    Debug.LogException(e);
                }
            }
    }
    
    /// <summary>
    /// Internal helper to validate an item's enrichments within the main collection.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    /// <param name="enrichments">The collection of enrichments validated.</param>
    public static void Validate(Item item, out List<EnrichmentData> enrichments)
    {
        if (!Enrichments.TryGetValue(item, out var list))
        {
            list = new List<EnrichmentData>();
            Enrichments[item] = list;
        }

        enrichments = list;
    }

    /// <summary>
    /// Determines whether the specified <paramref name="item"/> has any enrichments.
    /// </summary>
    /// <param name="item">The item to check for enrichments.</param>
    /// <returns>
    /// <see langword="true"/> if the item has enrichments; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool HasEnrichments(Item item) => Enrichments.ContainsKey(item);

    /// <summary>
    /// Determines whether the specified <paramref name="item"/> is at the maximum number of enrichments.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>
    /// <see langword="true"/> if the item’s enrichment count is greater than or equal to 
    /// <see cref="ContentCustomEnrichment.MaxEnrichments"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsAtMaxEnrichments(Item item)
    {
        if (!HasEnrichments(item)) return false;
        return Enrichments[item].Count == item.GetOrAddCustomData<ContentCustomEnrichment>().MaxEnrichments;
    }

    /// <summary>
    /// Determines whether the specified <paramref name="item"/> has an enrichment 
    /// with the given <paramref name="id"/>.
    /// </summary>
    /// <param name="item">The item to check for the enrichment.</param>
    /// <param name="id">The identifier of the enrichment to look for.</param>
    /// <returns>
    /// <see langword="true"/> if the enrichment with the specified <paramref name="id"/> exists; 
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool HasEnrichment(Item item, string id) => item != null && Enrichments.ContainsKey(item) && Enrichments[item].Any(e => e?.id == id);

    /// <summary>
    /// Retrieves the enrichment with the specified <paramref name="id"/> from the given <paramref name="item"/>, otherwise returns null.
    /// </summary>
    /// <param name="item">The item to search for enrichments.</param>
    /// <param name="id">The identifier of the enrichment to retrieve.</param>
    /// <returns>
    /// The <see cref="EnrichmentData"/> with the specified <paramref name="id"/>, 
    /// or <see langword="null"/> if no enrichment is found.
    /// </returns>
    public static EnrichmentData GetEnrichment(Item item, string id) => Enrichments[item].FirstOrDefault(e => e.id == id);

    /// <summary>
    /// Attempts to retrieve an enrichment with the specified <paramref name="id"/> from the given <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The item to search for enrichments.</param>
    /// <param name="id">The identifier of the enrichment to retrieve.</param>
    /// <param name="enrichment">
    /// When this method returns <see langword="true"/>, contains the <see cref="EnrichmentData"/> 
    /// with the specified <paramref name="id"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the enrichment was found; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetEnrichment(Item item, string id, out EnrichmentData enrichment)
    {
        enrichment = null;

        if (item == null || string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[Enrichments] Item or id is null or empty!");
            return false;
        }

        if (Enrichments.TryGetValue(item, out var list))
        {
            enrichment = list.Find(e => e.id == id);
            return enrichment != null;
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve all enrichments associated with the given <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The item to search for enrichments.</param>
    /// <param name="enrichments">
    /// When this method returns <see langword="true"/>, contains the list of <see cref="EnrichmentData"/> 
    /// associated with the <paramref name="item"/>; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the item has one or more enrichments; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetEnrichments(Item item, out List<EnrichmentData> enrichments)
    {
        enrichments = null;

        if (item == null)
        {
            Debug.LogWarning($"[Enrichments] Item is null!");
            return false;
        }

        return Enrichments.TryGetValue(item, out enrichments);
    }

    /// <summary>
    /// Adds an enrichment to the specified <paramref name="item"/> by enrichment id.
    /// </summary>
    /// <param name="item">The item to enrich.</param>
    /// <param name="id">The identifier of the enrichment to add.</param>
    public static void AddEnrichment(Item item, string id)
    {
        var enrichmentData = Catalog.GetData<EnrichmentData>(id);
        AddEnrichment(item, enrichmentData);
    }

    /// <summary>
    /// Adds the specified <paramref name="enrichmentData"/> to the given <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The item to enrich.</param>
    /// <param name="enrichmentData">The enrichment data to attach to the item.</param>
    public static void AddEnrichment(Item item, EnrichmentData enrichmentData)
    {
        item.GetOrAddCustomData<ContentCustomEnrichment>().Add(enrichmentData.id);
        Validate(item, out var enrichments);
        try
        {
            Debug.Log($"[Enrichments] Item: {item.data.id} loaded enrichment: {enrichmentData.id}");
            if (enrichmentData.Clone() is not EnrichmentData clone) return;
            clone.OnEnrichmentLoaded(item);
            enrichments.Add(clone);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Enrichments] Caught exception while loading enrichment: {enrichmentData.id} on item: {item.data.id}. {e}");
        }
    }

    /// <summary>
    /// Removes the enrichment with the given <paramref name="id"/> from the specified <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The item from which to remove the enrichment.</param>
    /// <param name="id">The identifier of the enrichment to remove.</param>
    public static void RemoveEnrichment(Item item, string id)
    {
        if (!Enrichments.TryGetValue(item, out var enrichments)) return;
        var enrichmentData = enrichments.FirstOrDefault(e => e.id == id);
        if (enrichmentData == null) return;
        var contentCustomEnrichment = item.GetOrAddCustomData<ContentCustomEnrichment>();
        if (contentCustomEnrichment.Has(id)) contentCustomEnrichment.Remove(enrichmentData.id);
        try
        {
            Debug.Log($"[Enrichments] Item: {item.data.id} unloaded enrichment: {enrichmentData.id}");
            enrichmentData.OnEnrichmentUnloaded(item);
            enrichments.Remove(enrichmentData);
            if (enrichments.Count == 0)
            {
                Enrichments.Remove(item);
                item.RemoveCustomData<ContentCustomEnrichment>();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Enrichments] Caught exception while unloading enrichment: {enrichmentData.id} on item: {item.data.id}. {e}");
        }
    }


    /// <summary>
    /// Loads and initializes all enrichments previously stored on the given <paramref name="item"/>'s <see cref="ContentCustomEnrichment"/>. Exits silently if not found.
    /// </summary>
    /// <param name="item">The item whose enrichments should be loaded.</param>
    public static void LoadEnrichments(Item item)
    {
        if (item.TryGetCustomData(out ContentCustomEnrichment contentEnrichment))
        {
            var ids = new List<string>(contentEnrichment.Enrichments);
            Validate(item, out var enrichments);
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                var id = ids[i];
                if (Catalog.TryGetData(id, out EnrichmentData enrichmentData))
                {
                    try
                    {
                        if (enrichmentData.Clone() is not EnrichmentData clone)
                        {
                            Debug.LogWarning($"[Enrichments] Clone() for enrichment '{enrichmentData.id}' returned a non-{nameof(EnrichmentData)} type ({enrichmentData.Clone()?.GetType().FullName ?? "null"}). Skipping.");
                            continue;
                        }

                        clone.OnEnrichmentLoaded(item);
                        enrichments.Add(clone);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Enrichments] Caught exception while loading enrichment, skipping: {id} on item: {item.data.id}. {e}");
                        ids.RemoveAt(i);
                    }
                }
                else Debug.LogWarning($"[Enrichments] Enrichment: {id} exists in item: {item.data.id}, but does not exist as a Json. Ensure your mod has a manifest and that the enrichment is valid.");
            }

            foreach (EnrichmentData enrichmentData in enrichments) enrichmentData.OnLateEnrichmentsLoaded(enrichments);
            string slot = item.holder != null ? $"from slot: {item.holder}" : "";
            Debug.Log($"[{item.data.id}] Loaded item enrichments{slot}:\n- " + string.Join("\n- ", ids));
        }
    }

    /// <summary>
    /// Unloads and clears all enrichments currently applied to the specified <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The item whose enrichments should be unloaded.</param>
    public static void UnloadEnrichments(Item item)
    {
        if (!Enrichments.TryGetValue(item, out var enrichments)) return;
        var unloadedIds = new List<string>();
        foreach (var enrichment in enrichments)
        {
            try
            {
                enrichment.OnEnrichmentUnloaded(item);
                unloadedIds.Add(enrichment.id);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Enrichments] Caught exception while unloading enrichment, skipping: {enrichment.id} on item: {item.data.id}. {e}");
            }
        }

        Debug.Log($"[{item.data.id}] Unloaded item enrichments:\n- " + string.Join("\n- ", unloadedIds));
        enrichments.Clear();
        Enrichments.Remove(item);
    }
}