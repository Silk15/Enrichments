using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;

namespace Enrichments;

public class UIEnrichmentCore : ThunderBehaviour, IShowable
{
    public Item item;
    public ItemMagnet itemMagnet;
    public EffectInstance effectInstance;
    public List<EnrichmentData> coreEnrichments = new();
    public SkillTreeCrystal skillTreeCrystal;
    public ExclusionLineRenderer coreExclusionLineRenderer;
    public ItemModuleEnrichmentCore itemModuleEnrichmentCore;
    public List<EnrichmentOrb> coreEnrichmentOrbs = new();
    public List<UIEnrichmentNode> uiEnrichmentNodes = new();
    public bool isGlowing;
    public Action<UIEnrichmentCore> onAsyncLoadingComplete;
    Transform orbitalTransform;
    public GameObject follower;
    public Item heldItem;
    public int tier;
    public float glowTime;
    public float checkCooldown = 0.1f;
    
    private const float CoreOrbOrbitRadius = 0.15f;
    private const float NodeOrbOrbitRadius = 0.1f;
    private const float NodeOrbitRadius = 0.35f;

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
        coreExclusionLineRenderer = orbitalTransform.gameObject.AddComponent<ExclusionLineRenderer>();
        coreExclusionLineRenderer.Disable(0.01f);
        skillTreeCrystal = item.GetComponent<SkillTreeCrystal>();
        var gameObject = new GameObject();
        gameObject.AddComponent<Rigidbody>().isKinematic = true;
        var sphereCollider = gameObject.AddComponent<SphereCollider>();
        sphereCollider.radius = 0.2f;
        sphereCollider.isTrigger = true;
        itemMagnet = gameObject.AddComponent<ItemMagnet>();
        itemMagnet.slots = new List<string>(new[] { "SkillTreeCrystal", "Arrow", "Throwables" });
        itemMagnet.tagFilter = FilterLogic.AnyExcept;
        itemMagnet.catchedItemIgnoreGravityPush = true;
        itemMagnet.magnetReactivateDurationOnRelease = 1f;
        itemMagnet.kinematicLock = true;
        itemMagnet.releaseOnGrabOrTKOnly = true;
        itemMagnet.maxCount = 1;
        itemMagnet.trigger = sphereCollider;
        itemMagnet.massMultiplier = 2f;
        itemModuleEnrichmentCore.Load();
        tier = ExtractTier(item.data.id);
        foreach (EnrichmentData enrichmentData in Catalog.GetDataList<EnrichmentData>().Where(e => !string.IsNullOrEmpty(e.primarySkillTreeId) && e.primarySkillTreeId == skillTreeCrystal.treeName & string.IsNullOrEmpty(e.secondarySkillTreeId)))
        {
            if (tier >= enrichmentData.tier)
                coreEnrichments.Add(enrichmentData);
        }
        foreach (SkillTreeData skillTreeData in Catalog.GetDataList<SkillTreeData>().Where(s => s.showInInfuser))
        {
            var enrichmentsForNode = Catalog.GetDataList<EnrichmentData>().Where(e => !string.IsNullOrEmpty(e.primarySkillTreeId) && e.primarySkillTreeId == skillTreeCrystal.treeName && !string.IsNullOrEmpty(e.secondarySkillTreeId) && e.secondarySkillTreeId == skillTreeData.id && tier >= e.tier);
            if (!enrichmentsForNode.Any()) continue;
            UIEnrichmentNode uiEnrichmentNode = new GameObject($"Enrichment Node: {skillTreeData.id}").AddComponent<UIEnrichmentNode>();
            uiEnrichmentNode.transform.position = transform.position;
            uiEnrichmentNode.transform.rotation = transform.rotation;
            uiEnrichmentNode.Init(this, skillTreeData, enrichmentsForNode.ToList());
            uiEnrichmentNodes.Add(uiEnrichmentNode);
            Debug.Log($"Creating UI Enrichment Node: {uiEnrichmentNode.name}");
        }
        Catalog.LoadAssetAsync<GameObject>( ItemModuleEnrichmentCore.lineRendererAddress, line =>
        {
            coreExclusionLineRenderer.linePrefab = line; 
            onAsyncLoadingComplete?.Invoke(this);
        },  ItemModuleEnrichmentCore.lineRendererAddress);

