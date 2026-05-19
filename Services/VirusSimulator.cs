// =============================================================================
// Services/VirusSimulator.cs — Gerçek Virüs Simülasyonu Servisi
// =============================================================================
// Windows'un yerleşik C# derleyicisi (csc.exe) kullanılarak gerçek, çalışabilir
// ama tamamen zararsız bir .exe dosyası üretir.
//
// Oluşturulan EXE davranışı:
//   Bilgisayar her açıldığında bir MessageBox gösterir:
//   "⚠ UYARI: Bu bir RegShield test virüsüdür! Bilgisayarınız tehlikede!"
//
// Gerçek zararlı kod içermez. Sadece MessageBox.Show çağrısı yapar.
// =============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RegShield.Services
{
    public sealed class VirusSimulator
    {
        private readonly RegistryManager _registryManager;

        private const string TestExeName = "RegShield_TestVirus.exe";
        private const string TestRegName = "RegShield_TestVirus";

        public VirusSimulator(RegistryManager registryManager)
        {
            _registryManager = registryManager
                ?? throw new ArgumentNullException(nameof(registryManager));
        }

        public static string TestRegistryName => TestRegName;

        // ── Ana Metot ─────────────────────────────────────────────────────────

        /// <summary>
        /// Gerçek imzasız .exe derler ve Startup registry'e kaydeder.
        /// </summary>
        public async Task<string> CreateAndRegisterTestVirusAsync()
        {
            string exePath = Path.Combine(Path.GetTempPath(), TestExeName);

            // 1. Gerçek EXE derle
            await CompileVirusExeAsync(exePath).ConfigureAwait(false);

            // 2. Registry'e startup kaydi olarak ekle
            await _registryManager.WriteStartupEntryAsync(TestRegName, exePath)
                .ConfigureAwait(false);

            return exePath;
        }

        // ── EXE Derleme ───────────────────────────────────────────────────────

        /// <summary>
        /// Windows'un yerlesik csc.exe (C# Compiler) ile zararsiz bir .exe derler.
        /// csc.exe .NET Framework ile birlikte gelir, ekstra kurulum gerektirmez.
        /// </summary>
        private static async Task CompileVirusExeAsync(string outputPath)
        {
            // 1. Kaynak kodu gecici .cs dosyasina yaz
            string sourceFile = Path.Combine(Path.GetTempPath(), "RegShield_TestVirus.cs");

            // MessageBox gösteren basit zararsiz kaynak kod
            // Unicode escape kullanarak özel karakterleri encode ettik
            string sourceCode =
                "using System;\n" +
                "using System.Windows.Forms;\n" +
                "\n" +
                "class TestVirus\n" +
                "{\n" +
                "    [STAThread]\n" +
                "    static void Main()\n" +
                "    {\n" +
                "        MessageBox.Show(\n" +
                "            \"UYARI: ALLAH'IN (azze ve celle)DEDİĞİ OLUR!\\n\\n\" +\n" +
                "            \"İMANINI KAYBETME!\\n\" +\n" +
                "            \"RegShield Antivirus ile tarama yapin.\",\n" +
                "            \"RegShield TEST VIRUS - UYARI\",\n" +
                "            MessageBoxButtons.OK,\n" +
                "            MessageBoxIcon.Warning\n" +
                "        );\n" +
                "    }\n" +
                "}\n";

            await File.WriteAllTextAsync(sourceFile, sourceCode).ConfigureAwait(false);

            // 2. csc.exe yolunu bul
            string cscPath = FindCscExe();
            if (string.IsNullOrEmpty(cscPath))
                throw new InvalidOperationException(
                    "csc.exe bulunamadi. .NET Framework yuklu oldugundan emin olun.\n" +
                    "Beklenen konum: C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe");

            // 3. csc.exe ile derle
            // /target:winexe  -> konsol penceresi acilmadan calisan Windows uygulamasi
            // /reference      -> MessageBox icin System.Windows.Forms.dll
            // /out            -> cikti dosyasi
            string arguments =
                "/target:winexe " +
                "/reference:System.Windows.Forms.dll " +
                $"/out:\"{outputPath}\" " +
                "/optimize " +
                $"\"{sourceFile}\"";

            await RunProcessAsync(cscPath, arguments).ConfigureAwait(false);

            // 4. Kaynak dosyayi temizle
            try { File.Delete(sourceFile); } catch { }

            // 5. Derleme basarili mi kontrol et
            if (!File.Exists(outputPath))
                throw new InvalidOperationException(
                    "Derleme tamamlandi ancak .exe dosyasi olusturulamadi.");
        }

        // ── csc.exe Bulma ─────────────────────────────────────────────────────

        /// <summary>
        /// Sistemdeki csc.exe konumunu bulur.
        /// </summary>
        private static string FindCscExe()
        {
            string[] candidates = new[]
            {
                @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
                @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe",
                @"C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe",
                @"C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe",
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                    return path;
            }

            return string.Empty;
        }

        // ── Process Calistirma ────────────────────────────────────────────────

        /// <summary>
        /// Verilen executable'i argümanlarla çalistirir ve bitmesini bekler.
        /// </summary>
        private static Task RunProcessAsync(string executable, string arguments)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = executable,
                    Arguments              = arguments,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    WindowStyle            = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException("csc.exe baslatilAmadi.");

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string errorDetail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                    throw new InvalidOperationException(
                        $"csc.exe derleme hatasi (kod {process.ExitCode}):\n{errorDetail}");
                }
            });
        }
    }
}
