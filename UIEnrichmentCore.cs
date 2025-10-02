using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ThunderRoad;
using UnityEngine;

namespace Enrichments;

public class UIEnrichmentCore : ThunderBehaviour
{
    private const float CoreOrbOrbitRadius = 0.15f;
    private const float NodeOrbitRadius = 0.35f;

    public List<UIEnrichmentTierNode> uiEnrichmentNodes = new();
    public List<EnrichmentOrb> coreEnrichmentOrbs = new();
    public List<EnrichmentData> coreEnrichments = new();

    public ItemModuleEnrichmentCore itemModuleEnrichmentCore;
    public ExclusionLineRenderer coreExclusionLineRenderer;
    public SkillTreeCrystal skillTreeCrystal;
    public EffectInstance effectInstance;
    public Transform orbitalTransform;
    public Quaternion hoverRotation;
    public ItemMagnet itemMagnet;
    public Vector3 hoverOrigin;
    public GameObject follower;
    public Item heldItem;
    public RBPID pid;
    public Item item;

    public bool initialized;
    public bool isGlowing;
    public bool isShown;
    public int tier;

    public float bobbingFrequency = 0.25f;
    public float bobbingAmplitude = 0.05f;
    protected float bobbingTime;

    public override ManagedLoops EnabledManagedLoops => ManagedLoops.FixedUpdate | ManagedLoops.Update;

    public float BobbingOffset()
    {
        bobbingTime += Time.fixedDeltaTime;
        return Mathf.Sin(bobbingTime * bobbingFrequency * Mathf.PI * 2f) * bobbingAmplitude;
    }

    public void Init(Item item, ItemModuleEnrichmentCore itemModuleEnrichmentCore)
    { 
        if (initialized) ResetCore();
        initialized = true;
        this.item = item;
        this.itemModuleEnrichmentCore = itemModuleEnrichmentCore;
        pid = new RBPID(item.physicBody.rigidBody, forceMode: ForceMode.Acceleration).Position(30f, 1f, 10f).Rotation(40f, 0f, 10f);
        
        follower = new GameObject("Follower");
        orbitalTransform = new GameObject("Orbital").transform;
        orbitalTransform.SetParent(follower.transform);
        orbitalTransform.localPosition = Vector3.zero;
        orbitalTransform.localRotation = Quaternion.Euler(90, 0, 0);
        
        follower.transform.SetPositionAndRotation(item.transform.position, Quaternion.identity);
        coreExclusionLineRenderer = orbitalTransform.gameObject.GetOrAddComponent<ExclusionLineRenderer>();
        coreExclusionLineRenderer.Disable(0.01f);
        skillTreeCrystal = item.GetComponent<SkillTreeCrystal>();
        var gameObject = new GameObject();
        gameObject.GetOrAddComponent<Rigidbody>().isKinematic = true;
        var sphereCollider = gameObject.GetOrAddComponent<SphereCollider>();
        sphereCollider.radius = 0.2f;
        sphereCollider.isTrigger = true;
        itemMagnet = gameObject.GetOrAddComponent<ItemMagnet>();
        itemMagnet.slots = new List<string>(new[] { "SkillTreeCrystal", "Arrow", "Throwables" });
        itemMagnet.tagFilter = FilterLogic.AnyExcept;
        itemMagnet.catchedItemIgnoreGravityPush = true;
        itemMagnet.magnetReactivateDurationOnRelease = 1f;
        itemMagnet.kinematicLock = true;
        itemMagnet.releaseOnGrabOrTKOnly = false;
        itemMagnet.maxCount = 1;
        itemMagnet.trigger = sphereCollider;
        itemMagnet.massMultiplier = 2f;
        itemModuleEnrichmentCore.Load();
        tier = ExtractTier(item.data.id);

        foreach (EnrichmentData enrichmentData in Catalog.GetDataList<EnrichmentData>().Where(e => !string.IsNullOrEmpty(e.primarySkillTreeId) && e.primarySkillTreeId == skillTreeCrystal.treeName & string.IsNullOrEmpty(e.secondarySkillTreeId) && e.showInCore))
            if (tier >= enrichmentData.tier)
                coreEnrichments.Add(enrichmentData);

        Catalog.LoadAssetAsync<GameObject>(ItemModuleEnrichmentCore.lineRendererAddress, line => { coreExclusionLineRenderer.linePrefab = line; }, ItemModuleEnrichmentCore.lineRendererAddress);

        if (uiEnrichmentNodes.Count > 0) 
            foreach (UIEnrichmentTierNode node in uiEnrichmentNodes)
                Destroy(node);
        
        foreach (SkillTreeData skillTreeData in Catalog.GetDataList<SkillTreeData>().Where(s => s.showInInfuser))
        {
            var enrichmentsForNode = Catalog.GetDataList<EnrichmentData>().Where(e => !string.IsNullOrEmpty(e.primarySkillTreeId) && e.primarySkillTreeId == skillTreeCrystal.treeName && !string.IsNullOrEmpty(e.secondarySkillTreeId) && e.secondarySkillTreeId == skillTreeData.id && tier >= e.tier).ToList();
            if (enrichmentsForNode.Count == 0) continue;
            UIEnrichmentTierNode uiEnrichmentTierNode = new GameObject($"Enrichment Node: {skillTreeData.id}").AddComponent<UIEnrichmentTierNode>();
            uiEnrichmentTierNode.transform.position = transform.position;
            uiEnrichmentTierNode.transform.rotation = transform.rotation;
            uiEnrichmentTierNode.Init(this, skillTreeData, enrichmentsForNode?.ToList());
            uiEnrichmentNodes.Add(uiEnrichmentTierNode);
        }

        item.OnHeldActionEvent -= OnHeldAction;
        item.OnHeldActionEvent += OnHeldAction;
        item.OnDespawnEvent += OnDespawnEvent;
    }
    
