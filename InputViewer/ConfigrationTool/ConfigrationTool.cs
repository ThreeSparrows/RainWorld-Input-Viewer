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

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
