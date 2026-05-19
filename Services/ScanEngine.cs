// =============================================================================
// Services/ScanEngine.cs — Tarama Motoru (Orkestrasyon Servisi)
// =============================================================================
// 4 adımlı tarama mantığını koordine eder:
//   1. Registry'den startup kayıtlarını oku
//   2. Dijital sertifika kontrolü
//   3. Beyaz liste (safelist.json) kontrolü
//   4. Şüpheli kayıtlar için kullanıcı kararı al
//
// UI thread'i ile iletişim: IProgress<T> ve event callback mekanizması ile.
// Bu sınıf iş mantığını tamamen UI'dan ayırır (Separation of Concerns).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RegShield.Models;

namespace RegShield.Services
{
    /// <summary>
    /// Kullanıcıya şüpheli kayıt sorulduğunda çağrılacak callback tipi.
    /// true döndürülürse "Güvenli" (beyaz listeye ekle), false döndürülürse "Sil".
    /// </summary>
    /// <param name="registryName">Registry'deki değer adı.</param>
    /// <param name="filePath">Şüpheli dosyanın tam yolu.</param>
    public delegate Task<bool> SuspiciousEntryCallback(string registryName, string filePath);

    /// <summary>
    /// Tüm tarama akışını yöneten merkezi servis.
    /// </summary>
    public sealed class ScanEngine
    {
        // ── Bağımlılıklar ──────────────────────────────────────────────────────
        private readonly RegistryManager     _registryManager;
        private readonly CertificateChecker  _certificateChecker;
        private readonly SafelistManager     _safelistManager;

        public ScanEngine(
            RegistryManager    registryManager,
            CertificateChecker certificateChecker,
            SafelistManager    safelistManager)
        {
            _registryManager    = registryManager    ?? throw new ArgumentNullException(nameof(registryManager));
            _certificateChecker = certificateChecker ?? throw new ArgumentNullException(nameof(certificateChecker));
            _safelistManager    = safelistManager    ?? throw new ArgumentNullException(nameof(safelistManager));
        }

        // ── Ana Tarama Metodu ──────────────────────────────────────────────────

        /// <summary>
        /// 4 adımlı taramayı başlatır ve sonuçları listesiyle döndürür.
        /// </summary>
        /// <param name="progress">
        /// Her adımda UI'ı bilgilendirmek için ilerleme raporu (durum metni).
        /// </param>
        /// <param name="suspiciousCallback">
        /// Şüpheli bir kayıt bulunduğunda kullanıcıya sorulacak karar mekanizması.
        /// UI katmanı bu delegate'i implemente eder.
        /// </param>
        /// <param name="cancellationToken">Taramayı iptal etmek için token.</param>
        /// <returns>Her bir kayıt defteri girdisi için tarama sonucu listesi.</returns>
        public async Task<List<ScanResult>> RunScanAsync(
            IProgress<string>        progress,
            SuspiciousEntryCallback  suspiciousCallback,
            CancellationToken        cancellationToken = default)
        {
            var results = new List<ScanResult>();

            // ── ADIM 1: Registry Kayıtlarını Oku ─────────────────────────────
            progress.Report("📂 Adım 1/4: Startup registry kayıtları okunuyor...");

            List<(string Name, string FilePath)> entries;

            try
            {
                entries = await _registryManager.ReadStartupEntriesAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progress.Report($"❌ Registry okunamadı: {ex.Message}");
                return results;
            }

            if (entries.Count == 0)
            {
                progress.Report("ℹ️ Startup registry anahtarı boş — tarama tamamlandı.");
                return results;
            }

            progress.Report($"✅ {entries.Count} başlangıç kaydı bulundu. İnceleniyor...");

            // ── Her Kaydı İşle ────────────────────────────────────────────────
            for (int i = 0; i < entries.Count; i++)
            {
                // İptal kontrolü
                cancellationToken.ThrowIfCancellationRequested();

                var (name, filePath) = entries[i];
                progress.Report($"🔍 [{i + 1}/{entries.Count}] İnceleniyor: {name}");

                ScanResult result = await ProcessEntryAsync(
                    name, filePath, progress, suspiciousCallback, cancellationToken)
                    .ConfigureAwait(false);

                results.Add(result);
            }

            progress.Report($"🏁 Tarama tamamlandı. {results.Count} kayıt işlendi.");
            return results;
        }

        // ── Tekil Kayıt İşleme ─────────────────────────────────────────────────

