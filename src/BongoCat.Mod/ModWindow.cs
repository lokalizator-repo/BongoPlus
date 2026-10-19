using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BongoCat;
using BongoCat.UiThemes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BongoCatMod;

internal sealed class ModWindow : MonoBehaviour
{
    private Plugin _plugin = null!;
    private GameObject _canvas = null!;
    private RectTransform _panel = null!;
    private RectTransform _body = null!;
    private TextMeshProUGUI _status = null!;
    private TextMeshProUGUI _balance = null!;
    private TextMeshProUGUI _detail = null!;
    private Button _applyBalance = null!;
    private Button _restoreBalance = null!;
    private readonly List<Action> _refreshers = new List<Action>();
    private RectTransform _grid = null!;
    private TextMeshProUGUI _pageLabel = null!;
    private TMP_InputField _search = null!;
    private TextMeshProUGUI _catalogHint = null!;
    private Button _menuButton = null!;
    private string _slot = "skin";
    private int _page;
    private int _tab;
    private float _nextRefresh;
    private string? _error;
    private BongoCat.OnDemandRenderHelper _renderOwner = null!;
    private bool _ownsRenderDemand;

    public void Initialize(Plugin plugin)
    {
        _plugin = plugin;
        NativeStyle.Font = Resources.FindObjectsOfTypeAll<TMP_Text>().Select(t => t.font).First(f => f);
        _canvas = new GameObject("Bongo Cat Mods", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas.layer = 5;
        _canvas.transform.SetParent(transform, false);
        var canvas = _canvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = Mathf.Clamp(Screen.height / 1080f, 0.8f, 1.2f);

        _panel = NativeStyle.Rect("Mods window", _canvas.transform);
        _panel.anchorMin = _panel.anchorMax = new Vector2(1, 0);
        _panel.pivot = new Vector2(1, 0);
        _panel.anchoredPosition = new Vector2(-30, 110);
        _panel.sizeDelta = new Vector2(420, 510);
        NativeStyle.Image(_panel, UiSpriteRole.PopupBackground);

        var header = NativeStyle.Rect("Header", _panel);
        header.anchorMin = new Vector2(0, 1);
        header.anchorMax = Vector2.one;
        NativeStyle.Offsets(header, new Vector2(18, -47), new Vector2(-18, -10));
        var title = NativeStyle.Text(header, "MODS", 22);
        title.rectTransform.anchorMin = Vector2.zero;
        title.rectTransform.anchorMax = Vector2.one;
        NativeStyle.Offsets(title.rectTransform, Vector2.zero, new Vector2(-45, 0));
        title.alignment = TextAlignmentOptions.MidlineLeft;
        var close = NativeStyle.Button(header, "X", Toggle);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, 0.5f);
        closeRect.pivot = new Vector2(1, 0.5f);
        closeRect.sizeDelta = new Vector2(32, 32);

        var tabs = NativeStyle.Rect("Tabs", _panel);
        tabs.anchorMin = new Vector2(0, 1);
        tabs.anchorMax = Vector2.one;
        NativeStyle.Offsets(tabs, new Vector2(16, -87), new Vector2(-16, -51));
        tabs.gameObject.AddComponent<HorizontalLayoutGroup>().spacing = 6;
        NativeStyle.Button(tabs, "General", () => ShowTab(0));
        NativeStyle.Button(tabs, "Lobby", () => ShowTab(1));
        NativeStyle.Button(tabs, "Cosmetics", () => ShowTab(2));

        var viewport = NativeStyle.Rect("Viewport", _panel);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        NativeStyle.Offsets(viewport, new Vector2(18, 55), new Vector2(-18, -99));
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        _body = NativeStyle.Rect("Content", viewport);
        _body.anchorMin = new Vector2(0, 1);
        _body.anchorMax = Vector2.one;
        _body.pivot = new Vector2(0.5f, 1);
        _body.sizeDelta = Vector2.zero;
        NativeStyle.Vertical(_body);
        _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = _body;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25;

        _status = NativeStyle.Text(_panel, "F9 to toggle • v" + Plugin.Version, 12);
        _status.rectTransform.anchorMin = Vector2.zero;
        _status.rectTransform.anchorMax = new Vector2(1, 0);
        NativeStyle.Offsets(_status.rectTransform, new Vector2(18, 10), new Vector2(-18, 48));
        _status.textWrappingMode = TextWrappingModes.Normal;
        CreateMenuButton();
        ShowTab(0);
        _panel.gameObject.SetActive(false);
    }

