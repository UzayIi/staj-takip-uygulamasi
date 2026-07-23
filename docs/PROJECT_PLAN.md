# StajAmed – Kurumsal Stajyer Yönetim ve Takip Sistemi

## 1. Amaç ve Kapsam

Staj360, bir kurumdaki stajyerlerin tüm staj sürecini (kimlik/rol yönetimi, devam
takibi, günlük raporlama, proje/görev yönetimi, izin, değerlendirme ve yapay zekâ
destekli özetleme) tek bir kurumsal panel üzerinden yönetmek için geliştirilen,
modüler katmanlı bir **ASP.NET Core MVC monoliti**dir.

Bu doküman uygulamanın mimari planını ve geliştirme aşamalarını tanımlar.

## 2. Teknoloji Yığını

| Alan | Teknoloji |
|------|-----------|
| Runtime / SDK | .NET 10 LTS (10.0.301) |
| Dil | C# 14 (nullable enabled) |
| Web | ASP.NET Core MVC + Razor Views |
| Kimlik | ASP.NET Core Identity |
| ORM | Entity Framework Core 10 |
| Veritabanı | Microsoft SQL Server (`localhost\SQLEXPRESS01` geliştirme) |
| UI | Bootstrap 5 + Bootstrap Icons + vanilla JS |
| Test | xUnit + EF Core InMemory/Sqlite |
| Loglama | ILogger (built-in) |
| Yapay Zekâ | OpenAI resmî .NET SDK, Responses API, Structured Outputs |
| Doğrulama | DataAnnotations + server-side service doğrulaması |

Ayrı SPA (React/Angular/Vue/Flutter) yok. Mikroservis yok. Docker/K8s/Redis yok.

## 3. Solution ve Katman Yapısı

```
Staj360.sln
├── src/
│   ├── Staj360.Domain          → Entity, Enum, ortak sözleşmeler (bağımsız)
│   ├── Staj360.Application      → İş kuralları, servisler, DTO/ViewModel, arayüzler (yalnızca Domain'e bağlı)
│   ├── Staj360.Infrastructure   → EF Core DbContext, Identity, migration, servis implementasyonları, AI provider (Application + Domain)
│   └── Staj360.Web              → MVC, Area, Controller, View, Program.cs (Application + Infrastructure)
├── tests/
│   ├── Staj360.UnitTests        → Servis iş kuralı testleri
│   └── Staj360.IntegrationTests → Authorization / uçtan uca senaryo testleri
└── docs/
    ├── PROJECT_PLAN.md
    ├── DATABASE.md
    ├── BUSINESS_RULES.md
    └── AI_SUMMARY.md
```

**Bağımlılık kuralları**
- Domain hiçbir projeye bağımlı değildir; Identity'ye bağımlı değildir.
- Application yalnızca Domain'e bağımlıdır.
- Infrastructure, Application + Domain'e bağımlıdır (Identity burada).
- Web, Application + Infrastructure'ı kullanır.
- Domain'de kullanıcı bağlantıları `Guid UserId` ile tutulur (Identity referansı yok).
- Generic Repository / Unit of Work kurulmaz; EF Core DbContext doğrudan servislerde kullanılır. İş mantığı application service'lerde.

## 4. Roller ve Yetkiler

`SuperAdmin`, `Admin`, `Mentor`, `Intern`. Rol adları `AppRoles` sabit sınıfında.
Yetkilendirme hem controller/action seviyesinde (`[Authorize(Roles=...)]` / policy)
hem de application service seviyesinde (resource-based, örn. mentor yalnızca kendi
stajyerini görebilir) uygulanır. Herkese açık kayıt ekranı yoktur; hesapları
Admin/SuperAdmin oluşturur.

## 5. Domain Modeli (özet)

