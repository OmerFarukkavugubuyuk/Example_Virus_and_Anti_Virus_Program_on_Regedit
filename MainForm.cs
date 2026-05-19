// =============================================================================
// MainForm.cs — Ana WinForms Formu
// =============================================================================
// Tüm servisleri (RegistryManager, CertificateChecker, SafelistManager,
// ScanEngine, VirusSimulator) koordine eden ana UI sınıfı.
//
// UI Thread Güvenliği: Async/await ile tarama arka planda çalışır.
// Progress<T> ile UI güncellemeleri main thread'de yapılır.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RegShield.Models;
using RegShield.Services;
using RegShield.UI;

namespace RegShield
{
    public sealed partial class MainForm : Form
    {
        // ── Servis Bağımlılıkları ──────────────────────────────────────────────
        private readonly RegistryManager    _registryManager;
        private readonly CertificateChecker _certificateChecker;
        private readonly SafelistManager    _safelistManager;
        private readonly ScanEngine         _scanEngine;
        private readonly VirusSimulator     _virusSimulator;

        // ── İptal Mekanizması ──────────────────────────────────────────────────
        private CancellationTokenSource? _scanCts;

        // ── UI Kontrolleri ─────────────────────────────────────────────────────
        private Button         _btnCreateVirus  = null!;
        private Button         _btnStartScan    = null!;
        private Button         _btnCancelScan   = null!;
        private Button         _btnClearLog     = null!;
        private DataGridView   _dgvResults      = null!;
        private RichTextBox    _rtbLog          = null!;
        private StatusStrip    _statusStrip     = null!;
        private ToolStripLabel _lblStatus       = null!;
        private ToolStripProgressBar _progressBar = null!;
        private Label          _lblSafelistPath = null!;
        private Panel          _pnlHeader       = null!;
        private Label          _lblAppTitle     = null!;
        private Label          _lblAppSubtitle  = null!;

        // ── Yapıcı ─────────────────────────────────────────────────────────────

        public MainForm()
        {
            // Servisleri oluştur (Dependency Injection yerine manuel composition)
            _registryManager    = new RegistryManager();
            _certificateChecker = new CertificateChecker();
            _safelistManager    = new SafelistManager();
            _scanEngine         = new ScanEngine(_registryManager, _certificateChecker, _safelistManager);
            _virusSimulator     = new VirusSimulator(_registryManager);

            // UI kurulumu
            InitializeComponents();

            // Safelist dosya yolunu göster
            _lblSafelistPath.Text = $"Safelist: {_safelistManager.SafelistFilePath}";
        }

        // ── UI Kurulumu ────────────────────────────────────────────────────────

        private void InitializeComponents()
        {
            // Form ayarları
            this.Text            = "RegShield — Registry Antivirüs";
            this.Size            = new Size(1000, 700);
            this.MinimumSize     = new Size(900, 600);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = Color.FromArgb(20, 20, 25);
            this.ForeColor       = Color.White;
            this.Font            = new Font("Segoe UI", 9.5f);

            // ── Header Panel ──────────────────────────────────────────────────
            _pnlHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 72,
                BackColor = Color.FromArgb(15, 15, 20)
            };

            _lblAppTitle = new Label
            {
                Text      = "🛡  RegShield",
                Location  = new Point(20, 10),
                Size      = new Size(300, 36),
                Font      = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 255),
                BackColor = Color.Transparent
            };

