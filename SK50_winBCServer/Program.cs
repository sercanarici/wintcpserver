using System;
using System.Threading;
using System.Windows.Forms;

namespace winTCPServer
{
    static class Program
    {
        private const string MutexName = "PC88-FiyatGor-TCPServer";

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Yakalanmamış hatalar önceden log'a hiç düşmeden uygulamayı kapatıyordu.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Fiyatgör TCP Server zaten çalışıyor."
                        + Environment.NewLine + Environment.NewLine
                        + "Saatin yanındaki sistem tepsisi simgelerini veya Görev Yöneticisi'ni kontrol edin.",
                        "Fiyatgör TCP Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.Run(new frmMain());
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Logger.Write("UI thread hatası: " + e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Write("Yakalanmamış hata (uygulama kapanıyor): " + e.ExceptionObject);
        }
    }
}
