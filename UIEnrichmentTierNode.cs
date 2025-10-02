using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using ThunderRoad.DebugViz;
using UnityEngine;
using UnityEngine.VFX;

namespace Enrichments;

public class UIEnrichmentTierNode : ThunderBehaviour
{
    public List<EnrichmentOrb> enrichmentOrbs = new();
    public List<EnrichmentData> enrichments = new();
    
    public ExclusionLineRenderer exclusionLineRenderer;
    public UIEnrichmentCore uiEnrichmentCore;
    public LineRenderer linkLineRenderer;
    public EffectInstance effectInstance;
    public SkillTreeData skillTreeData;
    public ItemMagnet itemMagnet;
    public Item heldCrystal;

    public GameObject magnetObject;
    public bool isGlowing;
    public bool isShown;
    public bool vfxShown;
    public Side lastSide;

    public Item fakeItem;

    public override ManagedLoops EnabledManagedLoops => ManagedLoops.Update | ManagedLoops.FixedUpdate;

    public void Init(UIEnrichmentCore uiEnrichmentCore, SkillTreeData skillTreeData, List<EnrichmentData> enrichments)
    {
        this.uiEnrichmentCore = uiEnrichmentCore;
        this.skillTreeData = skillTreeData;
        this.enrichments = enrichments;

        gameObject.AddComponent<Rigidbody>().isKinematic = true;
        magnetObject = new GameObject("NodeMagnet");
        magnetObject.transform.SetParent(transform);
        magnetObject.transform.localPosition = Vector3.zero;
        magnetObject.transform.localRotation = Quaternion.Euler(-90, 0, 0);

        var sphereCollider = magnetObject.AddComponent<SphereCollider>();
        sphereCollider.radius = 0.075f;
        sphereCollider.isTrigger = true;

        itemMagnet = magnetObject.AddComponent<ItemMagnet>();
        itemMagnet.slots = new List<string>(new[] { "SkillTreeCrystal", "Arrow", "Throwables" });
        itemMagnet.tagFilter = FilterLogic.AnyExcept;
        itemMagnet.catchedItemIgnoreGravityPush = true;
        itemMagnet.magnetReactivateDurationOnRelease = 1f;
        itemMagnet.kinematicLock = true;
        itemMagnet.releaseOnGrabOrTKOnly = false;
        itemMagnet.maxCount = 1;
        itemMagnet.trigger = sphereCollider;
        itemMagnet.massMultiplier = 2f;
        itemMagnet.enabled = true;
        itemMagnet.trigger.enabled = true;

        itemMagnet.OnItemCatchEvent -= OnItemCatch;
        itemMagnet.OnItemReleaseEvent -= OnItemRelease;
        itemMagnet.OnItemCatchEvent += OnItemCatch;
        itemMagnet.OnItemReleaseEvent += OnItemRelease;
        
        Player.currentCreature.handLeft.OnGrabEvent += OnGrab;
        Player.currentCreature.handRight.OnGrabEvent += OnGrab;
        Player.currentCreature.handLeft.OnUnGrabEvent += OnUngrab;
        Player.currentCreature.handRight.OnUnGrabEvent += OnUngrab;

        exclusionLineRenderer = gameObject.AddComponent<ExclusionLineRenderer>();
        Catalog.LoadAssetAsync<GameObject>(ItemModuleEnrichmentCore.lineRendererAddress, line =>
        {
            exclusionLineRenderer.linePrefab = line;
            linkLineRenderer = Instantiate(line).GetComponent<LineRenderer>();
        }, ItemModuleEnrichmentCore.lineRendererAddress);
        exclusionLineRenderer.Disable(0.01f);
    }

    public void RemoveDelegates()
    {
        itemMagnet.OnItemCatchEvent -= OnItemCatch;
        itemMagnet.OnItemReleaseEvent -= OnItemRelease;
        
        Player.currentCreature.handLeft.OnGrabEvent -= OnGrab;
        Player.currentCreature.handRight.OnGrabEvent -= OnGrab;
        Player.currentCreature.handLeft.OnUnGrabEvent -= OnUngrab;
        Player.currentCreature.handRight.OnUnGrabEvent -= OnUngrab;
    }
    
    private void OnGrab(Side side, Handle handle, float axisPosition, HandlePose orientation, EventTime eventTime)
    {
        if (eventTime == EventTime.OnStart) return;
        ToggleVfx(side, handle?.item, true);
    }

    private void OnUngrab(Side side, Handle handle, bool throwing, EventTime eventTime)
    {
        if (eventTime == EventTime.OnStart || isShown) return;
        ToggleVfx(side, handle?.item, false);
    }

