using System;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace BongoCatMod;

internal sealed partial class ModWindow
{
    private bool _capturingShortcut;
    private float _captureUntil;
    private static readonly KeyCode[] ModifierKeys = { KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt,
        KeyCode.RightAlt, KeyCode.LeftShift, KeyCode.RightShift };
    private static readonly KeyCode[] ShortcutKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
        .Where(key => key > KeyCode.None && key < KeyCode.Mouse0 && key != KeyCode.Escape && !ModifierKeys.Contains(key)).Distinct().ToArray();

    private void ShortcutSettings()
    {
        var row = NativeStyle.Row(_body);
        var title = NativeStyle.Text(row, "Open Mods shortcut", 14);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1;
        var bind = NativeStyle.Button(row, "", () =>
        {
            _capturingShortcut = !_capturingShortcut;
            _captureUntil = Time.realtimeSinceStartup + 15;
        }, 140);
        Hint(bind.transform, "Click, then press a key or Ctrl / Alt / Shift combination. Escape cancels.");
        var clear = NativeStyle.IconButton(row, ControlIcon.Trash, () => SetShortcut(KeyboardShortcut.Empty));
        Hint(clear.transform, "Disable the Mods shortcut");
        _refreshers.Add(() =>
        {
            bind.GetComponentInChildren<TMP_Text>().text = _capturingShortcut ? "Press keys…"
                : _settings.ToggleShortcut.Value.MainKey == KeyCode.None ? "Not assigned" : _settings.ToggleShortcut.Value.ToString();
            clear.interactable = _capturingShortcut || _settings.ToggleShortcut.Value.MainKey != KeyCode.None;
        });
        bind.GetComponentInChildren<TMP_Text>().text = _settings.ToggleShortcut.Value.MainKey == KeyCode.None
            ? "Not assigned" : _settings.ToggleShortcut.Value.ToString();
    }

    private void CaptureShortcut()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Time.realtimeSinceStartup > _captureUntil)
        {
            _capturingShortcut = false;
            return;
        }
        foreach (var key in ShortcutKeys)
        {
            if (!Input.GetKeyDown(key)) continue;
            SetShortcut(new KeyboardShortcut(key, ModifierKeys.Where(Input.GetKey).ToArray()));
            return;
        }
    }

    private void SetShortcut(KeyboardShortcut shortcut)
    {
        _capturingShortcut = false;
        _settings.ToggleShortcut.Value = shortcut;
        _settings.Save();
    }
}