            _lblAppSubtitle = new Label
            {
                Text      = "Registry Odaklı Başlangıç Kaydı Güvenlik Tarayıcısı",
                Location  = new Point(22, 46),
                Size      = new Size(500, 18),
                Font      = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 120, 130),
                BackColor = Color.Transparent
            };

            _pnlHeader.Controls.AddRange(new Control[] { _lblAppTitle, _lblAppSubtitle });

            // ── Toolbar Panel ─────────────────────────────────────────────────
            var pnlToolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 58,
                BackColor = Color.FromArgb(28, 28, 35)
            };

            // "Virüs Test Kaydı Oluştur" butonu
            _btnCreateVirus = new Button
            {
                Text      = "🦠  Virüs Test Kaydı Oluştur",
                Location  = new Point(12, 10),
                Size      = new Size(220, 38),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(160, 60, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            _btnCreateVirus.FlatAppearance.BorderColor = Color.FromArgb(200, 80, 0);
            _btnCreateVirus.FlatAppearance.BorderSize  = 1;
            _btnCreateVirus.Click += BtnCreateVirus_Click;

            // "Sistem Taraması Yap" butonu
            _btnStartScan = new Button
            {
                Text      = "🔍  Sistem Taraması Yap",
                Location  = new Point(244, 10),
                Size      = new Size(200, 38),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 100, 180),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            _btnStartScan.FlatAppearance.BorderColor = Color.FromArgb(0, 130, 220);
            _btnStartScan.FlatAppearance.BorderSize  = 1;
            _btnStartScan.Click += BtnStartScan_Click;

            // "İptal" butonu
            _btnCancelScan = new Button
            {
                Text      = "⏹  İptal",
                Location  = new Point(456, 10),
                Size      = new Size(90, 38),
                Font      = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.FromArgb(60, 60, 70),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnCancelScan.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 100);
            _btnCancelScan.Click += BtnCancelScan_Click;

            // "Logu Temizle" butonu
            _btnClearLog = new Button
            {
                Text      = "🗑 Temizle",
                Location  = new Point(558, 10),
                Size      = new Size(100, 38),
                Font      = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.FromArgb(150, 150, 150),
                BackColor = Color.FromArgb(40, 40, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            _btnClearLog.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);
            _btnClearLog.Click += (s, e) => { _rtbLog.Clear(); _dgvResults.Rows.Clear(); };

            pnlToolbar.Controls.AddRange(new Control[]
            {
                _btnCreateVirus, _btnStartScan, _btnCancelScan, _btnClearLog
            });

            // ── Ana İçerik (SplitContainer) ───────────────────────────────────
            var splitContainer = new SplitContainer
            {
                Dock           = DockStyle.Fill,
                Orientation    = Orientation.Horizontal,
                SplitterWidth  = 4,
                SplitterDistance = 320,
                BackColor      = Color.FromArgb(45, 45, 55),
                Panel1MinSize  = 150,
                Panel2MinSize  = 100
            };

            // ── Üst Panel: DataGridView (Tarama Sonuçları) ───────────────────
            var lblResultsHeader = new Label
            {
                Text      = "Tarama Sonuçları",
                Dock      = DockStyle.Top,
                Height    = 26,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 255),
                BackColor = Color.FromArgb(22, 22, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            };

            _dgvResults = new DataGridView
            {
                Dock                   = DockStyle.Fill,
                BackgroundColor        = Color.FromArgb(28, 28, 36),
                ForeColor              = Color.White,
                GridColor              = Color.FromArgb(50, 50, 60),
                BorderStyle            = BorderStyle.None,
                ColumnHeadersHeight    = 32,
                RowHeadersVisible      = false,
                AllowUserToAddRows     = false,
                AllowUserToDeleteRows  = false,
                ReadOnly               = true,
                SelectionMode          = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode    = DataGridViewAutoSizeColumnsMode.Fill
            };

            // DataGridView görsel stili
            _dgvResults.ColumnHeadersDefaultCellStyle.BackColor  = Color.FromArgb(35, 35, 45);
            _dgvResults.ColumnHeadersDefaultCellStyle.ForeColor  = Color.FromArgb(0, 180, 255);
            _dgvResults.ColumnHeadersDefaultCellStyle.Font       = new Font("Segoe UI", 9, FontStyle.Bold);
            _dgvResults.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 35, 45);
            _dgvResults.DefaultCellStyle.BackColor               = Color.FromArgb(28, 28, 36);
            _dgvResults.DefaultCellStyle.ForeColor               = Color.White;
            _dgvResults.DefaultCellStyle.SelectionBackColor      = Color.FromArgb(50, 80, 120);
            _dgvResults.DefaultCellStyle.SelectionForeColor      = Color.White;
            _dgvResults.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(32, 32, 42);

            // Sütunları ekle
            SetupDataGridColumns();

            splitContainer.Panel1.Controls.Add(_dgvResults);
            splitContainer.Panel1.Controls.Add(lblResultsHeader);

            // ── Alt Panel: RichTextBox (Log) ──────────────────────────────────
            var lblLogHeader = new Label
            {
                Text      = "İşlem Logu",
                Dock      = DockStyle.Top,
                Height    = 26,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 255),
                BackColor = Color.FromArgb(22, 22, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            };

            _rtbLog = new RichTextBox
            {
                Dock      = DockStyle.Fill,
                ReadOnly  = true,
                BackColor = Color.FromArgb(12, 12, 18),
                ForeColor = Color.FromArgb(200, 200, 210),
                Font      = new Font("Cascadia Code", 9, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                ScrollBars  = RichTextBoxScrollBars.Vertical
            };

            splitContainer.Panel2.Controls.Add(_rtbLog);
            splitContainer.Panel2.Controls.Add(lblLogHeader);

            // ── Status Bar ────────────────────────────────────────────────────
            _statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(15, 15, 20),
                ForeColor = Color.White,
                SizingGrip = false
            };

            _lblStatus = new ToolStripLabel
            {
                Text      = "Hazır — Tarama başlatmak için butona tıklayın.",
                ForeColor = Color.FromArgb(130, 200, 130)
            };

            _progressBar = new ToolStripProgressBar
            {
                Visible = false,
                Size    = new Size(200, 16),
                Style   = ProgressBarStyle.Marquee
            };

            _lblSafelistPath = new Label
            {
                Location  = new Point(0, 0), // Status strip tarafından konumlandırılacak
                AutoSize  = true,
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(80, 80, 90)
            };

            var safelistStatusLabel = new ToolStripLabel
            {
                ForeColor   = Color.FromArgb(80, 80, 90),
                Alignment   = ToolStripItemAlignment.Right
            };

            _statusStrip.Items.AddRange(new ToolStripItem[]
            {
                _lblStatus, _progressBar,
                new ToolStripSeparator(), safelistStatusLabel
            });

            // Forma kontrol ekle (sıralama önemli: Dock sırası)
            this.Controls.Add(splitContainer);    // Fill → ortada
            this.Controls.Add(pnlToolbar);        // Top → araç çubuğu
            this.Controls.Add(_pnlHeader);        // Top → başlık (en üstte kalır)
            this.Controls.Add(_statusStrip);      // Bottom → durum çubuğu

            // Status strip'in safelist yolunu göster
            this.Shown += (s, e) =>
            {
                safelistStatusLabel.Text = $"Safelist: {_safelistManager.SafelistFilePath}";
            };
        }

        // ── DataGridView Sütun Tanımları ───────────────────────────────────────

        private void SetupDataGridColumns()
        {
            _dgvResults.Columns.AddRange(
                new DataGridViewTextBoxColumn
                {
                    Name       = "colName",
                    HeaderText = "Kayıt Adı",
                    FillWeight = 20
                },
                new DataGridViewTextBoxColumn
                {
                    Name       = "colPath",
                    HeaderText = "Dosya Yolu",
                    FillWeight = 45
                },
                new DataGridViewTextBoxColumn
                {
                    Name       = "colStatus",
                    HeaderText = "Durum",
                    FillWeight = 20
                },
                new DataGridViewTextBoxColumn
                {
                    Name       = "colDetail",
                    HeaderText = "Detay",
                    FillWeight = 15
                }
            );
        }

        // ── Event Handler: Virüs Test Kaydı Oluştur ───────────────────────────

        private async void BtnCreateVirus_Click(object? sender, EventArgs e)
        {
            _btnCreateVirus.Enabled = false;
            AppendLog("🦠 Test virüs kaydı oluşturuluyor...", Color.FromArgb(255, 140, 0));

            try
            {
                string exePath = await _virusSimulator.CreateAndRegisterTestVirusAsync();
                AppendLog($"✅ Test dosyası oluşturuldu: {exePath}", Color.FromArgb(100, 220, 100));
                AppendLog($"✅ Registry'e eklendi: [{VirusSimulator.TestRegistryName}]", Color.FromArgb(100, 220, 100));
                AppendLog("ℹ️  Şimdi 'Sistem Taraması Yap' ile tespit edilip edilmediğini kontrol edebilirsiniz.", Color.FromArgb(180, 180, 180));

                SetStatus("Test virüs kaydı başarıyla oluşturuldu.", Color.FromArgb(255, 140, 0));
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Hata: {ex.Message}", Color.FromArgb(220, 80, 80));
                MessageBox.Show(
                    $"Test kaydı oluşturulamadı:\n\n{ex.Message}",
                    "Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _btnCreateVirus.Enabled = true;
            }
        }

        // ── Event Handler: Sistem Taraması Başlat ─────────────────────────────

        private async void BtnStartScan_Click(object? sender, EventArgs e)
        {
            // UI'ı tarama moduna al
            SetScanningState(isScanning: true);
            _dgvResults.Rows.Clear();

            AppendLog("═══════════════════════════════════════", Color.FromArgb(60, 60, 80));
            AppendLog($"🚀 Tarama başladı: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", Color.FromArgb(0, 180, 255));
            AppendLog("═══════════════════════════════════════", Color.FromArgb(60, 60, 80));

            // İptal token'ı oluştur
            _scanCts = new CancellationTokenSource();

            // Progress reporter: UI thread'inde çalışır (SynchronizationContext)
            var progress = new Progress<string>(message =>
            {
                AppendLog(message, GetLogColor(message));
                SetStatus(message, Color.FromArgb(180, 180, 200));
            });

            try
            {
                // Taramayı başlat
                List<ScanResult> results = await _scanEngine.RunScanAsync(
                    progress,
                    SuspiciousEntryCallbackAsync,  // UI callback'i
                    _scanCts.Token
                );

                // Sonuçları DataGridView'e ekle
                DisplayResults(results);

                AppendLog("═══════════════════════════════════════", Color.FromArgb(60, 60, 80));
                AppendLog($"✅ Tarama tamamlandı: {results.Count} kayıt işlendi.", Color.FromArgb(100, 220, 100));
                SetStatus($"Tarama tamamlandı — {results.Count} kayıt işlendi.", Color.FromArgb(100, 220, 100));
            }
            catch (OperationCanceledException)
            {
                AppendLog("⏹ Tarama kullanıcı tarafından iptal edildi.", Color.FromArgb(220, 180, 0));
                SetStatus("Tarama iptal edildi.", Color.FromArgb(220, 180, 0));
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Kritik hata: {ex.Message}", Color.FromArgb(220, 80, 80));
                MessageBox.Show(
                    $"Tarama sırasında hata oluştu:\n\n{ex.Message}",
                    "Tarama Hatası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                SetStatus("Tarama hatayla sonuçlandı.", Color.FromArgb(220, 80, 80));
            }
            finally
            {
                _scanCts?.Dispose();
                _scanCts = null;
                SetScanningState(isScanning: false);
            }
        }

        // ── Event Handler: Taramayı İptal Et ──────────────────────────────────

        private void BtnCancelScan_Click(object? sender, EventArgs e)
        {
            _scanCts?.Cancel();
            AppendLog("⏹ İptal isteği gönderildi...", Color.FromArgb(220, 180, 0));
        }

        // ── Kullanıcı Karar Callback'i ─────────────────────────────────────────

        /// <summary>
        /// ScanEngine'den şüpheli kayıt için çağrılan callback.
        /// UI thread'inde diyalog açarak kullanıcıdan karar alır.
        /// </summary>
        private Task<bool> SuspiciousEntryCallbackAsync(string registryName, string filePath)
        {
            bool userSaidSafe = false;

            // Invoke kullanarak UI thread'inde güvenli şekilde çalıştırıyoruz
            this.Invoke(() =>
            {
                using var dialog = new SuspiciousEntryDialog(registryName, filePath);

                // 1. ADIM: Sahiplik ilişkisini netleştiriyoruz
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.Owner = this;

                // 2. ADIM: Ana formu değil, AÇILAN DİYALOGU en üste getiriyoruz!
                dialog.TopMost = true;

                // 3. ADIM: Diyalog açıldığında direkt odaklanmasını sağlıyoruz
                dialog.Shown += (s, e) => {
                    dialog.Activate();
                    dialog.Focus();
                };

                // Modal olarak açıyoruz (Ana formu arkada kilitler, öne geçmesini engeller)
                DialogResult result = dialog.ShowDialog(this);

                userSaidSafe = result == DialogResult.Cancel
                    ? true   // "Atla" seçilirse
                    : dialog.UserApprovedAsSafe;
            });

            return Task.FromResult(userSaidSafe);
        }


        // ── Sonuçları DataGridView'e Yaz ───────────────────────────────────────

        private void DisplayResults(List<ScanResult> results)
        {
            _dgvResults.Rows.Clear();

            foreach (var result in results)
            {
                string statusIcon = result.Status switch
                {
                    SecurityStatus.TrustedByCertificate => "✅ Sertifika Güvenli",
                    SecurityStatus.TrustedBySafelist    => "✅ Whitelist'te",
                    SecurityStatus.ApprovedByUser       => "👍 Kullanıcı Onayı",
                    SecurityStatus.RemovedByUser        => "🗑️ Temizlendi",
                    SecurityStatus.FileNotFound         => "⚠️ Dosya Yok",
                    SecurityStatus.Error                => "❌ Hata",
                    _                                   => "?"
                };

                string detail = result.Status switch
                {
                    SecurityStatus.TrustedByCertificate => result.CertificateSubject ?? "",
                    SecurityStatus.RemovedByUser        =>
                        $"Reg:{(result.RegistryDeleted ? "✓" : "✗")} Dosya:{(result.FileDeleted ? "✓" : "✗")}",
                    _ => ""
                };

                int rowIdx = _dgvResults.Rows.Add(
                    result.RegistryName,
                    result.FilePath,
                    statusIcon,
                    detail
                );

                // Duruma göre satır rengi
                Color rowColor = result.Status switch
                {
                    SecurityStatus.TrustedByCertificate => Color.FromArgb(20, 50, 30),
                    SecurityStatus.TrustedBySafelist    => Color.FromArgb(20, 45, 30),
                    SecurityStatus.ApprovedByUser       => Color.FromArgb(30, 50, 25),
                    SecurityStatus.RemovedByUser        => Color.FromArgb(60, 20, 20),
                    SecurityStatus.FileNotFound         => Color.FromArgb(50, 45, 20),
                    _                                   => Color.FromArgb(40, 25, 25)
                };

                _dgvResults.Rows[rowIdx].DefaultCellStyle.BackColor = rowColor;
            }
        }

        // ── Yardımcı UI Metotları ──────────────────────────────────────────────

        /// <summary>
        /// Log kutusuna renkli metin ekler ve otomatik kaydırır.
        /// </summary>
        private void AppendLog(string message, Color color)
        {
            // Zaten UI thread'indeyiz (Progress<T> garanti eder)
            // Ancak direkt çağrılabilmesi için kontrol ekleyelim
            if (_rtbLog.InvokeRequired)
            {
                _rtbLog.Invoke(() => AppendLog(message, color));
                return;
            }

            _rtbLog.SelectionStart  = _rtbLog.TextLength;
            _rtbLog.SelectionLength = 0;
            _rtbLog.SelectionColor  = color;
            _rtbLog.AppendText(message + "\n");
            _rtbLog.SelectionColor  = _rtbLog.ForeColor;

            // En sona scroll
            _rtbLog.ScrollToCaret();
        }

        /// <summary>
        /// Status bar metnini ve rengini günceller.
        /// </summary>
        private void SetStatus(string message, Color color)
        {
            if (_statusStrip.InvokeRequired)
            {
                _statusStrip.Invoke(() => SetStatus(message, color));
                return;
            }

            // Çok uzun mesajları kırp
            string displayMessage = message.Length > 100
                ? message.Substring(0, 97) + "..."
                : message;

            _lblStatus.Text      = displayMessage;
            _lblStatus.ForeColor = color;
        }

        /// <summary>
        /// Tarama başlarken/biterken buton durumlarını günceller.
        /// </summary>
        private void SetScanningState(bool isScanning)
        {
            _btnStartScan.Enabled    = !isScanning;
            _btnCreateVirus.Enabled  = !isScanning;
            _btnCancelScan.Enabled   = isScanning;
            _progressBar.Visible     = isScanning;

            if (isScanning)
                SetStatus("Tarama devam ediyor...", Color.FromArgb(0, 180, 255));
        }

        /// <summary>
        /// Log mesajının içeriğine göre uygun rengi döndürür.
        /// </summary>
        private static Color GetLogColor(string message) => message switch
        {
            var m when m.StartsWith("✅") => Color.FromArgb(100, 220, 100),
            var m when m.StartsWith("❌") => Color.FromArgb(220, 80, 80),
            var m when m.StartsWith("⚠️") || m.StartsWith("⚠") => Color.FromArgb(220, 180, 0),
            var m when m.StartsWith("🔐") => Color.FromArgb(100, 160, 255),
            var m when m.StartsWith("📋") => Color.FromArgb(180, 180, 255),
            var m when m.StartsWith("📂") => Color.FromArgb(200, 200, 200),
            var m when m.StartsWith("🗑") => Color.FromArgb(200, 100, 100),
            var m when m.StartsWith("🚀") => Color.FromArgb(0, 180, 255),
            var m when m.StartsWith("🏁") => Color.FromArgb(0, 220, 150),
            var m when m.Contains("═══") => Color.FromArgb(50, 50, 70),
            _                             => Color.FromArgb(180, 180, 200)
        };

        // ── Form Kapanış ───────────────────────────────────────────────────────

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Tarama devam ediyorsa sor
            if (_scanCts is not null && !_scanCts.IsCancellationRequested)
            {
                var result = MessageBox.Show(
                    "Tarama devam ediyor. Çıkmak istediğinizden emin misiniz?",
                    "Tarama Aktif",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _scanCts.Cancel();
            }

            base.OnFormClosing(e);
        }
    }
}
