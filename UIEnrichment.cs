using ThunderRoad;
using TMPro;
using UnityEngine;

namespace Enrichments
{
    public class UIEnrichment : ThunderBehaviour
    {
        public SpriteRenderer outlineSpriteRenderer;
        public SpriteRenderer colorSpriteRenderer;

        public void SetColor(EnrichmentData enrichmentData) => colorSpriteRenderer.color = enrichmentData.secondarySkillTreeId.IsNullOrEmptyOrWhitespace() ? enrichmentData.primarySkillTree.color : Color.Lerp(enrichmentData.primarySkillTree.color, enrichmentData.secondarySkillTree.color, 0.5f);
    }
}