Ortak alanlar (uygun entity'lerde): `Id (Guid)`, `CreatedAtUtc`, `CreatedByUserId`,
`UpdatedAtUtc`, `UpdatedByUserId`, `IsDeleted`, `RowVersion`.

Ana varlıklar: Department, InternProfile, InternshipPeriod, WorkSchedule,
AttendanceDay, AttendanceEvent, AttendanceCorrectionRequest, DailyReport,
DailyWorkItem, Project, ProjectAssignment, ProjectTask, LeaveRequest, Evaluation,
Announcement, Notification, AuditLog, AiReportSummary.

Enum'lar EF Core `HasConversion<string>()` ile okunur biçimde saklanır; UI'da Türkçe
karşılıkları gösterilir.

Detaylar `DATABASE.md` ve `BUSINESS_RULES.md` içinde.

## 6. Tarih/Saat

- DB'de her şey UTC. `IClock` (`SystemClock`) servisi üzerinden zaman alınır;
  doğrudan `DateTime.Now/UtcNow` kullanılmaz.
- UI'da `Europe/Istanbul` diliminde gösterilir (`ITimeZoneService`).
- İş günü yerel tarihle belirlenir. Giriş/çıkış zamanı sunucudan alınır.

## 7. Ana İş Kuralları (özet)

- **Attendance:** aktif dönem zorunlu, çalışma günü/izin kontrolü, açık CheckIn
  varken tekrar CheckIn yasak, CheckIn'siz CheckOut yasak, çoklu çift desteği, ilk
  giriş/son çıkış/toplam süre hesabı, geç kalma & eksik çıkış, stajyer doğrudan
  düzenleyemez (yalnızca düzeltme talebi), admin düzeltmesi AuditLog'a yazılır,
  idempotency koruması.
- **DailyReport:** Draft→Submitted→(RevisionRequested|Approved|Rejected). Stajyer
  kendi raporunu, günde tek rapor, en az bir DailyWorkItem, mentor yalnızca kendi
  stajyerini, onaylanan rapor kilitli, düzeltme isterken açıklama zorunlu, kritik
  işlemler AuditLog'a.
- **Project/Task:** admin/mentor proje oluşturur, mentor yalnızca kendi projesini,
  stajyer yalnızca kendi görev durumunu, tamamlanınca CompletedAtUtc otomatik.
- **Leave/Evaluation:** tarih tutarlılığı, çakışma uyarısı, dosya doğrulama; puanlar
  1–5, ortalama; AI çıktısı doğrudan puana çevrilmez.
- **AI:** yalnızca Approved raporlar, hassas veri gönderilmez, InputHash ile cache,
  hata → Status=Failed + güvenli FailureReason, API yoksa uygulama çökmez.

## 8. Result Modeli & Hata Yönetimi

`ServiceResult` / `ServiceResult<T>`: Success, ErrorCode, ErrorMessage,
ValidationErrors, Data. Beklenen iş hatalarında exception fırlatılmaz; kullanıcıya
Türkçe mesaj. Beklenmeyen hatalar merkezi exception handler + ILogger ile yakalanır.

## 9. Güvenlik

Güçlü parola politikası, hesap kilitleme, güvenli cookie, HTTPS yönlendirme, CSRF
(anti-forgery), server-side validation, Razor encoding, 403/login yönlendirmeleri,
pasif kullanıcı giriş engeli, overposting'e karşı ViewModel/DTO, dosya yükleme
doğrulaması, secret'ların (connection string / API key) koddan uzak tutulması
(User Secrets + environment variables), audit log'da hassas veri tutulmaması.

## 10. Yapay Zekâ Modülü

`IReportSummaryService`, `IAiProvider`, `IReportSummaryPromptBuilder` arayüzleri;
`OpenAiReportSummaryService`, `NullReportSummaryService`, `FakeReportSummaryService`
implementasyonları. Responses API + Structured Outputs. Model `OpenAI:Model`
ayarından; API anahtarı yalnızca `OPENAI_API_KEY` env veya User Secrets. Detay
`AI_SUMMARY.md`.

## 11. Geliştirme Aşamaları

1. **İskelet:** global.json, solution, projeler, referanslar, docs. ✅
2. **Domain:** enum + entity + ortak taban. 
3. **Application:** ServiceResult, IClock, arayüzler, DTO, çekirdek servis mantığı.
4. **Infrastructure:** ApplicationUser, DbContext + Fluent config, servisler, AI
   provider'lar, seed, InitialCreate migration.
5. **Web:** Program.cs, appsettings, Area'lar, controller/view, layout, auth.
6. **Dikey senaryo:** admin→stajyer→devam→rapor→onay→AI özeti uçtan uca.
7. **Testler:** attendance, daily report, AI, authorization.
8. **Doğrulama:** restore/build/test, migration, README ve dokümanlar.

## 12. Kapsam Dışı (2. Aşama)

Grafik dashboard'ları, QR/Kiosk devam, gerçek Excel/PDF export uygulaması (altyapı
hazır bırakılır), e-posta/SMS bildirimleri, çoklu dil, gelişmiş raporlama, gerçek
dosya depolama sağlayıcısı (yerel dosya servis arayüzü hazır).
