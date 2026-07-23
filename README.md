# staj-takip-uygulamasi

# StajAmed – Kurumsal Stajyer Yönetim ve Takip Sistemi

ASP.NET Core MVC ile geliştirilmiş, katmanlı monolit bir stajyer yönetim uygulaması
(teknik proje adı: Staj360; kullanıcıya görünen marka: **StajAmed**).
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

## 3. Hızlı başlatma (arkadaşlar için)

```powershell
dotnet restore
dotnet build
dotnet run --project '.\src\Staj360.Web\Staj360.Web.csproj'
```

Beklenen adresler:

```text
https://localhost:7189
http://localhost:5299
```

Giriş sayfası: `/Identity/Account/Login`

SQL Server instance adınız farklıysa (ör. `SQLEXPRESS` / `SQLEXPRESS01`)
`src/Staj360.Web/appsettings.Development.json` içindeki `ConnectionStrings:DefaultConnection`
değerini kendi makinenize göre güncelleyin veya ortam değişkeni kullanın:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost\\SQLEXPRESS;Database=Staj360Db;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

## 4. Demo giriş bilgileri

Aşağıdaki hesaplar **yalnızca Development / demo** ortamı içindir.
`appsettings.Development.json` içinde `Seed:SampleData=true` ve `Seed:DemoPassword`
tanımlıysa uygulama açılışında otomatik oluşturulur/güncellenir.

| Rol | E-posta |
|-----|---------|
| SuperAdmin | `superadmin@gmail.com` |
| Admin | `admin@gmail.com` |
| Manager (Yönetici) | `yonetici@gmail.com` |
| Mentor (Danışman) | `danisman@gmail.com` |
| Intern (Stajyer) | `stajyer@gmail.com` |

**Ortak demo parola:** `baris123`

> Bu parola kasıtlı olarak paylaşılan bir demo parolasıdır.
> **Production ortamında kullanmayın.** Production’da `Seed:SampleData` kapalı kalmalı
> ve demo parola yapılandırması eklenmemelidir.

Ek User Secrets komutu gerekmez; Development’ta klonlayıp çalıştırmanız yeterlidir.

## 5. Paketleri geri yükleme

```powershell
dotnet restore
```

veya solution dosyası ile:

```powershell
dotnet restore Staj360.slnx
```

## 6. Migration

Uygulama Development ortamında başlarken `MigrateAsync` çalıştırır.
İsteğe bağlı manuel uygulama:

```powershell
dotnet ef database update --project src/Staj360.Infrastructure --startup-project src/Staj360.Web
```

## 7. İlk SuperAdmin (isteğe bağlı, demo dışı)

Demo hesapları kullanmıyorsanız kendi SuperAdmin hesabınızı ortam değişkeniyle oluşturabilirsiniz:

```powershell
$env:SEED_ADMIN_EMAIL = "admin@ornek.local"
$env:SEED_ADMIN_PASSWORD = "KULLANICININ_KENDI_GUCLU_PAROLASI"
```

## 8. Testleri çalıştırma

```powershell
dotnet test
```

Unit ve integration testler gerçek OpenAI çağrısı yapmaz.

## 9. OpenAI entegrasyonu (isteğe bağlı)

Yapay zekâ günlük rapor özeti kapalı gelebilir. API anahtarını **kaynak koda yazmayın**.

```powershell
$env:OPENAI_API_KEY = "KULLANICININ_KENDI_OPENAI_ANAHTARI"
```

`appsettings.json` içinde `OpenAI:Enabled` varsayılan olarak `false`dır.
Ayrıntılar: `docs/AI_SUMMARY.md`.

## 10. Roller

| Rol | Özet |
|-----|------|
| SuperAdmin | Tüm sistem |
| Admin | Hesap, sabit teşkilat atamaları, rapor görünümü, AI özeti; idari izin onaylayamaz |
| Manager (Yönetici) | Atandığı müdürlükler; izin onayı; stajyer transferi |
| Mentor (Danışman) | Atanmış stajyerler, rapor onay, proje/görev, değerlendirme |
| Intern | Kendi devamı, raporu, görevleri, izinleri |

Organizasyon birimleri sabittir (CRUD yok). Kaynak: [docs/ORGANIZATION.md](docs/ORGANIZATION.md).

## 11. Proje klasör yapısı

```
Staj360.slnx
├── src/Staj360.Domain
├── src/Staj360.Application
├── src/Staj360.Infrastructure
├── src/Staj360.Web
├── tests/Staj360.UnitTests
├── tests/Staj360.IntegrationTests
└── docs/
```

## Güvenlik notları

- Production’da güçlü Identity parola kuralları geçerlidir; Development’ta demo parola için gevşetilir
- Hassas alanlar (T.C., gerçek API anahtarı) log/AI/audit’e yazılmaz
- Production’da demo seed çalışmaz (`Seed:SampleData` / Development şartı)
- `appsettings.Local.json` ve `.env` dosyaları Git’e dahil edilmez

## Sık karşılaşılan hatalar

| Belirti | Çözüm |
|---------|--------|
| SDK bulunamadı | .NET 10 kurun; `dotnet --list-sdks` |
| SQL bağlantı hatası | Instance adını ve connection string’i kontrol edin |
| Demo hesap yok | Development + `Seed:SampleData=true` + `Seed:DemoPassword` |
| AI butonu kapalı | `OpenAI:Enabled=true` + `OPENAI_API_KEY` |

## İlgili dokümanlar

- `docs/PROJECT_PLAN.md` – mimari plan
- `docs/DATABASE.md` – şema ve kurallar
- `docs/BUSINESS_RULES.md` – iş kuralları
- `docs/AI_SUMMARY.md` – yapay zekâ modülü
- `docs/ORGANIZATION.md` – teşkilat yapısı
