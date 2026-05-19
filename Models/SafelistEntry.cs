// =============================================================================
// Models/SafelistEntry.cs — Beyaz Liste Girdisi Modeli
// =============================================================================
// safelist.json dosyasına serileştirilen/deserileştirilen veri sınıfı.
// System.Text.Json ile tam uyumludur.
// =============================================================================

using System;
using System.Text.Json.Serialization;

namespace RegShield.Models
{
    /// <summary>
    /// Kullanıcı tarafından onaylanmış ve beyaz listeye eklenmiş
    /// bir kayıt defteri girişini temsil eder.
    /// </summary>
    public sealed class SafelistEntry
    {
        /// <summary>Kayıt defterindeki değer adı.</summary>
        [JsonPropertyName("registryName")]
        public string RegistryName { get; set; } = string.Empty;

        /// <summary>Güvenilir olarak işaretlenmiş tam dosya yolu.</summary>
        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Kullanıcının beyaz listeye ekleme tarihi (ISO 8601).</summary>
        [JsonPropertyName("approvedAt")]
        public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

        /// <summary>İsteğe bağlı kullanıcı notu.</summary>
        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }
}
