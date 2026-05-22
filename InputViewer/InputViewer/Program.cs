using System;
using System.Threading;
using System.Windows.Forms;

namespace SuzumeInputViewer
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのエントリポイント
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;

            using (Mutex mutex = new Mutex(true, "Suzume_6741876", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}