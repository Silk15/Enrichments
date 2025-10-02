using System.Collections.Generic;
using ThunderRoad;
using ThunderRoad.DebugViz;
using UnityEngine;

namespace Enrichments;

public static class VizManager
{
    [ModOption("Debug Visualisation", "Toggles debug visualisation, this mode will draw a lot of lines in a lot of places for debugging.")]
    public static bool debug = false;

    private static readonly Dictionary<(object handler, string id), VizType> vizObjects = new();

    public static object AddOrUpdateViz(object handler, string id, Color color, VizType vizType, Vector3[] positions = null, Vector3 position = default, Vector3 extents = default)
    {
        if (!debug) return null;
        object viz = vizType switch
        {
            VizType.Lines => Viz.Lines(handler, id).SetPoints(positions).Color(color).Show(),
            VizType.Dot => Viz.Dot(handler, id, position).Color(color).Show(),
            VizType.Box => Viz.Box(handler, id).Extents(extents).Color(color).Show(),
            VizType.Quat => Viz.Quat(handler, id),
            _ => throw new System.NotSupportedException()
        };

        vizObjects[(handler, id)] = vizType;
        return viz;
    }

    public static void ClearViz(object handler, string id)
    {
        if (!vizObjects.TryGetValue((handler, id), out var type)) return;

        object _ = type switch
        {
            VizType.Lines => Viz.Lines(handler, id).Hide(),
            VizType.Dot => Viz.Dot(handler, id, Vector3.zero).Hide(),
            VizType.Box => Viz.Box(handler, id).Hide(),
            VizType.Quat => null,
            _ => null
        };

        vizObjects.Remove((handler, id));
    }

    public enum VizType
    {
        Dot,
        Lines,
        Box,
        Quat
    }
}