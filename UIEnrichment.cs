using ThunderRoad;
using UnityEngine;

namespace Enrichments
{
    public class UIEnrichment : ThunderBehaviour
    {
        public SpriteRenderer outlineSpriteRenderer;
        public SpriteRenderer colorSpriteRenderer;
        
        public void SetColor(Color color) => colorSpriteRenderer.color = color;
    }
}