    private void CreateMenuButton()
    {
        var menus = Resources.FindObjectsOfTypeAll<MenuTabs>();
        var menu = menus.FirstOrDefault(m => m.gameObject.scene.IsValid());
        if (!menu) return;
        var button = NativeStyle.Button(menu.transform, "Mods", Toggle);
        _menuButton = button;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 8);
        rect.sizeDelta = new Vector2(0, 34);
        rect.gameObject.GetComponent<LayoutElement>().ignoreLayout = true;
    }

    private void Update()
    {
        if (!_canvas) return;
        if (Input.GetKeyDown(KeyCode.F9)) Toggle();
        else if (_panel.gameObject.activeSelf)
        {
            var outside = !RectTransformUtility.RectangleContainsScreenPoint(_panel, Input.mousePosition, null);
            var onMenuButton = _menuButton && RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)_menuButton.transform, Input.mousePosition, null);
            if (Input.GetKeyDown(KeyCode.Escape) ||
                ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && outside && !onMenuButton))
                SetVisible(false);
        }
        if (!_panel.gameObject.activeSelf || Time.realtimeSinceStartup < _nextRefresh) return;
        _nextRefresh = Time.realtimeSinceStartup + 0.5f;
        if (_balance) _balance.text = "Available points: " + GameAccess.Balance.ToString("N0", CultureInfo.InvariantCulture);
        if (_detail) _detail.text = _plugin.Balance.Status +
            "\nMax balance: " + Math.Max(0, BalanceMath.Maximum - (int)GameAccess.Spent.GetValue(Pets.Instance)).ToString("N0");
        if (_applyBalance) _applyBalance.interactable = GameAccess.Ready && !_plugin.Balance.Pending;
        if (_restoreBalance) _restoreBalance.interactable = _plugin.Balance.HasBackup && !_plugin.Balance.Pending;
        foreach (var refresh in _refreshers) refresh();
        _status.text = _error ?? (_tab == 2
            ? (_slot == "emote" ? _plugin.Cosmetics.EmoteStatus : _plugin.Cosmetics.Status)
            : _plugin.Automation.Status);
    }

    public void Toggle() => SetVisible(!_panel.gameObject.activeSelf);

    private void SetVisible(bool show)
    {
        if (!_panel || _panel.gameObject.activeSelf == show) return;
        _panel.gameObject.SetActive(show);
        _renderOwner = OnDemandRenderHelper.Instance;
        if (_renderOwner)
        {
            if (show && !_ownsRenderDemand) { _renderOwner.ResumeRendering(); _ownsRenderDemand = true; }
            else if (!show && _ownsRenderDemand) { _renderOwner.TryPauseRendering(); _ownsRenderDemand = false; }
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SetVisible(false);
    }

    public void ShowError(string message) => _error = message;

    private void ShowTab(int tab)
    {
        _tab = tab;
        _error = null;
        _refreshers.Clear();
        _balance = null!;
        _detail = null!;
        _applyBalance = null!;
        _restoreBalance = null!;
        foreach (Transform child in _body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        _body.anchoredPosition = Vector2.zero;
        if (tab == 0) General();
        else if (tab == 1) Lobby();
        else Cosmetics();
    }

    private void General()
    {
        _balance = NativeStyle.Label(_body, "Available points", 28, 19);
        var input = NativeStyle.Input(_body, GameAccess.Balance.ToString(CultureInfo.InvariantCulture), "Target balance");
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        var actions = NativeStyle.Row(_body);
        _applyBalance = NativeStyle.Button(actions, "Set balance", () => _plugin.Run(() =>
        {
            if (!int.TryParse(input.text, out var target) || target < 0 || target > BalanceMath.Maximum)
                throw new ArgumentException("Enter a balance from 0 to 2,147,483,646.");
            _plugin.StopAutomation();
            _plugin.Balance.Set(target);
            _error = null;
        }));
        _restoreBalance = NativeStyle.Button(actions, "Restore balance", () => _plugin.Run(() =>
        {
            _plugin.StopAutomation();
            _plugin.Balance.Restore();
            _error = null;
        }));
        _detail = NativeStyle.Label(_body, "", 54, 12);
        _detail.textWrappingMode = TextWrappingModes.Normal;
        NativeStyle.Label(_body, "CHEST AUTOMATION", 25, 17);
        ToggleRow("Buy my ready chests", () => _plugin.Automation.BuyOwn, v => _plugin.Automation.BuyOwn = v);
        ToggleRow("Cosmetic chests", () => _plugin.Automation.Cosmetics, v => _plugin.Automation.Cosmetics = v);
        ToggleRow("Emote chests", () => _plugin.Automation.Emotes, v => _plugin.Automation.Emotes = v);
        NativeStyle.Button(_body, "Stop all automation", () => _plugin.Run(_plugin.StopAutomation));
    }

    private void Lobby()
    {
        NativeStyle.Label(_body, "GIFTS", 28, 19);
        NativeStyle.Label(_body, "Your points pay for their chest. They receive the item.", 42, 13).textWrappingMode = TextWrappingModes.Normal;
        ToggleRow("Auto-gift ready chests", () => _plugin.Automation.Gift, v => _plugin.Automation.Gift = v);
        ToggleRow("Include everyone in this lobby", () => _plugin.Automation.AllPlayers, v => _plugin.Automation.AllPlayers = v);
        ToggleRow("Cosmetic chests", () => _plugin.Automation.Cosmetics, v => _plugin.Automation.Cosmetics = v);
        ToggleRow("Emote chests", () => _plugin.Automation.Emotes, v => _plugin.Automation.Emotes = v);
        NativeStyle.Button(_body, "Refresh players", () => ShowTab(1));
        var players = _plugin.Automation.Players().ToList();
        if (players.Count == 0) NativeStyle.Label(_body, "Join a lobby to choose recipients.", 35, 14);
        foreach (var user in players)
        {
            var id = user.SteamId;
            ToggleRow(user.Name, () => _plugin.Automation.SelectedPlayers.Contains(id), value =>
            {
                if (value) _plugin.Automation.SelectedPlayers.Add(id);
                else _plugin.Automation.SelectedPlayers.Remove(id);
            });
        }
        NativeStyle.Button(_body, "Stop all automation", () => _plugin.Run(_plugin.StopAutomation));
    }

    private void Cosmetics()
    {
        ToggleRow("Share standard appearance in lobby", () => _plugin.Cosmetics.Publish,
            value => _plugin.Cosmetics.SetPublish(value), false);
        var slots = NativeStyle.Row(_body);
        foreach (var slot in new[] { "skin", "hat", "bongos", "emote" })
        {
            var selectedSlot = slot;
            NativeStyle.Button(slots, slot == "emote" ? "Emotes" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slot), () =>
            {
                _slot = selectedSlot;
                _page = 0;
                FillGrid();
            });
        }
        _search = NativeStyle.Input(_body, "", "Search cosmetics");
        _search.onValueChanged.AddListener(_ => { _page = 0; FillGrid(); });
        _catalogHint = NativeStyle.Label(_body, "", 32, 12);
        _catalogHint.textWrappingMode = TextWrappingModes.Normal;
        _grid = NativeStyle.Rect("Cosmetic grid", _body);
        var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(87, 88);
        grid.spacing = new Vector2(6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        _grid.gameObject.AddComponent<LayoutElement>().preferredHeight = 282;
        var pages = NativeStyle.Row(_body);
        NativeStyle.Button(pages, "Previous", () => { if (_page > 0) _page--; FillGrid(); });
        _pageLabel = NativeStyle.Text(pages, "", 13);
        _pageLabel.alignment = TextAlignmentOptions.Center;
        _pageLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 80;
        NativeStyle.Button(pages, "Next", () => { _page++; FillGrid(); });
        NativeStyle.Button(_body, "Restore equipped appearance", () => _plugin.Run(_plugin.Cosmetics.Restore));
        NativeStyle.Label(_body, "Appearance is saved between launches. Steam inventory is unchanged.", 42, 12).textWrappingMode = TextWrappingModes.Normal;
        FillGrid();
    }

    private void FillGrid()
    {
        if (!_grid) return;
        _catalogHint.text = _slot == "emote" ? "Click a standard emote to send it to your current lobby."
            : _slot == "bongos" ? "Bongos are the instruments in front of your cat's paws."
            : "Click an item to apply and save your appearance.";
        foreach (Transform child in _grid) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var items = _plugin.Cosmetics.Items(_slot, _search ? _search.text : "").ToList();
        var pages = Math.Max(1, (items.Count + 11) / 12);
        _page = Mathf.Clamp(_page, 0, pages - 1);
        if (_pageLabel) _pageLabel.text = (_page + 1) + " / " + pages;
        foreach (var item in items.Skip(_page * 12).Take(12))
        {
            var cosmetic = item;
            var card = NativeStyle.Button(_grid, "", () => _plugin.Run(() =>
            {
                if (cosmetic.IsEmote) _plugin.Cosmetics.SendEmote(cosmetic);
                else _plugin.Cosmetics.Select(cosmetic);
            }));
            var imageRect = NativeStyle.Rect("Icon", card.transform);
            imageRect.anchorMin = new Vector2(0, 0.26f);
            imageRect.anchorMax = Vector2.one;
            NativeStyle.Offsets(imageRect, new Vector2(5, 1), new Vector2(-5, -4));
            var image = imageRect.gameObject.AddComponent<Image>();
            image.sprite = cosmetic.Icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var label = card.GetComponentInChildren<TMP_Text>();
            label.text = cosmetic.ItemName;
            label.fontSize = 10;
            label.rectTransform.anchorMax = new Vector2(1, 0.25f);
        }
    }

    private void ToggleRow(string title, Func<bool> get, Action<bool> set, bool automation = true)
    {
        var row = NativeStyle.Row(_body);
        var label = NativeStyle.Text(row, title, 14);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var button = NativeStyle.Button(row, "Off", () => _plugin.Run(() =>
        {
            set(!get());
            if (automation) _plugin.SaveAutomation();
        }), 64);
        var state = button.GetComponentInChildren<TMP_Text>();
        void Refresh() => state.text = get() ? "On" : "Off";
        _refreshers.Add(Refresh);
        Refresh();
    }

    private void OnDestroy()
    {
        if (_ownsRenderDemand && _renderOwner) _renderOwner.TryPauseRendering();
        if (_canvas) Destroy(_canvas);
        if (_menuButton) Destroy(_menuButton.gameObject);
    }
}
