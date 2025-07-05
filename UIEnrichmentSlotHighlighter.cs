using Enrichments;
using ThunderRoad;
using UnityEngine;
using UnityEngine.UI;

public class UIEnrichmentSlotHighlighter : ThunderBehaviour
{
    private Creature creature;
    public Sprite sprite;
    public Sprite oldSprite;
    public bool leftActive;
    public bool rightActive;

    public void Load(Creature creature)
    {
        this.creature = creature;
        Catalog.LoadAssetAsync<Sprite>("Silk.UI.Enrichments.DesignationIcon", sprite => { this.sprite = sprite; }, nameof(Sprite));
    }

    public void Unload() => Catalog.ReleaseAsset(sprite);

    void Update()
    {
        HandleSide(Highlighter.left, ref leftActive);
        HandleSide(Highlighter.right, ref rightActive);
    }

    void HandleSide(Highlighter highlighter, ref bool active)
    {
        var image = highlighter.GetChildByNameRecursive("Image").GetComponent<Image>();

        if (highlighter.active && GetHolderItemBySide(highlighter.side) is Item item && EnrichmentManager.HasEnrichments(item) && item.data.type != ItemData.Type.Quiver)
        {
            if (!active)
            {
                if (oldSprite == null) oldSprite = image.sprite;
                image.sprite = sprite;
                highlighter.altDesignationParent.gameObject.SetActive(true);
                highlighter.altDesignationText.text = "Enriched";
                active = true;
            }
        }
        else if (active)
        {
            image.sprite = oldSprite;
            highlighter.altDesignationParent.gameObject.SetActive(false);
            highlighter.altDesignationText.text = "";
            active = false;
        }
    }

    public Item GetHolderItemBySide(Side side)
    {
        foreach (Holder holder in creature.holders)
        {
            var highlighter = side == Side.Left ? Highlighter.left : Highlighter.right;
            foreach (Item item in holder.items)
                if (item.data.displayName == highlighter.titleText.text)
                    return item;
        }

        return null;
    }
}