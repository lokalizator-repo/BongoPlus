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
        text.font = Font;
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
        var image = Image(rect, UiSpriteRole.InputField);
        var viewport = Rect("Viewport", rect);
        Stretch(viewport, 7);
        viewport.gameObject.AddComponent<RectMask2D>();
        var label = Text(viewport, initial);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        var hint = Text(viewport, placeholder);
        Stretch(hint.rectTransform);
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        var field = rect.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = viewport;
        field.textComponent = label;
        field.placeholder = hint;
        field.targetGraphic = image;
        field.text = initial;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.characterLimit = 80;
        var layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 34;
        layout.flexibleWidth = 1;
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
            text.font = entry.Font;
            if (entry.Material) text.fontSharedMaterial = entry.Material;
        }
    }
}
