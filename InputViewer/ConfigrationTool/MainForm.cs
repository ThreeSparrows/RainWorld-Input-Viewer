using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConfigrationTool
{
    public class MainForm : Form
    {
        // "Section\0Key" → Control
        private readonly Dictionary<string, Control> _controls = new Dictionary<string, Control>();
        private readonly IniHelper _ini = new IniHelper();
        private TextBox _txtPath;
        private Label _lblStatus;
        private bool _updateTimeChanged = false;

        public MainForm()
        {
            Text = "RainWorld InputViewer Configration Tool";
            ClientSize = new Size(700, 660);
            MinimumSize = new Size(700, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Meiryo UI", 9f);
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            MaximizeBox = false;
            BuildUI();
            TryAutoLoad();
        }

        // ─────────────────────────────────────────────────────────
        // UI 構築
        // ─────────────────────────────────────────────────────────

        private void BuildUI()
        {
            // 下部パネル（パス・ボタン）
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 90 };

            var lblPath = new Label { Text = "config.ini:", AutoSize = true };
            lblPath.Location = new Point(8, 16);

            _txtPath = new TextBox { Location = new Point(90, 12), Width = 460 };

            var btnBrowse = new Button { Text = "参照(Browse)...", Location = new Point(556, 10), Width = 110 };
            btnBrowse.Click += OnBrowse;

            var btnLoad = new Button { Text = "読み込み(Load)", Location = new Point(8, 52), Width = 110 };
            btnLoad.Click += OnLoad;

            var btnSave = new Button
            {
                Text = "保存(Save)",
                Location = new Point(130, 52),
                Width = 96,
                BackColor = Color.FromArgb(190, 230, 190)
            };
            btnSave.Click += OnSave;

            _lblStatus = new Label { Location = new Point(240, 56), AutoSize = true };

            bottom.Controls.AddRange(new Control[] { lblPath, _txtPath, btnBrowse, btnLoad, btnSave, _lblStatus });

            // タブコントロール
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildControllerTab());
            tabs.TabPages.Add(BuildKeyboardTab());
            tabs.TabPages.Add(BuildGamePadTab());
            tabs.TabPages.Add(BuildSystemTab());
            tabs.TabPages.Add(BuildOverlayTab());

            Controls.Add(tabs);
            Controls.Add(bottom);
        }

        // ─────────────────────────────────────────────────────────
        // 各タブ
        // ─────────────────────────────────────────────────────────

        private TabPage BuildControllerTab()
        {
            var tab = new TabPage("コントローラー選択(Device Select)");
            var tbl = MakeTable(tab, 1);
            AddRow(tbl, 0, "デバイス種別\r\n(Device Type)",
                MakeCombo(new[] { "0: キーボード(KeyBoard)", "1: ゲームパッド (XInput GamePad)" }),
                "Controller", "DeviceType",
                "使用する入力デバイスを選択してください\r\nSelect the input device to use");
            return tab;
        }

        private TabPage BuildKeyboardTab()
        {
            var tab = new TabPage("キーボード(KeyBoard)");
            var tbl = MakeTable(tab, 14);
            int r = 0;

            AddRow(tbl, r++, "左移動(Left)",           MakeKeyButton(), "Input_Keyboard", "Left",     "");
            AddRow(tbl, r++, "右移動(Right)",           MakeKeyButton(), "Input_Keyboard", "Right",    "");
            AddRow(tbl, r++, "上移動(Up)",           MakeKeyButton(), "Input_Keyboard", "Up",       "");
            AddRow(tbl, r++, "下移動(Down)",           MakeKeyButton(), "Input_Keyboard", "Down",     "");
            AddRow(tbl, r++, "ジャンプ(Jump)",         MakeKeyButton(), "Input_Keyboard", "Jump",     "");
            AddRow(tbl, r++, "投げる(Throw)",   MakeKeyButton(), "Input_Keyboard", "Throw",    "");
            AddRow(tbl, r++, "掴む(Grab)",    MakeKeyButton(), "Input_Keyboard", "Grab",     "");
            AddRow(tbl, r++, "スペシャル(Special)",       MakeKeyButton(), "Input_Keyboard", "Special",  "");
            AddRow(tbl, r++, "ファストロール(FastRoll)",   MakeKeyButton(), "Input_Keyboard", "FastRoll", "FastRoll MODと同じキーを割り当ててください\r\nPlease assign the same key as FastRoll MOD");
            AddRow(tbl, r++, "スクリーンショット(Perfect)\r\nScreenShot(Perfect)", MakeKeyButton(), "Input_Keyboard", "Perfect",  "");
            AddRow(tbl, r++, "スクリーンショット(Good)\r\nScreenShot(Good)",    MakeKeyButton(), "Input_Keyboard", "Good",     "");
            AddRow(tbl, r++, "スクリーンショット(Failed)\r\nScreenShot(Failed)",  MakeKeyButton(), "Input_Keyboard", "Failed",   "");
            AddRow(tbl, r++, "アプリ終了(App Exit)",       MakeKeyButton(), "Input_Keyboard", "Exit", "アプリケーション終了キー\r\nApplication Exit Key");
            AddRow(tbl, r++, "Input Log",     MakeKeyButton(), "Input_Keyboard", "InputLog", "Input Log 表示/非表示の切り替えキー\r\nInput Log Toggle Key");
            return tab;
        }

        private TabPage BuildGamePadTab()
        {
            var tab = new TabPage("ゲームパッド(GamePad)");
            var tbl = MakeTable(tab, 6);
            int r = 0;
            string[] btns = { "1: A", "2: B", "3: X", "4: Y", "5: RB ", "6: LB ", "7: RT ", "8: LT" };

            AddRow(tbl, r++, "ジャンプ(Jump)",         MakeCombo(btns), "Input_GamePad", "Jump",     "");
            AddRow(tbl, r++, "投げる(Throw)",   MakeCombo(btns), "Input_GamePad", "Throw",    "");
            AddRow(tbl, r++, "掴む(Grab)",    MakeCombo(btns), "Input_GamePad", "Grab",     "");
            AddRow(tbl, r++, "スペシャル(Special)",       MakeCombo(btns), "Input_GamePad", "Special",  "");
            AddRow(tbl, r++, "ファストロール(FastRoll)",   MakeCombo(btns), "Input_GamePad", "FastRoll", "FastRoll MODと同じキーを割り当ててください\r\nPlease assign the same key as FastRoll MOD");
            AddRow(tbl, r++, "トリガー閾値\r\n(Trigger Threshold)",     MakeText(),      "Input_GamePad", "TT", "RT / LT の入力を検出する閾値 (0 ～ 255)\r\nThreshold for detecting RT / LT input (0 – 255)");
            return tab;
        }

        private TabPage BuildSystemTab()
        {
            var tab = new TabPage("システム(System)");
            var tbl = MakeTable(tab, 5);
            AddRow(tbl, 0, "常時最前面\r\n(Always on Top)",
                MakeCombo(new[] { "0: OFF", "1: ON" }),
                "Settings", "TopMost",
                "ウィンドウを常に最前面に表示するか\r\nKeep the window always on top");
            AddRow(tbl, 1, "入力サンプリング周期\r\n(Sampling Interval)",
                MakeText(),
                "Settings", "UpdateTime",
                "入力取得タイマーの更新間隔 (ms, 1.0 ～) \r\nInput polling timer interval (ms, 1.0 –)");
            ((TextBox)_controls["Settings\0UpdateTime"]).TextChanged += (s, e) => _updateTimeChanged = true;
            AddRow(tbl, 2, "1F辺りの線の長さ\r\n(Trail/Frame)",
                MakeText(),
                "Settings", "ResponseTime",
                "入力線の長さ/1F (0.01 ～ 1.00) 小さいほど長くなる \r\nTrail Length/Frame(0.01 - 1.00)\r\nSmaller = longer");
            AddRow(tbl, 3, "アイドルリセット時間\r\n(Idle Reset Time)",
                MakeText(),
                "Settings", "IdleResetTime",
                "無操作による軌跡消去時間 (Sec, 0.0 ～ 3.0)\r\nTrail erasure time on no input (Sec, 0.0 – 3.0)");
            AddRow(tbl, 4, "軌跡の最大表示数\r\n(Max Trail Count)",
                MakeText(),
                "Settings", "TrailMaxCount",
                "通常モードの軌跡最大保持数 (1 ～ 500)\r\nMaximum trail count in normal mode (1 – 500)");
            return tab;
        }

        private TabPage BuildOverlayTab()
        {
            var tab = new TabPage("Input Display");
            var tbl = MakeTable(tab, 6);
            int r = 0;
            AddRow(tbl, r++, "Input Display",
                MakeCombo(new[] { "0: OFF", "1: ON" }),
                "Overlay", "OverlayMode",
                "Input Display Modeの有効 / 無効\r\nEnable / Disable Input Display Mode");
            AddRow(tbl, r++, "不透明度\r\n(Opacity)",
                MakeText(),
                "Overlay", "OverlayFillOpacity",
                "全体の不透明度 (1 ～ 100)\r\nOverall opacity (1 – 100)");
            AddRow(tbl, r++, "表示スケール\r\n(Display Scale)",
                MakeText(),
                "Overlay", "OverlayScale",
                "表示全体のスケール (0.25 ～ 1.00)\r\nOverall display scale (0.25 – 1.00)");
            AddRow(tbl, r++, "軌跡の最大表示数\r\n(Max Trail Count)",
                MakeText(),
                "Overlay", "OverlayTrailMaxCount",
                "Input Displayモードの軌跡最大保持数 (1 ～ 500)\r\nMaximum trail count in Input Display mode\r\n(1 – 500)");
            AddRow(tbl, r++, "軌跡表示時間\r\n(Trail Duration)",
                MakeText(),
                "Overlay", "OverlayTrailDuration",
                "軌跡の表示継続時間 (Sec, 0.1 ～ 3.0)\r\nTrail display duration (Sec, 0.1 – 3.0)");
            AddRow(tbl, r++, "フェード時間\r\n(Fade Time)",
                MakeText(),
                "Overlay", "OverlayTrailFadeTime",
                "軌跡のフェードアウト時間 (Sec, 0.0 ～ 3.0)\r\nTrail fade-out duration (Sec, 0.0 – 3.0)");
            return tab;
        }

        // ─────────────────────────────────────────────────────────
        // UI ヘルパー
        // ─────────────────────────────────────────────────────────

        private TableLayoutPanel MakeTable(TabPage tab, int rows)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
            var tbl = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                RowCount = rows,
                Padding = new Padding(4),
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            for (int i = 0; i < rows; i++)
                tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            scroll.Controls.Add(tbl);
            tab.Controls.Add(scroll);
            return tbl;
        }

        private void AddRow(TableLayoutPanel tbl, int row, string label,
            Control ctrl, string section, string key, string desc)
        {
            var lbl = new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 0, 4, 0),
            };
            var cell = new Panel { Dock = DockStyle.Fill, Margin = new Padding(2, 0, 6, 0) };
            ctrl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cell.Controls.Add(ctrl);
            cell.Layout += (s, e) =>
            {
                ctrl.Width = cell.ClientSize.Width;
                ctrl.Top = Math.Max(0, (cell.ClientSize.Height - ctrl.Height) / 2);
            };
            var dsc = new Label
            {
                Text = desc,
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                AutoSize = false,
            };
            tbl.Controls.Add(lbl, 0, row);
            tbl.Controls.Add(cell, 1, row);
            tbl.Controls.Add(dsc, 2, row);
            _controls[section + "\0" + key] = ctrl;
        }

        private static ComboBox MakeCombo(string[] items)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            c.Items.AddRange(items);
            if (c.Items.Count > 0) c.SelectedIndex = 0;
            return c;
        }

        private static Button MakeKeyButton()
        {
            var btn = new Button { Text = "", TextAlign = ContentAlignment.MiddleCenter };
            btn.Click += (s, e) =>
            {
                using (var dlg = new KeyCaptureDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK && dlg.CapturedKey != null)
                        btn.Text = dlg.CapturedKey;
                }
            };
            return btn;
        }

        private static TextBox MakeText() => new TextBox();

        // ─────────────────────────────────────────────────────────
        // 読み込み / 保存
        // ─────────────────────────────────────────────────────────

        private void TryAutoLoad()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            if (File.Exists(path))
            {
                _txtPath.Text = path;
                LoadFromPath(path);
            }
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "config.ini を選択",
                Filter = "INI ファイル (*.ini)|*.ini|すべてのファイル (*.*)|*.*",
                FileName = "config.ini"
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    _txtPath.Text = dlg.FileName;
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            string path = _txtPath.Text.Trim();
            if (!File.Exists(path)) { SetStatus("ファイルが見つかりません: " + path, Color.Red); return; }
            LoadFromPath(path);
        }

        private void LoadFromPath(string path)
        {
            try
            {
                _ini.Load(path);
                Populate();
                _updateTimeChanged = false;
                SetStatus("読み込み完了(Load Complete): " + Path.GetFileName(path), Color.Green);
            }
            catch (Exception ex)
            {
                SetStatus("読み込みエラー(Load Error): " + ex.Message, Color.Red);
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            string path = _txtPath.Text.Trim();
            if (string.IsNullOrEmpty(path)) { SetStatus("config.ini のパスを指定してください(Please specify the path to config.ini)", Color.Red); return; }

            var errors = Validate();
            if (errors.Count > 0)
            {
                MessageBox.Show("入力エラーがあります(Input error(s) found):\n\n" + string.Join("\n", errors),
                    "検証エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string updateTimeVal = GetControlValue(_controls["Settings\0UpdateTime"]);
            float updateTimeF;
            if (_updateTimeChanged && float.TryParse(updateTimeVal, NumberStyles.Float, CultureInfo.InvariantCulture, out updateTimeF) && updateTimeF < 12.5f)
            {
                var result = MessageBox.Show(
                    "タイマー速度の値を小さく設定するとCPUに大きな負荷が掛かり、パソコンの動作に悪影響を及ぼす可能性があります。\r\n本当に変更しますか？\r\nSetting a smaller timer interval may cause high CPU usage and negatively affect system performance.\r\nAre you sure you want to change this?",
                    "警告(Warning)",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }

            try
            {
                Collect();
                _ini.Save(path);
                _updateTimeChanged = false;
                SetStatus("保存完了(Save Complete): " + Path.GetFileName(path), Color.Green);
            }
            catch (Exception ex)
            {
                SetStatus("保存エラー(Save Error): " + ex.Message, Color.Red);
            }
        }

        private void Populate()
        {
            foreach (var kv in _controls)
            {
                string[] parts = kv.Key.Split('\0');
                string section = parts[0], key = parts[1];
                string val = _ini.Get(section, key);
                if (string.IsNullOrEmpty(val)) continue;

                var ctrl = kv.Value;
                if (ctrl is ComboBox combo)
                {
                    if (combo.DropDownStyle == ComboBoxStyle.DropDown)
                    {
                        combo.Text = val;
                    }
                    else
                    {
                        foreach (var item in combo.Items)
                        {
                            string s = item.ToString();
                            string itemVal = s.Contains(":") ? s.Split(':')[0].Trim() : s;
                            if (itemVal == val) { combo.SelectedItem = item; break; }
                        }
                    }
                }
                else if (ctrl is TextBox tb)
                {
                    tb.Text = val;
                }
                else if (ctrl is Button btn)
                {
                    btn.Text = val;
                }
            }
        }

        private void Collect()
        {
            foreach (var kv in _controls)
            {
                string[] parts = kv.Key.Split('\0');
                string section = parts[0], key = parts[1];
                _ini.Set(section, key, GetControlValue(kv.Value));
            }
        }

        private static string GetControlValue(Control ctrl)
        {
            if (ctrl is ComboBox combo)
            {
                if (combo.DropDownStyle == ComboBoxStyle.DropDown)
                    return combo.Text.Trim();
                string s = combo.SelectedItem != null ? combo.SelectedItem.ToString() : "";
                return s.Contains(":") ? s.Split(':')[0].Trim() : s;
            }
            if (ctrl is TextBox tb)
                return tb.Text.Trim();
            if (ctrl is Button btn)
                return btn.Text.Trim();
            return "";
        }

        // ─────────────────────────────────────────────────────────
        // バリデーション
        // ─────────────────────────────────────────────────────────

        private List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var kv in _controls)
            {
                string[] parts = kv.Key.Split('\0');
                string section = parts[0], key = parts[1];
                string val = GetControlValue(kv.Value);
                string err = ValidateOne(section, key, val);
                if (err != null)
                    errors.Add(string.Format("[{0}] {1}: {2}", section, key, err));
            }
            return errors;
        }

        private static string ValidateOne(string section, string key, string val)
        {
            if (string.IsNullOrEmpty(val)) return "値が空です";

            if (section == "Controller" && key == "DeviceType")
                return (val == "0" || val == "1") ? null : "0 または 1 を指定してください";

            if (section == "Input_Keyboard")
            {
                Keys k;
                return Enum.TryParse(val, true, out k)
                    ? null
                    : string.Format("無効なキー名です: {0}", val);
            }

            if (section == "Input_GamePad" && key == "TT")
            {
                int n;
                return (int.TryParse(val, out n) && n >= 0 && n <= 255)
                    ? null : "0 ～ 255 の整数を指定してください";
            }

            if (section == "Input_GamePad")
            {
                int n;
                return (int.TryParse(val, out n) && n >= 1 && n <= 8)
                    ? null : "1 ～ 8 の整数を指定してください";
            }

            if ((section == "Settings" && key == "TopMost") ||
                (section == "Overlay" && key == "OverlayMode"))
                return (val == "0" || val == "1") ? null : "0 または 1 を指定してください";

            if (section == "Settings" && key == "UpdateTime")
            {
                float f;
                return (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f)
                        && f >= 1.0f)
                    ? null : "1.0 以上の値を指定してください";
            }

            if (section == "Settings" && key == "ResponseTime")
            {
                float f;
                return (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f)
                        && f >= 0.01f && f <= 1.00f)
                    ? null : "0.01 ～ 1.00 の値を指定してください";
            }

            if (section == "Settings" && key == "IdleResetTime")
            {
                float f;
                return (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f)
                        && f >= 0f && f <= 3.0f)
                    ? null : "0.0 ～ 3.0 の値を指定してください";
            }

            if ((section == "Settings" && key == "TrailMaxCount") ||
                (section == "Overlay" && key == "OverlayTrailMaxCount"))
            {
                int n;
                return (int.TryParse(val, out n) && n >= 1 && n <= 500)
                    ? null : "1 ～ 500 の整数を指定してください";
            }

            if (section == "Overlay" && key == "OverlayFillOpacity")
            {
                int n;
                return (int.TryParse(val, out n) && n >= 1 && n <= 100)
                    ? null : "1 ～ 100 の整数を指定してください";
            }

            if (section == "Overlay" && key == "OverlayScale")
            {
                float f;
                return (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f)
                        && f >= 0.25f && f <= 1.00f)
                    ? null : "0.25 ～ 1.00 の値を指定してください";
            }

            if (section == "Overlay" && key == "OverlayTrailDuration")
            {
                float f;
                return (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f)
                        && f >= 0.1f)
                    ? null : "0.1 以上の値を指定してください";
            }

            if (section == "Overlay" && key == "OverlayTrailFadeTime")
            {
                float f;
                return (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f)
                        && f >= 0f)
                    ? null : "0.0 以上の値を指定してください";
            }

            return null;
        }

        private void SetStatus(string msg, Color color)
        {
            _lblStatus.Text = msg;
            _lblStatus.ForeColor = color;
        }

        // ─────────────────────────────────────────────────────────
        // キー入力キャプチャダイアログ
        // ─────────────────────────────────────────────────────────

        private class KeyCaptureDialog : Form
        {
            public string CapturedKey { get; private set; }

            [DllImport("user32.dll")]
            private static extern short GetKeyState(int nVirtKey);

            public KeyCaptureDialog()
            {
                Text = "キー設定";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(300, 110);
                KeyPreview = true;

                var lbl = new Label
                {
                    Text = "設定したいキーを押してください\r\nPress the key you want to assign",
                    Bounds = new Rectangle(0, 20, 300, 40),
                    TextAlign = ContentAlignment.MiddleCenter,
                };
                var btnCancel = new Button
                {
                    Text = "キャンセル",
                    DialogResult = DialogResult.Cancel,
                    Bounds = new Rectangle(100, 68, 100, 28),
                };
                CancelButton = btnCancel;
                Controls.AddRange(new Control[] { lbl, btnCancel });

                KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.None) return;

                    Keys key = e.KeyCode;
                    // ShiftKey / ControlKey / Menu は左右区別なしで届くため GetKeyState で判定
                    if (key == Keys.ShiftKey)
                        key = GetKeyState(0xA0) < 0 ? Keys.LShiftKey : Keys.RShiftKey;
                    else if (key == Keys.ControlKey)
                        key = GetKeyState(0xA2) < 0 ? Keys.LControlKey : Keys.RControlKey;
                    else if (key == Keys.Menu)
                        key = GetKeyState(0xA4) < 0 ? Keys.LMenu : Keys.RMenu;

                    CapturedKey = key.ToString();
                    DialogResult = DialogResult.OK;
                    Close();
                };
            }
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.ResumeLayout(false);

        }
    }
}
