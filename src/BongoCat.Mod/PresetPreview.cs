using System.Collections.Generic;
using BongoCat;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal sealed class PresetPreview : MonoBehaviour
{
    private sealed class Layer
    {
        public RectTransform Rect = null!;
        public Vector2 Min;
        public Vector2 Max;
    }

    private readonly List<Layer> _layers = new List<Layer>();
    private Vector2 _min;
    private Vector2 _max;
    private bool _initialized;

    public void Initialize(AppearancePreset preset)
    {
        if (!Cat.Instance || !MemoryImageCache.Instance || !SteamItemIdReference.Inst) return;
        Sprite? Skin(string suffix, string fallback)
        {
            if (preset.Skin >= 0 && SteamItemIdReference.Inst.Ids.Contains(preset.Skin))
                return MemoryImageCache.Instance.GetSprite(SteamItemIdReference.Inst[preset.Skin].ItemName.Replace(" ", "") + suffix);
            return (Sprite)GameAccess.RequiredField(typeof(Cat), fallback).GetValue(Cat.Instance);
        }
        var hat = preset.Hat >= 0 && SteamItemIdReference.Inst.Ids.Contains(preset.Hat)
            ? SteamItemIdReference.Inst[preset.Hat].FullImage : null;
        _min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        _max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        Add("_bodyImage", Skin("Left", "_catNormalDefault"));
        Add("_rightPawImage", Skin("Right", "_catNormalFrontDefault"));
        Add("_hatImage", hat);
        _initialized = true;
        Reflow();
    }

    private void Add(string field, Sprite? sprite)
    {
        if (sprite == null || !sprite) return;
        var source = (Image)GameAccess.RequiredField(typeof(Cat), field).GetValue(Cat.Instance);
        var corners = new Vector3[4];
        source.rectTransform.GetWorldCorners(corners);
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            var point = (Vector2)Cat.Instance.transform.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        var rect = NativeStyle.Rect(field, transform);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = source.preserveAspect;
        image.raycastTarget = false;
        _layers.Add(new Layer { Rect = rect, Min = min, Max = max });
        var drawing = source.rectTransform.rect;
        if (source.preserveAspect)
        {
            var fit = Mathf.Min(drawing.width / sprite.rect.width, drawing.height / sprite.rect.height);
            var fitted = sprite.rect.size * fit;
            drawing = new Rect(drawing.center - fitted / 2, fitted);
        }
        var content = SpriteContentBounds.Get(sprite);
        foreach (var normalized in new[] { content.min, content.max, new Vector2(content.xMin, content.yMax), new Vector2(content.xMax, content.yMin) })
        {
            var local = drawing.min + Vector2.Scale(drawing.size, normalized);
            var point = (Vector2)Cat.Instance.transform.InverseTransformPoint(source.rectTransform.TransformPoint(local));
            _min = Vector2.Min(_min, point);
            _max = Vector2.Max(_max, point);
        }
    }

    private void OnRectTransformDimensionsChange() => Reflow();

    private void Reflow()
    {
        if (!_initialized || _layers.Count == 0) return;
        var bounds = ((RectTransform)transform).rect;
        var size = _max - _min;
        var scale = Mathf.Min(bounds.width / Mathf.Max(1, size.x), bounds.height / Mathf.Max(1, size.y));
        var center = (_max + _min) / 2;
        foreach (var layer in _layers)
        {
            layer.Rect.anchorMin = layer.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            layer.Rect.sizeDelta = (layer.Max - layer.Min) * scale;
            layer.Rect.anchoredPosition = ((layer.Max + layer.Min) / 2 - center) * scale;
        }
    }
}
