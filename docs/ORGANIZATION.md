# Organizasyon Yapısı

## Kaynak

Resmî teşkilat şeması (yalnızca geliştirme referansı; çalışma zamanında internetten çekilmez):

https://www.diyarbakir.bel.tr/teskilat-semasi

Uygulama, bu sayfadaki **daire başkanlıkları** ve altlarındaki **şube müdürlükleri / birimleri** sabit `OrganizationUnit` seed verisi olarak kullanır.

## Kapsam dışı

Aşağıdakiler organizasyon birimi olarak **eklenmez**:

- Belediye Meclisi, eşbaşkanlar, kişilerin adları
- Genel sekreter yardımcıları, danışmanlar, encümen
- Hukuk Müşavirliği, Özel Kalem, İç Denetim, UKOME, AYKOME, DİSKİ Genel Müdürlüğü vb. bağımsız birimler

## Model

| Alan | Açıklama |
|------|----------|
| `Code` | Idempotent seed anahtarı (ör. `PARK_BAHCE_BAKIM`) |
| `Name` | Resmî Türkçe ad |
| `UnitType` | `Directorate` (daire) / `Branch` (şube) |
| `ParentId` | Şubeler için daire Id |
| `DisplayOrder` | Ağaç sırası |

## CRUD yasağı

Organizasyon birimleri hiçbir rol tarafından eklenemez, silinemez, yeniden adlandırılamaz veya taşınamaz.
Admin yalnızca kullanıcıları bu sabit birimlere atayabilir.

## Migration notu

Eski `Department` kayıtları kaldırılırken mevcut stajyer/proje satırları silinmez.
Eşleştirilemeyen birimler varsayılan olarak `BILGI_TEKNOLOJILERI` (Bilgi Teknolojileri Şube Müdürlüğü) koduna map edilir.
