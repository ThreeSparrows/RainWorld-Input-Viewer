using System;
using System.Threading;
using System.Windows.Forms;

namespace ConfigrationTool
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "RainWorldInputViewer_ConfigrationTool", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "既に起動しています。\r\n(Already running.)",
                        "多重起動(Already Running)",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // TODO: 地域設定変更 - テスト用。確認後に削除すること！
                //System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("it-IT");

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
