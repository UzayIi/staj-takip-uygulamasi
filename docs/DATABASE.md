# StajAmed – Veritabanı

## Motor

Microsoft SQL Server. Geliştirme instance: `localhost\SQLEXPRESS01`, veritabanı: `Staj360Db`.

Connection string yalnızca yapılandırma / ortam değişkeninden okunur; koda gömülmez.

## Migration

- Assembly: `Staj360.Infrastructure`
- İlk migration: `20260715142526_InitialCreate`
- Snapshot: `Migrations/AppDbContextModelSnapshot.cs`

```powershell
dotnet ef database update --project src/Staj360.Infrastructure --startup-project src/Staj360.Web
```

## Ana tablolar

Identity (`AspNetUsers`, `AspNetRoles`, …) + domain:

Department, InternProfile, InternshipPeriod, WorkSchedule, AttendanceDay, AttendanceEvent,
AttendanceCorrectionRequest, DailyReport, DailyWorkItem, Project, ProjectAssignment,
ProjectTask, LeaveRequest, Evaluation, Announcement, Notification, AuditLog, AiReportSummary.

## Önemli kısıtlar

| Kural | Uygulama |
|-------|----------|
| Dönem+tarih tek AttendanceDay | Unique index |
| Dönem+tarih tek DailyReport | Unique index |
| Aktif proje ataması tekil | Filtered unique index |
| ProgressPercentage 0–100 | Check constraint |
| Değerlendirme puanları 1–5 | Check constraint |
| DailyWorkItem.DurationMinutes > 0 ve üst sınır | Check + servis |
| Soft delete | `IsDeleted` + global query filter |
| Optimistic concurrency | `RowVersion` (rowversion) |
| Enum saklama | string conversion |

## Silme davranışı

Çoğu ilişkide `Restrict` / `NoAction` (yanlışlıkla zincirleme silmeyi önlemek için).
AttendanceEvent → AttendanceDay için `Cascade` (gün silinirse olaylar silinir; soft delete tercih edilir).

## Saat dilimi

DB’de anlık zamanlar UTC. UI’da `Europe/Istanbul` (`Organization:TimeZone`).
İş günü yerel `DateOnly` ile belirlenir.
