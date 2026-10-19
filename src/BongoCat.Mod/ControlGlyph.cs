using System;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal enum ControlIcon { Add, Remove, Previous, Next }

internal sealed class ControlGlyph : MaskableGraphic
{
    public ControlIcon Icon;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        var extent = Mathf.Min(rect.width, rect.height) * 0.32f;
        if (extent <= 0) return;
        var center = rect.center;
        Draw(mesh, center, extent, 3.5f, new Color32(255, 255, 255, 230));
        Draw(mesh, center, extent, 2f, new Color32(28, 28, 28, 255));
    }

    private void Draw(VertexHelper mesh, Vector2 center, float extent, float thickness, Color32 ink)
    {
        if (Icon == ControlIcon.Add || Icon == ControlIcon.Remove)
        {
            Line(mesh, center + Vector2.left * extent, center + Vector2.right * extent, thickness, ink);
            if (Icon == ControlIcon.Add)
                Line(mesh, center + Vector2.down * extent, center + Vector2.up * extent, thickness, ink);
            return;
        }
        var direction = Icon == ControlIcon.Previous ? -1f : 1f;
        var tip = center + Vector2.right * (direction * extent * 0.6f);
        Line(mesh, center + new Vector2(-direction * extent * 0.4f, extent), tip, thickness, ink);
        Line(mesh, tip, center + new Vector2(-direction * extent * 0.4f, -extent), thickness, ink);
    }

    private static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float thickness, Color32 ink)
    {
        var delta = to - from;
        var length = (float)Math.Sqrt(delta.x * delta.x + delta.y * delta.y);
        var normal = new Vector2(-delta.y, delta.x) * (thickness / (2 * length));
        var start = mesh.currentVertCount;
        mesh.AddVert(from - normal, ink, Vector2.zero);
        mesh.AddVert(from + normal, ink, Vector2.zero);
        mesh.AddVert(to + normal, ink, Vector2.zero);
        mesh.AddVert(to - normal, ink, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
