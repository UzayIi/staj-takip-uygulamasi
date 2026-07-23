using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Identity;

namespace Staj360.Infrastructure.Persistence;

/// <summary>
/// Development demo seeder: kanonik giriş hesapları + isteğe bağlı parola senkronu.
/// Production'da çalışmaz. Parola/token loglanmaz. Gerçek kullanıcıları değiştirmez.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Geriye dönük uyumluluk: SuperAdmin demo e-postası.</summary>
    public const string MarkerEmail = DemoLoginAccounts.SuperAdminEmail;
    public const string DemoEmailDomain = "gmail.com";

    public static readonly string[] DemoStudentNumbers =
    [
        "DEMO-2001", "DEMO-2002", "DEMO-2003", "DEMO-2004", "DEMO-2005", "DEMO-2006",
        "DEMO-2007", "DEMO-2008", "DEMO-2009", "DEMO-2010", "DEMO-2011", "DEMO-2012"
    ];

    /// <summary>Parola senkronunda dokunulacak kanonik demo hesaplar.</summary>
    public static readonly string[] KnownDemoEmails = DemoLoginAccounts.CanonicalEmails;

    public static async Task SeedAsync(IServiceProvider services, bool isDevelopment, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        if (!isDevelopment)
        {
            logger.LogDebug("Demo seeder atlandı: ortam Development değil.");
            return;
        }

        var config = services.GetRequiredService<IConfiguration>();
        if (!config.GetValue("Seed:SampleData", false))
        {
            logger.LogInformation("Seed:SampleData kapalı. Demo giriş hesapları oluşturulmadı/güncellenmedi.");
            return;
        }

        var password = ResolveDemoPassword(config);
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Seed:DemoPassword tanımlı değil (appsettings.Development.json). " +
                "Demo hesaplar oluşturulmadı/güncellenmedi. Parola loglanmaz.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();

        // Organizasyon birimleri kanonik hesap grafiği için gerekli.
        await DbSeeder.SeedOrganizationUnitsAsync(db, logger, cancellationToken);

        var ensure = await DemoLoginAccounts.EnsureAsync(userManager, db, password, logger, cancellationToken);
        if (!ensure.Success)
        {
            logger.LogError("Demo giriş hesapları güncellenemedi: {Reason}", ensure.ConflictMessage);
            return;
        }

        logger.LogInformation(
            "Demo giriş hesapları hazır ({Count} hesap). E-postalar: {Emails}",
            ensure.UserIdsByEmail.Count,
            string.Join(", ", ensure.UserIdsByEmail.Keys.OrderBy(x => x)));
    }

    /// <summary>
    /// Development + Seed:SampleData + Seed:DemoPassword iken yalnızca kanonik demo
    /// hesapların parolasını Identity ResetPasswordAsync ile günceller.
    /// </summary>
    public static async Task ResetDemoPasswordsAsync(
        IServiceProvider services, bool isDevelopment, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        if (!isDevelopment)
        {
            logger.LogDebug("Demo parola sıfırlama atlandı: ortam Development değil.");
            return;
        }

        var config = services.GetRequiredService<IConfiguration>();
        if (!config.GetValue("Seed:SampleData", false))
        {
            logger.LogDebug("Demo parola sıfırlama atlandı: Seed:SampleData kapalı.");
            return;
        }

        var password = ResolveDemoPassword(config);
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DemoPassword tanımlı değil. Demo parolaları sıfırlanmadı.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var success = await DemoLoginAccounts.ResetPasswordsAsync(userManager, password, logger, cancellationToken);
        logger.LogInformation("Demo parola senkronu tamamlandı. Güncellenen hesap: {Success}", success);
    }

    /// <summary>
    /// Seed:DemoPassword — Development'ta appsettings.Development.json üzerinden okunur.
    /// İsteğe bağlı SEED_DEMO_PASSWORD ortam değişkeni üzerine yazabilir.
    /// </summary>
    internal static string? ResolveDemoPassword(IConfiguration config) =>
        config["Seed:DemoPassword"]
        ?? Environment.GetEnvironmentVariable("SEED_DEMO_PASSWORD")
        ?? config["SEED_DEMO_PASSWORD"];

    // --- Aşağıdaki metotlar eski zengin demo veri üretimi içindir (manuel/geri uyumluluk). ---

    /// <summary>
    /// Eski demo birim adlarını OrganizationSeedCatalog şube kodlarına bağlar.
    /// Birimleri oluşturmaz; DbSeeder / katalog seed'inin önceden çalışmış olmasını bekler.
    /// "Demo Pasif Arşiv Birimi" atlanır (katalog birimleri IsActive ile yönetilir).
    /// </summary>
    private static async Task<Dictionary<string, OrganizationUnit>> EnsureDepartmentsAsync(
        AppDbContext db, SeedStats stats, CancellationToken ct)
    {
        var nameToCode = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Yazılım Geliştirme"] = "BILGI_TEKNOLOJILERI",
            ["Bilgi İşlem"] = "BILGI_TEKNOLOJILERI",
            ["Yapay Zekâ ve Veri Analitiği"] = "ELEKTRONIK_HABERLESME",
            ["İnsan Kaynakları"] = "INSAN_KAYNAKLARI",
            ["Mali Hizmetler"] = "MUHASEBE",
            ["Basın ve Halkla İlişkiler"] = "HALKLA_ILISKILER"
        };

        var neededCodes = nameToCode.Values.Distinct().ToArray();
        var unitsByCode = await db.OrganizationUnits.AsNoTracking()
            .Where(u => neededCodes.Contains(u.Code) && !u.IsDeleted)
            .ToDictionaryAsync(u => u.Code, ct);

        var result = new Dictionary<string, OrganizationUnit>(StringComparer.Ordinal);
        foreach (var (name, code) in nameToCode)
        {
            if (!unitsByCode.TryGetValue(code, out var unit))
            {
                throw new InvalidOperationException(
                    $"Demo seed için OrganizationUnit bulunamadı: Code={code} (eski demo adı: '{name}'). " +
                    "Önce OrganizationSeedCatalog ile birimler seed edilmeli.");
            }

            result[name] = unit;
            stats.UnitsResolved++;
        }

        return result;
    }

    private static async Task<WorkSchedule> EnsureWorkScheduleAsync(AppDbContext db, SeedStats stats, CancellationToken ct)
    {
        const string name = "Demo Standart Mesai (09:00-18:00)";
        var existing = await db.WorkSchedules.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Name == name && !s.IsDeleted, ct);
        if (existing is not null)
        {
            stats.Skipped++;
            return existing;
        }

        var schedule = new WorkSchedule
        {
            Name = name,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0),
            GracePeriodMinutes = 15,
            MondayEnabled = true, TuesdayEnabled = true, WednesdayEnabled = true,
            ThursdayEnabled = true, FridayEnabled = true
        };
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync(ct);
        return schedule;
    }

    private static async Task<List<ApplicationUser>> EnsureAdminsAsync(
        UserManager<ApplicationUser> users, string password, ILogger logger, SeedStats stats)
    {
        var list = new List<ApplicationUser>();
        var defs = new[]
        {
            (MarkerEmail, "Demo Süper Admin", AppRoles.SuperAdmin),
            ("admin.yonetim@stajamed.local", "Demo Yönetici", AppRoles.Admin)
        };
        foreach (var (email, name, role) in defs)
        {
            var u = await EnsureUserAsync(users, email, name, role, password, logger, stats);
            if (u is not null) list.Add(u);
        }
        return list;
    }

    private static async Task<List<ApplicationUser>> EnsureMentorsAsync(
        UserManager<ApplicationUser> users, string password, ILogger logger, SeedStats stats)
    {
        var list = new List<ApplicationUser>();
        var defs = new[]
        {
            ("mentor.aylin@stajamed.local", "Aylin Demir"),
            ("mentor.mehmet@stajamed.local", "Mehmet Kara"),
            ("mentor.selin@stajamed.local", "Selin Aksoy"),
            ("mentor.can@stajamed.local", "Can Öztürk")
        };
        foreach (var (email, name) in defs)
        {
            var u = await EnsureUserAsync(users, email, name, AppRoles.Mentor, password, logger, stats);
            if (u is not null) list.Add(u);
        }
        return list;
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string fullName, string role,
        string password, ILogger logger, SeedStats stats)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                await userManager.UpdateAsync(existing);
            }
            if (!await userManager.IsInRoleAsync(existing, role))
                await userManager.AddToRoleAsync(existing, role);
            stats.UsersSkipped++;
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Demo kullanıcı oluşturulamadı: {Email}", email);
            return null;
        }
        await userManager.AddToRoleAsync(user, role);
        stats.UsersCreated++;
        return user;
    }

    private static async Task<List<InternBundle>> EnsureInternsAsync(
        AppDbContext db, UserManager<ApplicationUser> users, Dictionary<string, OrganizationUnit> units,
        List<ApplicationUser> mentors, WorkSchedule schedule, string password, IClock clock, ITimeZoneService tz,
        ILogger logger, SeedStats stats, CancellationToken ct)
    {
        var today = tz.LocalDate(clock.UtcNow);
        var specs = new[]
        {
            new InternSpec("Ayşe Yılmaz", "stajyer.ayse@stajamed.local", "DEMO-2001", "0555 200 1001", "Fırat Üniversitesi", "Yazılım Mühendisliği", "3", "Yazılım Geliştirme", 0, "Demo Mah. No:1 Elazığ", false),
            new InternSpec("Emir Arslan", "stajyer.emir@stajamed.local", "DEMO-2002", "0555 200 1002", "Dicle Üniversitesi", "Bilgisayar Mühendisliği", "4", "Bilgi İşlem", 1, "Demo Cad. No:2 Diyarbakır", false),
            new InternSpec("Zeynep Kaya", "stajyer.zeynep@stajamed.local", "DEMO-2003", "0555 200 1003", "İnönü Üniversitesi", "Yapay Zekâ Mühendisliği", "3", "Yapay Zekâ ve Veri Analitiği", 2, "Demo Sok. No:3 Malatya", false),
            new InternSpec("Can Aydın", "stajyer.can@stajamed.local", "DEMO-2004", "0555 200 1004", "Hacettepe Üniversitesi", "Yönetim Bilişim Sistemleri", "2", "İnsan Kaynakları", 3, "Demo Bulvar No:4 Ankara", false),
            new InternSpec("Elif Şahin", "stajyer.elif@stajamed.local", "DEMO-2005", "0555 200 1005", "ODTÜ", "Endüstri Mühendisliği", "3", "Mali Hizmetler", 0, "Demo Park No:5 Ankara", false),
            new InternSpec("Burak Tekin", "stajyer.burak@stajamed.local", "DEMO-2006", "0555 200 1006", "Gazi Üniversitesi", "Bilgisayar Mühendisliği", "4", "Yazılım Geliştirme", 1, "Demo Sit. No:6 Ankara", false),
            new InternSpec("Ceren Yılmaz", "stajyer.ceren@stajamed.local", "DEMO-2007", "0555 200 1007", "İTÜ", "Yazılım Mühendisliği", "3", "Yapay Zekâ ve Veri Analitiği", 2, "Demo Mah. No:7 İstanbul", false),
            new InternSpec("Deniz Çelik", "stajyer.deniz@stajamed.local", "DEMO-2008", "0555 200 1008", "Boğaziçi Üniversitesi", "Elektrik-Elektronik", "2", "Bilgi İşlem", 3, "Demo Cad. No:8 İstanbul", false),
            new InternSpec("Eren Korkmaz", "stajyer.eren@stajamed.local", "DEMO-2009", "0555 200 1009", "Yıldız Teknik Üniversitesi", "Makine Mühendisliği", "4", "Basın ve Halkla İlişkiler", 0, "Demo Sok. No:9 İstanbul", false),
            new InternSpec("Fatma Öztürk", "stajyer.fatma@stajamed.local", "DEMO-2010", "0555 200 1010", "Ankara Üniversitesi", "İletişim", "3", "Basın ve Halkla İlişkiler", 1, "Demo Yol No:10 Ankara", false),
            new InternSpec("Gökhan Polat", "stajyer.gokhan@stajamed.local", "DEMO-2011", "0555 200 1011", "Ege Üniversitesi", "İstatistik", "3", "Yapay Zekâ ve Veri Analitiği", 2, "Demo Liman No:11 İzmir", false),
            new InternSpec("Hale Nur", "stajyer.hale@stajamed.local", "DEMO-2012", "0555 200 1012", "Selçuk Üniversitesi", "İşletme", "2", "İnsan Kaynakları", 3, "Demo Mevlana No:12 Konya", true)
        };

        var bundles = new List<InternBundle>();
        foreach (var spec in specs)
        {
            var user = await EnsureUserAsync(users, spec.Email, spec.FullName, AppRoles.Intern, password, logger, stats);
            if (user is null) continue;

            var mentor = mentors[spec.MentorIndex % mentors.Count];
            var unitId = units[spec.DepartmentName].Id;

            var profile = await db.InternProfiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.StudentNumber == spec.StudentNumber, ct)
                ?? await db.InternProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

            if (profile is null)
            {
                profile = new InternProfile
                {
                    UserId = user.Id,
                    StudentNumber = spec.StudentNumber,
                    University = spec.University,
                    SchoolDepartment = spec.SchoolDepartment,
                    Faculty = "Demo Fakülte",
                    ClassLevel = spec.ClassLevel,
                    PhoneNumber = spec.Phone,
                    Address = spec.Address,
                    CurrentOrganizationUnitId = unitId,
                    IsActive = true,
                    EmergencyContactName = "Demo Acil Kişi",
                    EmergencyContactPhone = "0555 000 0000"
                };
                db.InternProfiles.Add(profile);
                await db.SaveChangesAsync(ct);
                stats.ProfilesCreated++;
            }
            else
            {
                if (profile.IsDeleted) { profile.IsDeleted = false; await db.SaveChangesAsync(ct); }
                if (string.IsNullOrWhiteSpace(profile.Address))
                {
                    profile.Address = spec.Address;
                    await db.SaveChangesAsync(ct);
                }
                if (profile.CurrentOrganizationUnitId == Guid.Empty)
                {
                    profile.CurrentOrganizationUnitId = unitId;
                    await db.SaveChangesAsync(ct);
                }
                stats.ProfilesSkipped++;
            }

            await EnsureInternUnitAssignmentAsync(db, profile.Id, unitId, mentor.Id, today, stats, ct);

            InternshipPeriod? primaryPeriod = null;
            var periods = new List<InternshipPeriod>();

            if (!spec.SkipPeriod)
            {
                // Aktif dönem (2026)
                primaryPeriod = await EnsurePeriodAsync(db, profile.Id, mentor.Id, schedule.Id,
                    today.AddDays(-25), today.AddDays(40), InternshipStatus.Active, 30, 8, stats, ct);
                periods.Add(primaryPeriod);

                // Bazı stajyerlere geçmiş dönem (2024/2025)
                if (spec.StudentNumber is "DEMO-2001" or "DEMO-2003" or "DEMO-2005" or "DEMO-2007")
                {
                    var past = await EnsurePeriodAsync(db, profile.Id, mentor.Id, schedule.Id,
                        new DateOnly(2025, 6, 2), new DateOnly(2025, 8, 29), InternshipStatus.Completed, 40, 40, stats, ct);
                    periods.Add(past);
                }
                if (spec.StudentNumber is "DEMO-2002" or "DEMO-2006")
                {
                    var past = await EnsurePeriodAsync(db, profile.Id, mentor.Id, schedule.Id,
                        new DateOnly(2024, 7, 1), new DateOnly(2024, 9, 13), InternshipStatus.Completed, 35, 35, stats, ct);
                    periods.Add(past);
                }
                if (spec.StudentNumber == "DEMO-2004")
                {
                    periods.Add(await EnsurePeriodAsync(db, profile.Id, mentor.Id, schedule.Id,
                        new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 28), InternshipStatus.Pending, 30, 0, stats, ct));
                }
                if (spec.StudentNumber == "DEMO-2008")
                {
                    periods.Add(await EnsurePeriodAsync(db, profile.Id, mentor.Id, schedule.Id,
                        new DateOnly(2025, 2, 3), new DateOnly(2025, 3, 14), InternshipStatus.Terminated, 20, 5, stats, ct));
                }
            }

            bundles.Add(new InternBundle(profile, user, mentor, primaryPeriod, periods, spec));
        }

        return bundles;
    }

    private static async Task EnsureInternUnitAssignmentAsync(
        AppDbContext db, Guid profileId, Guid organizationUnitId, Guid advisorUserId,
        DateOnly startDate, SeedStats stats, CancellationToken ct)
    {
        var existing = await db.InternUnitAssignments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.InternProfileId == profileId && a.IsActive && !a.IsDeleted, ct);
        if (existing is not null)
        {
            stats.Skipped++;
            return;
        }

        db.InternUnitAssignments.Add(new InternUnitAssignment
        {
            InternProfileId = profileId,
            OrganizationUnitId = organizationUnitId,
            AdvisorUserId = advisorUserId,
            StartDate = startDate,
            IsActive = true
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<InternshipPeriod> EnsurePeriodAsync(
        AppDbContext db, Guid profileId, Guid mentorId, Guid scheduleId,
        DateOnly start, DateOnly end, InternshipStatus status, int required, int completed,
        SeedStats stats, CancellationToken ct)
    {
        var existing = await db.InternshipPeriods.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.InternProfileId == profileId && p.StartDate == start && p.EndDate == end && !p.IsDeleted, ct);
        if (existing is not null)
        {
            stats.PeriodsSkipped++;
            return existing;
        }

        var period = new InternshipPeriod
        {
            InternProfileId = profileId,
            MentorUserId = mentorId,
            WorkScheduleId = scheduleId,
            StartDate = start,
            EndDate = end,
            RequiredWorkDays = required,
            CompletedWorkDays = completed,
            Status = status
        };
        db.InternshipPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        stats.PeriodsCreated++;
        return period;
    }

    private static async Task SeedProjectsAndTasksAsync(
        AppDbContext db, Dictionary<string, OrganizationUnit> units, List<ApplicationUser> mentors,
        List<InternBundle> interns, IClock clock, SeedStats stats, CancellationToken ct)
    {
        var withPeriod = interns.Where(i => i.PrimaryPeriod is not null).ToList();
        if (withPeriod.Count == 0) return;

        var projectDefs = new (string Name, string Desc, string Dept, int MentorIdx, ProjectStatus Status, int Progress, int Year, int StartMonth)[]
        {
            ("StajAmed Devam Takip Modülü", "Giriş-çıkış ve devam panellerinin demo senaryosu.", "Yazılım Geliştirme", 0, ProjectStatus.InProgress, 55, 2026, 5),
            ("Kurumsal Evrak Yönetim Sistemi", "Evrak akışı ve arşivleme (demo).", "Bilgi İşlem", 1, ProjectStatus.InProgress, 40, 2026, 3),
            ("Atık Yağ Talep Yönetimi", "Belediye atık yağ talep süreçleri (demo).", "Yazılım Geliştirme", 0, ProjectStatus.Planned, 10, 2026, 7),
            ("Belediye Mobil Uygulaması", "Vatandaş mobil uygulaması (demo).", "Yazılım Geliştirme", 2, ProjectStatus.InProgress, 65, 2025, 9),
            ("Veri Görselleştirme Paneli", "Operasyonel dashboard (demo).", "Yapay Zekâ ve Veri Analitiği", 2, ProjectStatus.Completed, 100, 2025, 2),
            ("Yapay Zekâ Destekli Rapor Özeti", "Sentetik AI özet akışları (demo).", "Yapay Zekâ ve Veri Analitiği", 3, ProjectStatus.InProgress, 50, 2026, 4),
            ("Personel İzin Takip Sistemi", "İzin talebi ve onay süreçleri (demo).", "İnsan Kaynakları", 1, ProjectStatus.Completed, 100, 2024, 6),
            ("Arşiv Belge Tarama Sistemi", "Belge tarama ve indeksleme (demo).", "Bilgi İşlem", 3, ProjectStatus.OnHold, 25, 2025, 11),
            ("Kurumsal Web Sitesi Yenileme", "Kurumsal site yenileme (demo).", "Basın ve Halkla İlişkiler", 0, ProjectStatus.InProgress, 35, 2026, 1),
            ("Vatandaş Talep Analizi", "Talep sınıflandırma analitiği (demo).", "Yapay Zekâ ve Veri Analitiği", 2, ProjectStatus.Planned, 5, 2026, 8),
            ("Bütçe İzleme Paneli", "Mali gösterge paneli (demo).", "Mali Hizmetler", 1, ProjectStatus.Completed, 100, 2024, 3),
            ("Stajyer Onboarding Portalı", "Oryantasyon içerikleri (demo).", "İnsan Kaynakları", 3, ProjectStatus.InProgress, 45, 2026, 2),
            ("Siber Güvenlik Kontrol Listesi", "Periyodik güvenlik kontrolleri (demo).", "Bilgi İşlem", 0, ProjectStatus.Cancelled, 20, 2025, 5),
            ("Sosyal Medya İçerik Takvimi", "İçerik planlama aracı (demo).", "Basın ve Halkla İlişkiler", 1, ProjectStatus.InProgress, 60, 2026, 6),
            ("Sensör Veri Toplama Servisi", "IoT veri toplama (demo).", "Yazılım Geliştirme", 2, ProjectStatus.Planned, 0, 2024, 10),
            ("Raporlama Otomasyonu", "Zamanlanmış rapor üretimi (demo).", "Mali Hizmetler", 3, ProjectStatus.Completed, 100, 2025, 7),
            ("Kimlik Doğrulama Geliştirmesi", "MFA ve oturum yönetimi (demo).", "Yazılım Geliştirme", 0, ProjectStatus.InProgress, 70, 2026, 4),
            ("İş Zekâsı Küp Tasarımı", "OLAP küpleri (demo).", "Yapay Zekâ ve Veri Analitiği", 2, ProjectStatus.OnHold, 30, 2024, 8)
        };

        var projects = new List<Project>();
        foreach (var def in projectDefs)
        {
            var start = new DateOnly(def.Year, def.StartMonth, 5);
            var end = start.AddMonths(3);
            var project = await EnsureProjectAsync(db, def.Name, def.Desc, units[def.Dept].Id,
                mentors[def.MentorIdx % mentors.Count].Id, def.Status, def.Progress, start, end, stats, ct);
            projects.Add(project);
        }

        // Atamalar: birden fazla stajyer bazı projelere
        for (var i = 0; i < Math.Min(projects.Count, withPeriod.Count); i++)
        {
            await EnsureAssignmentAsync(db, projects[i].Id, withPeriod[i].Profile.Id, "Demo geliştirici", stats, ct);
            await EnsureAssignmentAsync(db, projects[i].Id, withPeriod[(i + 3) % withPeriod.Count].Profile.Id, "Demo destek", stats, ct);
        }

        var taskTitles = new[]
        {
            "API uç noktası", "Form doğrulama", "Birim testleri", "UI bileşeni", "Dokümantasyon",
            "Veri migrasyonu", "Performans iyileştirme", "Yetkilendirme", "Rapor şablonu", "Grafik paneli",
            "Bildirim kuyruğu", "Arama filtresi", "Excel dışa aktarma", "Mobil uyumluluk", "Loglama",
            "Cache katmanı", "Seed senaryosu", "Hata sayfası", "Audit kaydı", "Dashboard kartı",
            "Cron işi", "Dosya yükleme", "Rol kontrolü", "Tarih filtresi", "Özet servisi",
            "İzin akışı", "Devam kaydı", "Proje atama", "Görev panosu", "Onay ekranı"
        };

        for (var t = 0; t < taskTitles.Length; t++)
        {
            var project = projects[t % projects.Count];
            var intern = withPeriod[t % withPeriod.Count].Profile;
            // Gecikmiş görevler: süresi geçmiş + henüz Done/Cancelled olmayan durum (ayrı enum yok).
            var overdue = t % 5 == 4;
            var status = overdue
                ? ProjectTaskStatus.InProgress
                : (t % 4) switch
                {
                    0 => ProjectTaskStatus.Todo,
                    1 => ProjectTaskStatus.InProgress,
                    2 => ProjectTaskStatus.Done,
                    _ => ProjectTaskStatus.InReview
                };
            var due = DateOnly.FromDateTime(clock.UtcNow.Date).AddDays(overdue ? -(3 + t % 5) : 7 + (t % 10));
            await EnsureTaskAsync(db, project.Id, intern.Id, $"Demo: {taskTitles[t]}", status,
                (TaskPriority)(t % 4), due, stats, ct);
        }
    }

    private static async Task<Project> EnsureProjectAsync(
        AppDbContext db, string name, string description, Guid organizationUnitId, Guid mentorId,
        ProjectStatus status, int progress, DateOnly start, DateOnly end, SeedStats stats, CancellationToken ct)
    {
        var existing = await db.Projects.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Name == name && !p.IsDeleted, ct);
        if (existing is not null)
        {
            stats.ProjectsSkipped++;
            return existing;
        }

        var project = new Project
        {
            Name = name,
            Description = description,
            StartDate = start,
            EndDate = end,
            Status = status,
            ProgressPercentage = progress,
            OrganizationUnitId = organizationUnitId,
            MentorUserId = mentorId
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        stats.ProjectsCreated++;
        return project;
    }

    private static async Task EnsureAssignmentAsync(AppDbContext db, Guid projectId, Guid internId, string role, SeedStats stats, CancellationToken ct)
    {
        if (await db.ProjectAssignments.IgnoreQueryFilters()
                .AnyAsync(a => a.ProjectId == projectId && a.InternProfileId == internId && a.IsActive && !a.IsDeleted, ct))
        {
            stats.Skipped++;
            return;
        }
        db.ProjectAssignments.Add(new ProjectAssignment
        {
            ProjectId = projectId,
            InternProfileId = internId,
            AssignedAtUtc = DateTime.UtcNow,
            RoleDescription = role,
            IsActive = true
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureTaskAsync(
        AppDbContext db, Guid projectId, Guid? internId, string title, ProjectTaskStatus status,
        TaskPriority priority, DateOnly due, SeedStats stats, CancellationToken ct)
    {
        if (await db.ProjectTasks.IgnoreQueryFilters()
                .AnyAsync(t => t.ProjectId == projectId && t.Title == title && !t.IsDeleted, ct))
        {
            stats.TasksSkipped++;
            return;
        }

        db.ProjectTasks.Add(new ProjectTask
        {
            ProjectId = projectId,
            AssignedInternProfileId = internId,
            Title = title,
            Description = $"Sentetik demo görev: {title}",
            Priority = priority,
            Status = status,
            DueDate = due,
            EstimatedMinutes = 120 + (priority == TaskPriority.High ? 120 : 0),
            CompletedAtUtc = status == ProjectTaskStatus.Done ? DateTime.UtcNow.AddDays(-1) : null
        });
        await db.SaveChangesAsync(ct);
        stats.TasksCreated++;
    }

    private static async Task SeedAttendanceAsync(
        AppDbContext db, IClock clock, ITimeZoneService tz, List<InternBundle> interns,
        WorkSchedule schedule, SeedStats stats, CancellationToken ct)
    {
        var active = interns.Where(i => i.PrimaryPeriod is not null).Take(8).ToList();
        if (active.Count == 0) return;

        var today = tz.LocalDate(clock.UtcNow);
        var workDays = Enumerable.Range(1, 45)
            .Select(i => today.AddDays(-i))
            .Where(d => d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            .Take(28)
            .OrderBy(d => d)
            .ToList();

        for (var i = 0; i < active.Count; i++)
        {
            var bundle = active[i];
            var periodId = bundle.PrimaryPeriod!.Id;
            for (var d = 0; d < workDays.Count; d++)
            {
                var day = workDays[d];
                var pattern = (i + d) % 7;
                switch (pattern)
                {
                    case 0:
                        await EnsureAttendanceDayAsync(db, tz, periodId, day, schedule, AttendanceStatus.Present, late: false, incomplete: false, early: false, stats, ct);
                        break;
                    case 1:
                        await EnsureAttendanceDayAsync(db, tz, periodId, day, schedule, AttendanceStatus.Late, late: true, incomplete: false, early: false, stats, ct);
                        break;
                    case 2:
                        await EnsureAttendanceDayAsync(db, tz, periodId, day, schedule, AttendanceStatus.Present, late: false, incomplete: false, early: true, stats, ct);
                        break;
                    case 3:
                        await EnsureAttendanceDayAsync(db, tz, periodId, day, schedule, AttendanceStatus.Incomplete, late: false, incomplete: true, early: false, stats, ct);
                        break;
                    case 4:
                        await EnsureAttendanceDayAsync(db, tz, periodId, day, schedule, AttendanceStatus.OnLeave, late: false, incomplete: false, early: false, stats, ct, leaveOnly: true);
                        break;
                    case 5:
                        await EnsureAttendanceDayAsync(db, tz, periodId, day, schedule, AttendanceStatus.Absent, late: false, incomplete: false, early: false, stats, ct, absent: true);
                        break;
                    default:
                        await EnsureAttendanceDayAsync(db, tz, periodId, day, schedule, AttendanceStatus.Present, late: false, incomplete: false, early: false, stats, ct);
                        break;
                }
            }
        }
    }

    private static async Task EnsureAttendanceDayAsync(
        AppDbContext db, ITimeZoneService tz, Guid periodId, DateOnly workDate, WorkSchedule schedule,
        AttendanceStatus status, bool late, bool incomplete, bool early, SeedStats stats, CancellationToken ct,
        bool leaveOnly = false, bool absent = false)
    {
        if (await db.AttendanceDays.IgnoreQueryFilters()
                .AnyAsync(d => d.InternshipPeriodId == periodId && d.WorkDate == workDate && !d.IsDeleted, ct))
        {
            stats.AttendanceSkipped++;
            return;
        }

        DateTime? checkInUtc = null;
        DateTime? checkOutUtc = null;
        var minutes = 0;
        var day = new AttendanceDay
        {
            InternshipPeriodId = periodId,
            WorkDate = workDate,
            Status = status,
            IsLate = late,
            IsIncomplete = incomplete,
            IsEarlyLeave = early
        };

        if (!leaveOnly && !absent)
        {
            // Gerçekçi mesai: giriş 08:00–09:30, çıkış 16:30–18:00
            var inHour = late ? 9 : 8;
            var inMin = late ? 15 + (workDate.DayNumber % 16) : workDate.DayNumber % 45; // late: 09:15–09:30
            if (!late && inMin > 45) inMin = 45;
            var outHour = early ? 16 : (17 + (workDate.DayNumber % 2)); // 16:xx early, 17–18 normal
            var outMin = early ? 30 : (workDate.DayNumber % 60);
            if (outHour < inHour || (outHour == inHour && outMin <= inMin))
            {
                outHour = inHour + 8;
                outMin = inMin;
            }

            var checkInLocal = workDate.ToDateTime(new TimeOnly(inHour, Math.Min(inMin, 59)));
            checkInUtc = tz.ToUtc(checkInLocal);
            day.FirstCheckInUtc = checkInUtc;
            day.Events.Add(new AttendanceEvent
            {
                EventType = AttendanceEventType.CheckIn,
                EventTimeUtc = checkInUtc.Value,
                Source = AttendanceSource.WebButton,
                Notes = "Demo giriş"
            });

            if (!incomplete)
            {
                var checkOutLocal = workDate.ToDateTime(new TimeOnly(outHour, Math.Min(outMin, 59)));
                checkOutUtc = tz.ToUtc(checkOutLocal);
                if (checkOutUtc <= checkInUtc) checkOutUtc = checkInUtc.Value.AddHours(8);
                day.LastCheckOutUtc = checkOutUtc;
                minutes = (int)(checkOutUtc.Value - checkInUtc.Value).TotalMinutes;
                day.Events.Add(new AttendanceEvent
                {
                    EventType = AttendanceEventType.CheckOut,
                    EventTimeUtc = checkOutUtc.Value,
                    Source = AttendanceSource.WebButton,
                    Notes = "Demo çıkış"
                });
            }
        }

        day.TotalWorkedMinutes = minutes;
        db.AttendanceDays.Add(day);
        await db.SaveChangesAsync(ct);
        stats.AttendanceCreated++;
    }

    private static async Task SeedReportsAsync(
        AppDbContext db, IClock clock, List<InternBundle> interns, List<ApplicationUser> mentors,
        SeedStats stats, CancellationToken ct)
    {
        var active = interns.Where(i => i.PrimaryPeriod is not null).ToList();
        if (active.Count == 0) return;

        var contents = new[]
        {
            ("Giriş ekranındaki form doğrulamaları düzenlendi.", "C#, ASP.NET Core", "Form validasyonu"),
            ("Entity Framework sorgularında performans iyileştirmesi yapıldı.", "EF Core, SQL Server", "Sorgu optimizasyonu"),
            ("Dashboard için grafik verileri hazırlandı.", "Chart.js, C#", "Grafik veri servisi"),
            ("Mobil ekranların responsive kontrolleri gerçekleştirildi.", "Bootstrap, CSS", "Responsive test"),
            ("Kullanıcı yetkilendirme testleri yazıldı.", "xUnit, Identity", "Yetki testleri"),
            ("Proje dokümantasyonu güncellendi.", "Markdown", "Dokümantasyon"),
            ("İzin talebi onay akışı gözden geçirildi.", "ASP.NET Core MVC", "İzin akışı"),
            ("Excel dışa aktarma senaryosu denendi.", "ClosedXML", "Excel export"),
            ("Devam kaydı edge-case senaryoları incelendi.", "C#", "Devam senaryoları"),
            ("Bildirim toast bileşeni entegre edildi.", "Bootstrap Toast", "UI bildirim")
        };

        var today = DateOnly.FromDateTime(clock.UtcNow);
        var statuses = new[]
        {
            DailyReportStatus.Draft, DailyReportStatus.Submitted, DailyReportStatus.Approved,
            DailyReportStatus.Rejected, DailyReportStatus.RevisionRequested, DailyReportStatus.Submitted,
            DailyReportStatus.Approved, DailyReportStatus.Submitted
        };

        var reportIndex = 0;
        foreach (var bundle in active)
        {
            for (var r = 0; r < 5; r++)
            {
                var date = today.AddDays(-(r * 2 + 1 + reportIndex % 3));
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    date = date.AddDays(-2);
                var content = contents[reportIndex % contents.Length];
                var status = statuses[reportIndex % statuses.Length];
                var mentorId = bundle.Mentor.Id;
                await EnsureReportAsync(db, bundle.PrimaryPeriod!.Id, date, status, mentorId,
                    content.Item1, content.Item2, content.Item3, stats, ct);
                reportIndex++;
            }
        }
    }

    private static async Task EnsureReportAsync(
        AppDbContext db, Guid periodId, DateOnly date, DailyReportStatus status, Guid mentorId,
        string notes, string tech, string workTitle, SeedStats stats, CancellationToken ct)
    {
        if (await db.DailyReports.IgnoreQueryFilters()
                .AnyAsync(r => r.InternshipPeriodId == periodId && r.ReportDate == date && !r.IsDeleted, ct))
        {
            stats.ReportsSkipped++;
            return;
        }

        var report = new DailyReport
        {
            InternshipPeriodId = periodId,
            ReportDate = date,
            GeneralNotes = notes,
            ProblemsEncountered = status == DailyReportStatus.RevisionRequested ? "Demo: eksik örnek veri." : null,
            SolutionsApplied = status == DailyReportStatus.Approved ? "Demo: çözüm uygulandı." : null,
            TomorrowPlan = "Demo: bir sonraki iş kalemi planlandı.",
            Status = status,
            SubmittedAtUtc = status == DailyReportStatus.Draft ? null : DateTime.UtcNow.AddDays(-1),
            ReviewedAtUtc = status is DailyReportStatus.Approved or DailyReportStatus.Rejected or DailyReportStatus.RevisionRequested
                ? DateTime.UtcNow.AddHours(-5) : null,
            ReviewedByUserId = status is DailyReportStatus.Approved or DailyReportStatus.Rejected or DailyReportStatus.RevisionRequested
                ? mentorId : null,
            MentorComment = status switch
            {
                DailyReportStatus.Approved => "Demo onay: düzenli rapor.",
                DailyReportStatus.Rejected => "Demo red: içerik yetersiz.",
                DailyReportStatus.RevisionRequested => "Demo: lütfen örnekleri genişletin.",
                _ => null
            },
            WorkItems =
            {
                new DailyWorkItem
                {
                    Title = workTitle,
                    Description = notes,
                    DurationMinutes = 90 + (date.Day % 4) * 30,
                    TechnologiesUsed = tech,
                    Result = "Demo tamamlandı"
                }
            }
        };
        db.DailyReports.Add(report);
        await db.SaveChangesAsync(ct);
        stats.ReportsCreated++;
    }

    private static async Task SeedLeavesAsync(
        AppDbContext db, IClock clock, List<InternBundle> interns, List<ApplicationUser> mentors,
        SeedStats stats, CancellationToken ct)
    {
        var active = interns.Where(i => i.PrimaryPeriod is not null).Take(8).ToList();
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var defs = new (int InternIdx, LeaveType Type, LeaveRequestStatus Status, string Reason, int StartOffset, int Days)[]
        {
            (0, LeaveType.Excuse, LeaveRequestStatus.Pending, "Demo mazeret: ailevi iş.", 3, 0),
            (1, LeaveType.Sick, LeaveRequestStatus.Approved, "Demo sağlık: sentetik rapor.", -8, 1),
            (2, LeaveType.Administrative, LeaveRequestStatus.Rejected, "Demo idari: uygun görülmedi.", 5, 0),
            (3, LeaveType.Other, LeaveRequestStatus.Cancelled, "Demo diğer: talep iptal.", 2, 0),
            (4, LeaveType.Excuse, LeaveRequestStatus.Approved, "Demo mazeret onaylı.", -15, 0),
            (5, LeaveType.Sick, LeaveRequestStatus.Pending, "Demo sağlık bekleyen.", 7, 2),
            (0, LeaveType.Administrative, LeaveRequestStatus.Approved, "Demo idari onay.", -20, 1),
            (1, LeaveType.Excuse, LeaveRequestStatus.Rejected, "Demo mazeret red.", -3, 0),
            (2, LeaveType.Other, LeaveRequestStatus.Pending, "Demo diğer bekleyen.", 10, 0),
            (3, LeaveType.Sick, LeaveRequestStatus.Cancelled, "Demo sağlık iptal.", 4, 1),
            (6, LeaveType.Excuse, LeaveRequestStatus.Approved, "Demo yarım gün.", -12, 0),
            (7, LeaveType.Administrative, LeaveRequestStatus.Pending, "Demo idari bekleyen.", 14, 2)
        };

        foreach (var def in defs)
        {
            if (def.InternIdx >= active.Count) continue;
            var periodId = active[def.InternIdx].PrimaryPeriod!.Id;
            var start = today.AddDays(def.StartOffset);
            var end = start.AddDays(def.Days);
            if (await db.LeaveRequests.IgnoreQueryFilters()
                    .AnyAsync(l => l.InternshipPeriodId == periodId && l.Reason == def.Reason && !l.IsDeleted, ct))
            {
                stats.LeavesSkipped++;
                continue;
            }

            db.LeaveRequests.Add(new LeaveRequest
            {
                InternshipPeriodId = periodId,
                OrganizationUnitId = active[def.InternIdx].Profile.CurrentOrganizationUnitId,
                LeaveType = def.Type,
                StartDate = start,
                EndDate = end,
                Reason = def.Reason,
                Status = def.Status,
                ReviewedAtUtc = def.Status is LeaveRequestStatus.Approved or LeaveRequestStatus.Rejected
                    ? DateTime.UtcNow.AddDays(-1) : null,
                ReviewedByUserId = def.Status is LeaveRequestStatus.Approved or LeaveRequestStatus.Rejected
                    ? mentors[def.InternIdx % mentors.Count].Id : null,
                ReviewerNote = def.Status switch
                {
                    LeaveRequestStatus.Approved => "Demo: onaylandı.",
                    LeaveRequestStatus.Rejected => "Demo: reddedildi.",
                    _ => null
                }
            });
            stats.LeavesCreated++;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedEvaluationsAsync(
        AppDbContext db, IClock clock, List<InternBundle> interns, List<ApplicationUser> mentors,
        SeedStats stats, CancellationToken ct)
    {
        foreach (var bundle in interns)
        {
            foreach (var period in bundle.Periods.Where(p => p.Status == InternshipStatus.Completed))
            {
                if (await db.Evaluations.IgnoreQueryFilters()
                        .AnyAsync(e => e.InternshipPeriodId == period.Id && !e.IsDeleted, ct))
                {
                    stats.EvaluationsSkipped++;
                    continue;
                }

                db.Evaluations.Add(new Evaluation
                {
                    InternshipPeriodId = period.Id,
                    MentorUserId = bundle.Mentor.Id,
                    EvaluationDate = period.EndDate,
                    TechnicalKnowledgeScore = 4,
                    ResponsibilityScore = 5,
                    TeamworkScore = 4,
                    CommunicationScore = 4,
                    ProblemSolvingScore = 3,
                    TimeManagementScore = 4,
                    AttendanceScore = 5,
                    GeneralComment = "Demo değerlendirme: sentetik dönem sonu yorumu."
                });
                stats.EvaluationsCreated++;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAnnouncementsAsync(AppDbContext db, IClock clock, Guid createdBy, SeedStats stats, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var defs = new (string Title, string Content, int PubDays, int? ExpDays, bool Active)[]
        {
            ("Demo: Genel oryantasyon", "Tüm stajyerler için genel oryantasyon duyurusu (demo).", -2, 14, true),
            ("Demo: Yazılım birimi toplantısı", "Yazılım Geliştirme birimine özel toplantı (demo).", -1, 7, true),
            ("Demo: Stajyer rapor hatırlatması", "Stajyerlere günlük rapor hatırlatması (demo).", 0, 10, true),
            ("Demo: Süresi dolmuş duyuru", "Bu duyurunun süresi dolmuştur (demo).", -30, -5, false),
            ("Demo: İK bilgilendirme", "İnsan Kaynakları süreç bilgilendirmesi (demo).", -3, 20, true),
            ("Demo: Mali dönem kapanışı", "Mali Hizmetler dönem kapanış notu (demo).", -4, 5, true),
            ("Demo: Basın etkinliği", "Basın ve Halkla İlişkiler etkinlik duyurusu (demo).", -1, 12, true),
            ("Demo: AI workshop", "Yapay zekâ workshop duyurusu (demo).", -6, 3, true),
            ("Demo: Pasif arşiv notu", "Pasif birim için arşiv notu (demo).", -10, null, false),
            ("Demo: Güvenlik bilgilendirmesi", "Bilgi İşlem güvenlik uyarısı (demo).", -2, 30, true)
        };

        foreach (var def in defs)
        {
            if (await db.Announcements.IgnoreQueryFilters()
                    .AnyAsync(a => a.Title == def.Title && !a.IsDeleted, ct))
            {
                stats.AnnouncementsSkipped++;
                continue;
            }

            db.Announcements.Add(new Announcement
            {
                Title = def.Title,
                Content = def.Content,
                PublishedAtUtc = now.AddDays(def.PubDays),
                ExpiresAtUtc = def.ExpDays.HasValue ? now.AddDays(def.ExpDays.Value) : null,
                CreatedByUserId = createdBy,
                IsActive = def.Active
            });
            stats.AnnouncementsCreated++;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAiSummariesAsync(
        AppDbContext db, IClock clock, List<InternBundle> interns,
        SeedStats stats, CancellationToken ct)
    {
        var targets = interns.Where(i => i.PrimaryPeriod is not null).Take(6).ToList();
        foreach (var bundle in targets)
        {
            var period = bundle.PrimaryPeriod!;
            await EnsureAiSummaryAsync(db, clock, period, bundle.Mentor.Id,
                $"demo-ai-weekly-{period.Id:N}", AiSummaryType.Weekly,
                period.StartDate, period.StartDate.AddDays(14),
                "Demo haftalık özet: düzenli ilerleme ve tutarlı raporlama.",
                "Form doğrulama, dashboard veri hazırlığı, dokümantasyon.",
                stats, ct);
            await EnsureAiSummaryAsync(db, clock, period, bundle.Mentor.Id,
                $"demo-ai-monthly-{period.Id:N}", AiSummaryType.Monthly,
                period.StartDate, period.StartDate.AddDays(30),
                "Demo aylık özet: teknik yetkinlik arttı; ekip içi iletişim güçlendirilebilir.",
                "Yetkilendirme testleri, Excel export, devam kaydı senaryoları.",
                stats, ct);
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureAiSummaryAsync(
        AppDbContext db, IClock clock, InternshipPeriod period, Guid mentorId, string hash,
        AiSummaryType type, DateOnly start, DateOnly end, string executive, string completedWork,
        SeedStats stats, CancellationToken ct)
    {
        if (await db.AiReportSummaries.IgnoreQueryFilters()
                .AnyAsync(s => s.InternshipPeriodId == period.Id && s.InputHash == hash && !s.IsDeleted, ct))
        {
            stats.AiSummariesSkipped++;
            return;
        }

        db.AiReportSummaries.Add(new AiReportSummary
        {
            InternshipPeriodId = period.Id,
            RequestedByUserId = mentorId,
            PeriodStart = start,
            PeriodEnd = end,
            SummaryType = type,
            ExecutiveSummary = executive,
            CompletedWork = completedWork,
            Technologies = "C#, ASP.NET Core, EF Core, Bootstrap",
            ProblemsAndSolutions = "CORS gecikmesi yaşandı; ortam değişkenleri ayrıştırılarak çözüldü.",
            RisksOrBlockers = "Demo: harici API anahtarı yoksa AI canlı çağrı yapılmaz.",
            SuggestedNextSteps = "Test kapsamını artırın; filtre ve dışa aktarma senaryolarını doğrulayın.",
            SourceReportIds = "[]",
            InputHash = hash,
            ModelName = "demo-synthetic",
            PromptVersion = "demo-v1",
            Status = AiSummaryStatus.Completed,
            GeneratedAtUtc = clock.UtcNow.AddDays(-1)
        });
        stats.AiSummariesCreated++;
    }

    private static async Task SeedNotificationsAsync(
        AppDbContext db, IClock clock, List<ApplicationUser> admins, List<ApplicationUser> mentors,
        List<InternBundle> interns, SeedStats stats, CancellationToken ct)
    {
        var defs = new List<(Guid UserId, string Title, string Message, NotificationType Type, bool Read)>();
        if (admins.Count > 0)
        {
            defs.Add((admins[0].Id, "Demo: Yeni birim oluşturuldu", "Sentetik demo birim kaydı eklendi.", NotificationType.Info, false));
            defs.Add((admins[0].Id, "Demo: Proje güncellendi", "Demo proje ilerleme oranı güncellendi.", NotificationType.Success, true));
        }
        foreach (var m in mentors.Take(2))
        {
            defs.Add((m.Id, "Demo: Günlük rapor onaylandı", "Atanan stajyerin raporu onaylandı (demo).", NotificationType.Success, false));
            defs.Add((m.Id, "Demo: İzin talebi sonuçlandı", "Bir izin talebi sonuçlandırıldı (demo).", NotificationType.Warning, true));
        }
        foreach (var i in interns.Take(4))
        {
            defs.Add((i.User.Id, "Demo: Yeni proje atandı", "Size demo bir proje atandı.", NotificationType.Info, false));
            defs.Add((i.User.Id, "Demo: Yeni duyuru yayınlandı", "Yeni bir duyuru yayınlandı.", NotificationType.Info, true));
        }

        foreach (var def in defs)
        {
            if (await db.Notifications.IgnoreQueryFilters()
                    .AnyAsync(n => n.UserId == def.UserId && n.Title == def.Title && !n.IsDeleted, ct))
            {
                stats.NotificationsSkipped++;
                continue;
            }

            db.Notifications.Add(new Notification
            {
                UserId = def.UserId,
                Title = def.Title,
                Message = def.Message,
                Type = def.Type,
                IsRead = def.Read,
                ReadAtUtc = def.Read ? clock.UtcNow.AddHours(-2) : null
            });
            stats.NotificationsCreated++;
        }
        await db.SaveChangesAsync(ct);
    }

    private sealed class SeedStats
    {
        public int UsersCreated, UsersSkipped;
        public int UnitsResolved;
        public int ProfilesCreated, ProfilesSkipped;
        public int PeriodsCreated, PeriodsSkipped;
        public int ProjectsCreated, ProjectsSkipped;
        public int TasksCreated, TasksSkipped;
        public int ReportsCreated, ReportsSkipped;
        public int AttendanceCreated, AttendanceSkipped;
        public int LeavesCreated, LeavesSkipped;
        public int EvaluationsCreated, EvaluationsSkipped;
        public int AnnouncementsCreated, AnnouncementsSkipped;
        public int AiSummariesCreated, AiSummariesSkipped;
        public int NotificationsCreated, NotificationsSkipped;
        public int Skipped;
        public int TotalSkipped => UsersSkipped + ProfilesSkipped + PeriodsSkipped
            + ProjectsSkipped + TasksSkipped + ReportsSkipped + AttendanceSkipped + LeavesSkipped
            + EvaluationsSkipped + AnnouncementsSkipped + AiSummariesSkipped + NotificationsSkipped + Skipped;
    }

    private sealed record InternSpec(
        string FullName, string Email, string StudentNumber, string Phone,
        string University, string SchoolDepartment, string ClassLevel,
        string DepartmentName, int MentorIndex, string Address, bool SkipPeriod);

    private sealed record InternBundle(
        InternProfile Profile,
        ApplicationUser User,
        ApplicationUser Mentor,
        InternshipPeriod? PrimaryPeriod,
        List<InternshipPeriod> Periods,
        InternSpec Spec);
}
