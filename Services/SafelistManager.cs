// =============================================================================
// Services/SafelistManager.cs — Beyaz Liste (Safelist) Yöneticisi
// =============================================================================
// Kullanıcının onayladığı dosya yollarını safelist.json dosyasında saklar.
// Dosya yoksa otomatik oluşturulur. Tüm işlemler thread-safe ve asenkrondur.
//
// Kullanılan kütüphane: System.Text.Json (.NET 5+ dahili)
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RegShield.Models;

namespace RegShield.Services
{
    /// <summary>
    /// safelist.json dosyasını okur, yazar ve sorgular.
    /// </summary>
    public sealed class SafelistManager
    {
        // safelist.json uygulamanın çalıştığı dizinde tutulur
        private readonly string _safelistPath;

        // Eş zamanlı yazma/okumayı önlemek için asenkron kilit
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        // JSON serileştirme ayarları
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,                    // Okunabilir, girintili JSON
            PropertyNameCaseInsensitive = true,      // Deserializasyon'da büyük/küçük harf esnekliği
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// SafelistManager örneği oluşturur.
        /// </summary>
        /// <param name="safelistPath">
        /// safelist.json'ın tam yolu. Null ise uygulama dizininde aranır/oluşturulur.
        /// </param>
        public SafelistManager(string? safelistPath = null)
        {
            _safelistPath = safelistPath
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "safelist.json");
        }

        // ── Sorgulama ─────────────────────────────────────────────────────────

        /// <summary>
        /// Verilen dosya yolunun beyaz listede bulunup bulunmadığını kontrol eder.
        /// Karşılaştırma büyük/küçük harf duyarsızdır.
        /// </summary>
        /// <param name="filePath">Kontrol edilecek dosya yolu.</param>
        public async Task<bool> IsInSafelistAsync(string filePath)
        {
            List<SafelistEntry> entries = await LoadEntriesAsync().ConfigureAwait(false);

            return entries.Any(e =>
                string.Equals(e.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        // ── Ekleme ────────────────────────────────────────────────────────────

        /// <summary>
        /// Yeni bir girişi beyaz listeye ekler.
        /// Aynı dosya yolu zaten listede varsa çift kayıt oluşturmaz.
        /// </summary>
        /// <param name="entry">Eklenecek beyaz liste girdisi.</param>
        public async Task AddToSafelistAsync(SafelistEntry entry)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);

            try
            {
                List<SafelistEntry> entries = await LoadEntriesInternalAsync().ConfigureAwait(false);

                // Zaten listede varsa ekleme (idempotent)
                bool exists = entries.Any(e =>
                    string.Equals(e.FilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    entries.Add(entry);
                    await SaveEntriesAsync(entries).ConfigureAwait(false);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        // ── Silme ────────────────────────────────────────────────────────────

        /// <summary>
        /// Belirtilen dosya yoluna ait girişi beyaz listeden kaldırır.
        /// </summary>
        /// <param name="filePath">Kaldırılacak dosya yolu.</param>
        /// <returns>Girdi bulunup silindiyse true, yoksa false.</returns>
        public async Task<bool> RemoveFromSafelistAsync(string filePath)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);

            try
            {
                List<SafelistEntry> entries = await LoadEntriesInternalAsync().ConfigureAwait(false);
                int before = entries.Count;

                entries.RemoveAll(e =>
                    string.Equals(e.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

                if (entries.Count < before)
                {
                    await SaveEntriesAsync(entries).ConfigureAwait(false);
                    return true;
                }

                return false;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        // ── Listeleme ────────────────────────────────────────────────────────

        /// <summary>
        /// Beyaz listedeki tüm girişleri döndürür.
        /// </summary>
        public Task<List<SafelistEntry>> GetAllEntriesAsync()
        {
            return LoadEntriesAsync();
        }

        // ── Dosya İşlemleri (Dahili) ──────────────────────────────────────────

        /// <summary>
        /// safelist.json dosyasını okur. Dosya yoksa boş liste döner.
        /// Kilit almadan çağrılmaya uygundur (public API'den kilitli çağrılır).
        /// </summary>
        private async Task<List<SafelistEntry>> LoadEntriesAsync()
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await LoadEntriesInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// Kilit zaten alınmışken çağrılacak dahili yükleme metodu.
        /// </summary>
        private async Task<List<SafelistEntry>> LoadEntriesInternalAsync()
        {
            // Dosya yoksa boş liste döndür (ilk çalıştırma senaryosu)
            if (!File.Exists(_safelistPath))
                return new List<SafelistEntry>();

            try
            {
                // Dosyayı UTF-8 olarak oku
                await using FileStream stream = File.OpenRead(_safelistPath);

                List<SafelistEntry>? entries = await JsonSerializer
                    .DeserializeAsync<List<SafelistEntry>>(stream, _jsonOptions)
                    .ConfigureAwait(false);

                return entries ?? new List<SafelistEntry>();
            }
            catch (JsonException)
            {
                // JSON bozuksa boş liste ile başla (veri kaybı yerine temiz başlangıç)
                return new List<SafelistEntry>();
            }
        }

        /// <summary>
        /// Girdi listesini safelist.json dosyasına atomik olarak yazar.
        /// Önce geçici dosyaya yazar, sonra yerini değiştirir (crash-safe).
        /// </summary>
        private async Task SaveEntriesAsync(List<SafelistEntry> entries)
        {
            // Dizinin var olduğundan emin ol
            string? directory = Path.GetDirectoryName(_safelistPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Geçici dosyaya yaz
            string tempPath = _safelistPath + ".tmp";

            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, entries, _jsonOptions)
                    .ConfigureAwait(false);
            }

            // Geçici dosyayı gerçek konuma taşı (atomik işlem)
            File.Move(tempPath, _safelistPath, overwrite: true);
        }

        /// <summary>
        /// safelist.json dosyasının tam yolunu döndürür (debug/info amaçlı).
        /// </summary>
        public string SafelistFilePath => _safelistPath;
    }
}
