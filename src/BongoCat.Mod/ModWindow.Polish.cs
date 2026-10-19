using System;
using System.Linq;
using BongoCat;
using BongoCat.OSSpecific;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal sealed partial class ModWindow
{
    private void StateChanged()
    {
        _stateDirty = true;
        _saveAt = Time.realtimeSinceStartup + 0.8f;
    }

    private void SaveState()
    {
        _settings.MenuTab.Value = _tab;
        _settings.MenuSlot.Value = _slot;
        _settings.MenuSearch.Value = _query;
        _settings.MenuPage.Value = _page;
        _settings.CosmeticView.Value = _cosmeticView;
        _settings.FavoritesOnly.Value = _favoritesOnly;
        _settings.SetScroll(_scrollPositions);
        _settings.Save();
        _stateDirty = false;
    }

    private static Rect WorkArea()
    {
        var left = Mathf.Max(0, ScreenHelper.XMin);
        var right = Mathf.Min(Screen.width, ScreenHelper.XMax);
        var bottom = Mathf.Max(0, ScreenHelper.YMin);
        var top = Mathf.Min(Screen.height, ScreenHelper.YMax);
        return right > left && top > bottom ? Rect.MinMaxRect(left, bottom, right, top)
            : new Rect(0, 0, Screen.width, Screen.height);
    }

    private void FitWindow()
    {
        var area = WorkArea();
        var preferred = Mathf.Clamp(Screen.height / 1080f, 0.8f, 1.2f);
        var scale = (float)MenuLayout.Scale(area.width, area.height, preferred);
        if (_lastWorkArea == area && Mathf.Approximately(_scaler.scaleFactor, scale)) return;
        _lastWorkArea = area;
        _scaler.scaleFactor = scale;
        var bottom = area.yMin + Mathf.Clamp(110 * scale, 12, Mathf.Max(12, area.height - 560 * scale - 12));
        _panel.position = new Vector3(area.xMax - 12, bottom, 0);
        HideTooltip();
    }

    private void Hint(Transform target, string text)
    {
        var hint = target.gameObject.AddComponent<HoverHint>();
        hint.Hover = (owner, entered) =>
        {
            if (entered)
            {
                _hoverOwner = owner;
                _hoverText = text;
                _hoverAt = Time.realtimeSinceStartup + 0.4f;
                if (_tooltip) _tooltip.gameObject.SetActive(false);
            }
            else if (_hoverOwner == owner) HideTooltip();
        };
    }

    private void HideTooltip()
    {
        _hoverOwner = null;
        if (_tooltip) _tooltip.gameObject.SetActive(false);
    }

    private void UpdateTooltip()
    {
        if (!_tooltip || !_panel.gameObject.activeSelf || _hoverOwner == null || !_hoverOwner || !_hoverOwner.gameObject.activeInHierarchy)
        {
            HideTooltip();
            return;
        }
        if (Input.GetMouseButton(0) || Time.realtimeSinceStartup < _hoverAt) return;
        _tooltipText.text = _hoverText;
        _tooltip.sizeDelta = new Vector2(270, Mathf.Clamp(_tooltipText.GetPreferredValues(_hoverText, 252, float.PositiveInfinity).y + 18, 42, 130));
        _tooltip.gameObject.SetActive(true);
        _tooltip.SetAsLastSibling();
        var size = _tooltip.sizeDelta * _scaler.scaleFactor;
        var area = WorkArea();
        var cursor = Input.mousePosition;
        _tooltip.position = new Vector3(Mathf.Clamp(cursor.x + 12, area.xMin + 4, area.xMax - size.x - 4),
            Mathf.Clamp(cursor.y - size.y - 12, area.yMin + 4, area.yMax - size.y - 4), 0);
    }

    private void AutomationDetails()
    {
        NativeStyle.Label(_body, "ACTIVITY", 25, 16);
        var summary = NativeStyle.Label(_body, _plugin.Automation.Summary, 48, 12);
        summary.textWrappingMode = TextWrappingModes.Normal;
        var state = NativeStyle.Label(_body, "", 56, 12);
        state.textWrappingMode = TextWrappingModes.Normal;
        var retry = NativeStyle.Button(_body, "Resume after error", () => _plugin.Run(() =>
        {
            _plugin.Automation.Retry();
            _error = null;
        }));
        Hint(retry.transform, "Resume the previous automation options after a final error. Pending gifts are never paid again.");
        var history = NativeStyle.Label(_body, "", 160, 11);
        history.textWrappingMode = TextWrappingModes.Normal;
        Hint(summary.transform, "Session totals reset on restart. Gifts sent counts requests; ready cleared is not confirmation of item delivery.");
        _refreshers.Add(() =>
        {
            summary.text = _plugin.Automation.Summary;
            state.text = _plugin.Automation.Error ?? _plugin.Automation.Status;
            retry.gameObject.SetActive(_plugin.Automation.Error != null);
            retry.interactable = _plugin.Automation.CanRetry;
            history.text = _plugin.Automation.Recent;
        });
        retry.gameObject.SetActive(_plugin.Automation.Error != null);
    }

    private void CreatePresets()
    {
        NativeStyle.Label(_body, "APPEARANCE PRESETS", 26, 16);
        NativeStyle.Label(_body, "Save your current skin and hat as a set. Reuse a name to replace its saved appearance.", 48, 12)
            .textWrappingMode = TextWrappingModes.Normal;
        var actions = NativeStyle.Row(_body);
        var name = NativeStyle.Input(actions, "", "Preset name");
        name.characterLimit = 40;
        var save = NativeStyle.Button(actions, "Save", () => _plugin.Run(() =>
        {
            _plugin.Cosmetics.SavePreset(name.text);
            name.text = "";
            _error = null;
            FillPresets();
        }), 62);
        Hint(save.transform, "Save your current skin and hat. The same name replaces an existing preset.");
        _presetList = NativeStyle.Rect("Presets", _body);
        NativeStyle.Vertical(_presetList, 5);
        FillPresets();
        NativeStyle.Button(_body, "Restore equipped appearance", () => _plugin.Run(_plugin.Cosmetics.Restore));
    }

    private void FillPresets()
    {
        ClearChildren(_presetList);
        if (_plugin.Cosmetics.Presets.Count == 0)
            NativeStyle.Label(_presetList, "Save a skin and hat together for one-click switching.", 32, 12).textWrappingMode = TextWrappingModes.Normal;
        foreach (var preset in _plugin.Cosmetics.Presets.ToArray())
        {
            var row = NativeStyle.Row(_presetList);
            var apply = NativeStyle.Button(row, preset.Name, () => _plugin.Run(() =>
            {
                _plugin.Cosmetics.ApplyPreset(preset);
                _error = null;
                FillGrid();
            }));
            Hint(apply.transform, preset.Name + "\nApply this skin and hat.");
            var remove = NativeStyle.IconButton(row, ControlIcon.Remove, () => _plugin.Run(() =>
            {
                _plugin.Cosmetics.DeletePreset(preset);
                FillPresets();
            }));
            Hint(remove.transform, "Delete preset: " + preset.Name);
        }
    }

    private static void ClearChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void FillWheelEditor()
    {
        if (!_wheelEditor) return;
        HideTooltip();
        ClearChildren(_wheelEditor);
        var ids = _plugin.Wheel.ItemIds;
        _wheelSignature = string.Join(",", ids);
        var pages = WheelLayout.PageCount(ids.Length);
        _wheelPage = Mathf.Clamp(_wheelPage, 0, pages - 1);
        NativeStyle.Label(_wheelEditor, "YOUR EMOTE WHEEL", 25, 16);
        NativeStyle.Label(_wheelEditor, "Drag onto a slot to reorder. Drop onto a page arrow to move between pages.", 42, 12)
            .textWrappingMode = TextWrappingModes.Normal;
        if (ids.Length == 0)
        {
            NativeStyle.Label(_wheelEditor, "Your wheel is empty. Add emotes with + in the catalog.", 42, 13)
                .textWrappingMode = TextWrappingModes.Normal;
            return;
        }
        var grid = NativeStyle.Rect("Wheel slots", _wheelEditor);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(87, 88);
        layout.spacing = new Vector2(6, 6);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        grid.gameObject.AddComponent<LayoutElement>().preferredHeight = 182;
        for (var index = 0; index < WheelLayout.PageSize; index++)
        {
            var targetIndex = _wheelPage * WheelLayout.PageSize + index;
            var id = targetIndex < ids.Length ? ids[targetIndex] : -1;
            var slot = NativeStyle.Button(grid, "", () => { });
            slot.gameObject.AddComponent<WheelDropTarget>().Drop = source => _plugin.Run(() =>
            {
                _plugin.Wheel.Move(source, Math.Min(targetIndex, ids.Length - 1));
                FillWheelEditor();
            });
            var number = slot.GetComponentInChildren<TMP_Text>();
            number.text = (targetIndex + 1).ToString();
            number.fontSize = 10;
            number.alignment = TextAlignmentOptions.BottomLeft;
            if (id < 0) continue;
            var item = SteamItemIdReference.Inst[id];
            var icon = NativeStyle.Rect("Emote", slot.transform);
            NativeStyle.Stretch(icon, 9);
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = item.Icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var drag = slot.gameObject.AddComponent<WheelDragHandle>();
            drag.ItemId = id;
            drag.Icon = item.Icon;
            var remove = NativeStyle.IconButton(slot.transform, ControlIcon.Remove, () => _plugin.Run(() =>
            {
                _plugin.Wheel.Toggle(item);
                FillWheelEditor();
                FillGrid();
            }));
            var rect = (RectTransform)remove.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(23, 23);
            rect.anchoredPosition = new Vector2(-3, -3);
            Hint(slot.transform, item.ItemName + "\nDrag to another slot. − removes it from the wheel.");
        }
        var navigation = NativeStyle.Row(_wheelEditor);
        AddWheelPageButton(navigation, "Previous", -1, _wheelPage > 0);
        var label = NativeStyle.Text(navigation, (_wheelPage + 1) + " / " + pages, 13);
        label.alignment = TextAlignmentOptions.Center;
        label.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;
        AddWheelPageButton(navigation, "Next", 1, _wheelPage + 1 < pages);
    }

    private void AddWheelPageButton(Transform parent, string title, int direction, bool available)
    {
        var button = NativeStyle.Button(parent, title, () => { _wheelPage += direction; FillWheelEditor(); });
        button.interactable = available;
        if (!available) return;
        button.gameObject.AddComponent<WheelDropTarget>().Drop = id => _plugin.Run(() =>
        {
            _wheelPage += direction;
            _plugin.Wheel.Move(id, _wheelPage * WheelLayout.PageSize);
            FillWheelEditor();
        });
        Hint(button.transform, "Drop an emote here to move it to the " + title.ToLowerInvariant() + " page.");
    }
}
