using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Linq;
using BongoCat;
using BongoCat.UiThemes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BongoCatMod;

internal sealed partial class ModWindow : MonoBehaviour
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
    private Button _menuButton = null!;
    private string _slot = "skin";
    private int _page;
    private int _tab;
    private float _nextRefresh;
    private string? _error;
    private string? _emoteFeedback;
    private BongoCat.OnDemandRenderHelper _renderOwner = null!;
    private bool _ownsRenderDemand;
    private ModSettings _settings = null!;
    private ScrollRect _scroll = null!;
    private CanvasScaler _scaler = null!;
    private float[] _scrollPositions = new float[5];
    private int ScrollIndex => _tab == 2 ? 2 + _cosmeticView : _tab;
    private string _query = "";
    private bool _favoritesOnly;
    private bool _stateDirty;
    private float _saveAt;
    private bool _building;
    private Coroutine? _restoreScroll;
    private Button _favoritesButton = null!;
    private Button _previousPage = null!;
    private Button _nextPage = null!;
    private TextMeshProUGUI _emptyState = null!;
    private RectTransform _presetList = null!;
    private TextMeshProUGUI _presetEmpty = null!;
    private RectTransform _wheelEditor = null!;
    private bool _editingWheel;
    private int _cosmeticView;
    private int _wheelPage;
    private string _wheelSignature = "";
    private RectTransform _tooltip = null!;
    private TextMeshProUGUI _tooltipText = null!;
    private Transform? _hoverOwner;
    private string _hoverText = "";
    private float _hoverAt;
    private Rect _lastWorkArea;
    private Button[] _tabs = Array.Empty<Button>();
    public bool IsTyping
    {
        get
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || !selected || !_canvas || !selected.transform.IsChildOf(_canvas.transform)) return false;
            var input = selected.GetComponent<TMP_InputField>();
            return input && input.isFocused;
        }
    }

    public void Initialize(Plugin plugin)
    {
        _plugin = plugin;
        _settings = plugin.Settings;
        _tab = Mathf.Clamp(_settings.MenuTab.Value, 0, 2);
        _slot = new[] { "skin", "hat", "emote" }.Contains(_settings.MenuSlot.Value) ? _settings.MenuSlot.Value : "skin";
        _query = _settings.MenuSearch.Value;
        _page = Math.Max(0, _settings.MenuPage.Value);
        _favoritesOnly = _settings.FavoritesOnly.Value;
        _cosmeticView = Mathf.Clamp(_settings.CosmeticView.Value, 0, 2);
        _scrollPositions = _settings.ReadScroll();
        NativeStyle.Font = Resources.FindObjectsOfTypeAll<TMP_Text>().Select(t => t.font).First(f => f);
        _canvas = new GameObject("Bongo Cat Mods", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas.layer = 5;
        _canvas.transform.SetParent(transform, false);
        var canvas = _canvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = _canvas.GetComponent<CanvasScaler>();
        _scaler = scaler;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = Mathf.Clamp(Screen.height / 1080f, 0.8f, 1.2f);

        _panel = NativeStyle.Rect("Mods window", _canvas.transform);
        _panel.anchorMin = _panel.anchorMax = new Vector2(1, 0);
        _panel.pivot = new Vector2(1, 0);
        _panel.anchoredPosition = new Vector2(-30, 110);
        _panel.sizeDelta = new Vector2(420, 560);
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
        var close = NativeStyle.IconButton(header, ControlIcon.Close, Toggle);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, 0.5f);
        closeRect.pivot = new Vector2(1, 0.5f);
        closeRect.sizeDelta = new Vector2(32, 32);

        var tabs = NativeStyle.Rect("Tabs", _panel);
        tabs.anchorMin = new Vector2(0, 1);
        tabs.anchorMax = Vector2.one;
        NativeStyle.Offsets(tabs, new Vector2(16, -87), new Vector2(-16, -51));
        tabs.gameObject.AddComponent<HorizontalLayoutGroup>().spacing = 6;
        _tabs = new[] {
            NativeStyle.NavButton(tabs, "General", ControlIcon.Settings, () => ShowTab(0)),
            NativeStyle.NavButton(tabs, "Lobby", ControlIcon.Lobby, () => ShowTab(1)),
            NativeStyle.NavButton(tabs, "Cosmetics", ControlIcon.Palette, () => ShowTab(2)) };

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
        _scroll = scroll;
        scroll.viewport = viewport;
        scroll.content = _body;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25;
        scroll.onValueChanged.AddListener(_ =>
        {
            if (!_building && _panel.gameObject.activeSelf)
            {
                _scrollPositions[ScrollIndex] = Math.Max(0, _body.anchoredPosition.y);
                StateChanged();
            }
        });

        _status = NativeStyle.Text(_panel, "v" + Plugin.Version, 12);
        _status.rectTransform.anchorMin = Vector2.zero;
        _status.rectTransform.anchorMax = new Vector2(1, 0);
        NativeStyle.Offsets(_status.rectTransform, new Vector2(18, 10), new Vector2(-18, 48));
        _status.textWrappingMode = TextWrappingModes.Normal;
        _tooltip = NativeStyle.Rect("Tooltip", _canvas.transform);
        _tooltip.pivot = Vector2.zero;
        _tooltip.sizeDelta = new Vector2(270, 62);
        NativeStyle.Image(_tooltip, UiSpriteRole.PopupBackground).raycastTarget = false;
        _tooltipText = NativeStyle.Text(_tooltip, "", 12);
        NativeStyle.Stretch(_tooltipText.rectTransform, 9);
        _tooltipText.textWrappingMode = TextWrappingModes.Normal;
        _tooltip.gameObject.SetActive(false);
        CreateMenuButton();
        ShowTab(_tab, false);
        FitWindow();
        _panel.gameObject.SetActive(false);
    }

    private void CreateMenuButton()
    {
        var menus = Resources.FindObjectsOfTypeAll<MenuTabs>();
        var menu = menus.FirstOrDefault(m => m.gameObject.scene.IsValid());
        if (!menu) return;
        var button = NativeStyle.NavButton(menu.transform, "Mods", ControlIcon.Mods, Toggle);
        _menuButton = button;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-4, 4);
        rect.sizeDelta = new Vector2(88, 28);
        rect.gameObject.GetComponent<LayoutElement>().ignoreLayout = true;
        var label = button.GetComponentInChildren<TMP_Text>();
        label.fontSize = 13;
        Destroy(label.GetComponent<ModTextTheme>());
        button.gameObject.AddComponent<NativeMenuButton>().Initialize(button.transform.Find("Icon").GetComponent<Graphic>(), label);
        Hint(button.transform, "Open Mods");
    }

    private void Update()
    {
        if (!_canvas) return;
        FitWindow();
        UpdateTooltip();
        if (_stateDirty && Time.realtimeSinceStartup >= _saveAt) SaveState();
        var captured = _capturingShortcut;
        if (captured) CaptureShortcut();
        if (!captured && _settings.ToggleShortcut.Value.MainKey != KeyCode.None &&
            !Helper.InputFieldCache.FocusingInputField && _settings.ToggleShortcut.Value.IsDown()) Toggle();
        else if (_panel.gameObject.activeSelf)
        {
            var outside = !RectTransformUtility.RectangleContainsScreenPoint(_panel, Input.mousePosition, null);
            var onMenuButton = _menuButton && RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)_menuButton.transform, Input.mousePosition, null);
            if ((!captured && Input.GetKeyDown(KeyCode.Escape)) ||
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
        if (_tab == 2 && _editingWheel && _wheelSignature != string.Join(",", _plugin.Wheel.ItemIds)) FillWheelEditor();
        _status.text = _error ?? (_tab == 2
            ? (_editingWheel ? _plugin.Wheel.Status : _slot == "emote" && _cosmeticView == 0 ? _emoteFeedback ?? _plugin.Cosmetics.EmoteStatus : _plugin.Cosmetics.Status)
            : _plugin.Automation.Error ?? _plugin.Automation.Status);
    }

    public void Toggle() => SetVisible(!_panel.gameObject.activeSelf);

    private void SetVisible(bool show)
    {
        if (!_panel || _panel.gameObject.activeSelf == show) return;
        if (!show) { HideTooltip(); SaveState(); }
        if (!show) _capturingShortcut = false;
        _panel.gameObject.SetActive(show);
        if (_menuButton) _menuButton.GetComponent<NativeMenuButton>().SetOpen(show);
        if (show && _tab == 2) FillGrid();
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

    private void ShowTab(int tab, bool capture = true)
    {
        if (capture && !_building) _scrollPositions[ScrollIndex] = Math.Max(0, _body.anchoredPosition.y);
        if (_restoreScroll != null) StopCoroutine(_restoreScroll);
        _building = true;
        _capturingShortcut = false;
        HideTooltip();
        _tab = tab;
        _error = null;
        _refreshers.Clear();
        _balance = null!;
        _detail = null!;
        _applyBalance = null!;
        _restoreBalance = null!;
        _grid = null!;
        _presetList = null!;
        _wheelEditor = null!;
        foreach (Transform child in _body) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        _body.anchoredPosition = Vector2.zero;
        if (tab == 0) General();
        else if (tab == 1) Lobby();
        else Cosmetics();
        for (var i = 0; i < _tabs.Length; i++)
            _tabs[i].targetGraphic.color = i == tab ? new Color(0.76f, 1f, 0.88f) : Color.white;
        StateChanged();
        _restoreScroll = StartCoroutine(RestoreScroll(tab));
    }

    private IEnumerator RestoreScroll(int tab)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (_tab == tab)
        {
            var maximum = Mathf.Max(0, _body.rect.height - _scroll.viewport.rect.height);
            _body.anchoredPosition = new Vector2(0, Mathf.Clamp(_scrollPositions[ScrollIndex], 0, maximum));
            _scroll.velocity = Vector2.zero;
        }
        _building = false;
        _restoreScroll = null;
    }

    private void General()
    {
        ShortcutSettings();
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
        AutomationDetails();
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
        AutomationDetails();
    }

    private void Cosmetics()
    {
        var views = NativeStyle.Row(_body, 30);
        var names = new[] { "Catalog", "Presets", "Emote wheel" };
        var title = NativeStyle.Text(views, names[_cosmeticView], 17);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var icons = new[] { ControlIcon.Catalog, ControlIcon.Presets, ControlIcon.Wheel };
        for (var i = 0; i < names.Length; i++)
        {
            var view = i;
            var button = NativeStyle.IconButton(views, icons[i], () =>
            {
                if (!_building) _scrollPositions[ScrollIndex] = Math.Max(0, _body.anchoredPosition.y);
                _cosmeticView = view;
                ShowTab(2, false);
            });
            button.targetGraphic.color = view == _cosmeticView ? new Color(0.76f, 1f, 0.88f) : Color.white;
            Hint(button.transform, names[i]);
        }
        var share = NativeStyle.IconButton(views, ControlIcon.Share, () => _plugin.Run(() =>
        {
            _plugin.Cosmetics.SetPublish(!_plugin.Cosmetics.Publish);
            ShowTab(2);
        }));
        share.targetGraphic.color = _plugin.Cosmetics.Publish ? new Color(0.76f, 1f, 0.88f) : new Color(0.55f, 0.55f, 0.55f);
        Hint(share.transform, _plugin.Cosmetics.Publish ? "Lobby sharing is on. Click to use local appearance only." : "Lobby sharing is off. Click to share your appearance.");
        var restore = NativeStyle.IconButton(views, ControlIcon.Reset, () => _plugin.Run(() =>
        {
            _plugin.Cosmetics.Restore();
            FillGrid();
            if (_presetList) FillPresets();
        }));
        Hint(restore.transform, "Restore your normally equipped skin and hat");
        _editingWheel = _cosmeticView == 2;
        if (_editingWheel)
        {
            _wheelEditor = NativeStyle.Rect("Wheel editor", _body);
            NativeStyle.Vertical(_wheelEditor);
            FillWheelEditor();
            return;
        }
        if (_cosmeticView == 1)
        {
            CreatePresets();
            return;
        }
        var slots = NativeStyle.Row(_body, 32);
        slots.GetComponent<HorizontalLayoutGroup>().spacing = 5;
        foreach (var slot in new[] { "skin", "hat", "emote" })
        {
            var selectedSlot = slot;
            var button = NativeStyle.IconButton(slots, slot == "skin" ? ControlIcon.Skin : slot == "hat" ? ControlIcon.Hat : ControlIcon.Emote, () =>
            {
                _slot = selectedSlot;
                _page = 0;
                _editingWheel = false;
                ShowTab(2);
            });
            button.targetGraphic.color = slot == _slot ? new Color(0.76f, 1f, 0.88f) : Color.white;
            Hint(button.transform, slot == "emote" ? "Emotes" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slot));
        }
        _search = NativeStyle.Input(slots, _query, "Search cosmetics");
        NativeStyle.Offsets(_search.textViewport, new Vector2(7, 7), new Vector2(-27, -7));
        _search.onValueChanged.AddListener(value => { _query = value; _page = 0; FillGrid(); StateChanged(); });
        var clear = NativeStyle.IconButton(_search.transform, ControlIcon.Close, () => { _search.text = ""; });
        var clearRect = (RectTransform)clear.transform;
        clearRect.anchorMin = clearRect.anchorMax = clearRect.pivot = new Vector2(1, 0.5f);
        clearRect.sizeDelta = new Vector2(22, 22);
        clearRect.anchoredPosition = new Vector2(-4, 0);
        Hint(clear.transform, "Clear search");
        _favoritesButton = NativeStyle.IconButton(slots, ControlIcon.Favorite, () =>
        {
            _favoritesOnly = !_favoritesOnly;
            _page = 0;
            FillGrid();
            StateChanged();
        });
        Hint(_favoritesButton.transform, "Show only favorites. Starred items appear first in the full catalog too.");
        _emptyState = NativeStyle.Label(_body, "", 48, 14);
        _emptyState.textWrappingMode = TextWrappingModes.Normal;
        _grid = NativeStyle.Rect("Cosmetic grid", _body);
        var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(87, 88);
        grid.spacing = new Vector2(6, 6);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        _grid.gameObject.AddComponent<LayoutElement>().preferredHeight = 282;
        var pages = NativeStyle.Row(_body);
        _previousPage = NativeStyle.IconButton(pages, ControlIcon.Previous, () => { if (_page > 0) _page--; FillGrid(); StateChanged(); });
        _pageLabel = NativeStyle.Text(pages, "", 13);
        _pageLabel.alignment = TextAlignmentOptions.Center;
        _pageLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        _nextPage = NativeStyle.IconButton(pages, ControlIcon.Next, () => { _page++; FillGrid(); StateChanged(); });
        Hint(_previousPage.transform, "Previous page");
        Hint(_nextPage.transform, "Next page");
        FillGrid();
    }

    private void FillGrid()
    {
        if (!_grid) return;
        HideTooltip();
        _favoritesButton.targetGraphic.color = _favoritesOnly ? new Color(1f, 0.88f, 0.58f) : Color.white;
        foreach (Transform child in _grid) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var items = _plugin.Cosmetics.Items(_slot, _query)
            .Where(item => !_favoritesOnly || _plugin.Cosmetics.IsFavorite(item.Id))
            .OrderByDescending(item => _plugin.Cosmetics.IsFavorite(item.Id)).ThenBy(item => item.ItemName).ToList();
        var pages = Math.Max(1, (items.Count + 11) / 12);
        _page = Mathf.Clamp(_page, 0, pages - 1);
        if (_pageLabel) _pageLabel.text = (_page + 1) + " / " + pages + "  ·  " + items.Count + " items";
        _previousPage.interactable = _page > 0;
        _nextPage.interactable = _page + 1 < pages;
        _emptyState.gameObject.SetActive(items.Count == 0);
        _emptyState.text = _favoritesOnly ? "No favorite items match. Star items in All cosmetics or clear your search."
            : "No items match your search. Try another name or clear the search.";
        _grid.gameObject.SetActive(items.Count != 0);
        StateChanged();
        foreach (var item in items.Skip(_page * 12).Take(12))
        {
            var cosmetic = item;
            var card = NativeStyle.Button(_grid, "", () => _plugin.Run(() =>
            {
                _error = null;
                if (cosmetic.IsEmote)
                {
                    _plugin.Cosmetics.SendEmote(cosmetic);
                    _emoteFeedback = _plugin.Cosmetics.EmoteStatus;
                }
                else
                {
                    _plugin.Cosmetics.Select(cosmetic);
                    FillGrid();
                }
            }));
            card.gameObject.name = cosmetic.ItemName;
            Hint(card.transform, cosmetic.ItemName + (cosmetic.IsEmote ? "\nClick to send. + / − changes your wheel." : "\nClick to wear and save."));
            var imageRect = NativeStyle.Rect("Icon", card.transform);
            imageRect.anchorMin = new Vector2(0, 0.26f);
            imageRect.anchorMax = Vector2.one;
            NativeStyle.Offsets(imageRect, new Vector2(5, 1), new Vector2(-5, -4));
            var image = imageRect.gameObject.AddComponent<Image>();
            image.sprite = cosmetic.Icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var label = card.GetComponentInChildren<TMP_Text>();
            label.text = (cosmetic.IsEmote && _plugin.Wheel.Contains(cosmetic.Id) ? "[Wheel] " : "") + cosmetic.ItemName;
            label.fontSize = 10;
            label.rectTransform.anchorMax = new Vector2(1, 0.25f);
            if (!cosmetic.IsEmote)
            {
                NativeStyle.Stretch(imageRect, 4);
                label.gameObject.SetActive(false);
                var frame = NativeStyle.Rect("Selected item", card.transform);
                NativeStyle.Stretch(frame);
                frame.gameObject.AddComponent<RoundedGraphic>().InitializeSelection();
                frame.gameObject.SetActive(_plugin.Cosmetics.Selected(cosmetic));
            }
            if (cosmetic.IsEmote)
            {
                var toggle = NativeStyle.IconButton(card.transform,
                    _plugin.Wheel.Contains(cosmetic.Id) ? ControlIcon.Remove : ControlIcon.Add,
                    () => _plugin.Run(() =>
                    {
                        _error = null;
                        _plugin.Wheel.Toggle(cosmetic);
                        _emoteFeedback = _plugin.Wheel.Status;
                        FillGrid();
                    }));
                var rect = (RectTransform)toggle.transform;
                rect.anchorMin = rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.sizeDelta = new Vector2(26, 24);
                rect.anchoredPosition = new Vector2(-3, -3);
                Hint(toggle.transform, _plugin.Wheel.Contains(cosmetic.Id) ? "Remove from your emote wheel" : "Add to your emote wheel");
            }
            var favorite = NativeStyle.IconButton(card.transform, ControlIcon.Favorite, () => _plugin.Run(() =>
            {
                _plugin.Cosmetics.ToggleFavorite(cosmetic.Id);
                FillGrid();
            }));
            var favoriteRect = (RectTransform)favorite.transform;
            favoriteRect.anchorMin = favoriteRect.anchorMax = new Vector2(0, 1);
            favoriteRect.pivot = new Vector2(0, 1);
            favoriteRect.sizeDelta = new Vector2(23, 23);
            favoriteRect.anchoredPosition = new Vector2(3, -3);
            favorite.targetGraphic.color = _plugin.Cosmetics.IsFavorite(cosmetic.Id) ? new Color(1f, 0.82f, 0.36f) : Color.white;
            Hint(favorite.transform, _plugin.Cosmetics.IsFavorite(cosmetic.Id) ? "Remove from favorites" : "Add to favorites");
        }
    }

    private void ToggleRow(string title, Func<bool> get, Action<bool> set, bool automation = true)
    {
        var row = NativeStyle.Row(_body);
        var label = NativeStyle.Text(row, title, 14);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        Hint(row, title);
        var button = NativeStyle.Button(row, "Off", () => _plugin.Run(() =>
        {
            if (automation && _plugin.Automation.Error != null)
                throw new InvalidOperationException("Resume after the error or stop all automation before changing its options.");
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
        if (_settings != null) SaveState();
        if (_ownsRenderDemand && _renderOwner) _renderOwner.TryPauseRendering();
        if (_canvas) Destroy(_canvas);
        if (_menuButton) Destroy(_menuButton.gameObject);
    }
}