    public void ToggleVfx(Side side, Item item, bool active)
    {
        if (!uiEnrichmentCore.isShown || !item || !item.TryGetComponent(out SkillTreeCrystal skillTreeCrystal) || skillTreeCrystal.treeName != skillTreeData.id) return;
        if (active)
        {
            item.data.SpawnAsync(fake =>
            {
                fakeItem = fake;
                fakeItem.physicBody.isKinematic = true;
                fakeItem.physicBody.useGravity = false;
                fakeItem.Hide(true);
                foreach (Handle handle in fakeItem.handles) handle.SetTouch(false);
                fakeItem.SetColliders(false, true);
                if (itemMagnet.capturedItems.Select(c => c.item).Contains(fakeItem)) itemMagnet.ReleaseItem(itemMagnet.capturedItems.FirstOrDefault(c => c.item == fakeItem));
            }, itemMagnet.transform.position, Quaternion.identity, itemMagnet.transform);
            lastSide = side;
            vfxShown = true;
            Toggle(true);
        }
        else
        {
            foreach (Handle handle in fakeItem?.handles) handle?.SetTouch(true);
            fakeItem.Hide(false);
            fakeItem.SetColliders(true);
            if (itemMagnet.capturedItems.Select(c => c.item).Contains(fakeItem)) itemMagnet.ReleaseItem(itemMagnet.capturedItems.FirstOrDefault(c => c.item == fakeItem));
            fakeItem.DisallowDespawn = false;
            fakeItem.Despawn();
            vfxShown = false;
            if (!isShown) Toggle(false);
        }
    }

    public void Toggle(bool shown)
    {
        if (isGlowing == shown) return;
        isGlowing = shown;
        if (shown)
        {
            itemMagnet.enabled = true;
            itemMagnet.trigger.enabled = true;
            itemMagnet.OnItemCatchEvent -= OnItemCatch;
            itemMagnet.OnItemReleaseEvent -= OnItemRelease;
            itemMagnet.OnItemCatchEvent += OnItemCatch;
            itemMagnet.OnItemReleaseEvent += OnItemRelease;
        }
        else
        {
            effectInstance?.SetParent(null);
            effectInstance?.End();
            effectInstance = null;
            if (itemMagnet.capturedItems.Count == 0) uiEnrichmentCore.itemModuleEnrichmentCore.disconnectEffectData?.Spawn(transform).Play();
            itemMagnet.enabled = false;
            itemMagnet.trigger.enabled = false;
            itemMagnet.OnItemCatchEvent -= OnItemCatch;
            itemMagnet.OnItemReleaseEvent -= OnItemRelease;
            if (isShown) Hide();
            if (vfxShown) ToggleVfx(lastSide, null, false);
        }
    }

    private void OnItemCatch(Item caughtItem, EventTime time)
    {
        if (time == EventTime.OnEnd || caughtItem.data == null || string.IsNullOrEmpty(caughtItem.data.id) || !caughtItem.data.id.Contains(skillTreeData.id)) return;
        itemMagnet.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(caughtItem.ForwardVector(), Vector3.up));
        caughtItem.Haptic(1f);
        caughtItem.RunAfter(() =>
        {
            if (!caughtItem.IsHeldByPlayer) caughtItem.GetComponent<SkillTreeCrystal>().SetGlow(true);
        }, 0.25f);
        Show();
    }

    private void OnItemRelease(Item caughtItem, EventTime time)
    {
        if (caughtItem == null || time == EventTime.OnStart || !caughtItem.data.id.Contains("Crystal")) return;
        VizManager.ClearViz(this, $"crystal{caughtItem.data.id}{skillTreeData.id}");
        caughtItem.GetComponent<SkillTreeCrystal>().SetGlow(false);
        caughtItem.Haptic(1f);
        Hide();
    }

    protected override void ManagedFixedUpdate()
    {
        base.ManagedFixedUpdate();
        if (heldCrystal != null) VizManager.AddOrUpdateViz(this, $"crystal{heldCrystal.data.id}{skillTreeData.id}", skillTreeData.color, VizManager.VizType.Lines, new[] { itemMagnet.transform.position, heldCrystal.transform.position });

        if (!Player.local || enrichmentOrbs.Count == 0 || !isShown) return;
        for (int i = 0; i < enrichmentOrbs.Count; i++)
        {
            float angle = 360f / enrichmentOrbs.Count * i * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.1f;
            enrichmentOrbs[i].MoveTo(magnetObject.transform.TransformPoint(localOffset), Quaternion.identity); 
            VizManager.AddOrUpdateViz(this, $"{skillTreeData.id} + {enrichmentOrbs[i].enrichmentData.id}", skillTreeData.color, VizManager.VizType.Lines, new [] { enrichmentOrbs[i].transform.position, transform.TransformPoint(localOffset) });
        }
    }

    public void Show()
    {
        if (isShown) return;
        isShown = true;
        effectInstance = uiEnrichmentCore.itemModuleEnrichmentCore.loopEffectData?.Spawn(itemMagnet.transform);
        effectInstance?.Play();
        GameManager.local.StartCoroutine(uiEnrichmentCore.ShowCoroutine(enrichments, enrichmentOrbs, exclusionLineRenderer, 0.1f, skillTreeData.emissionColor, itemMagnet.transform));
    }

    public void Hide()
    {
        if (!isShown) return;
        isShown = false;
        effectInstance?.SetParent(null);
        effectInstance?.End();
        effectInstance = null;
        if (heldCrystal) VizManager.ClearViz(this, $"crystal{heldCrystal.data.id}{skillTreeData.id}");
        if (itemMagnet.capturedItems.Count == 1) itemMagnet.ReleaseItem(itemMagnet.capturedItems[0]);
        for (int i = 0; i < enrichmentOrbs.Count; i++) 
            VizManager.ClearViz(enrichmentOrbs[i], $"{skillTreeData.id} + {enrichmentOrbs[i].enrichmentData.id}");
        
        GameManager.local.StartCoroutine(uiEnrichmentCore.HideCoroutine(enrichmentOrbs, exclusionLineRenderer));
    }
}