        item.OnHeldActionEvent -= OnHeldAction;
        item.OnHeldActionEvent += OnHeldAction;
        itemMagnet.OnItemCatchEvent -= OnItemCatch;
        itemMagnet.OnItemReleaseEvent -= OnItemRelease;
        itemMagnet.OnItemCatchEvent += OnItemCatch;
        itemMagnet.OnItemReleaseEvent += OnItemRelease;
    }

    private void OnItemCatch(Item caughtItem, EventTime time)
    {
        if (time == EventTime.OnEnd || !isGlowing || caughtItem.data == null || string.IsNullOrEmpty(caughtItem.data.id)) return;
        itemModuleEnrichmentCore.connectEffectData?.Spawn(transform).Play();
        heldItem = caughtItem;
        itemMagnet.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(caughtItem.ForwardVector(), Vector3.up));
        item.Haptic(1f);
        caughtItem.Haptic(1f);
        Show();
    }

    private void OnItemRelease(Item caughtItem, EventTime time)
    {
        if (time == EventTime.OnStart || !isGlowing) return;
        item.Haptic(1f);
        caughtItem.Haptic(1f);
        heldItem = null;
        itemModuleEnrichmentCore.disconnectEffectData.Spawn(transform).Play();
        Hide();
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
        if (!isGlowing || !itemMagnet || !Player.local) return;
        if (coreEnrichmentOrbs.Count > 0)
        {
            for (int i = 0; i < coreEnrichmentOrbs.Count; i++)
            {
                float angle = 360f / coreEnrichmentOrbs.Count * i * Mathf.Deg2Rad;
                Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * CoreOrbOrbitRadius;
                coreEnrichmentOrbs[i].MoveTo(follower.transform.TransformPoint(localOffset), Quaternion.identity);
            }
        }

        if (uiEnrichmentNodes.Count != 0)
        {
            for (int i = 0; i < uiEnrichmentNodes.Count; i++)
            {
                float angle = 360f / uiEnrichmentNodes.Count * i * Mathf.Deg2Rad;
                Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * NodeOrbitRadius;
                uiEnrichmentNodes[i].transform.position = follower.transform.TransformPoint(localOffset);
                uiEnrichmentNodes[i].transform.rotation = Quaternion.Euler(90, 0, 0);
            }
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
            Hide();
        }
    }
    
    public void Show()
    {
        ShowOrbsInternal(coreEnrichments, coreEnrichmentOrbs, coreExclusionLineRenderer, CoreOrbOrbitRadius, skillTreeCrystal.skillTreeEmissionColor, follower.transform);
        foreach (UIEnrichmentNode uiEnrichmentNode in uiEnrichmentNodes)
            if (uiEnrichmentNode.enrichments.Count > 0) 
                ShowOrbsInternal(uiEnrichmentNode.enrichments, uiEnrichmentNode.enrichmentOrbs, uiEnrichmentNode.exclusionLineRenderer, NodeOrbOrbitRadius, uiEnrichmentNode.skillTreeData.emissionColor, uiEnrichmentNode.transform);
    }

    public void Hide()
    {
        HideOrbsInternal(coreEnrichmentOrbs, coreExclusionLineRenderer);
        foreach (UIEnrichmentNode uiEnrichmentNode in uiEnrichmentNodes) HideOrbsInternal(uiEnrichmentNode.enrichmentOrbs, uiEnrichmentNode.exclusionLineRenderer);
    }

    private void ShowOrbsInternal(List<EnrichmentData> enrichments, List<EnrichmentOrb> populateList, ExclusionLineRenderer exclusionLineRenderer, float orbitRadius, Color lineColor, Transform parentTransform)
    {
        if (enrichments.Count <= 0) return;
    
        populateList.Clear();
        List<EnrichmentOrb> orbs = new();
        List<Vector3> exclusionPoints = new();
        int targetCount = enrichments.Count;
        int loadedCount = 0;

        for (int i = 0; i < enrichments.Count; i++)
        {
            float angle = 360f / enrichments.Count * i * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * orbitRadius;
            Vector3 worldPos = parentTransform.TransformPoint(localOffset);
            exclusionPoints.Add(worldPos);
        }

        exclusionLineRenderer.radius = orbitRadius;
        exclusionLineRenderer.SetPoints(exclusionPoints);
        exclusionLineRenderer.Refresh();
        exclusionLineRenderer.SetColor(lineColor);
        exclusionLineRenderer.Enable();

        foreach (EnrichmentData enrichmentData in enrichments)
        {
            int index = enrichments.IndexOf(enrichmentData);
            float angle = 360f / enrichments.Count * index * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * orbitRadius;
            Vector3 worldPos = parentTransform.TransformPoint(localOffset);
        
            EnrichmentOrb.Get(enrichmentData, this, worldPos, Quaternion.identity, orb =>
            {
                orbs.Add(orb);
                loadedCount++;
                if (loadedCount == targetCount)
                {
                    populateList.AddRange(orbs);
                }
            });
        }
    }

    
    private void HideOrbsInternal(List<EnrichmentOrb> orbListToClear, ExclusionLineRenderer lineRenderer)
    {
        if (orbListToClear?.Count == 0 || orbListToClear == null) return;

        foreach (var orb in orbListToClear)
            if (orb != null) EnrichmentOrb.Release(orb);

        orbListToClear.Clear();
        lineRenderer.Disable();
    }
    
    private static int ExtractTier(string source)
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
}