using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal static class SpriteContentBounds
{
    private static readonly Dictionary<int, Rect> Cache = new Dictionary<int, Rect>();
    private static GameObject? _root;
    private static Camera _camera = null!;
    private static Image _image = null!;
    private static RenderTexture _target = null!;
    private static Texture2D _pixels = null!;
    private const int Size = 128;

    public static Rect Get(Sprite sprite)
    {
        // Imported textures are not readable, so alpha bounds are measured off screen once per sprite.
        if (Cache.TryGetValue(sprite.GetInstanceID(), out var result)) return result;
        if (!_root) Create();
        var previous = RenderTexture.active;
        try
        {
            _image.sprite = sprite;
            Canvas.ForceUpdateCanvases();
            _camera.Render();
            RenderTexture.active = _target;
            _pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            var pixels = _pixels.GetPixels32();
            var minX = Size;
            var minY = Size;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                if (pixels[y * Size + x].a <= 8) continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
            result = maxX < minX ? new Rect(0, 0, 1, 1)
                : Rect.MinMaxRect(Mathf.Max(0, minX - 1) / (float)Size, Mathf.Max(0, minY - 1) / (float)Size,
                    Mathf.Min(Size, maxX + 2) / (float)Size, Mathf.Min(Size, maxY + 2) / (float)Size);
            Cache[sprite.GetInstanceID()] = result;
            return result;
        }
        finally { RenderTexture.active = previous; _image.sprite = null; }
    }

    private static void Create()
    {
        _root = new GameObject("Mods preview measurement", typeof(Camera));
        _root.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(_root);
        _camera = _root.GetComponent<Camera>();
        _camera.enabled = false;
        _camera.cullingMask = 1 << 31;
        _camera.clearFlags = CameraClearFlags.Color;
        _camera.backgroundColor = Color.clear;
        _target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        _camera.targetTexture = _target;
        var canvasRect = NativeStyle.Rect("Canvas", _root.transform);
        canvasRect.gameObject.layer = 31;
        var canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _camera;
        canvas.planeDistance = 1;
        var imageRect = NativeStyle.Rect("Sprite", canvasRect);
        imageRect.gameObject.layer = 31;
        NativeStyle.Stretch(imageRect);
        _image = imageRect.gameObject.AddComponent<Image>();
        _image.raycastTarget = false;
        _pixels = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
    }

    public static void Dispose()
    {
        if (_root) Object.Destroy(_root);
        if (_target) Object.Destroy(_target);
        if (_pixels) Object.Destroy(_pixels);
        _root = null;
        Cache.Clear();
    }
}