        /// <summary>
        /// Tek bir registry kaydını 4 adım mantığıyla işler.
        /// </summary>
        private async Task<ScanResult> ProcessEntryAsync(
            string name,
            string filePath,
            IProgress<string> progress,
            SuspiciousEntryCallback suspiciousCallback,
            CancellationToken cancellationToken)
        {
            // Dosya var mı?
            if (!File.Exists(filePath))
            {
                progress.Report($"  ⚠️ Dosya bulunamadı: {filePath}");
                return new ScanResult
                {
                    RegistryName  = name,
                    FilePath      = filePath,
                    Status        = SecurityStatus.FileNotFound,
                    StatusMessage = "Dosya diskte bulunamadı — kayıt geçersiz olabilir."
                };
            }

            // ── ADIM 2: Dijital Sertifika Kontrolü ───────────────────────────
            progress.Report($"  🔐 Adım 2: Dijital sertifika kontrol ediliyor...");

            CertificateInfo? certInfo = null;

            try
            {
                certInfo = await _certificateChecker.GetCertificateInfoAsync(filePath)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progress.Report($"  ⚠️ Sertifika kontrolü hatası: {ex.Message}");
            }

            // Geçerli sertifika varsa → Güvenilir
            if (certInfo is not null && certInfo.IsCurrentlyValid)
            {
                progress.Report(
                    $"  ✅ Güvenilir sertifika: {certInfo.FriendlyName} ({certInfo.Issuer})");

                return new ScanResult
                {
                    RegistryName       = name,
                    FilePath           = filePath,
                    Status             = SecurityStatus.TrustedByCertificate,
                    StatusMessage      = $"Dijital sertifika geçerli: {certInfo.FriendlyName}",
                    CertificateSubject = certInfo.FriendlyName
                };
            }

            progress.Report("  ℹ️ Sertifika yok veya geçersiz — beyaz liste kontrol ediliyor...");

            // ── ADIM 3: Beyaz Liste Kontrolü ─────────────────────────────────
            progress.Report("  📋 Adım 3: safelist.json kontrol ediliyor...");

            bool inSafelist = await _safelistManager.IsInSafelistAsync(filePath)
                .ConfigureAwait(false);

            if (inSafelist)
            {
                progress.Report($"  ✅ Beyaz listede bulundu: {Path.GetFileName(filePath)}");

                return new ScanResult
                {
                    RegistryName  = name,
                    FilePath      = filePath,
                    Status        = SecurityStatus.TrustedBySafelist,
                    StatusMessage = "Daha önce güvenilir olarak işaretlendi (safelist.json)."
                };
            }

            // ── ADIM 4: Kullanıcı Kararı (Şüpheli) ───────────────────────────
            progress.Report($"  ⚠️ Adım 4: Şüpheli kayıt! Kullanıcı kararı bekleniyor: {name}");

            cancellationToken.ThrowIfCancellationRequested();

            // Callback aracılığıyla UI'dan kullanıcı kararını al
            bool userApproved = await suspiciousCallback(name, filePath)
                .ConfigureAwait(false);

            if (userApproved)
            {
                // Kullanıcı "Güvenli" dedi → Beyaz listeye ekle
                await _safelistManager.AddToSafelistAsync(new SafelistEntry
                {
                    RegistryName = name,
                    FilePath     = filePath,
                    ApprovedAt   = DateTime.UtcNow,
                    Note         = "Kullanıcı tarafından onaylandı"
                }).ConfigureAwait(false);

                progress.Report($"  ✅ Beyaz listeye eklendi: {name}");

                return new ScanResult
                {
                    RegistryName  = name,
                    FilePath      = filePath,
                    Status        = SecurityStatus.ApprovedByUser,
                    StatusMessage = "Kullanıcı onayı ile beyaz listeye eklendi."
                };
            }
            else
            {
                // Kullanıcı "Tehdit" dedi → Temizle
                bool regDeleted  = false;
                bool fileDeleted = false;

                // Registry kaydını sil
                try
                {
                    regDeleted = await _registryManager.DeleteStartupEntryAsync(name)
                        .ConfigureAwait(false);
                    progress.Report($"  🗑️ Registry kaydı silindi: {name}");
                }
                catch (Exception ex)
                {
                    progress.Report($"  ❌ Registry silinemedi: {ex.Message}");
                }

                // Dosyayı diskten sil
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        fileDeleted = true;
                        progress.Report($"  🗑️ Dosya silindi: {Path.GetFileName(filePath)}");
                    }
                }
                catch (Exception ex)
                {
                    progress.Report($"  ❌ Dosya silinemedi: {ex.Message}");
                }

                return new ScanResult
                {
                    RegistryName    = name,
                    FilePath        = filePath,
                    Status          = SecurityStatus.RemovedByUser,
                    StatusMessage   = "Kullanıcı tarafından tehdit olarak işaretlendi ve temizlendi.",
                    RegistryDeleted = regDeleted,
                    FileDeleted     = fileDeleted
                };
            }
        }
    }
}
