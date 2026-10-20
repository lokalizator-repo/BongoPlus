using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BongoPlusInstaller;

internal sealed class InstallerWindow : Form
{
    private static readonly Color Surface = Color.FromArgb(40, 48, 51);
    private static readonly Color Muted = Color.FromArgb(163, 177, 180);
    private static readonly Color Accent = Color.FromArgb(158, 214, 191);
    private readonly ComboBox _path = new ComboBox { Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly Label _state = new Label { AutoSize = true, ForeColor = Accent, Margin = new Padding(0, 18, 0, 6) };
    private readonly Label _detail = new Label { Dock = DockStyle.Fill, ForeColor = Muted, Margin = new Padding(0, 0, 0, 12), AutoEllipsis = true };
    private readonly Label _progressText = new Label { Dock = DockStyle.Fill, ForeColor = Muted, AutoEllipsis = true, Margin = new Padding(0, 6, 0, 4) };
    private readonly ProgressBar _progress = new ProgressBar { Dock = DockStyle.Fill, Height = 5, Visible = false, Style = ProgressBarStyle.Continuous };
    private readonly SoftButton _install = new SoftButton("Install BongoPlus", true) { Dock = DockStyle.Fill };
    private readonly SoftButton _remove = new SoftButton("Remove mod") { Dock = DockStyle.Fill, Visible = false };
    private readonly SoftButton _launch = new SoftButton("Play on Steam") { Dock = DockStyle.Fill, Visible = false };
    private readonly SoftButton _admin = new SoftButton("Retry as administrator") { Dock = DockStyle.Fill, Visible = false };
    private readonly SoftButton _browse = new SoftButton("Browse…") { Dock = DockStyle.Fill };
    private readonly string? _initial;
    private InstallEngine? _engine;
    private bool _busy;

    internal InstallerWindow(string? initial)
    {
        _initial = initial;
        Text = "BongoPlus";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(26, 33, 36);
        ForeColor = Color.FromArgb(233, 239, 237);
        ClientSize = new Size(596, 482);
        MinimumSize = new Size(612, 521);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MaximizeBox = false;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 20), ColumnCount = 1, RowCount = 11 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 84, 28, 38, 44, 70, 6, 28, 44, 42, 34, 22 })
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        Controls.Add(layout);
        layout.Controls.Add(new BrandHeader { Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(new Label { Text = "BONGO CAT FOLDER", ForeColor = Muted, AutoSize = true, Font = new Font(Font.FontFamily, 8, FontStyle.Bold), Margin = new Padding(0, 6, 0, 0) }, 0, 1);
        var folder = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        folder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folder.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        _path.BackColor = Surface;
        _path.ForeColor = ForeColor;
        _path.Margin = new Padding(0, 5, 10, 0);
        folder.Controls.Add(_path, 0, 0);
        folder.Controls.Add(_browse, 1, 0);
        layout.Controls.Add(folder, 0, 2);
        layout.Controls.Add(_state, 0, 3);
        layout.Controls.Add(_detail, 0, 4);
        layout.Controls.Add(_progress, 0, 5);
        layout.Controls.Add(_progressText, 0, 6);
        layout.Controls.Add(_install, 0, 7);
        var secondary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _remove.Margin = new Padding(0, 5, 5, 0);
        _launch.Margin = new Padding(5, 5, 0, 0);
        secondary.Controls.Add(_remove, 0, 0);
        secondary.Controls.Add(_launch, 1, 0);
        layout.Controls.Add(secondary, 0, 8);
        layout.Controls.Add(_admin, 0, 9);
        layout.Controls.Add(new Label { Text = "Portable installer · Windows x64 · No background app", ForeColor = Muted, AutoSize = true, Font = new Font(Font.FontFamily, 8), Margin = new Padding(0, 5, 0, 0) }, 0, 10);
        _browse.Click += (_, _) => Browse();
        _path.TextChanged += (_, _) => RefreshState();
        _install.Click += async (_, _) => await Perform(false);
        _remove.Click += async (_, _) => await Perform(true);
        _launch.Click += (_, _) => Launch();
        _admin.Click += (_, _) => Elevate();
        FormClosing += (_, args) => { if (_busy) args.Cancel = true; };
        Shown += async (_, _) => await Initialize();
    }

    private async Task Initialize()
    {
        SetBusy(true);
        _state.Text = "Looking for Bongo Cat…";
        try
        {
            var games = await Task.Run(SteamDiscovery.FindGames);
            using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("BongoPlus.Payload.zip")
                ?? throw new InvalidDataException("The embedded package is missing.");
            _engine = await Task.Run(() => new InstallEngine(payload));
            foreach (var game in games) _path.Items.Add(game);
            if (_initial != null) _path.Text = _initial;
            else if (games.Count > 0) _path.SelectedIndex = 0;
        }
        catch (Exception error) { ShowFailure(error); }
        finally { SetBusy(false); RefreshState(); }
    }

    private void RefreshState()
    {
        if (_busy) return;
        _admin.Visible = false;
        _state.ForeColor = Accent;
        _install.Enabled = false;
        _remove.Visible = _launch.Visible = false;
        if (_engine == null) { _state.Text = "Installer could not load"; return; }
        if (string.IsNullOrWhiteSpace(_path.Text))
        {
            _state.Text = "Game folder not found";
            _detail.Text = "Choose your Bongo Cat folder with Browse. In Steam: Manage → Browse local files.";
            return;
        }
        try
        {
            var root = InstallEngine.ValidateGame(_path.Text);
            var installed = InstallEngine.Installed(root);
            var manifest = InstallEngine.ReadManifest(root);
            var current = installed && manifest?.Version == InstallEngine.Version;
            _state.Text = installed ? "Installed · " + (manifest?.Version ?? "unknown version") : "Ready to install";
            _detail.Text = InstallEngine.KnownBuild(root)
                ? "Bongo Cat found. Settings are kept when updating or removing the mod."
                : "This game build has not been verified. BongoPlus will check feature compatibility when the game starts.";
            _install.Text = current ? "Repair installation" : installed ? "Update to BongoPlus " + InstallEngine.Version : "Install BongoPlus";
            _install.Enabled = true;
            _remove.Visible = installed && manifest != null;
            _launch.Visible = installed;
        }
        catch (Exception error) { _state.Text = "Check game folder"; _detail.Text = error.Message; }
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the folder containing BongoCat.exe", ShowNewFolderButton = false };
        if (Directory.Exists(_path.Text)) dialog.SelectedPath = _path.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = dialog.SelectedPath;
    }

    private async Task Perform(bool remove)
    {
        if (_engine == null) return;
        if (remove && MessageBox.Show(this, "Remove BongoPlus? Your settings will be kept.", "BongoPlus", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var root = _path.Text;
        SetBusy(true);
        _progress.Value = 0;
        _progress.Visible = true;
        _state.Text = remove ? "Removing BongoPlus…" : "Installing BongoPlus…";
        var progress = new Progress<Tuple<int, string>>(value => { _progress.Value = value.Item1; _progressText.Text = value.Item2; });
        try
        {
            var message = await Task.Run(() =>
            {
                Action<int, string> report = (percent, file) => ((IProgress<Tuple<int, string>>)progress).Report(Tuple.Create(percent, file));
                if (remove) return _engine.Uninstall(root, report);
                _engine.Install(root, report);
                return "BongoPlus installed. Start the game and open Mods in the game menu.";
            });
            SetBusy(false);
            RefreshState();
            _state.Text = remove ? "Removal complete" : "You're all set";
            _detail.Text = message;
        }
        catch (Exception error) { SetBusy(false); RefreshState(); ShowFailure(error); }
        finally { _progress.Visible = false; _progressText.Text = ""; }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _path.Enabled = _browse.Enabled = _install.Enabled = _remove.Enabled = _launch.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void ShowFailure(Exception error)
    {
        _state.ForeColor = Color.FromArgb(240, 173, 151);
        _state.Text = "Could not complete the operation";
        _detail.Text = error is UnauthorizedAccessException ? "Windows denied access to the game folder. Retry as administrator if this is your game installation." : error.Message;
        _admin.Visible = error is UnauthorizedAccessException;
    }

    private void Launch()
    {
        try { Process.Start(new ProcessStartInfo("steam://rungameid/" + SteamDiscovery.AppId) { UseShellExecute = true }); }
        catch (Exception error) { ShowFailure(error); }
    }

    private void Elevate()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--game \"" + _path.Text.Replace("\"", "") + "\"") { UseShellExecute = true, Verb = "runas" });
            Close();
        }
        catch (System.ComponentModel.Win32Exception error) { if (error.NativeErrorCode != 1223) ShowFailure(error); }
    }

    private sealed class BrandHeader : Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var background = new SolidBrush(Surface);
            using var accent = new SolidBrush(Accent);
            e.Graphics.FillEllipse(background, 0, 2, 58, 58);
            using var cat = new GraphicsPath();
            cat.AddPolygon(new[] { new PointF(11, 40), new PointF(13, 17), new PointF(24, 24), new PointF(35, 24), new PointF(45, 17), new PointF(47, 40) });
            e.Graphics.FillPath(accent, cat);
            using var eyes = new Pen(BackColor, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(eyes, 21, 32, 21, 34);
            e.Graphics.DrawLine(eyes, 36, 32, 36, 34);
            using var plus = new Pen(Accent, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(plus, 49, 44, 49, 56);
            e.Graphics.DrawLine(plus, 43, 50, 55, 50);
            using var title = new Font("Segoe UI", 23, FontStyle.Bold);
            using var caption = new Font("Segoe UI", 9);
            TextRenderer.DrawText(e.Graphics, "BongoPlus", title, new Point(73, 0), ForeColor);
            TextRenderer.DrawText(e.Graphics, "Your Bongo Cat, a little more you.  ·  " + InstallEngine.Version, caption, new Point(77, 43), Muted);
        }
    }

    private sealed class SoftButton : Button
    {
        private readonly bool _primary;
        private bool _hover;
        internal SoftButton(string text, bool primary = false)
        {
            Text = text;
            _primary = primary;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Margin = Padding.Empty;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var background = !Enabled ? Color.FromArgb(48, 57, 59) : _primary ? (_hover ? Color.FromArgb(180, 231, 210) : Accent) : (_hover ? Color.FromArgb(61, 73, 77) : Surface);
            using var brush = new SolidBrush(background);
            using var shape = new GraphicsPath();
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            const int corner = 14;
            shape.AddArc(bounds.Left, bounds.Top, corner, corner, 180, 90);
            shape.AddArc(bounds.Right - corner, bounds.Top, corner, corner, 270, 90);
            shape.AddArc(bounds.Right - corner, bounds.Bottom - corner, corner, corner, 0, 90);
            shape.AddArc(bounds.Left, bounds.Bottom - corner, corner, corner, 90, 90);
            shape.CloseFigure();
            e.Graphics.FillPath(brush, shape);
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, !Enabled ? Muted : _primary ? Color.FromArgb(26, 42, 36) : ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -5, -5));
        }
    }
}
