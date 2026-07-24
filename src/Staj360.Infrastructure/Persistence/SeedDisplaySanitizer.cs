using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Staj360.Infrastructure.Identity;

namespace Staj360.Infrastructure.Persistence;

/// <summary>
/// Seed edilmiş kayıtlardaki Demo/demo kullanıcıya görünen metinleri anlamlı Türkçe metinlerle değiştirir.
/// Satır silmez; yalnızca açıkça Demo/demo içeren alanları günceller.
/// </summary>
public static class SeedDisplaySanitizer
{
    public static async Task SanitizeAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var changed = 0;

        var profiles = await db.InternProfiles
            .Where(p => !p.IsDeleted && (
                (p.Faculty != null && p.Faculty.Contains("Demo")) ||
                (p.Address != null && p.Address.Contains("Demo")) ||
                (p.University != null && p.University.Contains("Demo")) ||
                (p.SchoolDepartment != null && p.SchoolDepartment.Contains("Demo")) ||
                (p.EmergencyContactName != null && p.EmergencyContactName.Contains("Demo")) ||
                (p.StudentNumber != null && p.StudentNumber.Contains("DEMO"))))
            .ToListAsync(cancellationToken);
        var i = 0;
        foreach (var p in profiles)
        {
            i++;
            if (ContainsDemo(p.Faculty))
                p.Faculty = "Mühendislik Fakültesi";
            if (ContainsDemo(p.University))
                p.University = "Dicle Üniversitesi";
            if (ContainsDemo(p.SchoolDepartment))
                p.SchoolDepartment = "Bilgisayar Mühendisliği";
            if (ContainsDemo(p.EmergencyContactName))
                p.EmergencyContactName = "Acil İletişim Kişisi";
            if (ContainsDemo(p.Address) || (p.Address != null && p.Address.Contains("Demo", StringComparison.OrdinalIgnoreCase)))
                p.Address = $"Diyarbakır/Yenişehir — Sentetik test adresi #{i:00}";
            if (p.StudentNumber != null && p.StudentNumber.StartsWith("DEMO-", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = p.StudentNumber.Length > 5 ? p.StudentNumber[5..] : "0000";
                var candidate = "STJ-2026-" + suffix;
                var clash = await db.InternProfiles.AnyAsync(
                    x => x.Id != p.Id && x.StudentNumber == candidate, cancellationToken);
                if (!clash)
                    p.StudentNumber = candidate;
            }
            changed++;
        }

        var users = await db.Set<ApplicationUser>()
            .Where(u => u.FullName.Contains("Demo") || u.FullName.Contains("demo")
                        || u.FullName.Contains("Test User") || u.FullName.Contains("Test Intern")
                        || u.FullName.Contains("Demo Stajyer") || u.FullName.Contains("Demo User"))
            .ToListAsync(cancellationToken);
        foreach (var u in users)
        {
            u.FullName = ReplaceDemoToken(u.FullName, "Sentetik Kullanıcı");
            changed++;
        }

        var projects = await db.Projects
            .Where(p => !p.IsDeleted && (
                (p.Name != null && (p.Name.Contains("Demo") || p.Name.Contains("demo"))) ||
                (p.Description != null && (p.Description.Contains("Demo") || p.Description.Contains("demo")))))
            .ToListAsync(cancellationToken);
        foreach (var p in projects)
        {
            if (ContainsDemo(p.Name))
                p.Name = ReplaceDemoToken(p.Name, "Kurumsal Staj Projesi");
            if (ContainsDemo(p.Description))
                p.Description = ReplaceDemoToken(p.Description, "Sentetik staj proje açıklaması.");
            changed++;
        }

        var tasks = await db.ProjectTasks
            .Where(t => !t.IsDeleted && (
                (t.Title != null && (t.Title.Contains("Demo") || t.Title.Contains("demo"))) ||
                (t.Description != null && (t.Description.Contains("Demo") || t.Description.Contains("demo")))))
            .ToListAsync(cancellationToken);
        foreach (var t in tasks)
        {
            if (ContainsDemo(t.Title))
                t.Title = ReplaceDemoToken(t.Title, "Staj görev kalemi");
            if (ContainsDemo(t.Description))
                t.Description = ReplaceDemoToken(t.Description, "Sentetik görev açıklaması.");
            changed++;
        }

        var assignments = await db.ProjectAssignments
            .Where(a => !a.IsDeleted && a.RoleDescription != null && a.RoleDescription.Contains("Demo"))
            .ToListAsync(cancellationToken);
        foreach (var a in assignments)
        {
            a.RoleDescription = ReplaceDemoToken(a.RoleDescription, "Proje ekip üyesi");
            changed++;
        }

        var schedules = await db.WorkSchedules
            .Where(s => !s.IsDeleted && s.Name.Contains("Demo"))
            .ToListAsync(cancellationToken);
        foreach (var s in schedules)
        {
            s.Name = ReplaceDemoToken(s.Name, "Standart Mesai (09:00-18:00)");
            changed++;
        }

        var aiSummaries = await db.AiReportSummaries
            .Where(s => !s.IsDeleted && (
                (s.ExecutiveSummary != null && s.ExecutiveSummary.Contains("Demo")) ||
                (s.RisksOrBlockers != null && s.RisksOrBlockers.Contains("Demo")) ||
                (s.CompletedWork != null && s.CompletedWork.Contains("Demo")) ||
                (s.ProblemsAndSolutions != null && s.ProblemsAndSolutions.Contains("Demo")) ||
                (s.SuggestedNextSteps != null && s.SuggestedNextSteps.Contains("Demo"))))
            .ToListAsync(cancellationToken);
        foreach (var s in aiSummaries)
        {
            if (ContainsDemo(s.ExecutiveSummary))
                s.ExecutiveSummary = ReplaceDemoToken(s.ExecutiveSummary, "Dönem özeti: düzenli ilerleme.");
            if (ContainsDemo(s.RisksOrBlockers))
                s.RisksOrBlockers = ReplaceDemoToken(s.RisksOrBlockers, "Harici API anahtarı yoksa canlı AI çağrısı yapılmaz.");
            if (ContainsDemo(s.CompletedWork))
                s.CompletedWork = ReplaceDemoToken(s.CompletedWork, "Tamamlanan çalışma özeti.");
            if (ContainsDemo(s.ProblemsAndSolutions))
                s.ProblemsAndSolutions = ReplaceDemoToken(s.ProblemsAndSolutions, "Sorun ve çözüm özeti.");
            if (ContainsDemo(s.SuggestedNextSteps))
                s.SuggestedNextSteps = ReplaceDemoToken(s.SuggestedNextSteps, "Sonraki adım önerileri.");
            changed++;
        }

        var reports = await db.DailyReports
            .Where(r => !r.IsDeleted && (
                (r.GeneralNotes != null && r.GeneralNotes.Contains("Demo")) ||
                (r.ProblemsEncountered != null && r.ProblemsEncountered.Contains("Demo")) ||
                (r.SolutionsApplied != null && r.SolutionsApplied.Contains("Demo")) ||
                (r.TomorrowPlan != null && r.TomorrowPlan.Contains("Demo")) ||
                (r.MentorComment != null && r.MentorComment.Contains("Demo"))))
            .ToListAsync(cancellationToken);
        foreach (var r in reports)
        {
            if (ContainsDemo(r.GeneralNotes)) r.GeneralNotes = ReplaceDemoToken(r.GeneralNotes, "Günlük çalışma özeti.");
            if (ContainsDemo(r.ProblemsEncountered)) r.ProblemsEncountered = ReplaceDemoToken(r.ProblemsEncountered, "Karşılaşılan teknik engel notu.");
            if (ContainsDemo(r.SolutionsApplied)) r.SolutionsApplied = ReplaceDemoToken(r.SolutionsApplied, "Uygulanan çözüm adımları.");
            if (ContainsDemo(r.TomorrowPlan)) r.TomorrowPlan = ReplaceDemoToken(r.TomorrowPlan, "Ertesi gün çalışma planı.");
            if (ContainsDemo(r.MentorComment)) r.MentorComment = ReplaceDemoToken(r.MentorComment, "Danışman değerlendirme notu.");
            changed++;
        }

        var workItems = await db.DailyWorkItems
            .Where(w => !w.IsDeleted && (
                (w.Title != null && w.Title.Contains("Demo")) ||
                (w.Description != null && w.Description.Contains("Demo")) ||
                (w.Result != null && w.Result.Contains("Demo"))))
            .ToListAsync(cancellationToken);
        foreach (var w in workItems)
        {
            if (ContainsDemo(w.Title)) w.Title = ReplaceDemoToken(w.Title, "Günlük iş kalemi");
            if (ContainsDemo(w.Description)) w.Description = ReplaceDemoToken(w.Description, "İş kalemi açıklaması.");
            if (ContainsDemo(w.Result)) w.Result = ReplaceDemoToken(w.Result, "Tamamlandı.");
            changed++;
        }

        var leaves = await db.LeaveRequests
            .Where(l => !l.IsDeleted && (
                l.Reason.Contains("Demo") ||
                (l.ReviewerNote != null && l.ReviewerNote.Contains("Demo"))))
            .ToListAsync(cancellationToken);
        foreach (var l in leaves)
        {
            if (ContainsDemo(l.Reason)) l.Reason = ReplaceDemoToken(l.Reason, "İzin gerekçesi.");
            if (ContainsDemo(l.ReviewerNote)) l.ReviewerNote = ReplaceDemoToken(l.ReviewerNote, "İzin karar notu.");
            changed++;
        }

        var evaluations = await db.Evaluations
            .Where(e => !e.IsDeleted && e.GeneralComment != null && e.GeneralComment.Contains("Demo"))
            .ToListAsync(cancellationToken);
        foreach (var e in evaluations)
        {
            e.GeneralComment = ReplaceDemoToken(e.GeneralComment, "Dönem sonu değerlendirme yorumu.");
            changed++;
        }

        var attendance = await db.AttendanceEvents
            .Where(a => !a.IsDeleted && a.Notes != null && a.Notes.Contains("Demo"))
            .ToListAsync(cancellationToken);
        foreach (var a in attendance)
        {
            a.Notes = ReplaceDemoToken(a.Notes, "Devam kaydı notu.");
            changed++;
        }

        var announcements = await db.Announcements
            .Where(a => !a.IsDeleted && (
                a.Title.Contains("Demo") || a.Title.Contains("demo") ||
                a.Content.Contains("Demo") || a.Content.Contains("demo")))
            .ToListAsync(cancellationToken);
        foreach (var a in announcements)
        {
            if (ContainsDemo(a.Title))
                a.Title = ReplaceDemoToken(a.Title, "Kurumsal duyuru");
            if (ContainsDemo(a.Content))
                a.Content = ReplaceDemoToken(a.Content, "Sentetik duyuru içeriği.");
            changed++;
        }

        var notifications = await db.Notifications
            .Where(n => !n.IsDeleted && (
                n.Title.Contains("Demo") || n.Title.Contains("demo") ||
                n.Message.Contains("Demo") || n.Message.Contains("demo")))
            .ToListAsync(cancellationToken);
        foreach (var n in notifications)
        {
            if (ContainsDemo(n.Title))
                n.Title = ReplaceDemoToken(n.Title, "Sistem bildirimi");
            if (ContainsDemo(n.Message))
                n.Message = ReplaceDemoToken(n.Message, "Sentetik bildirim mesajı.");
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seed görünen metin temizliği tamamlandı. Güncellenen kayıt: {Count}", changed);
        }
        else
        {
            logger.LogDebug("Seed görünen metin temizliği: değişiklik yok.");
        }
    }

    private static bool ContainsDemo(string? value) =>
        !string.IsNullOrEmpty(value) &&
        (value.Contains("Demo", StringComparison.OrdinalIgnoreCase)
         || value.Contains("TEST User", StringComparison.OrdinalIgnoreCase)
         || value.Contains("Test User", StringComparison.OrdinalIgnoreCase)
         || value.Contains("Test Intern", StringComparison.OrdinalIgnoreCase));

    private static string ReplaceDemoToken(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var cleaned = value
            .Replace("Demo", "Kurumsal", StringComparison.OrdinalIgnoreCase)
            .Replace("demo", "kurumsal", StringComparison.Ordinal)
            .Replace("TEST User", "Sentetik Kullanıcı", StringComparison.OrdinalIgnoreCase)
            .Replace("Test User", "Sentetik Kullanıcı", StringComparison.OrdinalIgnoreCase)
            .Replace("Test Intern", "Sentetik Stajyer", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }
}
