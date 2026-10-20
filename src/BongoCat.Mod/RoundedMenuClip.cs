using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal static class RoundedMenuClip
{
    private static Sprite? _sprite;

    public static RectTransform Wrap(RectTransform button)
    {
        var clip = NativeStyle.Rect("Mods rounded edge", button.parent);
        clip.anchorMin = button.anchorMin;
        clip.anchorMax = button.anchorMax;
        clip.pivot = button.pivot;
        clip.sizeDelta = button.sizeDelta;
        clip.anchoredPosition = button.anchoredPosition;
        clip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var image = clip.gameObject.AddComponent<Image>();
        image.sprite = Sprite();
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        button.SetParent(clip, false);
        NativeStyle.Stretch(button);
        return clip;
    }

    private static Sprite Sprite()
    {
        if (_sprite) return _sprite!;
        const int resolution = 4;
        const int size = 16;
        const float radius = 5;
        var pixels = new Color[size * size * resolution * resolution];
        for (var y = 0; y < size * resolution; y++)
        for (var x = 0; x < size * resolution; x++)
        {
            var dx = Mathf.Max(0, Mathf.Abs((x + 0.5f) / resolution - size / 2f) - (size / 2f - radius));
            var dy = Mathf.Max(0, Mathf.Abs((y + 0.5f) / resolution - size / 2f) - (size / 2f - radius));
            pixels[y * size * resolution + x] = new Color(1, 1, 1,
                Mathf.Clamp01(0.5f - (Mathf.Sqrt(dx * dx + dy * dy) - radius) * resolution));
        }
        var texture = new Texture2D(size * resolution, size * resolution);
        texture.SetPixels(pixels);
        texture.Apply();
        _sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size * resolution, size * resolution),
            new Vector2(0.5f, 0.5f), 100 * resolution, 0, SpriteMeshType.FullRect,
            new Vector4(radius * resolution, radius * resolution, radius * resolution, radius * resolution));
        return _sprite;
    }
}
