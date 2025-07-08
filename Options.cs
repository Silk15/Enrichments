using ThunderRoad;
using UnityEngine;

namespace Enrichments;

public class Options
{
    [ModOption("Toggle Enrichment Core", ""), ModOptionCategory("Creatures", -1), ModOptionButton]
    public static void Refresh(bool active)
    {
        if (active && Catalog.TryGetData("CrystalLightningT3", out ItemData itemData))
        {
            itemData.SpawnAsync(item =>
            {
                Player.currentCreature.handLeft.Grab(item.handles[0]);
                if (item.TryGetComponent(out UIEnrichmentCore uiEnrichmentCore)) uiEnrichmentCore.onAsyncLoadingComplete += core =>
                {
                    uiEnrichmentCore.Toggle(true);
                    Catalog.GetData<ItemData>("SwordShortCommon").SpawnAsync(item1 =>
                    {
                        item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
                        item1.transform.SetPositionAndRotation(uiEnrichmentCore.itemMagnet.transform.position, uiEnrichmentCore.itemMagnet.transform.rotation);
                        ConsoleCommands.ToggleOptionsMenu();
                    });
                };
            });
        }
    }
}