    public void ResetCore()
    {
        if (item)
        {
            item.OnHeldActionEvent -= OnHeldAction;
            item.OnDespawnEvent -= OnDespawnEvent;
        }
        if (itemMagnet)
        {
            itemMagnet.OnItemCatchEvent -= OnItemCatch;
            itemMagnet.OnItemReleaseEvent -= OnItemRelease;
        }
        
        effectInstance?.SetParent(null);
        effectInstance?.End();
        effectInstance = null;
        
        foreach (var orb in coreEnrichmentOrbs) VizManager.ClearViz(this, $"orb{orb.enrichmentData.id}{skillTreeCrystal.treeName}");
        coreEnrichmentOrbs.Clear();
        
        if (coreExclusionLineRenderer != null) Catalog.ReleaseAsset(coreExclusionLineRenderer);

        foreach (var node in uiEnrichmentNodes)
        {
            node.Hide();
            node.RemoveDelegates();
            Destroy(node.gameObject);
        }
        uiEnrichmentNodes.Clear();
        
        if (follower) Destroy(follower);
        follower = null;
        orbitalTransform = null;

        if (itemMagnet) Destroy(itemMagnet.gameObject);
        itemMagnet = null;
    }
    
    private void OnDespawnEvent(EventTime eventTime)
    {
        if (eventTime == EventTime.OnStart) Toggle(false);
    }

