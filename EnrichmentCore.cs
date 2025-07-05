using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;

namespace Enrichments;

public class EnrichmentCore : ThunderBehaviour
{
    public Item item;
    public ItemMagnet itemMagnet;
    public EffectInstance effectInstance;
    public List<EnrichmentData> enrichments = new();
    public SkillTreeCrystal skillTreeCrystal;
    public ExclusionLineRenderer exclusionLineRenderer;
    public ItemModuleEnrichmentCore itemModuleEnrichmentCore;
    public List<EnrichmentOrb> enrichmentOrbs = new();
    public bool isGlowing;
    Transform orbitalTransform;
    public GameObject follower;
    public Item heldItem;
    public override ManagedLoops EnabledManagedLoops => ManagedLoops.FixedUpdate | ManagedLoops.Update;

    public void Init(Item item, ItemModuleEnrichmentCore itemModuleEnrichmentCore)
    {
        this.item = item;
        this.itemModuleEnrichmentCore = itemModuleEnrichmentCore;
        follower = new GameObject("Follower");
        orbitalTransform = new GameObject("Orbital").transform;
        orbitalTransform.SetParent(follower.transform);
        orbitalTransform.localPosition = Vector3.zero;
        orbitalTransform.localRotation = Quaternion.Euler(90, 0, 0);
        follower.transform.SetPositionAndRotation(item.transform.position, Quaternion.identity);
        exclusionLineRenderer = orbitalTransform.gameObject.AddComponent<ExclusionLineRenderer>();
        Catalog.LoadAssetAsync<GameObject>(itemModuleEnrichmentCore.lineRendererAddress, line => { exclusionLineRenderer.linePrefab = line; }, itemModuleEnrichmentCore.lineRendererAddress);
        exclusionLineRenderer.Disable(0.01f);
        skillTreeCrystal = item.GetComponent<SkillTreeCrystal>();
        var gameObject = new GameObject();
        gameObject.AddComponent<Rigidbody>().isKinematic = true;
        var sphereCollider = gameObject.AddComponent<SphereCollider>();
        sphereCollider.radius = 0.2f;
        sphereCollider.isTrigger = true;
        itemMagnet = gameObject.AddComponent<ItemMagnet>();
        itemMagnet.slots = new List<string>(new[] { "SkillTreeCrystal", "Quiver", "Arrow", "Throwables" });
        itemMagnet.tagFilter = FilterLogic.AnyExcept;
        itemMagnet.catchedItemIgnoreGravityPush = true;
        itemMagnet.magnetReactivateDurationOnRelease = 0.5f;
        itemMagnet.maxCount = 1;
        itemMagnet.trigger = sphereCollider;
        itemMagnet.massMultiplier = 2f;
        itemModuleEnrichmentCore.Load();
        int itemTier = ExtractTier(item.data.id);
        foreach (EnrichmentData enrichmentData in Catalog.GetDataList<EnrichmentData>().Where(e => !string.IsNullOrEmpty(e.skillTreeId) && e.skillTreeId == skillTreeCrystal.treeName))
        {
            if (itemTier >= enrichmentData.tier)
                enrichments.Add(enrichmentData);
        }

        item.OnHeldActionEvent -= OnHeldAction;
        item.OnHeldActionEvent += OnHeldAction;
        itemMagnet.OnItemCatchEvent -= OnItemCatch;
        itemMagnet.OnItemReleaseEvent -= OnItemRelease;
        itemMagnet.OnItemCatchEvent += OnItemCatch;
        itemMagnet.OnItemReleaseEvent += OnItemRelease;
    }

    private int ExtractTier(string source)
    {
        if (string.IsNullOrEmpty(source)) return -1;
        for (int i = 0; i < source.Length - 1; i++)
        {
            if (source[i] == 'T' && char.IsDigit(source[i + 1]))
            {
                int j = i + 1;
                while (j < source.Length && char.IsDigit(source[j])) j++;
                if (int.TryParse(source.Substring(i + 1, j - i - 1), out int tier))
                    return tier;
            }
        }

        return -1;
    }


