# staj-takip-uygulamasi

# Staj360 – Kurumsal Stajyer Yönetim ve Takip Sistemi

ASP.NET Core MVC ile geliştirilmiş, katmanlı monolit bir stajyer yönetim uygulaması.
Devam (giriş-çıkış), günlük rapor, proje/görev, izin, değerlendirme, denetim kaydı ve
yapay zekâ destekli rapor özeti akışlarını kapsar.

GitHub deposu: [UzayIi/staj-takip-uygulamasi](https://github.com/UzayIi/staj-takip-uygulamasi)

## 1. Projeyi klonlama

```powershell
git clone https://github.com/UzayIi/staj-takip-uygulamasi.git
cd staj-takip-uygulamasi
```

## 2. Gereksinimler

- **.NET 10 SDK** (`global.json` ile uyumlu 10.x; önerilen 10.0.301+)
- **SQL Server Express** veya **Developer** sürümü
- (İsteğe bağlı) **SQL Server Management Studio (SSMS)**
- **Git**

```powershell
dotnet --version
dotnet --list-sdks
```

## 3. Paketleri geri yükleme

```powershell
dotnet restore
```

veya solution dosyası ile:

```powershell
dotnet restore Staj360.slnx
```

## 4. SQL Server connection string

Varsayılan geliştirme örneği Windows Authentication kullanır. Instance adı bilgisayara göre değişebilir
(`SQLEXPRESS`, `SQLEXPRESS01`, `MSSQLSERVER` vb.).

PowerShell ile ortam değişkeni üzerinden ayarlamanız önerilir:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost\\SQLEXPRESS;Database=Staj360Db;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Kendi instance adınız farklıysa `Server=` değerini güncelleyin. Bu ortam değişkeni,
`appsettings.Development.json` içindeki örneğin üzerine yazar.

> LocalDB kullanılmaz. Connection string içinde kullanıcı adı/parola commit etmeyin;
> SQL kimlik doğrulaması kullanıyorsanız bilgileri yalnızca ortam değişkeni veya User Secrets ile verin.

## 5. Migration uygulama

```powershell
dotnet ef database update --project src/Staj360.Infrastructure --startup-project src/Staj360.Web
```

İlk migration: `InitialCreate` (`src/Staj360.Infrastructure/Migrations`).
Uygulama Development ortamında başlarken de `MigrateAsync` çalıştırabilir.

`dotnet ef` aracı yoksa:

```powershell
dotnet tool install --global dotnet-ef
```

## 6. İlk SuperAdmin oluşturma

Sabit parola yoktur. Uygulamayı ilk kez çalıştırmadan önce kendi güçlü parolanızı tanımlayın:

```powershell
$env:SEED_ADMIN_EMAIL = "admin@ornek.local"
$env:SEED_ADMIN_PASSWORD = "KULLANICININ_KENDI_GUCLU_PAROLASI"
```

Tanımlı değilse roller seed edilir; admin hesabı oluşturulmaz.

## 7. Sentetik demo verisi (isteğe bağlı)

Yalnızca **Development** ortamında ve her iki değişken de tanımlıysa çalışır:

```powershell
$env:SEED_DEMO_DATA = "true"
$env:SEED_DEMO_PASSWORD = "KULLANICININ_KENDI_DEMO_PAROLASI"
```

Demo hesaplar `@staj360.local` e-posta alanını kullanır (ör. `ayse.yilmaz@staj360.local`).
Seeder idempotenttir; her başlatmada kayıtları çoğaltmaz. Production’da demo seed çalışmaz.

## 8. Uygulamayı çalıştırma

```powershell
dotnet run --project src/Staj360.Web
```

Konsolda yazılan URL’yi açın (ör. `http://localhost:5299`).
Giriş: `/Identity/Account/Login`

## 9. Testleri çalıştırma

```powershell
dotnet test
```

Unit ve integration testler gerçek OpenAI çağrısı yapmaz.

## 10. OpenAI entegrasyonu (isteğe bağlı)

Yapay zekâ günlük rapor özeti kapalı gelebilir. API anahtarını **kaynak koda yazmayın**.

```powershell
$env:OPENAI_API_KEY = "KULLANICININ_KENDI_OPENAI_ANAHTARI"
# veya
dotnet user-secrets set "OpenAI:ApiKey" "KULLANICININ_KENDI_OPENAI_ANAHTARI" --project src/Staj360.Web
```

`appsettings.json` içinde `OpenAI:Enabled` varsayılan olarak `false`dır.
Anahtar yoksa uygulama çökmez; özet butonu devre dışı kalır veya uyarı gösterir.
Ayrıntılar: `docs/AI_SUMMARY.md`.

## 11. Roller

| Rol | Özet |
|-----|------|
| SuperAdmin | Tüm sistem |
| Admin | Hesap, departman, dönem, rapor görünümü, AI özeti |
| Mentor | Atanmış stajyerler, rapor onay, proje/görev, değerlendirme |
| Intern | Kendi devamı, raporu, görevleri, izinleri |

Herkese açık kayıt yoktur.

## 12. Proje klasör yapısı ve temel modüller

```
Staj360.slnx
├── src/Staj360.Domain          # Entity, enum, domain kuralları
├── src/Staj360.Application     # Servis arayüzleri, DTO, iş kuralları
├── src/Staj360.Infrastructure  # EF Core, Identity, seeder, AI sağlayıcıları
├── src/Staj360.Web             # MVC Areas (Admin/Mentor/Intern/Identity), Razor, wwwroot
├── tests/Staj360.UnitTests
├── tests/Staj360.IntegrationTests
└── docs/
```

**Temel modüller:** devam (attendance), günlük rapor, AI özeti, proje/görev, izin,
değerlendirme, duyuru, denetim kaydı, admin stajyer yönetimi, Development demo seeder.

## Güvenlik notları

- Güçlü parola, hesap kilitleme, güvenli cookie, HTTPS, CSRF
- Hassas alanlar (T.C., parola, API anahtarı) log/AI/audit’e yazılmaz
- Production’da `SEED_DEMO_*` ve gerçek secret değerleri tanımlamayın
- `appsettings.Local.json` ve `.env` dosyaları Git’e dahil edilmez

## Sık karşılaşılan hatalar

| Belirti | Çözüm |
|---------|--------|
| SDK bulunamadı | .NET 10 kurun; `dotnet --list-sdks` |
| SQL bağlantı hatası | Instance adını ve `ConnectionStrings__DefaultConnection` değerini kontrol edin |
| Admin yok | `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` tanımlayın |
| AI butonu kapalı | `OpenAI:Enabled=true` + `OPENAI_API_KEY` veya User Secrets |

## İlgili dokümanlar

- `docs/PROJECT_PLAN.md` – mimari plan
- `docs/DATABASE.md` – şema ve kurallar
- `docs/BUSINESS_RULES.md` – iş kuralları
- `docs/AI_SUMMARY.md` – yapay zekâ modülü
