// =============================================================================
// Models/ScanResult.cs — Tarama Sonucu Veri Modeli
// =============================================================================
// Her bir registry kaydının tarama sonucunu temsil eden değişmez (immutable)
// veri sınıfıdır. UI ve iş mantığı arasında veri transferini sağlar.
// =============================================================================

namespace RegShield.Models
{
    /// <summary>
    /// Bir kayıt defteri girişinin tarama sonucunu temsil eder.
    /// </summary>
    public sealed class ScanResult
    {
        // ── Kayıt Defteri Bilgileri ──────────────────────────────────────────

        /// <summary>Kayıt defterindeki değer adı (örn: "MyApp", "Updater").</summary>
        public string RegistryName { get; init; } = string.Empty;

        /// <summary>Kayıt defterindeki tam dosya yolu (örn: C:\Program Files\...).</summary>
        public string FilePath { get; init; } = string.Empty;

        // ── Tarama Kararı ────────────────────────────────────────────────────

        /// <summary>Dosyanın tarama sonucunda belirlenen güvenlik durumu.</summary>
        public SecurityStatus Status { get; init; }

        /// <summary>Kullanıcıya gösterilecek açıklama metni.</summary>
        public string StatusMessage { get; init; } = string.Empty;

        // ── Ek Bilgiler ──────────────────────────────────────────────────────

        /// <summary>Dijital sertifika sahibi (varsa).</summary>
        public string? CertificateSubject { get; init; }

        /// <summary>Dosyanın diskten silinip silinmediği.</summary>
        public bool FileDeleted { get; init; }

        /// <summary>Kayıt defteri girişinin silinip silinmediği.</summary>
        public bool RegistryDeleted { get; init; }
    }

    /// <summary>
    /// Tarama sonucunda dosyanın alabileceği güvenlik durumları.
    /// </summary>
    public enum SecurityStatus
    {
        /// <summary>Dijital sertifika doğrulandı — güvenli.</summary>
        TrustedByCertificate,

        /// <summary>Beyaz listede kayıtlı — güvenli.</summary>
        TrustedBySafelist,

        /// <summary>Kullanıcı tarafından güvenilir onayı verildi ve beyaz listeye eklendi.</summary>
        ApprovedByUser,

        /// <summary>Kullanıcı tarafından tehdit olarak işaretlendi — temizlendi.</summary>
        RemovedByUser,

        /// <summary>Dosya bulunamadı — kayıt geçersiz.</summary>
        FileNotFound,

        /// <summary>İşlem sırasında hata oluştu.</summary>
        Error
    }
}
