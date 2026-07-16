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
/// Yalnızca Development + SEED_DEMO_DATA + SEED_DEMO_PASSWORD ile çalışan idempotent demo seeder.
/// Production'da çağrılmaz. Parola loglanmaz.
/// </summary>
public static class DemoDataSeeder
{
    public const string MarkerEmail = "selin.aksoy@staj360.local";

    public static async Task SeedAsync(IServiceProvider services, bool isDevelopment, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        if (!isDevelopment)
        {
            logger.LogDebug("Demo seeder atlandı: ortam Development değil.");
            return;
        }

        var config = services.GetRequiredService<IConfiguration>();
        var enabled = IsTruthy(Environment.GetEnvironmentVariable("SEED_DEMO_DATA"))
                      || IsTruthy(config["SEED_DEMO_DATA"]);
        var password = Environment.GetEnvironmentVariable("SEED_DEMO_PASSWORD")
                       ?? config["SEED_DEMO_PASSWORD"];

        if (!enabled || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("SEED_DEMO_DATA veya SEED_DEMO_PASSWORD tanımlı değil. Demo kullanıcı verisi oluşturulmadı.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var tz = services.GetRequiredService<ITimeZoneService>();

        // Idempotency: marker mentor zaten varsa demo seti tamamlanmış kabul edilir.
        if (await userManager.FindByEmailAsync(MarkerEmail) is not null
            && await db.InternProfiles.AnyAsync(p => p.StudentNumber == "DEMO-1001", cancellationToken))
        {
            logger.LogInformation("Demo veriler zaten mevcut; çoğaltma yapılmadı.");
            return;
        }

        logger.LogInformation("Development demo verileri ekleniyor (parola loglanmaz).");

        var departments = await EnsureDepartmentsAsync(db, cancellationToken);
        var schedule = await EnsureWorkScheduleAsync(db, cancellationToken);

        var mentorSelin = await EnsureUserAsync(userManager, "selin.aksoy@staj360.local", "Selin Aksoy", AppRoles.Mentor, password, logger);
        var mentorMurat = await EnsureUserAsync(userManager, "murat.koc@staj360.local", "Murat Koç", AppRoles.Mentor, password, logger);
        if (mentorSelin is null || mentorMurat is null)
        {
            logger.LogWarning("Demo mentor hesapları oluşturulamadı.");
            return;
        }

        var today = tz.LocalDate(clock.UtcNow);
        var start = today.AddDays(-10);
        var end = today.AddDays(30);

        var interns = new[]
        {
            new DemoIntern("Ayşe Yılmaz", "ayse.yilmaz@staj360.local", "DEMO-1001", "Fırat Üniversitesi", "Yazılım Mühendisliği", "Yazılım Geliştirme", mentorSelin.Id),
            new DemoIntern("Mehmet Kaya", "mehmet.kaya@staj360.local", "DEMO-1002", "Dicle Üniversitesi", "Bilgisayar Mühendisliği", "Bilgi İşlem", mentorSelin.Id),
            new DemoIntern("Zeynep Demir", "zeynep.demir@staj360.local", "DEMO-1003", "Fırat Üniversitesi", "Yapay Zekâ ve Veri Mühendisliği", "Yapay Zekâ ve Veri Analitiği", mentorMurat.Id),
            new DemoIntern("Emir Arslan", "emir.arslan@staj360.local", "DEMO-1004", "İnönü Üniversitesi", "Bilgisayar Mühendisliği", "Yazılım Geliştirme", mentorSelin.Id),
            new DemoIntern("Elif Şahin", "elif.sahin@staj360.local", "DEMO-1005", "Dicle Üniversitesi", "Yönetim Bilişim Sistemleri", "Bilgi İşlem", mentorMurat.Id),
            new DemoIntern("Can Aydın", "can.aydin@staj360.local", "DEMO-1006", "Fırat Üniversitesi", "Yazılım Mühendisliği", "Yapay Zekâ ve Veri Analitiği", mentorMurat.Id)
        };

        var profiles = new List<(InternProfile Profile, InternshipPeriod Period, ApplicationUser User, DemoIntern Spec)>();
        foreach (var spec in interns)
        {
            var user = await EnsureUserAsync(userManager, spec.Email, spec.FullName, AppRoles.Intern, password, logger);
            if (user is null) continue;

            var profile = await db.InternProfiles.FirstOrDefaultAsync(p => p.StudentNumber == spec.StudentNumber, cancellationToken);
            if (profile is null)
            {
                profile = new InternProfile
                {
                    UserId = user.Id,
                    StudentNumber = spec.StudentNumber,
                    University = spec.University,
                    SchoolDepartment = spec.SchoolDepartment,
                    Faculty = "Mühendislik Fakültesi",
                    ClassLevel = "3",
                    DepartmentId = departments[spec.DepartmentName].Id,
                    IsActive = true
                };
                db.InternProfiles.Add(profile);
                await db.SaveChangesAsync(cancellationToken);
            }

            var period = await db.InternshipPeriods.FirstOrDefaultAsync(
                p => p.InternProfileId == profile.Id && p.Status == InternshipStatus.Active, cancellationToken);
            if (period is null)
            {
                period = new InternshipPeriod
                {
                    InternProfileId = profile.Id,
                    MentorUserId = spec.MentorUserId,
                    WorkScheduleId = schedule.Id,
                    StartDate = start,
                    EndDate = end,
                    RequiredWorkDays = 30,
                    CompletedWorkDays = 5,
                    Status = InternshipStatus.Active
                };
                db.InternshipPeriods.Add(period);
                await db.SaveChangesAsync(cancellationToken);
            }

            profiles.Add((profile, period, user, spec));
        }

        await SeedAttendanceAsync(db, clock, tz, profiles, schedule, cancellationToken);
        await SeedReportsAsync(db, clock, profiles, mentorSelin.Id, mentorMurat.Id, cancellationToken);
        await SeedProjectsAsync(db, clock, departments, profiles, mentorSelin.Id, mentorMurat.Id, cancellationToken);
        await SeedLeavesAsync(db, profiles, cancellationToken);
        await SeedAnnouncementsAsync(db, clock, mentorSelin.Id, cancellationToken);

        logger.LogInformation(
            "Demo seed tamamlandı: {MentorCount} mentor, {InternCount} stajyer, ilişkili devam/rapor/proje/izin/duyuru kayıtları.",
            2, profiles.Count);
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || value == "1"
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static async Task<Dictionary<string, Department>> EnsureDepartmentsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var names = new[]
        {
            ("Yazılım Geliştirme", "Yazılım ve uygulama geliştirme birimi"),
            ("Bilgi İşlem", "Kurumsal bilgi işlem ve altyapı"),
            ("Yapay Zekâ ve Veri Analitiği", "Veri bilimi ve yapay zekâ uygulamaları")
        };

        foreach (var (name, desc) in names)
        {
            if (!await db.Departments.AnyAsync(d => d.Name == name, cancellationToken))
            {
                db.Departments.Add(new Department { Name = name, Description = desc, IsActive = true });
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        return await db.Departments
            .Where(d => names.Select(n => n.Item1).Contains(d.Name))
            .ToDictionaryAsync(d => d.Name, cancellationToken);
    }

    private static async Task<WorkSchedule> EnsureWorkScheduleAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        const string name = "Demo Standart Mesai (09:00-18:00)";
        var existing = await db.WorkSchedules.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
        if (existing is not null) return existing;

        var schedule = new WorkSchedule
        {
            Name = name,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0),
            GracePeriodMinutes = 15,
            MondayEnabled = true,
            TuesdayEnabled = true,
            WednesdayEnabled = true,
            ThursdayEnabled = true,
            FridayEnabled = true
        };
        db.WorkSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);
        return schedule;
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string password,
        ILogger logger)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, role))
                await userManager.AddToRoleAsync(existing, role);
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
            logger.LogWarning("Demo kullanıcı oluşturulamadı: {Email} — {Errors}",
                email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return null;
        }

        await userManager.AddToRoleAsync(user, role);
        logger.LogInformation("Demo kullanıcı oluşturuldu: {Email} ({Role})", email, role);
        return user;
    }

    private static async Task SeedAttendanceAsync(
        AppDbContext db,
        IClock clock,
        ITimeZoneService tz,
        List<(InternProfile Profile, InternshipPeriod Period, ApplicationUser User, DemoIntern Spec)> profiles,
        WorkSchedule schedule,
        CancellationToken cancellationToken)
    {
        if (profiles.Count == 0) return;
        var ayse = profiles.First(p => p.Spec.StudentNumber == "DEMO-1001");
        var mehmet = profiles.First(p => p.Spec.StudentNumber == "DEMO-1002");
        var zeynep = profiles.First(p => p.Spec.StudentNumber == "DEMO-1003");

        var today = tz.LocalDate(clock.UtcNow);
        // Son iş günleri: bugünden geriye hafta içi 3 gün.
        var workDays = Enumerable.Range(1, 10)
            .Select(i => today.AddDays(-i))
            .Where(d => d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            .Take(3)
            .OrderBy(d => d)
            .ToList();

        foreach (var (day, index) in workDays.Select((d, i) => (d, i)))
        {
            var isLast = index == workDays.Count - 1;
            await EnsurePresentDayAsync(db, tz, ayse.Period.Id, day, schedule, late: isLast, incomplete: false, cancellationToken);
            await EnsurePresentDayAsync(db, tz, mehmet.Period.Id, day, schedule, late: false, incomplete: false, cancellationToken);
            await EnsurePresentDayAsync(db, tz, zeynep.Period.Id, day, schedule, late: false, incomplete: isLast, cancellationToken);
        }
    }

    private static async Task EnsurePresentDayAsync(
        AppDbContext db,
        ITimeZoneService tz,
        Guid periodId,
        DateOnly workDate,
        WorkSchedule schedule,
        bool late,
        bool incomplete,
        CancellationToken cancellationToken)
    {
        var existing = await db.AttendanceDays
            .Include(d => d.Events)
            .FirstOrDefaultAsync(d => d.InternshipPeriodId == periodId && d.WorkDate == workDate, cancellationToken);
        if (existing is not null) return;

        var checkInLocal = workDate.ToDateTime(late ? schedule.StartTime.AddMinutes(schedule.GracePeriodMinutes + 20) : schedule.StartTime);
        var checkOutLocal = workDate.ToDateTime(schedule.EndTime);
        var checkInUtc = tz.ToUtc(checkInLocal);
        var checkOutUtc = tz.ToUtc(checkOutLocal);

        var day = new AttendanceDay
        {
            InternshipPeriodId = periodId,
            WorkDate = workDate,
            FirstCheckInUtc = checkInUtc,
            LastCheckOutUtc = incomplete ? null : checkOutUtc,
            TotalWorkedMinutes = incomplete ? 0 : (int)(checkOutUtc - checkInUtc).TotalMinutes,
            Status = late ? AttendanceStatus.Late : AttendanceStatus.Present,
            IsLate = late,
            IsIncomplete = incomplete,
            IsEarlyLeave = false
        };
        day.Events.Add(new AttendanceEvent
        {
            EventType = AttendanceEventType.CheckIn,
            EventTimeUtc = checkInUtc,
            Source = AttendanceSource.WebButton,
            Notes = "Demo giriş"
        });
        if (!incomplete)
        {
            day.Events.Add(new AttendanceEvent
            {
                EventType = AttendanceEventType.CheckOut,
                EventTimeUtc = checkOutUtc,
                Source = AttendanceSource.WebButton,
                Notes = "Demo çıkış"
            });
        }

        db.AttendanceDays.Add(day);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedReportsAsync(
        AppDbContext db,
        IClock clock,
        List<(InternProfile Profile, InternshipPeriod Period, ApplicationUser User, DemoIntern Spec)> profiles,
        Guid mentorSelinId,
        Guid mentorMuratId,
        CancellationToken cancellationToken)
    {
        var ayse = profiles.First(p => p.Spec.StudentNumber == "DEMO-1001");
        var mehmet = profiles.First(p => p.Spec.StudentNumber == "DEMO-1002");
        var zeynep = profiles.First(p => p.Spec.StudentNumber == "DEMO-1003");
        var today = DateOnly.FromDateTime(clock.UtcNow);

        await EnsureReportAsync(db, ayse.Period.Id, today.AddDays(-2), DailyReportStatus.Approved, mentorSelinId,
            "Kimlik doğrulama ve yetkilendirme katmanı tamamlandı.",
            "CORS yapılandırması beklenenden uzun sürdü.",
            "Ortam değişkenleri ayrıştırıldı.",
            "Rapor onay ekranı iyileştirilecek.",
            "JWT doğrulama uçları", "ASP.NET Core Identity ile rol kontrolü eklendi.", 210, "C#, ASP.NET Core", cancellationToken);

        await EnsureReportAsync(db, mehmet.Period.Id, today.AddDays(-1), DailyReportStatus.Submitted, null,
            "Sunucu izleme paneli için metrikler toplandı.",
            "Bazı sayaçlar gecikmeli güncelleniyor.",
            null,
            "Alarm eşikleri gözden geçirilecek.",
            "Prometheus metrikleri", "CPU ve bellek panelleri hazırlandı.", 180, "Grafana, SQL Server", cancellationToken);

        await EnsureReportAsync(db, zeynep.Period.Id, today.AddDays(-3), DailyReportStatus.RevisionRequested, mentorMuratId,
            "Veri temizleme betikleri yazıldı.",
            "Eksik kolonlar için varsayılan değer belirsiz.",
            "Geçici doldurma uygulandı.",
            "Mentor notuna göre örnek veri seti genişletilecek.",
            "CSV normalizasyonu", "Tarih formatları standartlaştırıldı.", 150, "Python, Pandas", cancellationToken);
    }

    private static async Task EnsureReportAsync(
        AppDbContext db, Guid periodId, DateOnly date, DailyReportStatus status, Guid? reviewerId,
        string notes, string? problems, string? solutions, string? tomorrow,
        string workTitle, string workDesc, int minutes, string tech,
        CancellationToken cancellationToken)
    {
        if (await db.DailyReports.AnyAsync(r => r.InternshipPeriodId == periodId && r.ReportDate == date, cancellationToken))
            return;

        var report = new DailyReport
        {
            InternshipPeriodId = periodId,
            ReportDate = date,
            GeneralNotes = notes,
            ProblemsEncountered = problems,
            SolutionsApplied = solutions,
            TomorrowPlan = tomorrow,
            Status = status,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-1),
            ReviewedAtUtc = status is DailyReportStatus.Approved or DailyReportStatus.RevisionRequested ? DateTime.UtcNow.AddHours(-6) : null,
            ReviewedByUserId = reviewerId,
            MentorComment = status == DailyReportStatus.RevisionRequested
                ? "Örnek veri kapsamını genişletin ve sonuçları tabloyla destekleyin."
                : status == DailyReportStatus.Approved ? "Düzenli ve anlaşılır rapor, teşekkürler." : null,
            WorkItems =
            {
                new DailyWorkItem
                {
                    Title = workTitle,
                    Description = workDesc,
                    DurationMinutes = minutes,
                    TechnologiesUsed = tech,
                    Result = "Tamamlandı"
                }
            }
        };
        db.DailyReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProjectsAsync(
        AppDbContext db,
        IClock clock,
        Dictionary<string, Department> departments,
        List<(InternProfile Profile, InternshipPeriod Period, ApplicationUser User, DemoIntern Spec)> profiles,
        Guid mentorSelinId,
        Guid mentorMuratId,
        CancellationToken cancellationToken)
    {
        var p1 = await EnsureProjectAsync(db, "Kurumsal Talep Yönetim Sistemi",
            "Birimlerin yazılım taleplerini kaydettiği ve takip ettiği kurumsal panel.",
            departments["Yazılım Geliştirme"].Id, mentorSelinId, ProjectStatus.InProgress, 45, cancellationToken);
        var p2 = await EnsureProjectAsync(db, "Veri Analizi ve Raporlama Paneli",
            "Staj ve operasyon verilerini özetleyen yönetim paneli.",
            departments["Yapay Zekâ ve Veri Analitiği"].Id, mentorMuratId, ProjectStatus.InProgress, 30, cancellationToken);

        var ayse = profiles.First(p => p.Spec.StudentNumber == "DEMO-1001").Profile;
        var emir = profiles.First(p => p.Spec.StudentNumber == "DEMO-1004").Profile;
        var zeynep = profiles.First(p => p.Spec.StudentNumber == "DEMO-1003").Profile;
        var can = profiles.First(p => p.Spec.StudentNumber == "DEMO-1006").Profile;

        await EnsureAssignmentAsync(db, p1.Id, ayse.Id, "Backend geliştirici", cancellationToken);
        await EnsureAssignmentAsync(db, p1.Id, emir.Id, "Frontend destek", cancellationToken);
        await EnsureAssignmentAsync(db, p2.Id, zeynep.Id, "Veri mühendisi", cancellationToken);
        await EnsureAssignmentAsync(db, p2.Id, can.Id, "Rapor geliştirici", cancellationToken);

        await EnsureTaskAsync(db, p1.Id, ayse.Id, "Talep formu API'si", ProjectTaskStatus.Done, TaskPriority.High, cancellationToken);
        await EnsureTaskAsync(db, p1.Id, emir.Id, "Talep listesi arayüzü", ProjectTaskStatus.InProgress, TaskPriority.Medium, cancellationToken);
        await EnsureTaskAsync(db, p1.Id, ayse.Id, "Bildirim kuyruğu", ProjectTaskStatus.Todo, TaskPriority.Low, cancellationToken);
        await EnsureTaskAsync(db, p2.Id, zeynep.Id, "Günlük metrik ETL", ProjectTaskStatus.InProgress, TaskPriority.High, cancellationToken);
        await EnsureTaskAsync(db, p2.Id, can.Id, "Yönetici özet kartları", ProjectTaskStatus.Todo, TaskPriority.Medium, cancellationToken);
    }

    private static async Task<Project> EnsureProjectAsync(
        AppDbContext db, string name, string description, Guid departmentId, Guid mentorId,
        ProjectStatus status, int progress, CancellationToken cancellationToken)
    {
        var existing = await db.Projects.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
        if (existing is not null) return existing;

        var project = new Project
        {
            Name = name,
            Description = description,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
            Status = status,
            ProgressPercentage = progress,
            DepartmentId = departmentId,
            MentorUserId = mentorId
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return project;
    }

    private static async Task EnsureAssignmentAsync(AppDbContext db, Guid projectId, Guid internId, string role, CancellationToken cancellationToken)
    {
        if (await db.ProjectAssignments.AnyAsync(a => a.ProjectId == projectId && a.InternProfileId == internId && a.IsActive, cancellationToken))
            return;
        db.ProjectAssignments.Add(new ProjectAssignment
        {
            ProjectId = projectId,
            InternProfileId = internId,
            AssignedAtUtc = DateTime.UtcNow,
            RoleDescription = role,
            IsActive = true
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureTaskAsync(
        AppDbContext db, Guid projectId, Guid? internId, string title, ProjectTaskStatus status, TaskPriority priority,
        CancellationToken cancellationToken)
    {
        if (await db.ProjectTasks.AnyAsync(t => t.ProjectId == projectId && t.Title == title, cancellationToken))
            return;

        db.ProjectTasks.Add(new ProjectTask
        {
            ProjectId = projectId,
            AssignedInternProfileId = internId,
            Title = title,
            Description = $"Demo görev: {title}",
            Priority = priority,
            Status = status,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            EstimatedMinutes = 240,
            CompletedAtUtc = status == ProjectTaskStatus.Done ? DateTime.UtcNow.AddDays(-1) : null
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedLeavesAsync(
        AppDbContext db,
        List<(InternProfile Profile, InternshipPeriod Period, ApplicationUser User, DemoIntern Spec)> profiles,
        CancellationToken cancellationToken)
    {
        var emir = profiles.First(p => p.Spec.StudentNumber == "DEMO-1004");
        var elif = profiles.First(p => p.Spec.StudentNumber == "DEMO-1005");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!await db.LeaveRequests.AnyAsync(l => l.InternshipPeriodId == emir.Period.Id && l.Reason.Contains("Demo mazeret"), cancellationToken))
        {
            db.LeaveRequests.Add(new LeaveRequest
            {
                InternshipPeriodId = emir.Period.Id,
                LeaveType = LeaveType.Excuse,
                StartDate = today.AddDays(3),
                EndDate = today.AddDays(3),
                Reason = "Demo mazeret: ailevi işler nedeniyle yarım gün izin talebi.",
                Status = LeaveRequestStatus.Pending
            });
        }

        if (!await db.LeaveRequests.AnyAsync(l => l.InternshipPeriodId == elif.Period.Id && l.Reason.Contains("Demo sağlık"), cancellationToken))
        {
            db.LeaveRequests.Add(new LeaveRequest
            {
                InternshipPeriodId = elif.Period.Id,
                LeaveType = LeaveType.Sick,
                StartDate = today.AddDays(-5),
                EndDate = today.AddDays(-4),
                Reason = "Demo sağlık: iki günlük raporlu izin (sentetik).",
                Status = LeaveRequestStatus.Approved,
                ReviewedAtUtc = DateTime.UtcNow.AddDays(-4),
                ReviewerNote = "Onaylandı (demo)."
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAnnouncementsAsync(AppDbContext db, IClock clock, Guid createdBy, CancellationToken cancellationToken)
    {
        if (!await db.Announcements.AnyAsync(a => a.Title == "Demo: Staj oryantasyon toplantısı", cancellationToken))
        {
            db.Announcements.Add(new Announcement
            {
                Title = "Demo: Staj oryantasyon toplantısı",
                Content = "Yeni dönem stajyerleri için oryantasyon toplantısı perşembe 10:00'da toplantı salonunda yapılacaktır.",
                PublishedAtUtc = clock.UtcNow.AddDays(-2),
                ExpiresAtUtc = clock.UtcNow.AddDays(14),
                CreatedByUserId = createdBy,
                IsActive = true
            });
        }

        if (!await db.Announcements.AnyAsync(a => a.Title == "Demo: Günlük rapor hatırlatması", cancellationToken))
        {
            db.Announcements.Add(new Announcement
            {
                Title = "Demo: Günlük rapor hatırlatması",
                Content = "Lütfen her iş günü sonunda günlük çalışma raporunuzu danışmanınıza gönderiniz. Eksik raporlar dönem değerlendirmesini etkiler.",
                PublishedAtUtc = clock.UtcNow.AddDays(-1),
                CreatedByUserId = createdBy,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record DemoIntern(
        string FullName,
        string Email,
        string StudentNumber,
        string University,
        string SchoolDepartment,
        string DepartmentName,
        Guid MentorUserId);
}
