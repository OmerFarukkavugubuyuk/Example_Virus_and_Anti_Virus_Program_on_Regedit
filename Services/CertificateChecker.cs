// =============================================================================
// Services/CertificateChecker.cs — Dijital Sertifika Doğrulama Servisi
// =============================================================================
// Verilen bir .exe dosyasının geçerli bir Authenticode (dijital imza) sertifikası
// taşıyıp taşımadığını kontrol eder.
//
// Kullanılan kütüphane: System.Security.Cryptography.X509Certificates
// .NET'e dahildir, ek paket gerektirmez.
// =============================================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace RegShield.Services
{
    /// <summary>
    /// Yürütülebilir dosyaların dijital sertifika durumunu denetler.
    /// </summary>
    public sealed class CertificateChecker
    {
        // ── Ana Doğrulama Metodu ──────────────────────────────────────────────

        /// <summary>
        /// Verilen dosya yolundaki .exe'nin Authenticode sertifikasını kontrol eder.
        /// </summary>
        /// <param name="filePath">Kontrol edilecek dosyanın tam yolu.</param>
        /// <returns>
        /// Sertifika bulunursa (subject, issuer) bilgisini içeren <see cref="CertificateInfo"/>,
        /// bulunamazsa veya geçersizse null döner.
        /// </returns>
        public Task<CertificateInfo?> GetCertificateInfoAsync(string filePath)
        {
            return Task.Run(() => GetCertificateInfoCore(filePath));
        }

        // ── İç İmplementasyon ─────────────────────────────────────────────────

        private static CertificateInfo? GetCertificateInfoCore(string filePath)
        {
            // 1. Dosya var mı?
            if (!File.Exists(filePath))
                return null;

            X509Certificate2? cert = null;

            try
            {
                // 2. Dosyanın Authenticode imzasını oku
                //    X509Certificate.CreateFromSignedFile: PE (Portable Executable) dosyalarından
                //    gömülü Authenticode sertifikasını çıkarır.
                X509Certificate rawCert = X509Certificate.CreateFromSignedFile(filePath);
                cert = new X509Certificate2(rawCert);
            }
            catch (CryptographicException)
            {
                // Sertifika yok veya bozuk — normal durum, null döndür
                return null;
            }
            catch (Exception)
            {
                // Dosya erişim hatası veya beklenmedik hata
                return null;
            }

            try
            {
                // 3. Sertifika zincirini doğrula (güvenilir kök CA'ya kadar)
                using var chain = new X509Chain();

                // Çevrimiçi iptal kontrolü yap
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                chain.ChainPolicy.VerificationTime = DateTime.Now;

                bool chainValid = chain.Build(cert);

                // 4. Sonuçları döndür — zincir geçerliliğini de raporla
                return new CertificateInfo
                {
                    Subject = cert.Subject,
                    Issuer = cert.Issuer,
                    ValidFrom = cert.NotBefore,
                    ValidTo = cert.NotAfter,
                    Thumbprint = cert.Thumbprint,
                    IsChainValid = chainValid,
                    // Zincir hatalarını insan okunabilir formata çevir
                    ChainStatusSummary = chainValid
                        ? "Zincir geçerli"
                        : BuildChainErrorSummary(chain)
                };
            }
            catch (CryptographicException ex)
            {
                // Çevrimiçi iptal kontrolü başarısız olabilir (ağ yok vb.)
                // Bu durumda sertifika bilgisini döndür ama zinciri geçersiz say.
                return new CertificateInfo
                {
                    Subject = cert.Subject,
                    Issuer = cert.Issuer,
                    ValidFrom = cert.NotBefore,
                    ValidTo = cert.NotAfter,
                    Thumbprint = cert.Thumbprint,
                    IsChainValid = false,
                    ChainStatusSummary = $"Zincir doğrulanamadı: {ex.Message}"
                };
            }
            finally
            {
                cert?.Dispose();
            }
        }

        // ── Yardımcı Metot ────────────────────────────────────────────────────

        /// <summary>
        /// X509Chain hata durumlarını okunabilir metne çevirir.
        /// </summary>
        private static string BuildChainErrorSummary(X509Chain chain)
        {
            var errors = new System.Text.StringBuilder();

            foreach (X509ChainStatus status in chain.ChainStatus)
            {
                if (errors.Length > 0) errors.Append(", ");
                errors.Append(status.StatusInformation.Trim());
            }

            return errors.Length > 0 ? errors.ToString() : "Bilinmeyen zincir hatası";
        }
    }

    // ── Veri Transfer Nesnesi ─────────────────────────────────────────────────

    /// <summary>
    /// Bir dosyanın dijital sertifika bilgilerini taşıyan değer nesnesi.
    /// </summary>
    public sealed class CertificateInfo
    {
        /// <summary>Sertifika sahibi (DN formatında, örn: CN=Microsoft Corporation, O=Microsoft...).</summary>
        public string Subject { get; init; } = string.Empty;

        /// <summary>Sertifikayı veren kuruluş (CA).</summary>
        public string Issuer { get; init; } = string.Empty;

        /// <summary>Sertifikanın geçerlilik başlangıç tarihi.</summary>
        public DateTime ValidFrom { get; init; }

        /// <summary>Sertifikanın geçerlilik bitiş tarihi.</summary>
        public DateTime ValidTo { get; init; }

        /// <summary>Sertifikanın benzersiz parmak izi (SHA-1).</summary>
        public string Thumbprint { get; init; } = string.Empty;

        /// <summary>Güvenilir CA zincirine kadar doğrulama başarılı mı?</summary>
        public bool IsChainValid { get; init; }

        /// <summary>Zincir doğrulama özeti veya hata açıklaması.</summary>
        public string ChainStatusSummary { get; init; } = string.Empty;

        /// <summary>
        /// Sertifikanın şu an geçerli olup olmadığını kontrol eder.
        /// Zincir geçerli VE tarihler uygunsa true döner.
        /// </summary>
        public bool IsCurrentlyValid =>
            IsChainValid &&
            DateTime.Now >= ValidFrom &&
            DateTime.Now <= ValidTo;

        /// <summary>
        /// Subject alanından "Common Name" (CN=...) değerini çıkarır.
        /// Örn: "CN=Microsoft Corporation, O=Microsoft..." → "Microsoft Corporation"
        /// </summary>
        public string FriendlyName
        {
            get
            {
                const string prefix = "CN=";
                int start = Subject.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (start < 0) return Subject;

                start += prefix.Length;
                int end = Subject.IndexOf(',', start);
                return end > start
                    ? Subject.Substring(start, end - start).Trim()
                    : Subject.Substring(start).Trim();
            }
        }
    }
}
