using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DeskOrder.Core;

namespace DeskOrder.App
{
    public class TrayService : IDisposable
    {
        private readonly DeskOrderApp _app;
        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _dockItem;
        private readonly ToolStripMenuItem _musicItem;
        private readonly ToolStripMenuItem _visualizerItem;
        private readonly ToolStripMenuItem _taskbarItem;
        private readonly ToolStripMenuItem _pureItem;
        private Icon _appIcon;

        public TrayService(DeskOrderApp app)
        {
            _app = app;
            _appIcon = LoadIcon();
            _menu = new ContextMenuStrip();
            _menu.Renderer = new DarkRenderer();
            _menu.BackColor = Color.FromArgb(22, 28, 41);
            _menu.ForeColor = Color.FromArgb(235, 240, 250);
            _menu.ShowImageMargin = false;
            _menu.Font = new Font("Microsoft YaHei UI", 9f);

            var open = new ToolStripMenuItem("打开桌序");
            open.Click += delegate { _app.ShowMain(0); };
            _menu.Items.Add(open);
            _menu.Items.Add(new ToolStripSeparator());

            _dockItem = new ToolStripMenuItem("显示 Dock");
            _dockItem.Click += delegate { _app.ToggleDock(); };
            _menu.Items.Add(_dockItem);

            _musicItem = new ToolStripMenuItem("音乐卡片");
            _musicItem.Click += delegate { _app.ToggleMusicWidget(); };
            _menu.Items.Add(_musicItem);

            _visualizerItem = new ToolStripMenuItem("桌面透明律动条");
            _visualizerItem.Click += delegate { _app.ToggleVisualizer(); };
            _menu.Items.Add(_visualizerItem);

            _taskbarItem = new ToolStripMenuItem("任务栏透明");
            _taskbarItem.Click += delegate { _app.SetTaskbarEnabled(!_app.Settings.Taskbar.Enabled); };
            _menu.Items.Add(_taskbarItem);

            _pureItem = new ToolStripMenuItem("纯净桌面");
            _pureItem.Click += delegate { _app.SetPureDesktop(!_app.Settings.General.HideDesktopIcons); };
            _menu.Items.Add(_pureItem);

            _menu.Items.Add(new ToolStripSeparator());
            var settings = new ToolStripMenuItem("设置");
            settings.Click += delegate { _app.ShowMain(4); };
            _menu.Items.Add(settings);
            var exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { _app.Exit(); };
            _menu.Items.Add(exit);

            _icon = new NotifyIcon();
            _icon.Icon = _appIcon;
            _icon.Text = "桌序 DeskOrder";
            _icon.ContextMenuStrip = _menu;
            _icon.Visible = true;
            _icon.DoubleClick += delegate { _app.ShowMain(0); };
            _menu.Opening += delegate { RefreshState(); };
        }

        public void Show()
        {
            _icon.Visible = true;
        }

        public void RefreshState()
        {
            try
            {
                _dockItem.Checked = _app.DockWindow != null && _app.DockWindow.IsDockVisible;
                _musicItem.Checked = _app.MusicWindow != null && _app.MusicWindow.IsVisible;
                _visualizerItem.Checked = _app.Visualizer != null && _app.Visualizer.IsOverlayVisible;
                _taskbarItem.Checked = _app.Settings.Taskbar.Enabled;
                _pureItem.Checked = _app.Settings.General.HideDesktopIcons;
            }
            catch { }
        }

        public void Balloon(string message, bool error)
        {
            try
            {
                _icon.BalloonTipTitle = error ? "桌序 · 提示" : "桌序";
                _icon.BalloonTipText = message;
                _icon.BalloonTipIcon = error ? ToolTipIcon.Warning : ToolTipIcon.Info;
                _icon.ShowBalloonTip(2600);
            }
            catch { }
        }

        private static Icon LoadIcon()
        {
            try
            {
                string path = Process.GetCurrentProcess().MainModule.FileName;
                Icon icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null) return icon;
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            try { _icon.Visible = false; _icon.Dispose(); } catch { }
            try { _menu.Dispose(); } catch { }
            try { if (_appIcon != null) _appIcon.Dispose(); } catch { }
        }

        private sealed class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors()) { }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Selected ? Color.White : Color.FromArgb(230, 236, 247);
                base.OnRenderItemText(e);
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.Clear(Color.FromArgb(22, 28, 41));
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(44, 255, 255, 255)))
                {
                    e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
                }
            }
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(22, 28, 41); } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(22, 28, 41); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(22, 28, 41); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(22, 28, 41); } }
            public override Color MenuItemSelected { get { return Color.FromArgb(48, 64, 92); } }
            public override Color MenuItemBorder { get { return Color.FromArgb(48, 64, 92); } }
            public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(48, 64, 92); } }
            public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(48, 64, 92); } }
            public override Color MenuBorder { get { return Color.FromArgb(55, 255, 255, 255); } }
        }
    }
}
