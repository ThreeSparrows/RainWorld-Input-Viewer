using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using static SuzumeInputViewer.GetController;
using GC = SuzumeInputViewer.GetController;


namespace SuzumeInputViewer
{

    public delegate void d_ElapsedEvent(long split);

    public partial class MainForm : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        // UpdateLayeredWindow 関連（オーバーレイのピクセル単位アルファ合成用）
        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool DeleteObject(IntPtr hObject);


        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        const int AC_SRC_OVER = 0;
        const int AC_SRC_ALPHA = 1;
        const int ULW_ALPHA = 2;

        const int WM_NCLBUTTONDOWN = 0xA1;
        const int HTCAPTION = 0x2;


        //Timer timer;

        // ===== デバイス設定 =====
        int DeviceType = 0;

        // ===== キー設定（変更可能） =====
        Keys keyLeft = Keys.A;
        Keys keyRight = Keys.D;
        Keys keyUp = Keys.W;
        Keys keyDown = Keys.S;
        Keys keyJump = Keys.I;
        Keys keyThrow = Keys.O;
        Keys keyGrab = Keys.U;
        Keys keySpecial = Keys.Y;
        Keys keyPerfectScreenshot = Keys.D1;    // 1キー
        Keys keyGoodScreenshot = Keys.D2;       // 2キー
        Keys keyFailedScreenshot = Keys.D3;     // 3キー
        Keys keyFastRoll = Keys.LShiftKey;
        Keys keyExit = Keys.M;
        Keys keyInutLog = Keys.P;

        // 入力
        float targetX = 0f;
        float targetY = 0f;
        float currentX = 0f;
        float currentY = 0f;

        //float responseTime = 0.1f;
        float responseTime = 0.05f;

        bool waitingForNewInputAfterIdle = false;
        bool wasInput = false;
        bool prevScreenshot = false;
        bool prevInputLog = false;

        // 軌跡
        class TrailPoint
        {
            public float X;
            public float Y;
            public float Time;
            public bool JumpLine;   // ← 色切替用
            public bool Jump;       // ←追加 0320ADD
            public bool Throw;      // ←追加 0320ADD
            public bool Grab;      // ←追加 0326ADD
            public bool Special;
            public bool FastRoll;
        }

        /*
        class ThrowPoint
        {
            public float X;
            public float Y;
        }

        class JumpPoint
        {
            public float X;
            public float Y;
        }
        */

        class InputLogPoint
        {
            public UInt16 frame = 0;  // 000～999
            public int LR = 0;     // None(0) or L(1) or R(2)
            public int UD = 0;     // None(0) or U(1) or D(2)
            public int J = 0;      // None(0) or J(1)
            public int T = 0;      // None(0) or T(1)
            public int G = 0;      // None(0) or G(1)
        }


        List<TrailPoint> trail = new List<TrailPoint>();
        //List<ThrowPoint> throwPoints = new List<ThrowPoint>();
        //List<JumpPoint> jumpPoints = new List<JumpPoint>();
        List<InputLogPoint> InputLogList = new List<InputLogPoint>();

        float currentTime = 0f;
        float idleTime = 0f;
        float idleResetTime = 2.0f;
        
        float updateTime = 16f;

        int JumpPointSize = 15;
        int ThrowPointSize = 12;
        int GrabPointSize = 9;
        int SpecialPointSize = 10;
        int FastRollPointSize = 10;
        int CursorPointSize = 12;

        bool prevJump = false;
        bool prevThrow = false;
        bool prevGrab = false;
        bool prevSpecial = false;
        bool prevFastRoll = false;
        bool prevExit = false;

        uint prevInputState = 0x00;

        bool InputLogEnabled = false;
        int InputLogReload = 0;

        int SavedNo = 0;
        int SavedLeaved = 0;

        int changeWidth = 110;
        int trailMaxCount = 500;
        int overlayTrailMaxCount = 500;

        bool overlayMode = false;
        volatile bool renderPending = false;
        int overlayCircleWidth = 6;
        int overlayCircleR = 255, overlayCircleG = 255, overlayCircleB = 255;
        int overlayFillR = 0, overlayFillG = 0, overlayFillB = 0;
        int overlayFillOpacity = 100;
        float overlayTrailFadeTime = 3.0f;
        float overlayFadeElapsed = 0f;
        float overlayTrailDuration = 5.0f;
        bool overlayButtonDisplay = false;
        int overlayButtonCellSize = 30;
        float overlayScale = 1.0f;

        SoundPlayer cameraPlayer;

        //https://note.com/haamit/n/ndd7faceeae2e
        HighAccuracyTimer timer;
        long prevTick = 0;

        FileSystemWatcher _configWatcher;
        System.Threading.Timer _reloadDebounce;

        Pen WriteLinePen = new Pen(Color.White, 1);

        private Font InputLogFont = new Font("Arial", 8, FontStyle.Bold);
        private int InputLocateX = 322;
        //private int InputLocateY = 300;
        private int InputLocateY = 10;
        private int InputDistanceY = 12;
        private int SaveLogCount = 25;

        // XInput 取得
        GC.XINPUT_STATE state;

        int GamePadJump = 0;
        int GamePadThrow = 0;
        int GamePadGrab = 0;
        int GamePadSpecial = 0;
        int GamePadFastRoll = 0;
        int GamePadTT = 0;


