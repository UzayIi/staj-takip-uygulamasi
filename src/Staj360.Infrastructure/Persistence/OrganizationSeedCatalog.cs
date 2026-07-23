using Staj360.Domain.Enums;

namespace Staj360.Infrastructure.Persistence;

/// <summary>
/// Resmî teşkilat şeması sabit kataloğu.
/// Kaynak: https://www.diyarbakir.bel.tr/teskilat-semasi
/// Çalışma zamanında internetten çekilmez; yalnızca seed için kullanılır.
/// </summary>
public static class OrganizationSeedCatalog
{
    public const string SourceUrl = "https://www.diyarbakir.bel.tr/teskilat-semasi";

    /// <summary>Demo ve varsayılan eşleme için Bilgi Teknolojileri şube kodu.</summary>
    public const string DefaultBranchCode = "BILGI_TEKNOLOJILERI";

    public sealed record UnitDef(string Code, string Name, OrganizationUnitType Type, string? ParentCode, int DisplayOrder);

    public static IReadOnlyList<UnitDef> All { get; } = Build();

    private static IReadOnlyList<UnitDef> Build()
    {
        var list = new List<UnitDef>();
        var order = 0;

        void Dir(string code, string name, params (string Code, string Name)[] branches)
        {
            order += 10;
            list.Add(new UnitDef(code, name, OrganizationUnitType.Directorate, null, order));
            var child = 0;
            foreach (var (bCode, bName) in branches)
            {
                child += 10;
                list.Add(new UnitDef(bCode, bName, OrganizationUnitType.Branch, code, child));
            }
        }

        Dir("RAYLI_SISTEMLER", "Raylı Sistemler Dairesi Başkanlığı",
            ("RAYLI_SISTEMLER_SUBE", "Raylı Sistemler Şube Müdürlüğü"),
            ("RAYLI_PROJELER", "Projeler Şube Müdürlüğü"));

        Dir("YAZI_ISLERI_KARARLAR", "Yazı İşleri ve Kararlar Dairesi Başkanlığı",
            ("YAZI_ISLERI", "Yazı İşleri Şube Müdürlüğü"),
            ("ARSIV", "Arşiv Şube Müdürlüğü"),
            ("MECLIS", "Meclis Şube Müdürlüğü"),
            ("ENCUMEN", "Encümen Şube Müdürlüğü"));

        Dir("ISLETME_ISTIRAKLER", "İşletme ve İştirakler Dairesi Başkanlığı",
            ("ISTIRAKLER", "İştirakler Şube Müdürlüğü"),
            ("OTOGAR_OTOPARK", "Otogar ve Otopark Şube Müdürlüğü"),
            ("CANLI_HAYVAN_MEZBAHANE", "Canlı Hayvan Borsası ve Mezbahane Şube Müdürlüğü"),
            ("EMLAK", "Emlak Şube Müdürlüğü"));

        Dir("BILGI_ISLEM", "Bilgi İşlem Dairesi Başkanlığı",
            ("BILGI_TEKNOLOJILERI", "Bilgi Teknolojileri Şube Müdürlüğü"),
            ("ELEKTRONIK_HABERLESME", "Elektronik Haberleşme Sistemleri Şube Müdürlüğü"),
            ("COGRAFI_BILGI_SISTEMLERI", "Coğrafi Bilgi Sistemleri Şube Müdürlüğü"));

        Dir("INSAN_KAYNAKLARI_EGITIM", "İnsan Kaynakları ve Eğitim Dairesi Başkanlığı",
            ("INSAN_KAYNAKLARI", "İnsan Kaynakları Şube Müdürlüğü"),
            ("BORDRO_TAHAKKUK", "Bordro ve Tahakkuk Şube Müdürlüğü"),
            ("EGITIM_ISLERI", "Eğitim İşleri Şube Müdürlüğü"),
            ("HIZMET_ALIM", "Hizmet Alım İşleri Şube Müdürlüğü"));

        Dir("BASIN_YAYIN_HALKLA", "Basın Yayın ve Halkla İlişkiler Dairesi Başkanlığı",
            ("ILETISIM", "İletişim Şube Müdürlüğü"),
            ("BASIN_YAYIN", "Basın ve Yayın Şube Müdürlüğü"),
            ("HALKLA_ILISKILER", "Halkla İlişkiler Şube Müdürlüğü"),
            ("ETKINLIK_ORGANIZASYON", "Etkinlik ve Organizasyon Şube Müdürlüğü"));

        Dir("KULTUR_SANAT_SOSYAL", "Kültür, Sanat ve Sosyal İşler Dairesi Başkanlığı",
            ("KULTUR_TURIZM", "Kültür ve Turizm Şube Müdürlüğü"),
            ("TIYATRO", "Tiyatro Şube Müdürlüğü"),
            ("KUTUPHANE", "Kütüphane Şube Müdürlüğü"));

        Dir("SOSYAL_HIZMETLER", "Sosyal Hizmetler Dairesi Başkanlığı",
            ("SOSYAL_HIZMET_YARDIM", "Sosyal Hizmetler ve Yardım İşleri Şube Müdürlüğü"),
            ("COCUK", "Çocuk Şube Müdürlüğü"),
            ("MESLEKI_EGITIM", "Mesleki Eğitim Şube Müdürlüğü"));

        Dir("KADIN_AILE", "Kadın ve Aile Hizmetleri Dairesi Başkanlığı",
            ("DIKASUM", "DİKASUM Şube Müdürlüğü"),
            ("KADINA_SIDDET", "Kadına Yönelik Şiddetle Mücadele Şube Müdürlüğü"),
            ("KADIN_EKONOMI", "Kadın Ekonomisini Güçlendirme Şube Müdürlüğü"),
            ("ESITLIK_BIRIMI", "Eşitlik Birimi"));

        Dir("KIRSAL_HIZMETLER", "Kırsal Hizmetler Dairesi Başkanlığı",
            ("EKONOMIK_ARASTIRMA", "Ekonomik Araştırma ve Yatırım İşleri Şube Müdürlüğü"),
            ("TARIM_HAYVANCILIK", "Tarım ve Hayvancılığı Geliştirme Şube Müdürlüğü"),
            ("YEREL_EKONOMI", "Yerel Ekonomiyi Güçlendirme Şube Müdürlüğü"));

        Dir("IKLIM_SIFIR_ATIK", "İklim Değişikliği ve Sıfır Atık Dairesi Başkanlığı",
            ("IKLIM_DEGISIKLIGI", "İklim Değişikliği Şube Müdürlüğü"),
            ("SIFIR_ATIK", "Sıfır Atık ve Geri Kazanım Şube Müdürlüğü"),
            ("BIYOCESITLILIK", "Biyoçeşitlilik ve Su Kaynakları Şube Müdürlüğü"));

        Dir("AFET_RISK", "Afet İşleri ve Risk Yönetimi Dairesi Başkanlığı",
            ("AFET_RISK_HAZIRLIK", "Afet Risk Yönetimi ve Hazırlık Şube Müdürlüğü"),
            ("AFET_KOORDINASYON", "Afet Koordinasyon Merkezi Şube Müdürlüğü"),
            ("SIVIL_SAVUNMA", "Sivil Savunma Uzmanlığı"));

        Dir("ENGELLI_YASLI", "Engelli ve Yaşlı Hizmetleri Dairesi Başkanlığı",
            ("ENGELLILER", "Engelliler Şube Müdürlüğü"),
            ("YASLI_HIZMETLERI", "Yaşlı Hizmetleri Şube Müdürlüğü"),
            ("NADIR_KRONIK", "Nadir ve Kronik Hastalıklar Şube Müdürlüğü"));

        Dir("DESTEK_HIZMETLERI", "Destek Hizmetleri Dairesi Başkanlığı",
            ("DESTEK_IDARI", "Destek İdari İşler Şube Müdürlüğü"),
            ("SATIN_ALMA_IHALE", "Satın Alma ve İhale Şube Müdürlüğü"),
            ("TASINIR_STOK", "Taşınır Destek ve Stok Şube Müdürlüğü"));

        Dir("MALI_HIZMETLER", "Mali Hizmetler Dairesi Başkanlığı",
            ("GELIRLER", "Gelirler Şube Müdürlüğü"),
            ("MUHASEBE", "Muhasebe Şube Müdürlüğü"),
            ("BUTCE_FINANS", "Bütçe ve Finans Şube Müdürlüğü"));

        Dir("PARK_BAHCELER", "Park ve Bahçeler Dairesi Başkanlığı",
            ("PARK_BAHCE_BAKIM", "Park ve Bahçeler Bakım Şube Müdürlüğü"),
            ("PARK_BAHCE_IDARE", "Park ve Bahçeler İdare Şube Müdürlüğü"),
            ("FIDANLIK_AGACLANDIRMA", "Fidanlık ve Ağaçlandırma Şube Müdürlüğü"));

        Dir("IMAR_SEHIRCILIK", "İmar ve Şehircilik Dairesi Başkanlığı",
            ("IMAR_SEHIRCILIK_SUBE", "İmar ve Şehircilik Şube Müdürlüğü"),
            ("HARITA_KAMULASTIRMA", "Harita ve Kamulaştırma Şube Müdürlüğü"),
            ("YAPI_KONTROL", "Yapı Kontrol Şube Müdürlüğü"));

        Dir("FEN_ISLERI", "Fen İşleri Dairesi Başkanlığı",
            ("ETUT_PROJE", "Etüt Proje Şube Müdürlüğü"),
            ("YAPIM_KONTROL", "Yapım ve Kontrol Şube Müdürlüğü"));

        Dir("YOL_YAPIM", "Yol Yapım, Bakım ve Onarım Dairesi Başkanlığı",
            ("ALTYAPI_KOORDINASYON", "Altyapı Koordinasyon Şube Müdürlüğü"),
            ("KENTSEL_YOLLAR", "Kentsel Yollar Şube Müdürlüğü"),
            ("KIRSAL_YOLLAR", "Kırsal Yollar Şube Müdürlüğü"));

        Dir("GENCLIK_SPOR", "Gençlik ve Spor Hizmetleri Dairesi Başkanlığı",
            ("GENCLIK_HIZMETLERI", "Gençlik Hizmetleri Şube Müdürlüğü"),
            ("GENCLIK_IDARI", "Gençlik İdari İşler Şube Müdürlüğü"),
            ("SPOR", "Spor Şube Müdürlüğü"));

        Dir("SAGLIK_ISLERI", "Sağlık İşleri Dairesi Başkanlığı",
            ("MEZARLIK_CENAZE", "Mezarlıklar ve Cenaze Hizmetleri Şube Müdürlüğü"),
            ("HALK_SAGLIGI", "Halk Sağlığı ve Denetim Şube Müdürlüğü"),
            ("ISG", "İş Sağlığı ve Güvenliği Şube Müdürlüğü"));

        Dir("ITFAIYE", "İtfaiye Dairesi Başkanlığı",
            ("ITFAIYE_DENETIM", "İtfaiye Denetim ve Önleme Şube Müdürlüğü"),
            ("ITFAIYE_MUDAHALE", "İtfaiye Müdahale Şube Müdürlüğü"),
            ("ARAMA_KURTARMA", "Arama Kurtarma Şube Müdürlüğü"));

        Dir("CEVRE_KORUMA", "Çevre Koruma ve Kontrol Dairesi Başkanlığı",
            ("CEVRE_HAFRIYAT", "Çevre Koruma ve Hafriyat Denetim Şube Müdürlüğü"),
            ("KATI_ATIK", "Katı Atık Yönetimi Şube Müdürlüğü"),
            ("YENILENEBILIR_ENERJI", "Yenilenebilir Enerji ve Enerji Yönetimi Şube Müdürlüğü"));

        Dir("ULASIM", "Ulaşım Dairesi Başkanlığı",
            ("TOPLU_ULASIM", "Toplu Ulaşım Şube Müdürlüğü"),
            ("ULASIM_KOORDINASYON", "Ulaşım Koordinasyon Şube Müdürlüğü"),
            ("OTOBUS_ISLETME", "Otobüs İşletme Şube Müdürlüğü"));

        Dir("ZABITA", "Zabıta Dairesi Başkanlığı",
            ("ZABITA_IDARI", "Zabıta İdari İşler Şube Müdürlüğü"),
            ("ZABITA_DENETIM", "Zabıta Denetim Şube Müdürlüğü"),
            ("ZABITA_TRAFIK", "Zabıta Trafik Şube Müdürlüğü"));

        Dir("VETERINER", "Veteriner İşleri Dairesi Başkanlığı",
            ("SOKAK_HAYVANLARI", "Sokak Hayvanları Koruma, Denetim ve Veteriner Hizmetleri Şube Müdürlüğü"));

        return list;
    }
}
