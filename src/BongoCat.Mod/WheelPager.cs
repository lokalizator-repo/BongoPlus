using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BongoCat;
using BongoCat.OSSpecific;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal sealed class WheelPager : MonoBehaviour
{
    private static readonly FieldInfo Entries = GameAccess.RequiredField(typeof(EmoteDonut), "IDtoEmoteEntry");
    private static readonly FieldInfo Parent = GameAccess.RequiredField(typeof(EmoteDonut), "emoteEntryParent");
    private static readonly FieldInfo Radius = GameAccess.RequiredField(typeof(EmoteDonut), "radius");
    private static readonly FieldInfo IsOpen = GameAccess.RequiredField(typeof(EmoteDonut), "_isOpen");
    private static readonly FieldInfo ActivelyClosed = GameAccess.RequiredField(typeof(EmoteDonut), "_activelyClosed");
    private static readonly FieldInfo ShowWheel = GameAccess.RequiredField(typeof(EmoteDonut), "_showEmoteWheel");
    private static readonly FieldInfo Menu = GameAccess.RequiredField(typeof(EmoteDonut), "_menu");
    private readonly Dictionary<int, RectTransform> _wrappers = new Dictionary<int, RectTransform>();
    private EmoteDonut _wheel = null!;
    private RectTransform _parent = null!;
    private int[] _ids = Array.Empty<int>();
    private Button _previous = null!;
    private Button _next = null!;
    private TextMeshProUGUI _pageLabel = null!;
    private int _page;
    private Vector3 _lastPosition;
    private Vector3 _lastScale;
    private Quaternion _lastRotation;
    private Vector2 _lastScreen;
    private Rect _lastWorkArea;
    private Rect _hitArea;
    private bool _dirty;

    private bool Opened => (bool)IsOpen.GetValue(_wheel);
    private Dictionary<int, EmoteDonutEntry> Items => (Dictionary<int, EmoteDonutEntry>)Entries.GetValue(_wheel);

    public void Initialize(EmoteDonut wheel)
    {
        _wheel = wheel;
        _parent = (RectTransform)Parent.GetValue(wheel);
        Refresh();
    }

    public void Refresh()
    {
        if (!_wheel) return;
        var entries = Items;
        foreach (var id in _wrappers.Keys.Where(id => !entries.ContainsKey(id)).ToArray())
        {
            _wrappers[id].gameObject.SetActive(false);
            Destroy(_wrappers[id].gameObject);
            _wrappers.Remove(id);
        }
        foreach (var pair in entries)
        {
            if (_wrappers.ContainsKey(pair.Key)) continue;
            var entry = (RectTransform)pair.Value.transform;
            var size = entry.rect.size;
            var wrapper = NativeStyle.Rect("Wheel slot " + pair.Key, _parent);
            wrapper.sizeDelta = size;
            // Native entry animations must not overwrite the scale that fits the page on screen.
            entry.SetParent(wrapper, false);
            entry.anchorMin = Vector2.zero;
            entry.anchorMax = Vector2.one;
            entry.pivot = new Vector2(0.5f, 0.5f);
            entry.sizeDelta = Vector2.zero;
            entry.anchoredPosition = Vector2.zero;
            _wrappers.Add(pair.Key, wrapper);
        }
        _ids = entries.Keys.ToArray();
        _page = Math.Min(_page, WheelLayout.PageCount(_ids.Length) - 1);
        _dirty = true;
        Reflow();
        Present();
    }

    public void Open()
    {
        if (Opened || (bool)ActivelyClosed.GetValue(_wheel) || _ids.Length == 0) return;
        IsOpen.SetValue(_wheel, true);
        Reflow();
        Present();
    }

    public void Close()
    {
        ActivelyClosed.SetValue(_wheel, false);
        if (!Opened) return;
        IsOpen.SetValue(_wheel, false);
        Present();
    }

    public bool KeepOpenOnHover() => Opened && _hitArea.Contains(Input.mousePosition) &&
        ((PlayerPrefsToggle)ShowWheel.GetValue(_wheel)).Value &&
        !((GameObject)Menu.GetValue(_wheel)).activeSelf && !TransparentWindow.Instance.GamingModeEnabled &&
        !TranslucentOnHoverSetting.Instance.DisablePopups;

    private void LateUpdate()
    {
        if (!_wheel) return;
        if (!_previous && NativeStyle.Font)
        {
            _previous = NativeStyle.IconButton(_parent, ControlIcon.Previous, () => ChangePage(-1));
            _next = NativeStyle.IconButton(_parent, ControlIcon.Next, () => ChangePage(1));
            _pageLabel = NativeStyle.Text(_parent, "", 12);
            ((RectTransform)_previous.transform).sizeDelta = new Vector2(28, 28);
            ((RectTransform)_next.transform).sizeDelta = new Vector2(28, 28);
            _pageLabel.rectTransform.sizeDelta = new Vector2(56, 24);
            _pageLabel.alignment = TextAlignmentOptions.Center;
            _dirty = true;
            Present();
        }
        if (_dirty || _parent.position != _lastPosition || _parent.lossyScale != _lastScale ||
            _parent.rotation != _lastRotation ||
            _lastScreen != new Vector2(Screen.width, Screen.height) || _lastWorkArea != WorkArea()) Reflow();
        if (Opened && _hitArea.Contains(Input.mousePosition) && Input.mouseScrollDelta.y != 0)
            ChangePage(Input.mouseScrollDelta.y > 0 ? -1 : 1);
    }

    private void ChangePage(int direction)
    {
        var pages = WheelLayout.PageCount(_ids.Length);
        if (pages <= 1) return;
        _page = (_page + direction + pages) % pages;
        Reflow();
        Present();
        OnDemandRenderHelper.Instance?.ResumeRenderingForDuration(0.3f);
    }

    private void Reflow()
    {
        _dirty = false;
        _lastPosition = _parent.position;
        _lastScale = _parent.lossyScale;
        _lastRotation = _parent.rotation;
        _lastScreen = new Vector2(Screen.width, Screen.height);
        _lastWorkArea = WorkArea();
        if (_ids.Length == 0) return;
        var uiScale = Mathf.Max(Mathf.Abs(_lastScale.x), Mathf.Abs(_lastScale.y));
        var size = _wrappers[_ids[0]].rect.size;
        var iconSize = Mathf.Max(size.x, size.y) * uiScale;
        var origin = RectTransformUtility.WorldToScreenPoint(null, _parent.position) - _lastWorkArea.position;
        var plan = WheelLayout.Arrange(_ids.Length, origin.x, origin.y, _lastWorkArea.width, _lastWorkArea.height,
            iconSize, (float)Radius.GetValue(_wheel) * uiScale, uiScale, _page);
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < _ids.Length; i++)
        {
            var start = _page * WheelLayout.PageSize;
            var slot = i >= start && i < start + plan.Slots.Length ? i - start : i % plan.Slots.Length;
            var point = plan.Slots[slot];
            var screen = new Vector2((float)point.X, (float)point.Y) + _lastWorkArea.position;
            Place(_wrappers[_ids[i]], screen, (float)plan.Scale);
            var half = Vector2.one * (float)(iconSize * plan.Scale / 2);
            min = Vector2.Min(min, screen - half);
            max = Vector2.Max(max, screen + half);
        }
        if (_previous)
        {
            var previous = new Vector2((float)plan.Previous.X, (float)plan.Previous.Y) + _lastWorkArea.position;
            var next = new Vector2((float)plan.Next.X, (float)plan.Next.Y) + _lastWorkArea.position;
            var counter = new Vector2((float)plan.Counter.X, (float)plan.Counter.Y) + _lastWorkArea.position;
            Place((RectTransform)_previous.transform, previous, (float)plan.Scale);
            Place((RectTransform)_next.transform, next, (float)plan.Scale);
            Place(_pageLabel.rectTransform, counter, (float)plan.Scale);
            _pageLabel.text = (_page + 1) + " / " + WheelLayout.PageCount(_ids.Length);
            if (WheelLayout.PageCount(_ids.Length) > 1)
            {
                var arrowHalf = Vector2.one * (float)(14 * uiScale * plan.Scale);
                var counterHalf = new Vector2(28, 12) * (float)(uiScale * plan.Scale);
                min = Vector2.Min(min, Vector2.Min(previous - arrowHalf, counter - counterHalf));
                max = Vector2.Max(max, Vector2.Max(next + arrowHalf, counter + counterHalf));
            }
        }
        _hitArea = Rect.MinMaxRect(min.x - 8, min.y - 8, max.x + 8, max.y + 8);
    }

    private void Place(RectTransform rect, Vector2 screen, float scale)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screen, null, out var local))
            rect.localPosition = new Vector3(local.x, local.y, 0);
        rect.localScale = Vector3.one * scale;
        rect.rotation = Quaternion.identity;
    }

    private static Rect WorkArea()
    {
        var left = Mathf.Max(0, ScreenHelper.XMin);
        var right = Mathf.Min(Screen.width, ScreenHelper.XMax);
        var bottom = Mathf.Max(0, ScreenHelper.YMin);
        var top = Mathf.Min(Screen.height, ScreenHelper.YMax);
        return right > left && top > bottom
            ? Rect.MinMaxRect(left, bottom, right, top)
            : new Rect(0, 0, Screen.width, Screen.height);
    }

    private void Present()
    {
        var open = Opened;
        for (var i = 0; i < _ids.Length; i++)
        {
            var wrapper = _wrappers[_ids[i]];
            var visible = open && i / WheelLayout.PageSize == _page;
            if (wrapper.gameObject.activeSelf != visible) wrapper.gameObject.SetActive(visible);
            if (visible) Items[_ids[i]].gameObject.SetActive(true);
        }
        if (!_previous) return;
        var navigation = open && WheelLayout.PageCount(_ids.Length) > 1;
        _previous.gameObject.SetActive(navigation);
        _next.gameObject.SetActive(navigation);
        _pageLabel.gameObject.SetActive(navigation);
    }
}
