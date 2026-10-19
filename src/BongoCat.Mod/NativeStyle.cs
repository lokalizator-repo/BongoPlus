using BongoCat.UiThemes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal static class NativeStyle
{
    public static TMP_FontAsset Font = null!;

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rect, float inset = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        Offsets(rect, new Vector2(inset, inset), new Vector2(-inset, -inset));
    }

    public static void Offsets(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.sizeDelta = max - min;
        rect.anchoredPosition = min + Vector2.Scale(rect.sizeDelta, rect.pivot);
    }

    public static Image Image(RectTransform rect, UiSpriteRole role)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.raycastTarget = true;
        rect.gameObject.AddComponent<ThemedImage>().SetRole(role);
        return image;
    }

    public static TextMeshProUGUI Text(Transform parent, string value, float size = 16)
    {
        var rect = Rect("Text", parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = ModFonts.Prepare(Font);
        text.fontSize = size;
        text.text = value;
        text.richText = false;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.gameObject.AddComponent<ModTextTheme>();
        return text;
    }

    public static Button Button(Transform parent, string title, UnityEngine.Events.UnityAction click, float width = 0)
    {
        var button = CreateButton(parent, title, click, width);
        var label = Text(button.transform, title, 15);
        Stretch(label.rectTransform, 5);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    public static Button IconButton(Transform parent, ControlIcon icon, UnityEngine.Events.UnityAction click)
    {
        var rect = Rect(icon.ToString(), parent);
        rect.sizeDelta = new Vector2(28, 28);
        var graphic = rect.gameObject.AddComponent<RoundedGraphic>();
        graphic.Initialize(icon);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(click);
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = layout.preferredHeight = 28;
        return button;
    }

    public static Button NavButton(Transform parent, string title, ControlIcon icon, UnityEngine.Events.UnityAction click)
    {
        var button = Button(parent, title, click);
        var label = button.GetComponentInChildren<TMP_Text>();
        label.fontSize = 13;
        Offsets(label.rectTransform, new Vector2(32, 4), new Vector2(-5, -4));
        var glyph = Rect("Icon", button.transform);
        glyph.anchorMin = glyph.anchorMax = new Vector2(0, 0.5f);
        glyph.pivot = new Vector2(0, 0.5f);
        glyph.anchoredPosition = new Vector2(5, 0);
        glyph.sizeDelta = new Vector2(24, 24);
        glyph.gameObject.AddComponent<RoundedGraphic>().Initialize(icon, false);
        glyph.GetComponent<RoundedGraphic>().raycastTarget = false;
        return button;
    }

    private static Button CreateButton(Transform parent, string title, UnityEngine.Events.UnityAction click, float width)
    {
        var rect = Rect(title, parent);
        var image = Image(rect, UiSpriteRole.FooterButtonDefault);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        button.colors = colors;
        button.onClick.AddListener(click);
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 32;
        if (width > 0) layout.preferredWidth = width;
        else layout.flexibleWidth = 1;
        return button;
    }

    public static TMP_InputField Input(Transform parent, string initial, string placeholder)
    {
        var rect = Rect("Input", parent);
        rect.gameObject.SetActive(false);
        var image = Image(rect, UiSpriteRole.FooterButtonDefault);
        var viewport = Rect("Viewport", rect);
        Stretch(viewport, 7);
        viewport.gameObject.AddComponent<RectMask2D>();
        var label = Text(viewport, initial);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Overflow;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var hint = Text(viewport, placeholder);
        Stretch(hint.rectTransform);
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        hint.overflowMode = TextOverflowModes.Overflow;
        hint.textWrappingMode = TextWrappingModes.NoWrap;
        var field = rect.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = viewport;
        field.textComponent = label;
        field.fontAsset = label.font;
        field.pointSize = 16;
        field.placeholder = hint;
        field.targetGraphic = image;
        field.text = initial;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.richText = false;
        field.customCaretColor = true;
        field.caretColor = new Color(0.9f, 0.94f, 0.93f);
        field.selectionColor = new Color(0.5f, 0.75f, 0.65f, 0.35f);
        field.characterLimit = 80;
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 34;
        layout.flexibleWidth = 1;
        rect.gameObject.SetActive(true);
        field.ForceLabelUpdate();
        hint.text = placeholder;
        hint.enabled = string.IsNullOrEmpty(initial);
        field.onValueChanged.AddListener(value => hint.enabled = string.IsNullOrEmpty(value));
        return field;
    }

    public static void Vertical(RectTransform rect, float spacing = 8)
    {
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    public static RectTransform Row(Transform parent, float height = 34)
    {
        var row = Rect("Row", parent);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    public static TextMeshProUGUI Label(Transform parent, string value, float height = 25, float size = 16)
    {
        var label = Text(parent, value, size);
        label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
}

internal sealed class ModTextTheme : ThemedComponent
{
    public override void Apply(UiTheme theme, UiTheme legacy)
    {
        var text = GetComponent<TMP_Text>();
        if (TryResolve(UiColorRole.NormalText, theme, legacy, out var color)) text.color = color;
        if (TryResolve(UiFontRole.Body, theme, legacy, out var entry))
        {
            text.font = ModFonts.Prepare(entry.Font);
            if (entry.Material) text.fontSharedMaterial = entry.Material;
            var field = GetComponentInParent<TMP_InputField>();
            if (field && field.textComponent == text) field.fontAsset = text.font;
        }
    }
}
