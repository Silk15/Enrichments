using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using ThunderRoad.DebugViz;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

namespace Enrichments;

public class UIEnrichmentTierNode : ThunderBehaviour
{
    private static readonly int Intensity = Shader.PropertyToID(nameof(Intensity));
    public static VisualEffect visualEffectAsset;
    public List<EnrichmentOrb> enrichmentOrbs = new();
    public List<EnrichmentData> enrichments = new();

    public ExclusionLineRenderer exclusionLineRenderer;
    public UIEnrichmentCore uiEnrichmentCore;
    public LineRenderer linkLineRenderer;
    public EffectInstance effectInstance;
    public SkillTreeData skillTreeData;
    public VisualEffect visualEffect;
    public ItemMagnet itemMagnet;
    public Item heldCrystal;

    public GameObject magnetObject;
    public string lastVizId;
    public bool isGlowing;
    public bool isShown;
    public bool vfxShown;
    public Side lastSide;

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
        itemMagnet.releaseOnGrabOrTKOnly = true;
        itemMagnet.maxCount = 1;
        itemMagnet.trigger = sphereCollider;
        itemMagnet.massMultiplier = 2f;
        itemMagnet.enabled = true;
        itemMagnet.trigger.enabled = true;

        visualEffect = GetVisualEffect(skillTreeData);

        itemMagnet.OnItemCatchEvent -= OnItemCatch;
        itemMagnet.OnItemReleaseEvent -= OnItemRelease;
        itemMagnet.OnItemCatchEvent += OnItemCatch;
        itemMagnet.OnItemReleaseEvent += OnItemRelease;

        Player.currentCreature.handLeft.OnGrabEvent += OnGrabEvent;
        Player.currentCreature.handRight.OnGrabEvent += OnGrabEvent;
        Player.currentCreature.handLeft.OnUnGrabEvent += OnUngrabEvent;
        Player.currentCreature.handRight.OnUnGrabEvent += OnUngrabEvent;

        exclusionLineRenderer = gameObject.AddComponent<ExclusionLineRenderer>();
        Catalog.LoadAssetAsync<GameObject>(ItemModuleEnrichmentCore.lineRendererAddress, line =>
        {
            exclusionLineRenderer.linePrefab = line;
            linkLineRenderer = Instantiate(line).GetComponent<LineRenderer>();
        }, ItemModuleEnrichmentCore.lineRendererAddress);
        exclusionLineRenderer.Disable(0.01f);
    }

    private void OnGrabEvent(Side side, Handle handle, float axisPosition, HandlePose orientation, EventTime eventTime)
    {
        if (eventTime == EventTime.OnEnd && handle.item is Item item && item.TryGetComponent(out SkillTreeCrystal skillTreeCrystal) && skillTreeCrystal.treeName == skillTreeData.id && uiEnrichmentCore.isGlowing) ToggleVfx(side, item, true);
    }

    private void OnUngrabEvent(Side side, Handle handle, bool throwing, EventTime eventTime)
    {
        if (eventTime == EventTime.OnEnd && vfxShown && side == lastSide) ToggleVfx(side, handle.item, false);
    }

    public void ToggleVfx(Side side, Item item, bool active)
    {
        if (active)
        {
            lastSide = side;
            visualEffect.transform.SetParent(item.transform);
            visualEffect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            vfxShown = true;
            visualEffect.gameObject.SetActive(true);
            visualEffect.Play();
            Toggle(true);
        }
        else
        {
            visualEffect.transform.SetParent(null);
            visualEffect.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            vfxShown = false;
            visualEffect.gameObject.SetActive(false);
            visualEffect.Stop();
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
        Show();
    }

    private void OnItemRelease(Item caughtItem, EventTime time)
    {
        if (caughtItem == null || time == EventTime.OnStart || !caughtItem.data.id.Contains("Crystal")) return;
        caughtItem.Haptic(1f);
        Hide();
    }

    protected override void ManagedFixedUpdate()
    {
        base.ManagedFixedUpdate();
        if (visualEffect != null && vfxShown)
        {
            Vector3 toMagnet = itemMagnet.transform.position - visualEffect.transform.position;
            if (toMagnet.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toMagnet.normalized);
                visualEffect.transform.rotation = Quaternion.Slerp(visualEffect.transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }

        if (!Player.local || enrichmentOrbs.Count == 0 || !isShown) return;
        for (int i = 0; i < enrichmentOrbs.Count; i++)
        {
            float angle = 360f / enrichmentOrbs.Count * i * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.1f;
            enrichmentOrbs[i].MoveTo(transform.TransformPoint(localOffset), Quaternion.identity);
        }
    }

    public void Show()
    {
        if (isShown) return;
        isShown = true;
        effectInstance = uiEnrichmentCore.itemModuleEnrichmentCore.loopEffectData?.Spawn(itemMagnet.transform);
        effectInstance?.Play();
        GameManager.local.StartCoroutine(uiEnrichmentCore.ShowCoroutine(enrichments, enrichmentOrbs, exclusionLineRenderer, 0.1f, skillTreeData.emissionColor, transform));
    }

    public void Hide()
    {
        if (!isShown) return;
        isShown = false;
        effectInstance?.SetParent(null);
        effectInstance?.End();
        effectInstance = null;
        if (itemMagnet.capturedItems.Count == 1) itemMagnet.ReleaseItem(itemMagnet.capturedItems[0]);
        GameManager.local.StartCoroutine(uiEnrichmentCore.HideCoroutine(enrichmentOrbs, exclusionLineRenderer));
    }

    public static VisualEffect GetVisualEffect(SkillTreeData skillTreeData)
    {
        if (visualEffectAsset != null)
        {
            VisualEffect visualEffect = Instantiate(visualEffectAsset).GetComponent<VisualEffect>();
            visualEffect.SetVector4("Source Color", skillTreeData.emissionColor);
            return visualEffect;
        }
        return null;
    }
}