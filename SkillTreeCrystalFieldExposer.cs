using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;
using UnityEngine.VFX;

namespace Enrichments;

public class SkillTreeCrystalFieldExposer : ThunderScript
{
    public static Dictionary<Item, ExposedFields> exposedFieldsMap = new();
    
    public override void ScriptEnable()
    {
        base.ScriptEnable();
        Item.OnItemSpawn += OnItemSpawn;
    }

    public override void ScriptDisable()
    {
        base.ScriptDisable();
        Item.OnItemSpawn -= OnItemSpawn;
    }

    public void OnItemSpawn(Item item)
    {
        if (item.TryGetComponent(out SkillTreeCrystal skillTreeCrystal) && !exposedFieldsMap.ContainsKey(item)) exposedFieldsMap.Add(item, new ExposedFields(skillTreeCrystal));
    }
    
    [Serializable]
    public class ExposedFields
    {
        public SkillTreeCrystal skillTreeCrystal;
        public VisualEffect mergeVfx;
        public VisualEffect linkVfx;
        public Transform mergeVfxTarget;
        public Transform linkVfxTarget;

        public ExposedFields(SkillTreeCrystal crystal)
        {
            skillTreeCrystal = crystal;
            mergeVfx = crystal.GetField("mergeVfx") as VisualEffect;
            linkVfx = crystal.GetField("linkVfx") as VisualEffect;

            mergeVfxTarget = crystal.GetField("mergeVfxTarget") as Transform;
            linkVfxTarget = crystal.GetField("linkVfxTarget") as Transform;
        }
    }
}