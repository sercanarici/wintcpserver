using System;
using System.IO;

namespace winTCPServer
{
    /// <summary>
    /// Thread-safe log.txt yazıcısı. Soket thread'leri aynı anda yazabildiği için
    /// tüm yazımlar tek kilit altında yapılır; loglama hatası asla dışarı fırlatılmaz.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

        public static void Write(string logMessage)
        {
            try
            {
                lock (_lock)
                {
                    using (StreamWriter w = File.AppendText(_path))
                    {
                        w.Write("\r\nLog Entry : ");
                        w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
                        w.WriteLine($"  :{logMessage}");
                        w.WriteLine("-------------------------------");
                    }
                }
            }
            catch
            {
                // Loglama hatası uygulamayı düşürmemeli.
            }
        }
    }
}
