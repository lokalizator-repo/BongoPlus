using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal sealed class RoundedGraphic : MaskableGraphic
{
    private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
    private ControlIcon? _icon;

    public void Initialize(ControlIcon icon)
    {
        _icon = icon;
        SetAllDirty();
    }

    public void InitializeSelection()
    {
        _icon = null;
        raycastTarget = false;
        SetAllDirty();
    }

    public override Texture mainTexture
    {
        get
        {
            var rect = rectTransform.rect;
            var width = Math.Max(8, Math.Min(128, (int)Math.Round(rect.width)));
            var height = Math.Max(8, Math.Min(128, (int)Math.Round(rect.height)));
            var key = (_icon?.ToString() ?? "selection") + ":" + width + ":" + height;
            if (Textures.TryGetValue(key, out var texture)) return texture;
            var bytes = _icon.HasValue ? ControlButtonArt.Render(_icon.Value, width, height)
                : ControlButtonArt.RenderSelection(width, height);
            var pixels = new Color[bytes.Length / 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                var offset = i * 4;
                pixels[i] = new Color(bytes[offset] / 255f, bytes[offset + 1] / 255f,
                    bytes[offset + 2] / 255f, bytes[offset + 3] / 255f);
            }
            texture = new Texture2D(width * ControlButtonArt.Resolution, height * ControlButtonArt.Resolution);
            texture.SetPixels(pixels);
            texture.Apply();
            Textures.Add(key, texture);
            return texture;
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        mesh.AddVert(new Vector2(rect.xMin, rect.yMin), color, new Vector2(0, 0));
        mesh.AddVert(new Vector2(rect.xMin, rect.yMax), color, new Vector2(0, 1));
        mesh.AddVert(new Vector2(rect.xMax, rect.yMax), color, new Vector2(1, 1));
        mesh.AddVert(new Vector2(rect.xMax, rect.yMin), color, new Vector2(1, 0));
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(0, 2, 3);
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetMaterialDirty();
    }
}
