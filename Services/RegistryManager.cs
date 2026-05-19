// =============================================================================
// Services/RegistryManager.cs — Registry Yönetim Servisi
// =============================================================================
// HKCU\Software\Microsoft\Windows\CurrentVersion\Run anahtarı altındaki
// başlangıç kayıtlarını okur, yeni kayıt ekler ve mevcut kayıtları siler.
//
// NOT: Bu sınıfın düzgün çalışması için uygulamanın Yönetici (Administrator)
//      yetkisiyle çalışması gerekir (bkz. app.manifest).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace RegShield.Services
{
    /// <summary>
    /// Windows Kayıt Defteri üzerinde okuma, yazma ve silme işlemleri yapar.
    /// Tüm işlemler asenkron wrapper ile UI thread'ini bloklamaz.
    /// </summary>
    public sealed class RegistryManager
    {
        // Taranacak kayıt defteri anahtarının tam yolu
        private const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        // ── Okuma ────────────────────────────────────────────────────────────

        /// <summary>
        /// HKCU\...\Run anahtarı altındaki tüm başlangıç kayıtlarını döndürür.
        /// </summary>
        /// <returns>
        /// Her bir kaydın (isim, dosya yolu) çiftlerini içeren liste.
        /// Anahtar boşsa veya açılamazsa boş liste döner.
        /// </returns>
        public Task<List<(string Name, string FilePath)>> ReadStartupEntriesAsync()
        {
            // Registry işlemi CPU-bound olsa da UI'ı bloke etmemek için
            // Task.Run içinde çalıştırıyoruz.
            return Task.Run(() =>
            {
                var entries = new List<(string, string)>();

                try
                {
                    // HKEY_CURRENT_USER altında salt okunur modda aç
                    using RegistryKey? rootKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);

                    if (rootKey is null)
                        return entries; // Anahtar mevcut değil

                    // Her bir değer adını oku
                    foreach (string valueName in rootKey.GetValueNames())
                    {
                        // REG_SZ veya REG_EXPAND_SZ tipindeki değerleri al
                        string? rawValue = rootKey.GetValue(valueName)?.ToString();

                        if (!string.IsNullOrWhiteSpace(rawValue))
                        {
                            // Dosya yolunu temizle: bazı kayıtlar "path" /args formatında gelir
                            string cleanPath = ExtractExecutablePath(rawValue);
                            entries.Add((valueName, cleanPath));
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Registry erişim hatası — loglama için yeniden fırlat
                    throw new InvalidOperationException(
                        $"Registry anahtarı okunamadı: {RunKeyPath}", ex);
                }

                return entries;
            });
        }

        // ── Yazma ────────────────────────────────────────────────────────────

        /// <summary>
        /// HKCU\...\Run anahtarına yeni bir başlangıç kaydı ekler.
        /// Virüs simülasyonu ve test amacıyla kullanılır.
        /// </summary>
        /// <param name="name">Kayıt değer adı.</param>
        /// <param name="filePath">Çalıştırılacak dosyanın tam yolu.</param>
        public Task WriteStartupEntryAsync(string name, string filePath)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Yazma (writable: true) modunda aç
                    using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(
                        RunKeyPath, writable: true);

                    if (runKey is null)
                        throw new InvalidOperationException(
                            "Registry Run anahtarı yazma için açılamadı.");

                    // REG_SZ tipinde değer yaz
                    runKey.SetValue(name, filePath, RegistryValueKind.String);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new UnauthorizedAccessException(
                        "Registry'ye yazma yetkisi yok. Uygulamayı Yönetici olarak çalıştırın.", ex);
                }
            });
        }

        // ── Silme ────────────────────────────────────────────────────────────

        /// <summary>
        /// HKCU\...\Run anahtarından belirtilen adlı kaydı siler.
        /// </summary>
        /// <param name="name">Silinecek kayıt değer adı.</param>
        /// <returns>Silme işlemi başarılıysa true, kayıt bulunamazsa false.</returns>
        public Task<bool> DeleteStartupEntryAsync(string name)
        {
            return Task.Run(() =>
            {
                try
                {
                    using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(
                        RunKeyPath, writable: true);

                    if (runKey is null) return false;

                    // Değer mevcut mu kontrol et
                    object? existing = runKey.GetValue(name);
                    if (existing is null) return false;

                    // Değeri sil
                    runKey.DeleteValue(name, throwOnMissingValue: false);
                    return true;
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new UnauthorizedAccessException(
                        $"'{name}' kaydı silinemedi. Yönetici yetkisi gerekli.", ex);
                }
            });
        }

        // ── Yardımcı Metotlar ─────────────────────────────────────────────────

        /// <summary>
        /// Registry değerinden sadece çalıştırılabilir dosya yolunu çıkarır.
        /// Örn: '"C:\Program Files\App\app.exe" /silent' → 'C:\Program Files\App\app.exe'
        /// </summary>
        private static string ExtractExecutablePath(string rawValue)
        {
            rawValue = rawValue.Trim();

            // Tırnak işareti ile başlıyorsa (örn: "C:\path\app.exe" /args)
            if (rawValue.StartsWith('"'))
            {
                int closingQuote = rawValue.IndexOf('"', 1);
                if (closingQuote > 1)
                    return rawValue.Substring(1, closingQuote - 1);
            }

            // Boşluktan önceki kısım dosya yolu, sonrası argüman
            int spaceIndex = rawValue.IndexOf(' ');
            if (spaceIndex > 0)
                return rawValue.Substring(0, spaceIndex);

            return rawValue;
        }
    }
}
