# 🛡 RegShield — Registry Odaklı Antivirüs

Registry başlangıç kayıtlarını tarayan, dijital sertifika doğrulaması yapan ve beyaz liste yönetimi sunan WinForms antivirüs uygulaması.

---

## Proje Yapısı

```
RegShield/
├── Program.cs                     # Uygulama giriş noktası
├── MainForm.cs                    # Ana WinForms formu (UI koordinatörü)
├── app.manifest                   # UAC yönetici yetki bildirimi
├── RegShield.csproj               # Proje dosyası
│
├── Models/
│   ├── ScanResult.cs              # Tarama sonucu veri modeli
│   └── SafelistEntry.cs           # Beyaz liste girdisi modeli (JSON uyumlu)
│
├── Services/
│   ├── RegistryManager.cs         # Registry okuma/yazma/silme
│   ├── CertificateChecker.cs      # X509 dijital sertifika doğrulama
│   ├── SafelistManager.cs         # safelist.json yönetimi (System.Text.Json)
│   ├── ScanEngine.cs              # 4 adımlı tarama motoru (orkestrasyon)
│   └── VirusSimulator.cs          # Test exe oluşturma + registry simülasyonu
│
└── UI/
    └── SuspiciousEntryDialog.cs   # Özel şüpheli kayıt onay diyalogu
```

---

## Kurulum ve Çalıştırma

### Gereksinimler
- **Visual Studio 2022** veya **VS Code** + .NET SDK
- **.NET 8.0 SDK** (veya .NET 6.0 LTS)
- **Windows 10/11** (Registry API'si Windows'a özgüdür)

### Adımlar

```bash
# 1. Projeyi aç
git clone <repo-url>
cd RegShield_Antivirus

# 2. Bağımlılıkları yükle
dotnet restore

# 3. Build et
dotnet build

# 4. Çalıştır (Yönetici olarak!)
# Visual Studio: Run as Administrator
# Komut satırı: PowerShell'i Admin olarak aç
dotnet run
```

> ⚠️ **ÖNEMLI**: Uygulamayı mutlaka **Yönetici (Administrator)** olarak çalıştırın.  
> `app.manifest` dosyasındaki `requireAdministrator` ayarı bunu otomatik talep eder.

### Visual Studio'da Manifest Ayarı
1. **Project > Properties > Application** sekmesine gidin
2. **Manifest** alanında **app.manifest** seçin
3. Projeyi Rebuild edin

---

## 4 Adımlı Tarama Mantığı

```
HKCU\...\Run altındaki her kayıt için:

Adım 1: Registry'den oku (Name + FilePath)
         ↓
Adım 2: Dijital Sertifika (X509Certificate2) var mı?
         ├─ EVET, Geçerli → ✅ TrustedByCertificate (BITIR)
         └─ HAYIR ↓
Adım 3: safelist.json'da dosya yolu var mı?
         ├─ EVET → ✅ TrustedBySafelist (BITIR)
         └─ HAYIR ↓
Adım 4: Kullanıcıya sor (SuspiciousEntryDialog)
         ├─ Güvenli → safelist.json'a ekle ✅ ApprovedByUser
         └─ Tehdit  → Registry kaydını sil + dosyayı diskten sil 🗑️ RemovedByUser
```

---

## Önemli Notlar

### VirusSimulator ve Gerçek EXE Üretimi
`VirusSimulator.cs` şu an metin tabanlı sahte bir dosya oluşturur.  
Gerçek imzasız `.exe` üretimi için **Roslyn** kullanılabilir:

```csharp
// NuGet: Microsoft.CodeAnalysis.CSharp
var compilation = CSharpCompilation.Create("TestVirus")
    .WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication))
    .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
    .AddSyntaxTrees(CSharpSyntaxTree.ParseText("class P { static void Main() {} }"));

using var ms = new MemoryStream();
compilation.Emit(ms);
File.WriteAllBytes(outputPath, ms.ToArray());
```

### Sertifika Kontrolü
- Yalnızca **Authenticode imzalı** dosyalar doğrulanır
- Sertifika zinciri (CA'ya kadar) **çevrimiçi** doğrulanır
- Ağ yoksa zincir geçersiz sayılır ama sertifika bilgisi döner

### safelist.json Konumu
```
<UygulamaDizini>/safelist.json
```
Örnek içerik:
```json
[
  {
    "registryName": "MyTrustedApp",
    "filePath": "C:\\Program Files\\MyApp\\app.exe",
    "approvedAt": "2025-01-15T10:30:00Z",
    "note": "Kullanıcı tarafından onaylandı"
  }
]
```

---

## Güvenlik Uyarısı

Bu uygulama **eğitim ve araştırma** amaçlıdır. Gerçek zararlı yazılım tespiti için:
- Windows Defender veya kurumsal EDR çözümlerini kullanın
- Bu uygulama imza tabanlı tarama yapmaz, yalnızca sertifika ve whitelist kontrolü yapar
- Test virüs dosyaları gerçek zararlı kod içermez

---

## Lisans

MIT License — Eğitim amaçlı serbestçe kullanılabilir.
