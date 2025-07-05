using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ThunderRoad;
using UnityEngine;

namespace Enrichments
{
    [HarmonyPatch(typeof(UIItemStats), "UpdateStatsPage")]
    public static class ItemStatPatcher
    {
        static void Postfix(UIItemStats __instance, ItemData newStatItem) => GameManager.local.StartCoroutine(WaitForStatsCoroutine(__instance, newStatItem));

        static IEnumerator WaitForStatsCoroutine(UIItemStats __instance, ItemData itemData)
        {
            Coroutine updateCoroutine = __instance.GetField("updateStatsPageCoroutine") as Coroutine;
            if (updateCoroutine != null) yield return updateCoroutine;
            __instance.itemTierIcon.gameObject.GetOrAddComponent<UIEnrichmentStat>().Load(__instance.itemTierIcon.transform, itemData, 0.06f, 25);
        }
    }
}