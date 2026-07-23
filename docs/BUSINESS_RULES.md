# StajAmed – İş Kuralları

## Devam (Attendance)

- Aktif staj dönemi yoksa giriş/çıkış yapılamaz.
- Çalışma günü değilse veya onaylı izin varsa giriş reddedilir (açıklayıcı kod/mesaj).
- Açık CheckIn varken tekrar CheckIn yasak; CheckIn yokken CheckOut yasak.
- Bir günde birden fazla CheckIn/CheckOut çifti desteklenir; toplam süre eşleştirilmiş çiftlerden hesaplanır.
- Geç kalma: mesai başlangıcı + tolerans (`GracePeriodMinutes`).
- Açık CheckIn varsa gün `IsIncomplete`.
- Zaman sunucudan (`IClock`) alınır; stajyer zamanı doğrudan düzenleyemez.
- İlk sürüm kaynağı: `AttendanceSource.WebButton`.

## Günlük rapor

Durumlar: Draft → Submitted → (RevisionRequested | Approved | Rejected).

- Stajyer yalnızca kendi raporunu oluşturur; günde tek rapor.
- Draft / RevisionRequested düzenlenebilir; Submitted / Approved düzenlenemez.
- Göndermek için en az bir `DailyWorkItem` gerekir; süreler sunucuda doğrulanır.
- Mentor yalnızca kendi stajyerinin raporunu inceler; düzeltme/red için yorum zorunlu.
- Onay/düzeltme/red AuditLog’a yazılır.

## Proje / görev

- Admin veya Mentor proje oluşturabilir; Mentor kendi projelerini yönetir.
- Stajyer yalnızca kendisine atanmış görevin durumunu günceller.
- Görev `Done` olunca `CompletedAtUtc` set edilir.
- İlerleme oranı 0–100.

## İzin

- Başlangıç ≤ bitiş; çakışan Pending/Approved izinlerde uyarı (kayıt engellenmez).
- Admin tüm talepleri, Mentor kendi stajyerininkini onaylayabilir.
- Onaylı izin gününde giriş engellenir.

## Değerlendirme

- Mentor yalnızca kendi stajyerini değerlendirir; puanlar 1–5; ortalama hesaplanır.
- Yapay zekâ özeti doğrudan puana dönüştürülmez.

## Yetkilendirme

Rol kontrolü menü + Controller policy + application service (kaynak bazlı) seviyesindedir.
Intern başka stajyerin verisine erişemez.