        public MainForm()
        {
            InitializeComponent();

            DoubleBuffered = true;
            Width = 340;
            Height = 360;
            BackColor = Color.Black;

            this.Text = "Rain World InputViewer";
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;


            var ini = new IniFile("config.ini");

            string topMostStr = ini.Get("Settings", "TopMost", "1");
            this.TopMost = topMostStr == "1" || topMostStr.ToLower() == "true";

            // デバイス設定/選択 0:キーボード / 1:ゲームパッド(XInput)
            int.TryParse(ini.Get("Controller", "DeviceType", "0"), out DeviceType);

            // キー設定
            keyLeft = ParseKey(ini.Get("Input_Keyboard", "Left", "A"));
            keyRight = ParseKey(ini.Get("Input_Keyboard", "Right", "D"));
            keyUp = ParseKey(ini.Get("Input_Keyboard", "Up", "W"));
            keyDown = ParseKey(ini.Get("Input_Keyboard", "Down", "S"));
            keyJump = ParseKey(ini.Get("Input_Keyboard", "Jump", "I"));
            keyThrow = ParseKey(ini.Get("Input_Keyboard", "Throw", "O"));
            keyGrab = ParseKey(ini.Get("Input_Keyboard", "Grab", "U"));
            keySpecial = ParseKey(ini.Get("Input_Keyboard", "Special", "Y"));
            keyPerfectScreenshot = ParseKey(ini.Get("Input_Keyboard", "Perfect", "D1"));
            keyGoodScreenshot = ParseKey(ini.Get("Input_Keyboard", "Good", "D2"));
            keyFailedScreenshot = ParseKey(ini.Get("Input_Keyboard", "Failed", "D3"));

            keyFastRoll = ParseKey(ini.Get("Input_Keyboard", "FastRoll", "LShiftKey"));
            keyExit = ParseKey(ini.Get("Input_Keyboard", "Exit", "M"));
            keyInutLog = ParseKey(ini.Get("Input_Keyboard", "InputLog", "P"));

            // ゲームパッド設定
            int.TryParse(ini.Get("Input_GamePad", "Jump", "1"), out GamePadJump);
            int.TryParse(ini.Get("Input_GamePad", "Throw", "2"), out GamePadThrow);
            int.TryParse(ini.Get("Input_GamePad", "Grab", "3"), out GamePadGrab);
            int.TryParse(ini.Get("Input_GamePad", "Special", "4"), out GamePadSpecial);
            int.TryParse(ini.Get("Input_GamePad", "FastRoll", "8"), out GamePadFastRoll);
            int.TryParse(ini.Get("Input_GamePad", "TT", "30"), out GamePadTT);

            // 数値設定
            float.TryParse(ini.Get("Settings", "ResponseTime", "0.1"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out responseTime);
            float.TryParse(ini.Get("Settings", "IdleResetTime", "2.0"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out idleResetTime);
            if (idleResetTime < 0f || idleResetTime > 3.0f) idleResetTime = 0f;
            float.TryParse(ini.Get("Settings", "UpdateTime", "10"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out updateTime);
            if (updateTime < 1.0f) updateTime = 1.0f;

            int.TryParse(ini.Get("Settings", "JumpPointSize", "15"), out JumpPointSize);
            int.TryParse(ini.Get("Settings", "ThrowPointSize", "12"), out ThrowPointSize);
            int.TryParse(ini.Get("Settings", "GrabPointSize", "9"), out GrabPointSize);
            int.TryParse(ini.Get("Settings", "SpecialPointSize", "10"), out SpecialPointSize);
            int.TryParse(ini.Get("Settings", "FastRollPointSize", "10"), out FastRollPointSize);
            int.TryParse(ini.Get("Settings", "CursorPointSize", "12"), out CursorPointSize);

            int.TryParse(ini.Get("Settings", "TrailMaxCount", "500"), out trailMaxCount);
            if (trailMaxCount > 500) trailMaxCount = 500;

            int.TryParse(ini.Get("Overlay", "OverlayTrailMaxCount", "500"), out overlayTrailMaxCount);
            if (overlayTrailMaxCount > 500) overlayTrailMaxCount = 500;

            /*
            timer = new Timer();
            //timer.Interval = 16;
            timer.Interval = updateTime;
            timer.Tick += Update;
            timer.Start();
            */

            timer = new HighAccuracyTimer(updateTime); // 10ms

            prevTick = 0;

            timer.Elapsed += (currentTick) =>
            {
                if (prevTick == 0) prevTick = currentTick;

                float dt = (float)(currentTick - prevTick) / Stopwatch.Frequency;
                prevTick = currentTick;

                UpdateHighPrecision(dt);
            };

            timer.Start();

            // オーバーレイモード設定
            string overlayStr = ini.Get("Overlay", "OverlayMode", "0");
            overlayMode = overlayStr == "1" || overlayStr.ToLower() == "true";

            if (overlayMode)
            {
                // 外周線の太さ
                int.TryParse(ini.Get("Overlay", "OverlayCircleWidth", "6"), out overlayCircleWidth);

                // 外周線の色 (R,G,B)
                string circleColorStr = ini.Get("Overlay", "OverlayCircleColor", "255,255,255");
                var circleRgb = circleColorStr.Split(',');
                if (circleRgb.Length == 3)
                {
                    int.TryParse(circleRgb[0].Trim(), out overlayCircleR);
                    int.TryParse(circleRgb[1].Trim(), out overlayCircleG);
                    int.TryParse(circleRgb[2].Trim(), out overlayCircleB);
                }

                // 円内塗りつぶし色 (R,G,B)
                string colorStr = ini.Get("Overlay", "OverlayFillColor", "0,0,0");
                var rgb = colorStr.Split(',');
                if (rgb.Length == 3)
                {
                    int.TryParse(rgb[0].Trim(), out overlayFillR);
                    int.TryParse(rgb[1].Trim(), out overlayFillG);
                    int.TryParse(rgb[2].Trim(), out overlayFillB);
                }

                // 円内の不透明度 (1～100)
                int.TryParse(ini.Get("Overlay", "OverlayFillOpacity", "100"), out overlayFillOpacity);
                overlayFillOpacity = Math.Max(1, Math.Min(100, overlayFillOpacity));

                // 軌跡フェードアウト時間（秒）
                float.TryParse(ini.Get("Overlay", "OverlayTrailFadeTime", "3.0"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out overlayTrailFadeTime);

                // 軌跡の表示継続時間（秒）
                float.TryParse(ini.Get("Overlay", "OverlayTrailDuration", "5.0"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out overlayTrailDuration);

                // ボタン表示
                string btnDispStr = ini.Get("Overlay", "OverlayButtonDisplay", "0");
                overlayButtonDisplay = btnDispStr == "1" || btnDispStr.ToLower() == "true";
                if (overlayButtonDisplay)
                    int.TryParse(ini.Get("Overlay", "OverlayButtonCellSize", "30"), out overlayButtonCellSize);

                // スケール（縮小率）
                float.TryParse(ini.Get("Overlay", "OverlayScale", "1.0"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out overlayScale);
                overlayScale = Math.Max(0.25f, Math.Min(1.0f, overlayScale));

                // 円形（radius=90）＋ボタン点（最大15px）＋余白 に最適化したサイズ（スケール適用後）
                int buttonAreaWidth = overlayButtonDisplay ? 5 + overlayButtonCellSize * 5 + 4 * 2 : 0;
                Width  = (int)((210 + buttonAreaWidth) * overlayScale);
                Height = (int)(210 * overlayScale);
                // WS_EX_LAYERED は CreateParams で設定、描画は RenderOverlay() が担当
            }

            InitConfigWatcher();
            this.FormClosing += (s, e) => { _configWatcher?.Dispose(); _reloadDebounce?.Dispose(); };
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
        }

        private void InitConfigWatcher()
        {
            string configPath = Path.GetFullPath("config.ini");
            if (!File.Exists(configPath)) return;

            _reloadDebounce = new System.Threading.Timer(_ =>
            {
                if (IsHandleCreated)
                    BeginInvoke((Action)ReloadConfig);
            }, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

            _configWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(configPath),
                Filter = Path.GetFileName(configPath),
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += (s, e) =>
                _reloadDebounce.Change(200, System.Threading.Timeout.Infinite);
        }

        private void ReloadConfig()
        {
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    var ini = new IniFile("config.ini");

                    // 再起動が必要な設定の変更を検知
                    float newUpdateTime;
                    float.TryParse(ini.Get("Settings", "UpdateTime", "10"),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out newUpdateTime);
                    if (newUpdateTime < 1.0f) newUpdateTime = 1.0f;

                    int newDeviceType;
                    int.TryParse(ini.Get("Controller", "DeviceType", "0"), out newDeviceType);

                    string newOverlayModeStr = ini.Get("Overlay", "OverlayMode", "0");
                    bool newOverlayMode = newOverlayModeStr == "1" || newOverlayModeStr.ToLower() == "true";

                    if (Math.Abs(newUpdateTime - updateTime) > 0.0001f || newDeviceType != DeviceType || newOverlayMode != overlayMode)
                    {
                        using (var owner = new Form { TopMost = true })
                        {
                            var _ = owner.Handle; // HWND を強制生成（WS_EX_TOPMOST 付き）
                            MessageBox.Show(owner,
                                "InputViewerを再起動します。\r\n(Restarting InputViewer.)",
                                "再起動(Restart)",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        Application.Restart();
                        return;
                    }

                    // TopMost
                    string topMostStr = ini.Get("Settings", "TopMost", "1");
                    this.TopMost = topMostStr == "1" || topMostStr.ToLower() == "true";

                    // キー設定
                    keyLeft    = ParseKey(ini.Get("Input_Keyboard", "Left",     "A"));
                    keyRight   = ParseKey(ini.Get("Input_Keyboard", "Right",    "D"));
                    keyUp      = ParseKey(ini.Get("Input_Keyboard", "Up",       "W"));
                    keyDown    = ParseKey(ini.Get("Input_Keyboard", "Down",     "S"));
                    keyJump    = ParseKey(ini.Get("Input_Keyboard", "Jump",     "I"));
                    keyThrow   = ParseKey(ini.Get("Input_Keyboard", "Throw",    "O"));
                    keyGrab    = ParseKey(ini.Get("Input_Keyboard", "Grab",     "U"));
                    keySpecial = ParseKey(ini.Get("Input_Keyboard", "Special",  "Y"));
                    keyPerfectScreenshot = ParseKey(ini.Get("Input_Keyboard", "Perfect",  "D1"));
                    keyGoodScreenshot    = ParseKey(ini.Get("Input_Keyboard", "Good",     "D2"));
                    keyFailedScreenshot  = ParseKey(ini.Get("Input_Keyboard", "Failed",   "D3"));
                    keyFastRoll = ParseKey(ini.Get("Input_Keyboard", "FastRoll", "LShiftKey"));
                    keyExit     = ParseKey(ini.Get("Input_Keyboard", "Exit",    "M"));
                    keyInutLog  = ParseKey(ini.Get("Input_Keyboard", "InputLog","P"));

                    // ゲームパッド設定
                    int.TryParse(ini.Get("Input_GamePad", "Jump",     "1"), out GamePadJump);
                    int.TryParse(ini.Get("Input_GamePad", "Throw",    "2"), out GamePadThrow);
                    int.TryParse(ini.Get("Input_GamePad", "Grab",     "3"), out GamePadGrab);
                    int.TryParse(ini.Get("Input_GamePad", "Special",  "4"), out GamePadSpecial);
                    int.TryParse(ini.Get("Input_GamePad", "FastRoll", "8"), out GamePadFastRoll);
                    int.TryParse(ini.Get("Input_GamePad", "TT",      "30"), out GamePadTT);

                    // 数値設定（UpdateTime・DeviceTypeは除外）
                    float.TryParse(ini.Get("Settings", "ResponseTime", "0.1"),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out responseTime);
                    float newIdleReset;
                    if (float.TryParse(ini.Get("Settings", "IdleResetTime", "2.0"),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out newIdleReset))
                        idleResetTime = (newIdleReset < 0f || newIdleReset > 3.0f) ? 0f : newIdleReset;

                    int newTrailMax;
                    if (int.TryParse(ini.Get("Settings", "TrailMaxCount", "500"), out newTrailMax))
                        trailMaxCount = Math.Min(newTrailMax, 500);

                    int newOverlayTrailMax;
                    if (int.TryParse(ini.Get("Overlay", "OverlayTrailMaxCount", "500"), out newOverlayTrailMax))
                        overlayTrailMaxCount = Math.Min(newOverlayTrailMax, 500);

                    // Overlay設定
                    int.TryParse(ini.Get("Overlay", "OverlayCircleWidth", "6"), out overlayCircleWidth);

                    string circleColorStr = ini.Get("Overlay", "OverlayCircleColor", "255,255,255");
                    var circleRgb = circleColorStr.Split(',');
                    if (circleRgb.Length == 3)
                    {
                        int.TryParse(circleRgb[0].Trim(), out overlayCircleR);
                        int.TryParse(circleRgb[1].Trim(), out overlayCircleG);
                        int.TryParse(circleRgb[2].Trim(), out overlayCircleB);
                    }

                    string colorStr = ini.Get("Overlay", "OverlayFillColor", "0,0,0");
                    var rgb = colorStr.Split(',');
                    if (rgb.Length == 3)
                    {
                        int.TryParse(rgb[0].Trim(), out overlayFillR);
                        int.TryParse(rgb[1].Trim(), out overlayFillG);
                        int.TryParse(rgb[2].Trim(), out overlayFillB);
                    }

                    int.TryParse(ini.Get("Overlay", "OverlayFillOpacity", "100"), out overlayFillOpacity);
                    overlayFillOpacity = Math.Max(1, Math.Min(100, overlayFillOpacity));

                    float.TryParse(ini.Get("Overlay", "OverlayTrailFadeTime", "3.0"),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out overlayTrailFadeTime);

                    float.TryParse(ini.Get("Overlay", "OverlayTrailDuration", "5.0"),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out overlayTrailDuration);

                    string btnDispStr = ini.Get("Overlay", "OverlayButtonDisplay", "0");
                    overlayButtonDisplay = btnDispStr == "1" || btnDispStr.ToLower() == "true";
                    if (overlayButtonDisplay)
                        int.TryParse(ini.Get("Overlay", "OverlayButtonCellSize", "30"), out overlayButtonCellSize);

                    float.TryParse(ini.Get("Overlay", "OverlayScale", "1.0"),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out overlayScale);
                    overlayScale = Math.Max(0.25f, Math.Min(1.0f, overlayScale));

                    // OverlayMode はモード変更時に再起動済みのため、ここではスケール変更のみ処理
                    if (overlayMode)
                    {
                        int buttonAreaWidth = overlayButtonDisplay ? 5 + overlayButtonCellSize * 5 + 4 * 2 : 0;
                        Width  = (int)((210 + buttonAreaWidth) * overlayScale);
                        Height = (int)(210 * overlayScale);
                    }

                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }


        bool GetKey(Keys key)
        {
            return (GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        Keys ParseKey(string keyName)
        {
            try
            {
                return (Keys)Enum.Parse(typeof(Keys), keyName, true);
            }
            catch
            {
                return Keys.None;
            }
        }

        bool GetGamePadInput(GC.XINPUT_STATE arg_state, int KeyNo)
        {
            bool ReturnValue = false;

            buttons = arg_state.Gamepad.wButtons;

            switch (KeyNo)
            {
                case 1:
                    // A
                    ReturnValue = (buttons & GC.A) != 0;
                    break;

                case 2:
                    // B
                    ReturnValue = (buttons & GC.B) != 0;
                    break;

                case 3:
                    // X
                    ReturnValue = (buttons & GC.X) != 0;
                    break;

                case 4:
                    // Y
                    ReturnValue = (buttons & GC.Y) != 0;
                    break;

                case 5:
                    // RB
                    ReturnValue = (buttons & GC.RIGHT_SHOULDER) != 0;
                    break;

                case 6:
                    // LB
                    ReturnValue = (buttons & GC.LEFT_SHOULDER) != 0;
                    break;

                case 7:
                    // RT
                    rt = arg_state.Gamepad.bRightTrigger;
                    ReturnValue = rt > GamePadTT;
                    break;

                case 8:
                    // LT
                    lt = arg_state.Gamepad.bLeftTrigger;
                    ReturnValue = lt > GamePadTT;
                    break;

                default:
                    break;
            }
            return ReturnValue;
        }



        UInt16 FrameCount;

        bool left = false;
        bool right = false;
        bool up = false;
        bool down = false;
        bool jump = false;
        bool currentThrow = false;
        bool currentGrab = false;
        bool currentSpecial = false;
        bool currentFastRoll = false;

        bool InputLog = false;
        bool anyInput = false;
        bool residueInput = false;  // 2026/05/17 Add

        // ゲームパッド関連 --->

        ushort buttons;
        byte lt;
        byte rt;

        bool GamePad_Enabled = false;

        //short lx;
        //short ly;

        //short rx;
        //short ry;

        // ゲームパッド関連 ---<



        //private void Update(object sender, EventArgs e)
        private void UpdateHighPrecision(float dt)
        {
            try
            {
                //float dt = timer.Interval / 1000f;

                currentTime += dt;
                FrameCount++;

                //Debug.WriteLine("★currentTime : " + currentTime);
                //Debug.WriteLine("★FrameCount : " + FrameCount);

                // 入力取得
                if (DeviceType == 1)
                {
                    // ゲームパッド取得
                    if (GC.XInputGetState(0, out state) == 0)
                    {
                        buttons = state.Gamepad.wButtons;

                        //lt = state.Gamepad.bLeftTrigger;
                        //rt = state.Gamepad.bRightTrigger;

                        up = (buttons & GC.UP) != 0;
                        down = (buttons & GC.DOWN) != 0;
                        left = (buttons & GC.LEFT) != 0;
                        right = (buttons & GC.RIGHT) != 0;

                        jump = GetGamePadInput(state, GamePadJump);
                        currentThrow = GetGamePadInput(state, GamePadThrow);
                        currentGrab = GetGamePadInput(state, GamePadGrab);
                        currentSpecial = GetGamePadInput(state, GamePadSpecial);
                        currentFastRoll = GetGamePadInput(state, GamePadFastRoll);

                        GamePad_Enabled = true;
                    }
                    else
                    {
                        GamePad_Enabled = false;
                    }

                }
                else
                {
                    // キーボード入力
                    up = GetKey(keyUp);
                    left = GetKey(keyLeft);
                    down = GetKey(keyDown);
                    right = GetKey(keyRight);
                    jump = GetKey(keyJump);
                    currentThrow = GetKey(keyThrow);
                    currentGrab = GetKey(keyGrab);
                    currentSpecial = GetKey(keySpecial);
                    currentFastRoll = GetKey(keyFastRoll);
                }


                InputLog = GetKey(keyInutLog);

                anyInput = left || right || up || down || jump || currentThrow || currentGrab || currentSpecial || currentFastRoll;

                // ===== 放置検知 =====
                if (!anyInput)
                {
                    idleTime += dt;

                    //if (idleTime > 2f)
                    if (idleTime > idleResetTime)
                    {
                        idleTime = idleResetTime;
                        waitingForNewInputAfterIdle = true;

                        // 2026/05/17 REP
                        //currentTime = idleTime;   
                        if (currentTime >= overlayTrailDuration)
                        {
                            currentTime = overlayTrailDuration;
                        }

                        FrameCount--;

                    }

                    // オーバーレイ: アイドル待機中にフェード経過時間を加算
                    if (overlayMode && waitingForNewInputAfterIdle)
                    {
                        overlayFadeElapsed += dt;
                    }
                }
                else
                {
                    // 入力が「新しく始まった瞬間」だけリセット
                    if (!wasInput && waitingForNewInputAfterIdle)
                    {
                        trail.Clear();
                        InputLogList.Clear();
                        waitingForNewInputAfterIdle = false;
                        overlayFadeElapsed = 0f;

                        currentTime = 0f;
                        FrameCount = 0;
                        currentX = 0f;
                        currentY = 0f;
                        trail.Add(new TrailPoint
                        {
                            X = 0f, Y = 0f, Time = 0f,
                            JumpLine = jump,
                            Jump = jump,
                            Throw = currentThrow,
                            Grab = currentGrab,
                            Special = currentSpecial,
                            FastRoll = currentFastRoll
                        });
                        prevJump = jump;
                        prevThrow = currentThrow;
                        prevGrab = currentGrab;
                        prevSpecial = currentSpecial;
                        prevFastRoll = currentFastRoll;

                        residueInput = false;  // 2026/05/17 Add

                    }

                    idleTime = 0f;
                }

                wasInput = anyInput;

                // 入力切り替え検出処理 ------->

                uint InputState = 0x00;

                if (InputLogEnabled)
                {
                    //0b 0000 0000
                    if (left && right)
                    {
                        //InputState &= 0xFC;     //0b XXXX XX00
                    }
                    else if (left)
                    {
                        InputState |= 0x01;     //0b 0000 0001
                    }
                    else if (right)
                    {
                        InputState |= 0x02;     //0b 0000 0010
                    }


                    if (up && down)
                    {
                        InputState &= 0xF3;     //0b XXXX 00XX
                    }
                    else if (up)
                    {
                        InputState |= 0x04;     //0b 0000 0100
                    }
                    else if (down)
                    {
                        InputState |= 0x08;     //0b 0000 1000
                    }

                    if (jump) { InputState |= 0x10; }               //0b 0001 0000
                    if (currentThrow) { InputState |= 0x20; }       //0b 0010 0000
                    if (currentGrab) { InputState |= 0x40; }        //0b 0100 0000
                    if (currentSpecial) { InputState |= 0x80; }     //0b 1000 0000
                    if (currentFastRoll) { InputState |= 0x100; }   //0b 0001 0000 0000

                    //Debug.WriteLine("currentTime: " + currentTime + " : " + InputState.ToString("X2"));

                    //InputState = 0x

                    //Debug.WriteLine("currentTime: " + currentTime);

                    if (prevInputState != InputState)
                    {
                        //Debug.WriteLine("currentTime: " + currentTime + " : " + InputState.ToString("X2"));

                        InputLogList.Add(new InputLogPoint
                        {
                            //frame = ConvertFrame(currentTime),
                            frame = FrameCount,
                            LR = (InputState & 0x03) == 0x01 ? 1 : (InputState & 0x03) == 0x02 ? 2 : 0,
                            UD = (InputState & 0x0C) == 0x04 ? 1 : (InputState & 0x0C) == 0x08 ? 2 : 0,
                            J = (InputState & 0x10) == 0x10 ? 1 : 0,
                            T = (InputState & 0x20) == 0x20 ? 1 : 0,
                            G = (InputState & 0x40) == 0x40 ? 1 : 0
                        });
                    }
                }

                prevInputState = InputState;

                // 入力切り替え検出処理 -------<

                // ===== ターゲット値 =====
                targetX = (right ? 1 : 0) - (left ? 1 : 0);
                targetY = (up ? 1 : 0) - (down ? 1 : 0);

                // 正規化（斜め補正）
                float targetLen = (float)Math.Sqrt(targetX * targetX + targetY * targetY);
                if (targetLen > 1f)
                {
                    targetX /= targetLen;
                    targetY /= targetLen;
                }

                // ===== 一定速度移動 =====
                float dx = targetX - currentX;
                float dy = targetY - currentY;

                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist > 0.0001f)
                {
                    float speed = 1f / responseTime;
                    float move = speed * dt;

                    if (move > dist)
                        move = dist;

                    currentX += dx / dist * move;
                    currentY += dy / dist * move;
                }

                // ===== 軌跡追加 =====
                if (trail.Count == 0)
                {
                    // 0F目の入力からボタン入力の記録を開始できるよう修正

                    trail.Add(new TrailPoint
                    {
                        X = currentX,
                        Y = currentY,
                        Time = currentTime,
                        //Jump = jump   //REP
                        JumpLine = jump,
                        Jump = (jump),
                        Throw = (currentThrow),
                        Grab = (currentGrab),
                        Special = (currentSpecial),
                        FastRoll = (currentFastRoll)
                    });

                }
                else
                {
                    var last = trail[trail.Count - 1];
                    //float dx = currentX - last.X;
                    //float dy = currentY - last.Y;
                    dx = currentX - last.X;
                    dy = currentY - last.Y;

                    // 位置が変わった or ボタン状態が変わったら追加
                    if (dx * dx + dy * dy > 0.0002f || jump != prevJump
                        || (currentThrow && !prevThrow) || (currentGrab && !prevGrab) || (currentSpecial && !prevSpecial)
                        || (currentFastRoll && !prevFastRoll))
                    {
                        trail.Add(new TrailPoint
                        {
                            X = currentX,
                            Y = currentY,
                            Time = currentTime,
                            //Jump = jump   //REP
                            JumpLine = jump,
                            Jump = (jump && !prevJump),
                            Throw = (currentThrow && !prevThrow),
                            Grab = (currentGrab && !prevGrab),
                            Special = (currentSpecial && !prevSpecial),
                            FastRoll = (currentFastRoll && !prevFastRoll)
                        });
                    }
                }

                prevJump = jump;
                prevThrow = currentThrow;
                prevGrab = currentGrab;
                prevSpecial = currentSpecial;
                prevFastRoll = currentFastRoll;

                if (!overlayMode)
                {
                    // ===== スクリーンショット処理 =====

                    bool screenshot = false;

                    int i_No;

                    for (i_No = 0; i_No < 3; i_No++)
                    {
                        switch (i_No)
                        {
                            case 0:
                                screenshot = GetKey(keyPerfectScreenshot);
                                break;

                            case 1:
                                screenshot = GetKey(keyGoodScreenshot);
                                break;

                            case 2:
                                screenshot = GetKey(keyFailedScreenshot);
                                break;
                        }

                        if (screenshot) break;

                    }

                    if (screenshot && !prevScreenshot && SavedLeaved == 0)
                    {
                        this.Invoke((MethodInvoker)(() =>
                        {
                            SaveScreenshot(i_No);
                        }));
                    }

                    prevScreenshot = screenshot;

                    // Input Log
                    if (InputLog && !prevInputLog && InputLogReload == 0)
                    {
                        InputLogEnabled = !InputLogEnabled;
                        InputLogReload = 20;

                        if (InputLogEnabled)
                        {
                            this.Invoke((MethodInvoker)(() =>
                            {
                                this.Width += changeWidth;
                            }));
                        }
                        else
                        {
                            this.Invoke((MethodInvoker)(() =>
                            {
                                this.Width -= changeWidth;
                            }));
                        }
                    }

                    prevInputLog = InputLog;
                }


                // 終了処理
                bool exit = GetKey(keyExit);

                if (exit && !prevExit)
                {
                    Application.Exit();
                }

                prevExit = exit;

                // 上限制限
                int activeTrailMaxCount = overlayMode ? overlayTrailMaxCount : trailMaxCount;
                if (trail.Count > activeTrailMaxCount)
                {
                    trail.RemoveAt(0);
                    residueInput = true;  // 2026/05/17 Add
                }

                if (InputLogList.Count > SaveLogCount) InputLogList.RemoveAt(0);

                // オーバーレイ: 経過時間による古い軌跡点の削除
                if (overlayMode && overlayTrailDuration > 0f)
                {
                    // 2026/05/17 REP
                    //while (trail.Count > 0 && currentTime - trail[0].Time > overlayTrailDuration)
                    if (trail.Count >= 2)
                    {
                        while (trail.Count > 0 && currentTime - trail[1].Time > overlayTrailDuration)
                            trail.RemoveAt(0);
                    }
                    else
                    {
                        while (trail.Count > 0 && currentTime - trail[0].Time > overlayTrailDuration)
                            trail.RemoveAt(0);
                    }
                }

                if (overlayMode)
                {
                    if (this.IsHandleCreated && !renderPending)
                    {
                        renderPending = true;
                        this.BeginInvoke((MethodInvoker)(() =>
                        {
                            RenderOverlay();
                            renderPending = false;
                        }));
                    }
                }
                else
                {
                    Invalidate();
                }

                //Debug.WriteLine("★trailCount : " + trail.Count);

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

        }

        /*
        int ConvertFrame(float frame)
        {
            if (frame <= 0f) return 0;
            return (int)(Math.Ceiling(frame * 100f) / 10);
        }
        */

        int ConvertFrame(float frame)
        {
            return (int)(Math.Floor(frame * 100f));
        }

        //string str_tmp_Paint = string.Empty;

        string GetButtonName(int KeyNo)
        {
            string str_ReturnValue = string.Empty;

            switch (KeyNo)
            {
                case 1:
                    // A
                    str_ReturnValue = "A";
                    break;

                case 2:
                    // B
                    str_ReturnValue = "B";
                    break;

                case 3:
                    // X
                    str_ReturnValue = "X";
                    break;

                case 4:
                    // Y
                    str_ReturnValue = "Y";
                    break;

                case 5:
                    // RB
                    str_ReturnValue = "RB";
                    break;

                case 6:
                    // LB
                    str_ReturnValue = "LB";
                    break;

                case 7:
                    // RT
                    str_ReturnValue = "RT";
                    break;

                case 8:
                    // LT
                    str_ReturnValue = "LT";
                    break;

                default:
                    str_ReturnValue = "None";
                    break;
            }

            return str_ReturnValue;

        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (overlayMode)
                    cp.ExStyle |= 0x80000; // WS_EX_LAYERED（UpdateLayeredWindow を使うために必要）
                return cp;
            }
        }

        void DrawButtonCell(Graphics g, int x, int y, int size, bool pressed, string label, int alpha = 255)
        {
            Color bg = pressed ? Color.FromArgb(alpha, 185, 185, 185) : Color.FromArgb(alpha, 58, 58, 58);
            Color fg = pressed ? Color.FromArgb(alpha, 58, 58, 58)  : Color.FromArgb(alpha, 210, 210, 210);
            using (SolidBrush bgBrush = new SolidBrush(bg))
                g.FillRectangle(bgBrush, x, y, size, size);
            using (SolidBrush borderBrush = new SolidBrush(Color.FromArgb(alpha, overlayCircleR, overlayCircleG, overlayCircleB)))
            {
                int bw = 3;
                g.FillRectangle(borderBrush, x,           y,            size, bw);    // 上辺
                g.FillRectangle(borderBrush, x,           y + size-bw,  size, bw);    // 下辺
                g.FillRectangle(borderBrush, x,           y,            bw,   size);  // 左辺
                g.FillRectangle(borderBrush, x + size-bw, y,            bw,   size);  // 右辺
            }
            float fontSize = size * 0.45f;
            using (Font f = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush fgBrush = new SolidBrush(fg))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(label, f, fgBrush, new RectangleF(x, y, size, size), sf);
            }
        }

        void RenderOverlay()
        {
            try
            {
                // 描画は常にフル解像度で行う（スケール後のウィンドウサイズとは独立）
                int buttonAreaWidth = overlayButtonDisplay ? 5 + overlayButtonCellSize * 5 + 4 * 2 : 0;
                int w = 210 + buttonAreaWidth;
                int h = 210;
                int centerX = 105; // 円エリアは常に210px固定
                int centerY = h / 2;
                int radius = 90;

                using (Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                        // 全体を透明にクリア（円外はこのまま完全透過になる）
                        g.Clear(Color.Transparent);

                        // 円内を設定色・設定透過度で塗りつぶし
                        int fillAlpha = overlayFillOpacity * 255 / 100;
                        using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(fillAlpha, overlayFillR, overlayFillG, overlayFillB)))
                        {
                            g.FillEllipse(fillBrush, centerX - radius, centerY - radius, radius * 2, radius * 2);
                        }

                        // 外円（fillAlpha を反映）← 軌跡より先に描画することで軌跡が上に来る
                        using (Pen circlePen = new Pen(Color.FromArgb(fillAlpha, overlayCircleR, overlayCircleG, overlayCircleB), overlayCircleWidth))
                            g.DrawEllipse(circlePen, centerX - radius, centerY - radius, radius * 2, radius * 2);

                        // フェードアルファ計算（オーバーレイアイドル時）
                        int fadeA = 255;
                        if (waitingForNewInputAfterIdle)
                        {
                            if (overlayTrailFadeTime <= 0f)
                            {
                                fadeA = 0; // 即時消去
                            }
                            else
                            {
                                float ratio = 1.0f - overlayFadeElapsed / overlayTrailFadeTime;
                                fadeA = (int)(Math.Max(0f, Math.Min(1f, ratio)) * 255);
                            }
                        }
                        // fillAlpha と合成（線・点に OverlayFillOpacity を反映）
                        int trailA = fadeA * fillAlpha / 255;

                        // 現在位置（ボタン点より後ろに描画・フェード対象）
                        if (trailA > 0)
                        {
                            float drawX = centerX + currentX * radius;
                            float drawY = centerY - currentY * radius;
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 255, 255)))
                                g.FillEllipse(b, drawX - CursorPointSize / 2f, drawY - CursorPointSize / 2f, CursorPointSize, CursorPointSize);
                        }

                        // 中央点（外周円と同色・fillAlpha を反映）
                        using (SolidBrush centerBrush = new SolidBrush(Color.FromArgb(fillAlpha, overlayCircleR, overlayCircleG, overlayCircleB)))
                            g.FillEllipse(centerBrush, centerX - 4.5f, centerY - 4.5f, 9, 9);

                        // ボタン表示グリッド（円内と同じ透過度）
                        if (overlayButtonDisplay)
                        {
                            int cs   = overlayButtonCellSize;
                            int gap  = 2;
                            int step = cs + gap;
                            int gx   = 215;
                            int gy   = (h - 2 * cs - gap) / 2;

                            int gridW  = 5 * cs + 4 * gap;
                            int row0X  = gx + step;           // col1始点
                            int row0W  = 4 * cs + 3 * gap;    // col1〜col4

                            // Row 0: ▲(col1)  F(col2)  G(col3)  S(col4)
                            DrawButtonCell(g, gx + 1 * step, gy,        cs, up,              "⯅", fillAlpha);
                            DrawButtonCell(g, gx + 2 * step, gy,        cs, currentFastRoll, "R",  fillAlpha);
                            DrawButtonCell(g, gx + 3 * step, gy,        cs, currentGrab,     "G",  fillAlpha);
                            DrawButtonCell(g, gx + 4 * step, gy,        cs, currentSpecial,  "S",  fillAlpha);

                            // Row 1: ◄(col0)  ▼(col1)  ►(col2)  J(col3)  T(col4)
                            DrawButtonCell(g, gx + 0 * step, gy + step, cs, left,           "⯇", fillAlpha);    //◀▶◀▶◄►▼▲▼▲▴▾⯅⯆⯇⯈
                            DrawButtonCell(g, gx + 1 * step, gy + step, cs, down,           "⯆", fillAlpha);
                            DrawButtonCell(g, gx + 2 * step, gy + step, cs, right,          "⯈", fillAlpha);
                            DrawButtonCell(g, gx + 3 * step, gy + step, cs, jump,           "J", fillAlpha);
                            DrawButtonCell(g, gx + 4 * step, gy + step, cs, currentThrow,   "T", fillAlpha);
                        }

                        // 軌跡・ボタン点（フェード対象）
                        if (trailA > 0)
                        {

                            // Pass 1: 軌跡線
                            for (int i = 1; i < trail.Count; i++)
                            {
                                TrailPoint pt1 = trail[i - 1];
                                TrailPoint pt2 = trail[i];
                                Color c2 = pt2.JumpLine ? Color.FromArgb(trailA, 255, 0, 0) : Color.FromArgb(trailA, 255, 255, 255);
                                using (Pen pen = new Pen(c2, 2))
                                    g.DrawLine(pen,
                                        centerX + pt1.X * radius, centerY - pt1.Y * radius,
                                        centerX + pt2.X * radius, centerY - pt2.Y * radius);
                            }

                            // Pass 2: ボタン点（線より上に描画）
                            // 先頭点（0F）: 自身のボタン状態で描画
                            // 2026/05/17 REP
                            if (trail.Count > 0 && residueInput == false)
                            {
                                TrailPoint pt0 = trail[0];
                                float px0 = centerX + pt0.X * radius;
                                float py0 = centerY - pt0.Y * radius;
                                if (pt0.Jump)    { int size = JumpPointSize;    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 0, 0)))   g.FillEllipse(b, px0 - size / 2f, py0 - size / 2f, size, size); }
                                if (pt0.Throw)   { int size = ThrowPointSize;   using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 0, 128, 0)))   g.FillEllipse(b, px0 - size / 2f, py0 - size / 2f, size, size); }
                                if (pt0.Grab)    { int size = GrabPointSize;    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 255, 0))) g.FillEllipse(b, px0 - size / 2f, py0 - size / 2f, size, size); }
                                if (pt0.Special) { int size = SpecialPointSize; using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 128, 0, 128))) g.FillEllipse(b, px0 - size / 2f, py0 - size / 2f, size, size); }
                                if (pt0.FastRoll){ int size = FastRollPointSize;using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 165, 0))) g.FillEllipse(b, px0 - size / 2f, py0 - size / 2f, size, size); }
                            }
                            // 中間: trail[i-1]の座標にtrail[i]のボタン状態（〇-）
                            for (int i = 1; i < trail.Count; i++)
                            {
                                TrailPoint pt  = trail[i - 1];
                                TrailPoint btn = trail[i];
                                float px = centerX + pt.X * radius;
                                float py = centerY - pt.Y * radius;
                                if (btn.Jump)
                                {
                                    int size = JumpPointSize;
                                    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 0, 0)))
                                        g.FillEllipse(b, px - size / 2f, py - size / 2f, size, size);
                                }
                                if (btn.Throw)
                                {
                                    int size = ThrowPointSize;
                                    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 0, 128, 0)))
                                        g.FillEllipse(b, px - size / 2f, py - size / 2f, size, size);
                                }
                                if (btn.Grab)
                                {
                                    int size = GrabPointSize;
                                    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 255, 0)))
                                        g.FillEllipse(b, px - size / 2f, py - size / 2f, size, size);
                                }
                                if (btn.Special)
                                {
                                    int size = SpecialPointSize;
                                    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 128, 0, 128)))
                                        g.FillEllipse(b, px - size / 2f, py - size / 2f, size, size);
                                }
                                if (btn.FastRoll)
                                {
                                    int size = FastRollPointSize;
                                    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 165, 0)))
                                        g.FillEllipse(b, px - size / 2f, py - size / 2f, size, size);
                                }
                            }
                            // 末尾点（最新）: 自身のボタン状態で描画
                            if (trail.Count > 1)
                            {
                                TrailPoint ptL = trail[trail.Count - 1];
                                float pxL = centerX + ptL.X * radius;
                                float pyL = centerY - ptL.Y * radius;
                                if (ptL.Jump)    { int size = JumpPointSize;    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 0, 0)))   g.FillEllipse(b, pxL - size / 2f, pyL - size / 2f, size, size); }
                                if (ptL.Throw)   { int size = ThrowPointSize;   using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 0, 128, 0)))   g.FillEllipse(b, pxL - size / 2f, pyL - size / 2f, size, size); }
                                if (ptL.Grab)    { int size = GrabPointSize;    using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 255, 0))) g.FillEllipse(b, pxL - size / 2f, pyL - size / 2f, size, size); }
                                if (ptL.Special) { int size = SpecialPointSize; using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 128, 0, 128))) g.FillEllipse(b, pxL - size / 2f, pyL - size / 2f, size, size); }
                                if (ptL.FastRoll){ int size = FastRollPointSize;using (SolidBrush b = new SolidBrush(Color.FromArgb(trailA, 255, 165, 0))) g.FillEllipse(b, pxL - size / 2f, pyL - size / 2f, size, size); }
                            }



                        }

                    }

                    // UpdateLayeredWindow でデスクトップに対してピクセル単位でアルファ合成
                    // スケール != 1.0 の場合はフル解像度ビットマップを縮小してから渡す
                    // fw/fh はウィンドウサイズ（LoadConfig時にスケール済み）を使う
                    int fw = Width;
                    int fh = Height;
                    Bitmap bmpFinal = bmp;
                    if (overlayScale < 1.0f)
                    {
                        bmpFinal = new Bitmap(fw, fh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (Graphics sg = Graphics.FromImage(bmpFinal))
                        {
                            sg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            sg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            sg.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                            sg.DrawImage(bmp, 0, 0, fw, fh);
                        }
                    }

                    IntPtr screenDC = GetDC(IntPtr.Zero);
                    IntPtr memDC = CreateCompatibleDC(screenDC);
                    IntPtr hBitmap = IntPtr.Zero;
                    IntPtr oldBitmap = IntPtr.Zero;

                    try
                    {
                        hBitmap = bmpFinal.GetHbitmap(Color.FromArgb(0));
                        oldBitmap = SelectObject(memDC, hBitmap);

                        Size size = new Size(fw, fh);
                        Point pointSource = new Point(0, 0);
                        Point topPos = new Point(this.Left, this.Top);
                        BLENDFUNCTION blend = new BLENDFUNCTION
                        {
                            BlendOp = AC_SRC_OVER,
                            BlendFlags = 0,
                            SourceConstantAlpha = 255,
                            AlphaFormat = AC_SRC_ALPHA
                        };

                        UpdateLayeredWindow(this.Handle, screenDC, ref topPos, ref size, memDC, ref pointSource, 0, ref blend, ULW_ALPHA);
                    }
                    finally
                    {
                        ReleaseDC(IntPtr.Zero, screenDC);
                        if (hBitmap != IntPtr.Zero)
                        {
                            SelectObject(memDC, oldBitmap);
                            DeleteObject(hBitmap);
                        }
                        DeleteDC(memDC);
                        if (bmpFinal != bmp) bmpFinal.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                if (overlayMode) return; // オーバーレイ時は RenderOverlay() が担当
                base.OnPaint(e);

                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                int centerX = 0;
                int centerY = 0;
                int radius = 90;

                if (!InputLogEnabled)
                {
                    centerX = (ClientSize.Width) / 2;  // Suzume 円の位置補正
                    centerY = ClientSize.Height / 2;
                }
                else
                {
                    centerX = ((ClientSize.Width) - changeWidth) / 2;  // Suzume 円の位置補正
                    centerY = ClientSize.Height / 2;
                }

                // 外円
                using (Pen circlePen = new Pen(Color.White, overlayMode ? overlayCircleWidth : 1))
                {
                    g.DrawEllipse(circlePen, centerX - radius, centerY - radius, radius * 2, radius * 2);
                }

                // 現在位置（ボタン点より後ろに描画）
                {
                    float drawX = centerX + currentX * radius;
                    float drawY = centerY - currentY * radius;
                    g.FillEllipse(Brushes.White, drawX - CursorPointSize / 2f, drawY - CursorPointSize / 2f, CursorPointSize, CursorPointSize);
                }

                // 中央点（外周円と同色）
                using (SolidBrush centerBrush = new SolidBrush(Color.FromArgb(255, overlayCircleR, overlayCircleG, overlayCircleB)))
                    g.FillEllipse(centerBrush, centerX - 4.5f, centerY - 4.5f, 9, 9);

                // Pass 1: 軌跡線
                for (int i = 1; i < trail.Count; i++)
                {
                    TrailPoint p1 = trail[i - 1];
                    TrailPoint p2 = trail[i];
                    Color color2 = p2.JumpLine ? Color.Red : Color.White;
                    using (Pen pen = new Pen(color2, 2))
                    {
                        float x1 = centerX + p1.X * radius;
                        float y1 = centerY - p1.Y * radius;
                        float x2 = centerX + p2.X * radius;
                        float y2 = centerY - p2.Y * radius;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }

                // Pass 2: ボタン点（線より上に描画）
                // 先頭点（0F）: 自身のボタン状態で描画
                // 2026/05/17 REP
                if (trail.Count > 0 && residueInput == false)
                {
                    TrailPoint pt0 = trail[0];
                    float px0 = centerX + pt0.X * radius;
                    float py0 = centerY - pt0.Y * radius;
                    if (pt0.Jump)     g.FillEllipse(Brushes.Red,    px0 - JumpPointSize / 2,     py0 - JumpPointSize / 2,     JumpPointSize,     JumpPointSize);
                    if (pt0.Throw)    g.FillEllipse(Brushes.Green,  px0 - ThrowPointSize / 2,    py0 - ThrowPointSize / 2,    ThrowPointSize,    ThrowPointSize);
                    if (pt0.Grab)     g.FillEllipse(Brushes.Yellow, px0 - GrabPointSize / 2,     py0 - GrabPointSize / 2,     GrabPointSize,     GrabPointSize);
                    if (pt0.Special)  using (SolidBrush b = new SolidBrush(Color.FromArgb(128, 0, 128)))  g.FillEllipse(b, px0 - SpecialPointSize / 2,  py0 - SpecialPointSize / 2,  SpecialPointSize,  SpecialPointSize);
                    if (pt0.FastRoll) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 165, 0)))  g.FillEllipse(b, px0 - FastRollPointSize / 2, py0 - FastRollPointSize / 2, FastRollPointSize, FastRollPointSize);
                }

                // 中間: trail[i-1]の座標にtrail[i]のボタン状態（〇-）
                for (int i = 1; i < trail.Count; i++)
                {
                    TrailPoint p   = trail[i - 1];
                    TrailPoint btn = trail[i];
                    float px = centerX + p.X * radius;
                    float py = centerY - p.Y * radius;

                    if (btn.Jump)
                        g.FillEllipse(Brushes.Red, px - JumpPointSize / 2, py - JumpPointSize / 2, JumpPointSize, JumpPointSize);
                    if (btn.Throw)
                        g.FillEllipse(Brushes.Green, px - ThrowPointSize / 2, py - ThrowPointSize / 2, ThrowPointSize, ThrowPointSize);
                    if (btn.Grab)
                        g.FillEllipse(Brushes.Yellow, px - GrabPointSize / 2, py - GrabPointSize / 2, GrabPointSize, GrabPointSize);
                    if (btn.Special)
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(128, 0, 128)))
                            g.FillEllipse(b, px - SpecialPointSize / 2, py - SpecialPointSize / 2, SpecialPointSize, SpecialPointSize);
                    if (btn.FastRoll)
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 165, 0)))
                            g.FillEllipse(b, px - FastRollPointSize / 2, py - FastRollPointSize / 2, FastRollPointSize, FastRollPointSize);
                }

                // 末尾点（最新）: 自身のボタン状態で描画
                if (trail.Count > 1)
                {
                    TrailPoint ptL = trail[trail.Count - 1];
                    float pxL = centerX + ptL.X * radius;
                    float pyL = centerY - ptL.Y * radius;
                    if (ptL.Jump)     g.FillEllipse(Brushes.Red,    pxL - JumpPointSize / 2,     pyL - JumpPointSize / 2,     JumpPointSize,     JumpPointSize);
                    if (ptL.Throw)    g.FillEllipse(Brushes.Green,  pxL - ThrowPointSize / 2,    pyL - ThrowPointSize / 2,    ThrowPointSize,    ThrowPointSize);
                    if (ptL.Grab)     g.FillEllipse(Brushes.Yellow, pxL - GrabPointSize / 2,     pyL - GrabPointSize / 2,     GrabPointSize,     GrabPointSize);
                    if (ptL.Special)  using (SolidBrush b = new SolidBrush(Color.FromArgb(128, 0, 128)))  g.FillEllipse(b, pxL - SpecialPointSize / 2,  pyL - SpecialPointSize / 2,  SpecialPointSize,  SpecialPointSize);
                    if (ptL.FastRoll) using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 165, 0)))  g.FillEllipse(b, pxL - FastRollPointSize / 2, pyL - FastRollPointSize / 2, FastRollPointSize, FastRollPointSize);
                }

                if (!overlayMode)
                {
                    // 情報

                    if (DeviceType == 1)
                    {
                        g.DrawString(
                            $"Move : Arrow keys",
                            Font,
                            Brushes.White,
                            10,
                            10
                        );

                        g.DrawString(
                            $"Controls  " + " Jump : " + GetButtonName(GamePadJump) + "  Throw : " + GetButtonName(GamePadThrow) + "  Grab : " + GetButtonName(GamePadGrab) ,
                            Font,
                            Brushes.White,
                            10,
                            23
                        );

                        g.DrawString(
                            $"               Special : " + GetButtonName(GamePadSpecial) + "  FastRoll : " + GetButtonName(GamePadFastRoll),
                            Font,
                            Brushes.White,
                            10,
                            37
                         );
                    }
                    else
                    {
                        g.DrawString(
                            $"Move : {keyUp} {keyLeft} {keyDown} {keyRight}",
                            Font,
                            Brushes.White,
                            10,
                            10
                        );

                        g.DrawString(
                            $"Controls   Jump : {keyJump}   Throw : {keyThrow}   Grab : {keyGrab}",
                            Font,
                            Brushes.White,
                            10,
                            23
                            );

                        g.DrawString(
                             $"               Special : {keySpecial}   FastRoll : {keyFastRoll}",
                            Font,
                            Brushes.White,
                            10,
                            37
                         );
                    }

                    Brush idleBrush = (idleTime >= idleResetTime) ? Brushes.Red : Brushes.White;
                    g.DrawString($"Input grace period : {idleResetTime:0.0} ms", Font, idleBrush, 10, 55);

                    g.DrawString($"Trail speed : {responseTime:0.00}", Font, Brushes.White, 10, 70);

                    if (GamePad_Enabled || DeviceType == 0)
                    {
                        g.DrawString($"Timer : {updateTime:0.0#} ms", Font, Brushes.White, 10, 85);
                    }
                    else if (GamePad_Enabled == false)
                    {
                        g.DrawString($"GamePad is not Found", Font, Brushes.Red, 10, 85);
                    }
                    else
                    {
                        g.DrawString($"Device Error", Font, Brushes.Red, 10, 85);
                    }

                    // 2026/05/23 REP arrow keysに対応のため、表記を単純化（文字がはみ出ちゃうから）
                    /*
                    g.DrawString($"{keyUp}", Font, Brushes.White, 157, 56);
                    g.DrawString($"{keyLeft}", Font, Brushes.White, 58, 155);
                    g.DrawString($"{keyRight}", Font, Brushes.White, 255, 155);
                    g.DrawString($"{keyDown}", Font, Brushes.White, 157, 254);
                    */
                    g.DrawString($"⯅", Font, Brushes.White, 155, 58);
                    g.DrawString($"⯇", Font, Brushes.White, 58, 155);
                    g.DrawString($"⯈", Font, Brushes.White, 253, 155);
                    g.DrawString($"⯆", Font, Brushes.White, 155, 253);

                    g.DrawString($"ScreenShot", Font, Brushes.White, 10, 235);

                    g.DrawString($"Perfect", Font, Brushes.White, 15, 250);
                    g.DrawString($": [{keyPerfectScreenshot}]", Font, Brushes.White, 60, 250);

                    g.DrawString($"Good ", Font, Brushes.White, 15, 265);
                    g.DrawString($": [{keyGoodScreenshot}]", Font, Brushes.White, 60, 265);

                    g.DrawString($"Failed", Font, Brushes.White, 15, 280);
                    g.DrawString($": [{keyFailedScreenshot}]", Font, Brushes.White, 60, 280);


                    g.DrawLine(WriteLinePen, 0, 0, this.Width, 0);

                    g.DrawLine(WriteLinePen, 0, 0, 0, this.Height);

                    g.DrawLine(WriteLinePen, 0, this.Height - 1, this.Width, this.Height - 1);

                    g.DrawLine(WriteLinePen, this.Width - 1, 0, this.Width - 1, this.Height);



                    if (!InputLogEnabled)
                    {
                        g.DrawString($"Show Input Log : [{keyInutLog}]", Font, Brushes.White, 10, 300);
                    }
                    else
                    {
                        g.DrawString($"Hide Input Log : [{keyInutLog}]", Font, Brushes.White, 10, 300);
                        g.DrawLine(WriteLinePen, 320, 1, 320, this.Height);

                        string str_tmp_Paint = String.Empty;

                        for (int LogNo = 0; LogNo < InputLogList.Count; LogNo++)
                        {
                            g.DrawString(InputLogList[LogNo].frame.ToString("D3") + " ::", InputLogFont, Brushes.White, InputLocateX, InputLocateY + InputDistanceY * LogNo);

                            str_tmp_Paint = InputLogList[LogNo].LR == 1 ? "L" : InputLogList[LogNo].LR == 2 ? "R" : "";
                            g.DrawString(str_tmp_Paint, InputLogFont, Brushes.White, InputLocateX + 40, InputLocateY + InputDistanceY * LogNo);

                            str_tmp_Paint = InputLogList[LogNo].UD == 1 ? "U" : InputLogList[LogNo].UD == 2 ? "D" : "";
                            g.DrawString(str_tmp_Paint, InputLogFont, Brushes.White, InputLocateX + 40 + 1 * 10, InputLocateY + InputDistanceY * LogNo);

                            str_tmp_Paint = InputLogList[LogNo].J == 1 ? "J" : "";
                            g.DrawString(str_tmp_Paint, InputLogFont, Brushes.White, InputLocateX + 40 + 2 * 10, InputLocateY + InputDistanceY * LogNo);

                            str_tmp_Paint = InputLogList[LogNo].T == 1 ? "T" : "";
                            g.DrawString(str_tmp_Paint, InputLogFont, Brushes.White, InputLocateX + 40 + 3 * 10, InputLocateY + InputDistanceY * LogNo);

                            str_tmp_Paint = InputLogList[LogNo].G == 1 ? "G" : "";
                            g.DrawString(str_tmp_Paint, InputLogFont, Brushes.White, InputLocateX + 40 + 4 * 10, InputLocateY + InputDistanceY * LogNo);
                        }
                    }

                    g.DrawString($"App Exit :", Font, Brushes.White, 240, 285);
                    g.DrawString($"[{keyExit}]", Font, Brushes.White, 240, 300);

                    if (SavedLeaved > 0)
                    {
                        int yPos = 0;

                        if (SavedNo == 0)
                        {
                            yPos = 250;
                        }
                        else if (SavedNo == 1)
                        {
                            yPos = 265;
                        }
                        else if (SavedNo == 2)
                        {
                            yPos = 280;
                        }

                        g.DrawString($"Saved!", Font, Brushes.White, 90, yPos);
                        SavedLeaved--;
                    }

                    if (InputLogReload > 0)
                    {
                        InputLogReload--;
                    }

                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        void PlayCameraSound()
        {
            try
            {
                if (cameraPlayer == null)
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    using (var res = asm.GetManifestResourceStream("SuzumeInputViewer.camera.wav"))
                    {
                        if (res == null) return;
                        var ms = new MemoryStream();
                        res.CopyTo(ms);
                        ms.Position = 0;
                        cameraPlayer = new SoundPlayer(ms);
                    }
                    cameraPlayer.Load(); // 事前ロード（遅延防止）
                }

                cameraPlayer.Play(); // 非同期再生
            }
            catch
            {
                // 無音でスルー（クラッシュ防止）
            }
        }

        void SaveScreenshot(int i_No)
        {
            try
            {
                // 保存フォルダ
                string folder = Path.Combine(Application.StartupPath, "screenshot");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                // ファイル名（yyyyMMdd_HHmmss.png）
                string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (i_No == 0)
                {
                    fileName += "_Perfect";
                }
                else if (i_No == 1)
                {
                    fileName += "_Good";
                }
                else if (i_No == 2)
                {
                    fileName += "_Failed";
                }

                fileName += ".png";

                string path = Path.Combine(folder, fileName);

                // フォーム全体をキャプチャ
                using (Bitmap bmp = new Bitmap(this.Width, this.Height))
                {
                    this.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(path, ImageFormat.Png);
                    PlayCameraSound();

                    SavedNo = i_No;
                    SavedLeaved = 100;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
