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
/// Development CBS (Coğrafi Bilgi Sistemleri) sentetik kullanıcı/proje seed'i.
/// Parola yalnızca SEED_CBS_USERS_PASSWORD üzerinden; yarım kayıt bırakılmaz.
/// </summary>
public static class CbsUsersSeeder
{
    public const string BranchCode = "COGRAFI_BILGI_SISTEMLERI";
    public const string FundaEmail = "funda.akis@stajamed.local";
    public const string RoniEmail = "roni.ozgunluk@stajamed.local";

    private static readonly (string FullName, string Email, string StudentNumber, string MentorEmail)[] Interns =
    [
        ("Kaan Kesip", "kaan.kesip@stajamed.local", "CBS-2026-001", FundaEmail),
        ("Berfin Koçer", "berfin.kocer@stajamed.local", "CBS-2026-002", FundaEmail),
        ("Asmin Aldemir", "asmin.aldemir@stajamed.local", "CBS-2026-003", FundaEmail),
        ("Nujin Balyen", "nujin.balyen@stajamed.local", "CBS-2026-004", FundaEmail),
        ("Barış Durmuş", "baris.durmus@stajamed.local", "CBS-2026-005", RoniEmail),
        ("Mehdi Umut Arslan", "mehdi.umut.arslan@stajamed.local", "CBS-2026-006", RoniEmail),
        ("Servan Gençdal", "servan.gencdal@stajamed.local", "CBS-2026-007", RoniEmail)
    ];

