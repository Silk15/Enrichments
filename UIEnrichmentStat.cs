using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace Enrichments;

public class UIEnrichmentStat : ThunderBehaviour
{
    public Transform root;
    public List<UIEnrichment> uiEnrichments = new();
    public Sprite outlineSprite;
    public Sprite insideSprite;
    public Item item;
    
    public void Load(Transform root, ItemData itemData, float offset, float scale, Item existingItem = null)
    {
        this.root = root;
        if (itemData == null)
        {
            Clear();
            return;
        }
        item = existingItem != null ? existingItem : GetHeldItem(itemData.id);
        if (EnrichmentManager.TryGetEnrichments(item, out List<EnrichmentData> enrichments)) StartCoroutine(Load(enrichments));
        else Clear();

        IEnumerator Load(List<EnrichmentData> enrichments)
        {
            yield return new WaitUntil(() => root.gameObject.activeSelf);
            List<IEnumerator> enumerators = new List<IEnumerator>();
            foreach (EnrichmentData enrichment in enrichments)
            {
                enumerators.Add(Catalog.LoadAssetCoroutine<GameObject>(enrichment.uiPrefabAddress, prefab =>
                {
                    var gameObject = Instantiate(prefab, root);
                    if (gameObject.TryGetComponent(out UIEnrichment uiEnrichment))
                    {
                        gameObject.transform.localScale *= scale;
                        gameObject.name = $"Enrichment: {enrichment.id}";
                        uiEnrichments.Add(uiEnrichment);
                        uiEnrichment.SetColor(enrichment.primarySkillTree.color);
                    }
                    else Debug.LogError($"[Enrichments] Prefab: ({enrichment.uiPrefabAddress}) does not contain a UIEnrichment component, thus nothing will be loaded.");
                }, enrichment.uiPrefabAddress));
            }

            yield return Yielders.YieldParallel(enumerators);
            Vector3[] positions = new[] { new Vector3(-offset * 7.4f, offset * 7.4f, 0), new Vector3(offset * 7.4f, offset * 7.4f, 0), new Vector3(-offset * 7.4f, -offset * 7.4f, 0), new Vector3(offset * 7.4f, -offset * 7.4f, 0) };
            for (int i = 0; i < uiEnrichments.Count; i++) uiEnrichments[i].transform.localPosition = positions[i];
        }

        void Clear()
        {
            foreach (var uiEnrichment in uiEnrichments) Destroy(uiEnrichment.gameObject);
            uiEnrichments.Clear();
            item = null;
        }
    }

    public Item GetHeldItem(string id)
    {
        if (Player.currentCreature?.handLeft?.grabbedHandle?.item is Item leftItem && leftItem.data.id == id) return leftItem;
        if (Player.currentCreature?.handRight?.grabbedHandle?.item is Item rightItem && rightItem.data.id == id) return rightItem;
        return null;
    }
}