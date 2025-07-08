using System.Collections.Generic;
using System.Linq;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Serialization;

namespace Enrichments;

public class UIEnrichmentNode : ThunderBehaviour
{
    public List<EnrichmentOrb> enrichmentOrbs = new();
    public List<EnrichmentData> enrichments = new();
    public ExclusionLineRenderer exclusionLineRenderer;
    public UIEnrichmentCore uiEnrichmentCore;
    public SkillTreeData skillTreeData;

    public override ManagedLoops EnabledManagedLoops => ManagedLoops.Update | ManagedLoops.FixedUpdate;
    
    public void Init(UIEnrichmentCore uiEnrichmentCore, SkillTreeData skillTreeData, List<EnrichmentData> enrichments)
    {
        this.uiEnrichmentCore = uiEnrichmentCore;
        this.skillTreeData = skillTreeData;
        this.enrichments = enrichments;
        gameObject.AddComponent<Rigidbody>().isKinematic = true;
        var sphereCollider = gameObject.AddComponent<SphereCollider>();
        sphereCollider.radius = 0.2f;
        sphereCollider.isTrigger = true;
        exclusionLineRenderer = gameObject.AddComponent<ExclusionLineRenderer>();
        Catalog.LoadAssetAsync<GameObject>(ItemModuleEnrichmentCore.lineRendererAddress, line => { exclusionLineRenderer.linePrefab = line; }, ItemModuleEnrichmentCore.lineRendererAddress);
        exclusionLineRenderer.Disable(0.01f); 
    }
    
    protected override void ManagedFixedUpdate()
    {
        base.ManagedFixedUpdate();
        if (!Player.local || enrichmentOrbs.Count == 0 || !uiEnrichmentCore.isGlowing) return;
        for (int i = 0; i < enrichmentOrbs.Count; i++)
        {
            float angle = 360f / enrichmentOrbs.Count * i * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.1f; 
            enrichmentOrbs[i].MoveTo(transform.TransformPoint(localOffset), Quaternion.identity);
        }
    }
}