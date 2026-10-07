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
        private static readonly string _directory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string _path = Path.Combine(_directory, "log.txt");

        // log.txt bu boyuta ulaşınca log.1.txt'ye taşınır; en fazla MaxArchiveCount eski dosya tutulur.
        private const long MaxLogSize = 10 * 1024 * 1024;
        private const int MaxArchiveCount = 5;

        public static void Write(string logMessage)
        {
            try
            {
                lock (_lock)
                {
                    RotateIfNeeded();

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

        private static string ArchivePath(int index)
        {
            return Path.Combine(_directory, "log." + index + ".txt");
        }

        private static void RotateIfNeeded()
        {
            try
            {
                FileInfo current = new FileInfo(_path);
                if (!current.Exists || current.Length < MaxLogSize)
                {
                    return;
                }

                // log.4 -> log.5, ..., log.1 -> log.2, log.txt -> log.1 (en eskisi silinir)
                if (File.Exists(ArchivePath(MaxArchiveCount)))
                {
                    File.Delete(ArchivePath(MaxArchiveCount));
                }
                for (int i = MaxArchiveCount - 1; i >= 1; i--)
                {
                    if (File.Exists(ArchivePath(i)))
                    {
                        File.Move(ArchivePath(i), ArchivePath(i + 1));
                    }
                }
                File.Move(_path, ArchivePath(1));
            }
            catch
            {
                // Taşıma başarısız olursa (ör. dosya başka programda açık) mevcut dosyaya yazmaya devam edilir.
            }
        }
    }
}
