using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BongoCatMod;

internal sealed class WheelDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int ItemId;
    public Sprite Icon = null!;
    private RectTransform? _ghost;
    private ScrollRect? _scroll;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        var canvas = GetComponentInParent<Canvas>();
        _ghost = NativeStyle.Rect("Dragged emote", canvas.transform);
        _ghost.sizeDelta = new Vector2(64, 64);
        var image = _ghost.gameObject.AddComponent<Image>();
        image.sprite = Icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        _scroll = GetComponentInParent<ScrollRect>();
        if (_scroll) _scroll.enabled = false;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_ghost == null || !_ghost) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_ghost.parent, eventData.position,
            eventData.pressEventCamera, out var point)) _ghost.localPosition = point;
    }

    public void OnEndDrag(PointerEventData eventData) => Clear();
    private void OnDisable() => Clear();

    private void Clear()
    {
        if (_ghost != null && _ghost) Destroy(_ghost.gameObject);
        _ghost = null;
        if (_scroll != null && _scroll) _scroll.enabled = true;
        _scroll = null;
    }
}

internal sealed class WheelDropTarget : MonoBehaviour, IDropHandler
{
    public Action<int> Drop = null!;

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !eventData.pointerDrag) return;
        var source = eventData.pointerDrag.GetComponent<WheelDragHandle>();
        if (source) Drop(source.ItemId);
    }
}

internal sealed class HoverHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action<Transform, bool> Hover = null!;
    public void OnPointerEnter(PointerEventData eventData) => Hover(transform, true);
    public void OnPointerExit(PointerEventData eventData) => Hover(transform, false);
    private void OnDisable() => Hover?.Invoke(transform, false);
}