    private void OnItemCatch(Item caughtItem, EventTime time)
    {
        if (time == EventTime.OnEnd || !isGlowing || caughtItem.data == null || string.IsNullOrEmpty(caughtItem.data.id) && caughtItem != item || caughtItem.data.id.Contains("Crystal")) return;
        itemModuleEnrichmentCore.connectEffectData?.Spawn(transform).Play();
        heldItem = caughtItem;
        itemMagnet.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(caughtItem.ForwardVector(), Vector3.up));
        item.Haptic(1f);
        caughtItem.Haptic(1f);
        follower.transform.position = item.transform.position;
        Show();
    }

    private void OnItemRelease(Item caughtItem, EventTime time)
    {
        if (caughtItem == null || time == EventTime.OnStart || !isGlowing && caughtItem != item || caughtItem.data.id.Contains("Crystal")) return;
        item.Haptic(1f);
        caughtItem.Haptic(1f);
        heldItem = null;
        itemModuleEnrichmentCore.disconnectEffectData.Spawn(transform).Play();
        Toggle(false);
    }

    public void OnHeldAction(RagdollHand hand, Handle handle, Interactable.Action action)
    {
        switch (action)
        {
            case Interactable.Action.AlternateUseStart:
                Toggle(true);
                break;
            case Interactable.Action.AlternateUseStop:
                Toggle(false);
                break;
            case Interactable.Action.Ungrab or Interactable.Action.Grab:
                hoverOrigin = item.transform.position;
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
        if (!isGlowing || !itemMagnet || !Player.local || !heldItem) return;
        if (!item.IsHeldByPlayer) UpdateMove(hoverOrigin, hoverRotation);
        if (coreEnrichmentOrbs.Count > 0)
        {
            for (int i = 0; i < coreEnrichmentOrbs.Count; i++)
            {
                float angle = 360f / coreEnrichmentOrbs.Count * i * Mathf.Deg2Rad;
                Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * CoreOrbOrbitRadius;
                coreEnrichmentOrbs[i].MoveTo(follower.transform.TransformPoint(localOffset), Quaternion.identity);
                VizManager.AddOrUpdateViz(this, $"orb{coreEnrichmentOrbs[i].enrichmentData.id}{skillTreeCrystal.treeName}", coreEnrichmentOrbs[i].enrichmentData.primarySkillTree.color, VizManager.VizType.Lines, new []{follower.transform.TransformPoint(localOffset), coreEnrichmentOrbs[i].transform.position});
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

    public void UpdateMove(Vector3 desiredPos, Quaternion desiredRot)
    {
        float bobOffset = BobbingOffset();
        pid.Update(desiredPos + Vector3.up * bobOffset, desiredRot);
    }

    public void Toggle(bool shown)
    {
        if (isGlowing == shown) return;
        item.Haptic(1);
        isGlowing = shown;
        if (shown)
        {
            itemMagnet.transform.position = transform.position + Vector3.up * 0.35f;
            itemMagnet.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.up, Vector3.up), Vector3.up);
            itemMagnet.transform.SetParent(null);
            itemMagnet.Unlock();
            itemMagnet.enabled = true;
            itemMagnet.trigger.enabled = true;
            effectInstance = itemModuleEnrichmentCore.loopEffectData?.Spawn(itemMagnet.transform);
            effectInstance?.Play();
            item.SetPhysicModifier(this, 0f);
            itemMagnet.OnItemCatchEvent -= OnItemCatch;
            itemMagnet.OnItemReleaseEvent -= OnItemRelease;
            itemMagnet.OnItemCatchEvent += OnItemCatch;
            itemMagnet.OnItemReleaseEvent += OnItemRelease;
        }
        else
        {
            if (heldItem != null && itemMagnet.capturedItems.Count > 0)
                itemMagnet.ReleaseItem(itemMagnet.capturedItems.FirstOrDefault(c => c.item == heldItem));
            effectInstance.SetParent(null);
            effectInstance?.Stop();
            effectInstance = null;
            if (itemMagnet.capturedItems.Count == 0)
                itemModuleEnrichmentCore.disconnectEffectData?.Spawn(transform).Play();
            itemMagnet.enabled = false;
            itemMagnet.trigger.enabled = false;
            itemMagnet.transform.SetParent(transform);
            itemMagnet.transform.localPosition = Vector3.zero;
            itemMagnet.transform.localRotation = Quaternion.identity;
            item.RemovePhysicModifier(this);
            itemMagnet.OnItemCatchEvent -= OnItemCatch;
            itemMagnet.OnItemReleaseEvent -= OnItemRelease;
            heldItem = null;
            if (isShown)
            {
                for (int i = 0; i < uiEnrichmentNodes.Count; i++) uiEnrichmentNodes[i].Toggle(false);
                Hide();
            }

            foreach (UIEnrichmentTierNode node in uiEnrichmentNodes) node.Hide();
        }
    }

    public void Show()
    {
        if (isShown) return;
        isShown = true;
        GameManager.local.StartCoroutine(ShowCoroutine(coreEnrichments, coreEnrichmentOrbs, coreExclusionLineRenderer, CoreOrbOrbitRadius, skillTreeCrystal.skillTreeEmissionColor, follower.transform));
    }

    public void Hide()
    {
        if (!isShown) return;
        isShown = false;
        foreach (EnrichmentOrb item in coreEnrichmentOrbs) VizManager.ClearViz(this, $"orb{item.enrichmentData.id}{skillTreeCrystal.treeName}");
        for (int i = 0; i < uiEnrichmentNodes.Count; i++) uiEnrichmentNodes[i].Toggle(false);
        if (itemMagnet)
        {
            if (itemMagnet.capturedItems.Count == 1) itemMagnet.ReleaseItem(itemMagnet.capturedItems[0]);
            itemMagnet.Lock();
        }

        GameManager.local.StartCoroutine(HideCoroutine(coreEnrichmentOrbs, coreExclusionLineRenderer));
    }

    public IEnumerator ShowCoroutine(List<EnrichmentData> enrichments, List<EnrichmentOrb> populateList, ExclusionLineRenderer exclusionLineRenderer, float orbitRadius, Color lineColor, Transform parentTransform)
    {
        if (enrichments.Count <= 0) yield break;
        populateList.Clear();
        yield return Yielders.EndOfFrame;

        List<EnrichmentData> validEnrichments = new();
        foreach (var enrichment in enrichments)
        {
            if (heldItem && enrichment.IsAllowedOnItem(heldItem))
                validEnrichments.Add(enrichment);
        }

        if (validEnrichments.Count <= 0) yield break;

        List<Vector3> exclusionPoints = new();
        for (int i = 0; i < validEnrichments.Count; i++)
        {
            float angle = 360f / validEnrichments.Count * i * Mathf.Deg2Rad;
            Vector3 worldPos = parentTransform.TransformPoint(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * orbitRadius);
            exclusionPoints.Add(worldPos);
            EnrichmentOrb.Get(validEnrichments[i], this, worldPos, Quaternion.identity, orb => populateList.Add(orb));
            yield return Yielders.EndOfFrame;
        }

        exclusionLineRenderer.radius = orbitRadius;
        exclusionLineRenderer.SetPoints(exclusionPoints);
        yield return Yielders.EndOfFrame;
        exclusionLineRenderer.Refresh();
        exclusionLineRenderer.SetColor(lineColor);
        exclusionLineRenderer.Enable();
    }

    public IEnumerator HideCoroutine(List<EnrichmentOrb> orbListToClear, ExclusionLineRenderer lineRenderer)
    {
        if (orbListToClear == null || orbListToClear.Count == 0) yield break;

        for (int i = 0; i < orbListToClear.Count; i++)
        {
            var orb = orbListToClear[i];
            if (orb != null) orb.Release();
            yield return null;
        }

        orbListToClear.Clear();
        lineRenderer.Disable();
    }

    private static int ExtractTier(string source)
    {
        if (string.IsNullOrEmpty(source)) return -1;
        var match = Regex.Match(source, @"T(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }
}