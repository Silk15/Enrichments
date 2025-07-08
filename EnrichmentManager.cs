using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ThunderRoad;
using UnityEngine;

namespace Enrichments;

public class EnrichmentManager : ThunderScript
{
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
        if (creature.isPlayer) creature.gameObject.GetOrAddComponent<UIEnrichmentSlotHighlighter>().Load(creature);
        creature.mana.OnImbueLoadEvent += ImbueLoad;
        creature.mana.OnImbueUnloadEvent += ImbueUnload;
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

    public static bool HasEnrichments(Item item) => Enrichments.ContainsKey(item);
    
    public static bool IsAtMaxEnrichments(Item item)
    {
        if (!HasEnrichments(item)) return false;
        return Enrichments[item].Count == 4;
    }

    private static void ImbueLoad(SpellCastCharge spellCastCharge, Imbue imbue)
    {
        if (!(imbue?.colliderGroup?.collisionHandler?.Entity is Item item)) return;
        if (Enrichments.ContainsKey(item))
            foreach (EnrichmentData enrichmentData in Enrichments[item])
            {
                try
                {
                    enrichmentData.OnItemImbued(item, imbue, spellCastCharge);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Enrichments] Caught exception while loading imbue: {spellCastCharge.id} on item: {item.data.id} for enrichment: {enrichmentData.id}");
                }
            }
    }

    private static void ImbueUnload(SpellCastCharge spellCastCharge, Imbue imbue)
    {
        if (!(imbue?.colliderGroup?.collisionHandler?.Entity is Item item)) return;
        if (Enrichments.ContainsKey(item))
            foreach (EnrichmentData enrichmentData in Enrichments[item])
            {
                try
                {
                    enrichmentData.OnItemUnimbued(item, imbue, spellCastCharge);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Enrichments] Caught exception while unloading imbue: {spellCastCharge.id} on item: {item.data.id} for enrichment: {enrichmentData.id}");
                }
            }
    }

    public static bool HasEnrichment(Item item, string id) => item != null && Enrichments.ContainsKey(item) && Enrichments[item].Any(e => e?.id == id);
    
    public static EnrichmentData GetEnrichment(Item item, string id) => Enrichments[item].FirstOrDefault(e => e.id == id);

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

    private static void Validate(Item item, out List<EnrichmentData> enrichments)
    {
        if (!Enrichments.TryGetValue(item, out var list))
        {
            list = new List<EnrichmentData>();
            Enrichments[item] = list;
        }

        enrichments = list;
    }

    public static void AddEnrichment(Item item, string id)
    {
        var enrichmentData = Catalog.GetData<EnrichmentData>(id);
        AddEnrichment(item, enrichmentData);
    }

    public static void AddEnrichment(Item item, EnrichmentData enrichmentData)
    {
        item.GetOrAddCustomData<ContentCustomEnrichment>().Add(enrichmentData.id);
        Validate(item, out var enrichments);
        try
        {
            Debug.Log($"[Enrichments] Item: {item.data.id} loaded enrichment: {enrichmentData.id}");
            enrichmentData.OnEnrichmentLoaded(item);
            enrichments.Add(enrichmentData);
        }
        catch (Exception e) { Debug.LogError($"[Enrichments] Caught exception while loading enrichment: {enrichmentData.id} on item: {item.data.id}. {e}"); }
    }

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
        catch (Exception e) { Debug.LogError($"[Enrichments] Caught exception while unloading enrichment: {enrichmentData.id} on item: {item.data.id}. {e}"); }
    }

    public static void LoadEnrichments(Item item)
    {
        if (item.TryGetCustomData(out ContentCustomEnrichment contentEnrichment))
        {
            var ids = new List<string>(contentEnrichment.enrichments);
            Validate(item, out var enrichments);
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                var id = ids[i];
                if (Catalog.TryGetData(id, out EnrichmentData enrichmentData))
                {
                    try
                    {
                        enrichmentData.OnEnrichmentLoaded(item);
                        enrichments.Add(enrichmentData);
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
            catch (Exception e) { Debug.LogError($"[Enrichments] Caught exception while unloading enrichment, skipping: {enrichment.id} on item: {item.data.id}. {e}"); }
        }

        Debug.Log($"[{item.data.id}] Unloaded item enrichments:\n- " + string.Join("\n- ", unloadedIds));
        enrichments.Clear();
        Enrichments.Remove(item);
    }
}