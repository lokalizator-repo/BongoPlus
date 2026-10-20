using BongoCat.UiThemes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BongoCatMod;

internal sealed class NativeMenuButton : ThemedComponent, IPointerEnterHandler, IPointerExitHandler
{
    private bool _open;
    private bool _hovered;
    private Graphic _icon = null!;
    private TMP_Text _label = null!;

    public void Initialize(Graphic icon, TMP_Text label)
    {
        _icon = icon;
        _label = label;
        Redraw();
    }

    public void SetOpen(bool open) { _open = open; Redraw(); }
    public void OnPointerEnter(PointerEventData eventData) { _hovered = true; Redraw(); }
    public void OnPointerExit(PointerEventData eventData) { _hovered = false; Redraw(); }
    protected override void OnDisable()
    {
        _hovered = false;
        base.OnDisable();
    }

    public override void Apply(UiTheme theme, UiTheme legacy)
    {
        var button = GetComponent<Button>();
        var image = GetComponent<Image>();
        var background = Resolve(_open ? UiSpriteRole.TabButtonSelected : _hovered ? UiSpriteRole.TabButtonHover : UiSpriteRole.TabButtonDefault, theme, legacy);
        if (background) image.sprite = background;
        button.transition = Selectable.Transition.ColorTint;
        if (_label && TryResolve(UiFontRole.Body, theme, legacy, out var font))
        {
            _label.font = ModFonts.Prepare(font.Font);
            if (font.Material) _label.fontSharedMaterial = font.Material;
        }
        var role = _open ? UiColorRole.TabButtonIconSelected : _hovered ? UiColorRole.TabButtonIconHover : UiColorRole.TabButtonIconDefault;
        if (TryResolve(role, theme, legacy, out var color))
        {
            if (_icon) _icon.color = color;
            if (_label) _label.color = color;
        }
    }
}
