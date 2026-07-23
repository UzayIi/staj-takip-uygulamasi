# StajAmed – Yapay Zekâ Rapor Özeti

## Amaç

Onaylanmış (`Approved`) günlük raporlardan haftalık / aylık / özel aralık / staj sonu
yapılandırılmış özet üretmek. Modül `IReportSummaryService` / `IAiProvider` /
`IReportSummaryPromptBuilder` ile değiştirilebilir.

## API anahtarı

Öncelik: ortam değişkeni `OPENAI_API_KEY`, ardından User Secrets / yapılandırma
`OpenAI:ApiKey`.

```powershell
$env:OPENAI_API_KEY = "YOUR_API_KEY"
# veya
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_API_KEY" --project src/Staj360.Web
```

Anahtar **appsettings.json / Git / log / veritabanına yazılmaz**.

## Model

`OpenAI:Model` (varsayılan örnek: `gpt-5.6-luna`). Kod içine sabitlenmez.

## Etkinleştirme

```json
"OpenAI": {
  "Enabled": true,
  "Model": "gpt-5.6-luna",
  "PromptVersion": "v1",
  "TimeoutSeconds": 60,
  "MaxInputCharacters": 30000
}
```

`Enabled=false` veya anahtar yoksa `NullReportSummaryService` kaydedilir; uygulama çökmez,
UI’da buton kapalı / uyarı gösterilir.

## API’ye gönderilenler

- Seçilen aralıktaki **Approved** günlük raporlar
- Çalışma başlıkları, açıklamalar, teknolojiler, sorunlar, çözümler, sonuçlar, yarın planı

## Gönderilmeyenler

T.C. kimlik, telefon, e-posta, adres, acil durum kişisi, parola/token, IP, izin/sağlık
belgesi içeriği ve gereksiz kişisel bilgiler.

## Davranış

- Responses API + Structured Outputs (JSON Schema)
- Mümkünse `store: false`
- Timeout, kontrollü retry, kullanıcı dostu hata mesajı
- `InputHash` ile aynı girdi için önceki başarılı özet yeniden kullanılır
- Başarısızlıkta `Status=Failed` + güvenli `FailureReason`
- UI uyarısı: nihai değerlendirmenin danışman/kuruma ait olduğu

## Örnek structured çıktı

```json
{
  "executiveSummary": "Hafta boyunca API ve raporlama çalışmaları tamamlandı.",
  "completedWork": ["REST uçları", "Rapor gönderimi"],
  "technologies": ["C#", "EF Core"],
  "problemsAndSolutions": [
    { "problem": "Bağımlılık çakışması", "solution": "Sürüm yükseltildi" }
  ],
  "risksOrBlockers": ["Raporlarda belirtilmemiştir"],
  "suggestedNextSteps": ["Entegrasyon testlerini genişlet"]
}
```

## Test

`FakeAiProvider` / `FakeReportSummaryService` kullanılır; gerçek OpenAI çağrısı yapılmaz.