    private void OnItemCatch(Item caughtItem, EventTime time)
    {
        if (time == EventTime.OnEnd || !isGlowing || caughtItem.data == null || string.IsNullOrEmpty(caughtItem.data.id)) return;
        itemModuleEnrichmentCore.connectEffectData?.Spawn(transform).Play();
        heldItem = caughtItem;
        itemMagnet.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(caughtItem.ForwardVector(), Vector3.up));
        item.Haptic(1f);
        caughtItem.Haptic(1f);
        Enable();
    }

    private void OnItemRelease(Item caughtItem, EventTime time)
    {
        if (time == EventTime.OnStart || !isGlowing) return;
        item.Haptic(1f);
        caughtItem.Haptic(1f);
        heldItem = null;
        itemModuleEnrichmentCore.disconnectEffectData.Spawn(transform).Play();
        Disable();
    }


    public void OnHeldAction(RagdollHand hand, Handle handle, Interactable.Action action)
    {
        switch (action)
        {
            case Interactable.Action.AlternateUseStart:
                Toggle(true);
                break;
            case Interactable.Action.AlternateUseStop:
            case Interactable.Action.Ungrab:
                Toggle(false);
                break;
        }
    }

    protected override void ManagedUpdate()
    {
        base.ManagedUpdate();
        if (!isGlowing || !itemMagnet || !Player.local) return;

        itemMagnet.transform.position = Vector3.Slerp(itemMagnet.transform.position, transform.position + Vector3.up * 0.35f, Time.deltaTime * 10f);
        follower.transform.position = Vector3.Slerp(follower.transform.position, item.transform.position, Time.deltaTime * 10f);
    }

    protected override void ManagedFixedUpdate()
    {
        base.ManagedFixedUpdate();
        if (!isGlowing || !itemMagnet || !Player.local || enrichmentOrbs.Count == 0) return;

        for (int i = 0; i < enrichmentOrbs.Count; i++)
        {
            float angle = 360f / enrichmentOrbs.Count * i * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.15f;
            enrichmentOrbs[i].MoveTo(follower.transform.TransformPoint(localOffset), Quaternion.identity);
        }
    }


    public void Toggle(bool shown)
    {
        if (isGlowing == shown) return;
        item.Haptic(1);
        isGlowing = shown;
        if (shown)
        {
            itemMagnet.transform.position = Vector3.Slerp(itemMagnet.transform.position, transform.position + Vector3.up * 0.35f, Time.deltaTime * 10f);
            itemMagnet.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.up, Vector3.up), Vector3.up);
            itemMagnet.transform.SetParent(null);
            itemMagnet.enabled = true;
            itemMagnet.trigger.enabled = true;
            effectInstance = itemModuleEnrichmentCore.loopEffectData?.Spawn(itemMagnet.transform);
            effectInstance?.Play();
        }
        else
        {
            effectInstance.SetParent(null);
            effectInstance?.End();
            effectInstance = null;
            if (itemMagnet.capturedItems.Count == 0) itemModuleEnrichmentCore.disconnectEffectData?.Spawn(transform).Play();
            itemMagnet.enabled = false;
            itemMagnet.trigger.enabled = false;
            itemMagnet.transform.SetParent(transform);
            itemMagnet.transform.localPosition = Vector3.zero;
            itemMagnet.transform.localRotation = Quaternion.identity;
            heldItem = null;
            Disable();
        }
    }

    public void Enable()
    {
        enrichmentOrbs.Clear();
        List<EnrichmentOrb> orbs = new();
        int targetCount = enrichments.Count;
        int loadedCount = 0;

        foreach (EnrichmentData enrichmentData in enrichments)
        {
            int index = enrichments.IndexOf(enrichmentData);
            float angle = 360f / enrichments.Count * index * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.15f;
            Vector3 worldPos = follower.transform.TransformPoint(localOffset);

            EnrichmentOrb.Get(enrichmentData, this, worldPos, Quaternion.identity, orb =>
            {
                orbs.Add(orb);
                loadedCount++;
                if (loadedCount == targetCount)
                {
                    enrichmentOrbs = orbs;
                    exclusionLineRenderer.SetPoints(enrichmentOrbs.Select(s => s.transform).ToList());
                    exclusionLineRenderer.radius = 0.15f;
                    exclusionLineRenderer.Refresh();
                    exclusionLineRenderer.SetColor(skillTreeCrystal.skillTreeEmissionColor);
                    exclusionLineRenderer.Enable();
                }
            });
        }
    }

    public void Disable()
    {
        if (enrichmentOrbs?.Count == 0 || enrichmentOrbs == null) return;

        foreach (var orb in enrichmentOrbs)
            if (orb != null) EnrichmentOrb.Release(orb);

        enrichmentOrbs.Clear();
        exclusionLineRenderer.Disable();
    }
}