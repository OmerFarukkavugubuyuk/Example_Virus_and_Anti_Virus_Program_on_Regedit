// =============================================================================
// Program.cs — Uygulamanın giriş noktası
// Yönetici yetkisiyle çalışmak için app.manifest gereklidir (aşağıda belirtildi).
// =============================================================================

using System;
using System.Windows.Forms;

namespace RegShield
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Uygulama genelinde Windows görsel stillerini etkinleştir
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Ana formu başlat
            Application.Run(new MainForm());
        }
    }
}