    private static readonly (string Name, string Description)[] ProjectDefs =
    [
        ("Mekânsal Veri Doğrulama Çalışması",
            "Belediye CBS katmanlarındaki geometri ve öznitelik tutarlılığının kontrolü."),
        ("Belediye Hizmet Noktaları Harita Katmanı",
            "Vatandaş hizmet noktalarının konumsal katmana işlenmesi ve doğrulanması."),
        ("Adres Verisi Kalite Kontrolü",
            "Adres envanterinin standartlaştırılması ve eksik kayıtların tamamlanması.")
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("CbsUsersSeeder");
        var config = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var tz = services.GetRequiredService<ITimeZoneService>();
        await SeedAsync(userManager, db, config, clock, tz, logger, cancellationToken);
    }

    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IConfiguration config,
        IClock clock,
        ITimeZoneService tz,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var password = ResolvePassword(config);
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("CBS kullanıcıları oluşturulamadı: SEED_CBS_USERS_PASSWORD tanımlı değil.");
            return;
        }

        var branch = await db.OrganizationUnits
            .FirstOrDefaultAsync(u => u.Code == BranchCode && !u.IsDeleted, cancellationToken);
        if (branch is null)
        {
            logger.LogWarning("CBS seed atlandı: {Code} birimi bulunamadı.", BranchCode);
            return;
        }

        var schedule = await db.WorkSchedules.FirstOrDefaultAsync(s => !s.IsDeleted, cancellationToken);
        if (schedule is null)
        {
            logger.LogWarning("CBS seed atlandı: çalışma programı yok.");
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var fundaId = await EnsureMentorAsync(userManager, db, FundaEmail, "Funda Akış", password, branch.Id, cancellationToken);
            var roniId = await EnsureMentorAsync(userManager, db, RoniEmail, "Roni Özgünlük", password, branch.Id, cancellationToken);
            await EnsureManagerOnCbsAsync(db, cancellationToken);

            var mentorByEmail = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
            {
                [FundaEmail] = fundaId,
                [RoniEmail] = roniId
            };

            var today = tz.LocalDate(clock.UtcNow);
            var periodStart = today.AddDays(-20);
            var periodEnd = today.AddDays(40);
            var internBundles = new List<(InternProfile Profile, Guid MentorId, InternshipPeriod Period)>();

            for (var i = 0; i < Interns.Length; i++)
            {
                var spec = Interns[i];
                var mentorId = mentorByEmail[spec.MentorEmail];
                var phone = $"0555 700 {(i + 1):0000}";
                var bundle = await EnsureInternAsync(
                    userManager, db, password, branch.Id, schedule.Id, mentorId,
                    spec.FullName, spec.Email, spec.StudentNumber, phone,
                    periodStart, periodEnd, i, cancellationToken);
                internBundles.Add(bundle);
            }

            await SeedProjectsTasksReportsAttendanceAsync(
                db, tz, branch.Id, fundaId, roniId, internBundles, periodStart, periodEnd, today, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            logger.LogInformation("CBS seed tamamlandı. Stajyer: {Count}", Interns.Length);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            logger.LogError(ex,
                "CBS kullanıcıları oluşturulamadı (Identity/parola veya veri hatası). Parola loglanmaz.");
        }
    }

    internal static string? ResolvePassword(IConfiguration config) =>
        Environment.GetEnvironmentVariable("SEED_CBS_USERS_PASSWORD")
        ?? config["SEED_CBS_USERS_PASSWORD"];

    private static async Task EnsureManagerOnCbsAsync(AppDbContext db, CancellationToken ct)
    {
        var manager = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == DemoLoginAccounts.ManagerEmail, ct);
        if (manager is null) return;

        var cbs = await db.OrganizationUnits.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Code == BranchCode && !u.IsDeleted, ct);
        if (cbs is null) return;

        var has = await db.ManagerUnitAssignments.AnyAsync(
            a => a.ManagerUserId == manager.Id && a.OrganizationUnitId == cbs.Id && a.IsActive && !a.IsDeleted, ct);
        if (has) return;

        db.ManagerUnitAssignments.Add(new ManagerUnitAssignment
        {
            ManagerUserId = manager.Id,
            OrganizationUnitId = cbs.Id,
            IsActive = true,
            AssignedAtUtc = DateTime.UtcNow
        });
    }

    private static async Task EnsureAdvisorAssignmentAsync(AppDbContext db, Guid advisorUserId, Guid branchId, CancellationToken ct)
    {
        var has = await db.AdvisorUnitAssignments.AnyAsync(
            a => a.AdvisorUserId == advisorUserId && a.OrganizationUnitId == branchId && a.IsActive && !a.IsDeleted, ct);
        if (has) return;

        db.AdvisorUnitAssignments.Add(new AdvisorUnitAssignment
        {
            AdvisorUserId = advisorUserId,
            OrganizationUnitId = branchId,
            IsActive = true,
            AssignedAtUtc = DateTime.UtcNow
        });
    }

    private static async Task<Guid> EnsureMentorAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        string email,
        string fullName,
        string password,
        Guid branchId,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                var codes = string.Join(", ", create.Errors.Select(e => e.Code));
                throw new InvalidOperationException($"CBS danışman oluşturulamadı ({email}). Kodlar: {codes}");
            }
        }
        else if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal))
        {
            user.FullName = fullName;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Mentor))
            await userManager.AddToRoleAsync(user, AppRoles.Mentor);

        await EnsureAdvisorAssignmentAsync(db, user.Id, branchId, ct);
        return user.Id;
    }

    private static async Task<(InternProfile Profile, Guid MentorId, InternshipPeriod Period)> EnsureInternAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        string password,
        Guid branchId,
        Guid scheduleId,
        Guid mentorId,
        string fullName,
        string email,
        string studentNumber,
        string phone,
        DateOnly periodStart,
        DateOnly periodEnd,
        int index,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                var codes = string.Join(", ", create.Errors.Select(e => e.Code));
                throw new InvalidOperationException($"CBS kullanıcı oluşturulamadı ({email}). Kodlar: {codes}");
            }
        }
        else
        {
            if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal))
            {
                user.FullName = fullName;
                await userManager.UpdateAsync(user);
            }

            if (!user.IsActive || !user.EmailConfirmed)
            {
                user.IsActive = true;
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }
        }

        if (!await userManager.IsInRoleAsync(user, AppRoles.Intern))
            await userManager.AddToRoleAsync(user, AppRoles.Intern);

        var profile = await db.InternProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.StudentNumber == studentNumber, ct)
            ?? await db.InternProfiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        if (profile is null)
        {
            profile = new InternProfile
            {
                UserId = user.Id,
                StudentNumber = studentNumber,
                University = index % 2 == 0 ? "Dicle Üniversitesi" : "Fırat Üniversitesi",
                Faculty = "Mühendislik Fakültesi",
                SchoolDepartment = index % 3 == 0 ? "Harita Mühendisliği" : "Bilgisayar Mühendisliği",
                ClassLevel = (3 + index % 2).ToString(),
                PhoneNumber = phone,
                Address = "Diyarbakır/Yenişehir — Sentetik test adresi",
                EmergencyContactName = "Sentetik Acil Kişi",
                EmergencyContactPhone = "0555 700 0099",
                CurrentOrganizationUnitId = branchId,
                IsActive = true
            };
            db.InternProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            profile.IsDeleted = false;
            profile.IsActive = true;
            profile.UserId = user.Id;
            profile.StudentNumber = studentNumber;
            profile.CurrentOrganizationUnitId = branchId;
            if (string.IsNullOrWhiteSpace(profile.Address) || profile.Address.Contains("Demo", StringComparison.OrdinalIgnoreCase))
                profile.Address = "Diyarbakır/Yenişehir — Sentetik test adresi";
            if (string.IsNullOrWhiteSpace(profile.University))
                profile.University = "Dicle Üniversitesi";
            if (string.IsNullOrWhiteSpace(profile.SchoolDepartment))
                profile.SchoolDepartment = "Harita Mühendisliği";
            if (string.IsNullOrWhiteSpace(profile.PhoneNumber))
                profile.PhoneNumber = phone;
            if (string.IsNullOrWhiteSpace(profile.EmergencyContactName))
                profile.EmergencyContactName = "Sentetik Acil Kişi";
            if (string.IsNullOrWhiteSpace(profile.EmergencyContactPhone))
                profile.EmergencyContactPhone = "0555 700 0099";
        }

        var assignment = await db.InternUnitAssignments
            .FirstOrDefaultAsync(a => a.InternProfileId == profile.Id && a.IsActive && !a.IsDeleted, ct);
        if (assignment is null)
        {
            db.InternUnitAssignments.Add(new InternUnitAssignment
            {
                InternProfileId = profile.Id,
                OrganizationUnitId = branchId,
                AdvisorUserId = mentorId,
                StartDate = periodStart,
                IsActive = true
            });
        }
        else
        {
            assignment.OrganizationUnitId = branchId;
            assignment.AdvisorUserId = mentorId;
        }

        var period = await db.InternshipPeriods
            .FirstOrDefaultAsync(p => p.InternProfileId == profile.Id && p.Status == InternshipStatus.Active && !p.IsDeleted, ct);
        if (period is null)
        {
            period = new InternshipPeriod
            {
                InternProfileId = profile.Id,
                MentorUserId = mentorId,
                WorkScheduleId = scheduleId,
                StartDate = periodStart,
                EndDate = periodEnd,
                RequiredWorkDays = 40,
                Status = InternshipStatus.Active
            };
            db.InternshipPeriods.Add(period);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            period.MentorUserId = mentorId;
            period.WorkScheduleId = scheduleId;
            if (period.StartDate > period.EndDate)
            {
                period.StartDate = periodStart;
                period.EndDate = periodEnd;
            }
        }

        return (profile, mentorId, period);
    }

    private static async Task SeedProjectsTasksReportsAttendanceAsync(
        AppDbContext db,
        ITimeZoneService tz,
        Guid branchId,
        Guid fundaId,
        Guid roniId,
        List<(InternProfile Profile, Guid MentorId, InternshipPeriod Period)> interns,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly today,
        CancellationToken ct)
    {
        var projects = new List<Project>();
        var mentors = new[] { fundaId, roniId, fundaId };
        for (var i = 0; i < ProjectDefs.Length; i++)
        {
            var def = ProjectDefs[i];
            var existing = await db.Projects.FirstOrDefaultAsync(
                p => p.Name == def.Name && p.OrganizationUnitId == branchId && !p.IsDeleted, ct);
            if (existing is null)
            {
                existing = new Project
                {
                    Name = def.Name,
                    Description = def.Description,
                    OrganizationUnitId = branchId,
                    MentorUserId = mentors[i],
                    Status = ProjectStatus.InProgress,
                    ProgressPercentage = 35 + i * 15,
                    StartDate = periodStart,
                    EndDate = periodEnd
                };
                db.Projects.Add(existing);
                await db.SaveChangesAsync(ct);
            }

            projects.Add(existing);
        }

        var taskTitles = new[]
        {
            "Parsel sınır geometrisi doğrulama",
            "Hizmet noktası koordinat güncelleme",
            "Adres standart kod eşleştirme",
            "Katman metaveri kontrol listesi",
            "Yeşil alan poligon sınıflandırma",
            "Yol ağının topoloji kontrolü"
        };

        for (var t = 0; t < taskTitles.Length; t++)
        {
            var project = projects[t % projects.Count];
            var intern = interns[t % interns.Count];
            var title = taskTitles[t];
            var hasTask = await db.ProjectTasks.AnyAsync(
                x => x.ProjectId == project.Id && x.Title == title && !x.IsDeleted, ct);
            if (!hasTask)
            {
                db.ProjectTasks.Add(new ProjectTask
                {
                    ProjectId = project.Id,
                    AssignedInternProfileId = intern.Profile.Id,
                    Title = title,
                    Description = $"{title} — CBS şubesi sentetik görev kaydı.",
                    Priority = TaskPriority.Medium,
                    Status = t % 3 == 0 ? ProjectTaskStatus.Done : ProjectTaskStatus.InProgress,
                    DueDate = today.AddDays(7 + t)
                });
            }

            var hasAssign = await db.ProjectAssignments.AnyAsync(
                a => a.ProjectId == project.Id && a.InternProfileId == intern.Profile.Id && !a.IsDeleted, ct);
            if (!hasAssign)
            {
                db.ProjectAssignments.Add(new ProjectAssignment
                {
                    ProjectId = project.Id,
                    InternProfileId = intern.Profile.Id,
                    RoleDescription = "CBS stajyer geliştirici",
                    AssignedAtUtc = DateTime.UtcNow,
                    IsActive = true
                });
            }
        }

        foreach (var bundle in interns)
        {
            var workDays = EnumerateWeekdays(periodStart, today).Take(5).ToList();
            for (var d = 0; d < workDays.Count; d++)
            {
                var day = workDays[d];
                if (await db.AttendanceDays.AnyAsync(
                        a => a.InternshipPeriodId == bundle.Period.Id && a.WorkDate == day && !a.IsDeleted, ct))
                    continue;

                var checkInLocal = day.ToDateTime(new TimeOnly(8, 30 + d));
                var checkOutLocal = day.ToDateTime(new TimeOnly(17, 0 + d));
                var checkInUtc = tz.ToUtc(checkInLocal);
                var checkOutUtc = tz.ToUtc(checkOutLocal);
                var att = new AttendanceDay
                {
                    InternshipPeriodId = bundle.Period.Id,
                    WorkDate = day,
                    FirstCheckInUtc = checkInUtc,
                    LastCheckOutUtc = checkOutUtc,
                    TotalWorkedMinutes = (int)(checkOutUtc - checkInUtc).TotalMinutes,
                    Status = AttendanceStatus.Present
                };
                att.Events.Add(new AttendanceEvent
                {
                    EventType = AttendanceEventType.CheckIn,
                    EventTimeUtc = checkInUtc,
                    Source = AttendanceSource.WebButton,
                    Notes = "Sentetik giriş"
                });
                att.Events.Add(new AttendanceEvent
                {
                    EventType = AttendanceEventType.CheckOut,
                    EventTimeUtc = checkOutUtc,
                    Source = AttendanceSource.WebButton,
                    Notes = "Sentetik çıkış"
                });
                db.AttendanceDays.Add(att);
            }

            var reportDays = workDays.Take(3).ToList();
            var reportTitles = new[]
            {
                "Katman doğrulama özeti",
                "Koordinat kalite kontrol notu",
                "Adres envanteri ilerleme raporu"
            };
            for (var r = 0; r < reportDays.Count; r++)
            {
                var reportDate = reportDays[r];
                if (await db.DailyReports.AnyAsync(
                        x => x.InternshipPeriodId == bundle.Period.Id && x.ReportDate == reportDate && !x.IsDeleted, ct))
                    continue;

                var report = new DailyReport
                {
                    InternshipPeriodId = bundle.Period.Id,
                    ReportDate = reportDate,
                    OrganizationUnitId = branchId,
                    GeneralNotes = $"{reportTitles[r]} — {bundle.Profile.StudentNumber} için sentetik günlük çalışma özeti.",
                    Status = DailyReportStatus.Approved,
                    SubmittedAtUtc = DateTime.UtcNow.AddDays(-r),
                    ReviewedAtUtc = DateTime.UtcNow.AddDays(-r).AddHours(2),
                    ReviewedByUserId = bundle.MentorId,
                    MentorComment = "Sentetik onay: çalışma yeterli."
                };
                report.WorkItems.Add(new DailyWorkItem
                {
                    Title = reportTitles[r],
                    Description = "CBS biriminde yürütülen sentetik görev kalemi.",
                    DurationMinutes = 180 + r * 30,
                    TechnologiesUsed = "QGIS, PostGIS",
                    Result = "Kontrol listesi güncellendi."
                });
                db.DailyReports.Add(report);
            }
        }
    }

    private static IEnumerable<DateOnly> EnumerateWeekdays(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            yield return d;
        }
    }
}
