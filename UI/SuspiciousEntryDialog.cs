// =============================================================================
// UI/SuspiciousEntryDialog.cs — Şüpheli Kayıt Onay Diyalogu
// =============================================================================
// Standart MessageBox yerine özel tasarlanmış, daha fazla bilgi sunan
// bir onay penceresi. Kullanıcıya dosya hakkında detaylı bilgi gösterir
// ve "Güvenli Onayla" / "Tehdidi Temizle" seçenekleri sunar.
// =============================================================================

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace RegShield.UI
{
    /// <summary>
    /// Şüpheli başlangıç kaydı için kullanıcı onay diyalogu.
    /// DialogResult.Yes = Güvenli onayla, DialogResult.No = Tehdidi temizle.
    /// </summary>
    public sealed class SuspiciousEntryDialog : Form
    {
        // ── Diyalog Sonucu ─────────────────────────────────────────────────────
        /// <summary>
        /// true = Kullanıcı "Güvenli" dedi (beyaz listeye ekle).
        /// false = Kullanıcı "Tehdit" dedi (sil).
        /// </summary>
        public bool UserApprovedAsSafe { get; private set; }

        // ── Kontrollar ─────────────────────────────────────────────────────────
        private Label       _lblTitle         = null!;
        private Label       _lblDescription   = null!;
        private Panel       _pnlFileInfo      = null!;
        private Label       _lblFileName      = null!;
        private Label       _lblFilePath      = null!;
        private Label       _lblFileSize      = null!;
        private Label       _lblWarning       = null!;
        private Button      _btnSafe          = null!;
        private Button      _btnThreat        = null!;
        private Button      _btnSkip          = null!;
        private PictureBox  _picWarningIcon   = null!;

        // ── Yapıcı ─────────────────────────────────────────────────────────────

        public SuspiciousEntryDialog(string registryName, string filePath)
        {
            InitializeComponents();
            PopulateFileInfo(registryName, filePath);
        }

        // ── UI Kurulumu ────────────────────────────────────────────────────────

        private void InitializeComponents()
        {
            // Form ayarları
            this.Text            = "RegShield — Şüpheli Kayıt Tespit Edildi";
            this.Size            = new Size(560, 400);
            this.MinimumSize     = new Size(560, 400);
            this.MaximumSize     = new Size(560, 400);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = Color.FromArgb(30, 30, 30);
            this.ForeColor       = Color.White;

            // ── Uyarı İkonu ──────────────────────────────────────────────────
            _picWarningIcon = new PictureBox
            {
                Size     = new Size(48, 48),
                Location = new Point(20, 20),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Image    = SystemIcons.Warning.ToBitmap()
            };

            // ── Başlık ───────────────────────────────────────────────────────
            _lblTitle = new Label
            {
                Text      = "⚠  Şüpheli Başlangıç Kaydı",
                Location  = new Point(80, 20),
                Size      = new Size(440, 30),
                Font      = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 180, 0),
                BackColor = Color.Transparent
            };

            // ── Açıklama ─────────────────────────────────────────────────────
            _lblDescription = new Label
            {
                Text      = "Dijital sertifikası olmayan ve beyaz listede bulunmayan\nbir başlangıç kaydı tespit edildi. Ne yapmak istersiniz?",
                Location  = new Point(80, 52),
                Size      = new Size(440, 40),
                Font      = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 200, 200),
                BackColor = Color.Transparent
            };

            // ── Dosya Bilgi Paneli ────────────────────────────────────────────
            _pnlFileInfo = new Panel
            {
                Location  = new Point(20, 110),
                Size      = new Size(510, 130),
                BackColor = Color.FromArgb(45, 45, 48),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Panel başlığı
            var lblPanelTitle = new Label
            {
                Text      = "Dosya Bilgileri",
                Location  = new Point(10, 8),
                Size      = new Size(490, 20),
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 200, 255),
                BackColor = Color.Transparent
            };

            // Dosya Adı
            var lblFileNameLabel = new Label
            {
                Text      = "Kayıt Adı:",
                Location  = new Point(10, 34),
                Size      = new Size(80, 18),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 160),
                BackColor = Color.Transparent
            };

            _lblFileName = new Label
            {
                Text      = "",
                Location  = new Point(95, 34),
                Size      = new Size(400, 18),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            // Dosya Yolu
            var lblFilePathLabel = new Label
            {
                Text      = "Dosya Yolu:",
                Location  = new Point(10, 58),
                Size      = new Size(80, 18),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 160),
                BackColor = Color.Transparent
            };

            _lblFilePath = new Label
            {
                Text      = "",
                Location  = new Point(95, 58),
                Size      = new Size(400, 36),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(255, 140, 0),
                BackColor = Color.Transparent
            };

            // Dosya Boyutu
            var lblFileSizeLabel = new Label
            {
                Text      = "Dosya Boyutu:",
                Location  = new Point(10, 100),
                Size      = new Size(90, 18),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(160, 160, 160),
                BackColor = Color.Transparent
            };

            _lblFileSize = new Label
            {
                Text      = "",
                Location  = new Point(105, 100),
                Size      = new Size(200, 18),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            _pnlFileInfo.Controls.AddRange(new Control[]
            {
                lblPanelTitle, lblFileNameLabel, _lblFileName,
                lblFilePathLabel, _lblFilePath,
                lblFileSizeLabel, _lblFileSize
            });

            // ── Uyarı Metni ───────────────────────────────────────────────────
            _lblWarning = new Label
            {
                Text      = "ℹ  Emin değilseniz \"Atla\" seçeneğini kullanın.",
                Location  = new Point(20, 254),
                Size      = new Size(510, 20),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(130, 130, 130),
                BackColor = Color.Transparent
            };

            // ── Butonlar ─────────────────────────────────────────────────────
            // "Güvenli Onayla" butonu
            _btnSafe = new Button
            {
                Text      = "✅  Güvenli Onayla",
                Location  = new Point(20, 290),
                Size      = new Size(160, 42),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 122, 64),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            _btnSafe.FlatAppearance.BorderColor = Color.FromArgb(0, 160, 80);
            _btnSafe.Click += BtnSafe_Click;

            // "Tehdidi Temizle" butonu
            _btnThreat = new Button
            {
                Text      = "🗑️  Tehdidi Temizle",
                Location  = new Point(196, 290),
                Size      = new Size(160, 42),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(180, 30, 30),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            _btnThreat.FlatAppearance.BorderColor = Color.FromArgb(220, 50, 50);
            _btnThreat.Click += BtnThreat_Click;

            // "Atla" butonu
            _btnSkip = new Button
            {
                Text      = "Atla",
                Location  = new Point(372, 290),
                Size      = new Size(80, 42),
                Font      = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            _btnSkip.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
            _btnSkip.Click += BtnSkip_Click;

            // Kontrolleri forma ekle
            this.Controls.AddRange(new Control[]
            {
                _picWarningIcon, _lblTitle, _lblDescription,
                _pnlFileInfo, _lblWarning,
                _btnSafe, _btnThreat, _btnSkip
            });
        }

        // ── Veri Doldurma ──────────────────────────────────────────────────────

        private void PopulateFileInfo(string registryName, string filePath)
        {
            _lblFileName.Text = registryName;
            _lblFilePath.Text = filePath.Length > 60
                ? "..." + filePath.Substring(filePath.Length - 60)
                : filePath;

            // Dosya boyutunu göster
            try
            {
                if (File.Exists(filePath))
                {
                    long bytes = new FileInfo(filePath).Length;
                    _lblFileSize.Text = FormatFileSize(bytes);
                }
                else
                {
                    _lblFileSize.Text = "Dosya bulunamadı";
                    _lblFileSize.ForeColor = Color.FromArgb(200, 80, 80);
                }
            }
            catch
            {
                _lblFileSize.Text = "Okunamadı";
            }
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024):F1} MB";
        }

        // ── Event Handler'lar ──────────────────────────────────────────────────

        private void BtnSafe_Click(object? sender, EventArgs e)
        {
            UserApprovedAsSafe = true;
            this.DialogResult  = DialogResult.Yes;
            this.Close();
        }

        private void BtnThreat_Click(object? sender, EventArgs e)
        {
            UserApprovedAsSafe = false;
            this.DialogResult  = DialogResult.No;
            this.Close();
        }

        private void BtnSkip_Click(object? sender, EventArgs e)
        {
            // Atla seçeneği: Bu taramada karar verme, sonraki taramada tekrar sor
            UserApprovedAsSafe = true; // Geçici olarak güvenli say
            this.DialogResult  = DialogResult.Cancel;
            this.Close();
        }
    }
}
