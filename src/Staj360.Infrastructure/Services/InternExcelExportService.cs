using System.Globalization;
using ClosedXML.Excel;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Services.Exports;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Persistence;

namespace Staj360.Infrastructure.Services;

public class InternExcelExportService : IInternExcelExportService
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly AppDbContext _db;
    private readonly ITimeZoneService _tz;
    private readonly IClock _clock;

    public InternExcelExportService(AppDbContext db, ITimeZoneService tz, IClock clock)
    {
        _db = db;
        _tz = tz;
        _clock = clock;
    }

    public async Task<InternExcelExportResult?> ExportAsync(Guid internProfileId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.InternProfiles.AsNoTracking()
            .Include(p => p.CurrentOrganizationUnit)
            .FirstOrDefaultAsync(p => p.Id == internProfileId && !p.IsDeleted, cancellationToken);
        if (profile is null)
            return null;

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == profile.UserId, cancellationToken);
        var fullName = string.IsNullOrWhiteSpace(user?.FullName) ? "Stajyer" : user!.FullName;

        var periods = await _db.InternshipPeriods.AsNoTracking()
            .Where(p => p.InternProfileId == internProfileId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var mentorIds = periods.Select(p => p.MentorUserId).Distinct().ToList();
        var mentors = await _db.Users.AsNoTracking()
            .Where(u => mentorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var periodIds = periods.Select(p => p.Id).ToList();

        var projects = await (
            from a in _db.ProjectAssignments.AsNoTracking()
            join p in _db.Projects.AsNoTracking() on a.ProjectId equals p.Id
            join d in _db.OrganizationUnits.AsNoTracking() on p.OrganizationUnitId equals d.Id
            where a.InternProfileId == internProfileId && !a.IsDeleted && !p.IsDeleted
            orderby p.StartDate descending
            select new
            {
                p.Name,
                OrganizationUnitName = d.Name,
                p.Status,
                p.ProgressPercentage,
                p.StartDate,
                p.EndDate,
                a.RoleDescription,
                a.IsActive
            }).ToListAsync(cancellationToken);

        var attendance = periodIds.Count == 0
            ? []
            : await _db.AttendanceDays.AsNoTracking()
                .Where(d => periodIds.Contains(d.InternshipPeriodId) && !d.IsDeleted)
                .OrderByDescending(d => d.WorkDate)
                .ToListAsync(cancellationToken);

        var reports = periodIds.Count == 0
            ? []
            : await _db.DailyReports.AsNoTracking()
                .Where(r => periodIds.Contains(r.InternshipPeriodId) && !r.IsDeleted)
                .OrderByDescending(r => r.ReportDate)
                .ToListAsync(cancellationToken);

        var leaves = periodIds.Count == 0
            ? []
            : await _db.LeaveRequests.AsNoTracking()
                .Where(l => periodIds.Contains(l.InternshipPeriodId) && !l.IsDeleted)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync(cancellationToken);

        var evaluations = periodIds.Count == 0
            ? []
            : await _db.Evaluations.AsNoTracking()
                .Where(e => periodIds.Contains(e.InternshipPeriodId) && !e.IsDeleted)
                .OrderByDescending(e => e.EvaluationDate)
                .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();

        WriteProfileSheet(workbook, profile, user?.Email, fullName, periods, mentors);
        WriteProjectsSheet(workbook, projects.Select(p => (
            p.Name,
            p.OrganizationUnitName,
            Of(p.Status),
            p.ProgressPercentage,
            p.StartDate,
            p.EndDate,
            (string?)p.RoleDescription,
            p.IsActive)).ToList());
        WriteAttendanceSheet(workbook, attendance);
        WriteReportsSheet(workbook, reports);
        WriteLeavesSheet(workbook, leaves);
        WriteEvaluationsSheet(workbook, evaluations);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();
        var fileName = BuildFileName(fullName, _tz.LocalDate(_clock.UtcNow));
        return new InternExcelExportResult(bytes, fileName, ContentType);
    }

    private static void WriteProfileSheet(
        XLWorkbook workbook,
        Domain.Entities.InternProfile profile,
        string? email,
        string fullName,
        List<Domain.Entities.InternshipPeriod> periods,
        Dictionary<Guid, string> mentors)
    {
        var ws = workbook.Worksheets.Add("Temel Bilgiler");
        var row = 1;

        row = WriteSectionHeader(ws, row, "Temel Bilgiler");
        row = WriteKv(ws, row, "Ad Soyad", fullName);
        row = WriteKv(ws, row, "E-posta", email ?? "-");
        row = WriteKv(ws, row, "Öğrenci Numarası", profile.StudentNumber);
        row = WriteKv(ws, row, "T.C. Kimlik No", MaskNationalId(profile.NationalId) ?? "-");
        row = WriteKv(ws, row, "Telefon", profile.PhoneNumber ?? "-");
        row = WriteKv(ws, row, "Adres", profile.Address ?? "-");
        row = WriteKv(ws, row, "Acil Durum Kişisi", profile.EmergencyContactName ?? "-");
        row = WriteKv(ws, row, "Acil Durum Telefonu", profile.EmergencyContactPhone ?? "-");
        row = WriteKv(ws, row, "Aktif", profile.IsActive ? "Evet" : "Hayır");
        row++;

        row = WriteSectionHeader(ws, row, "Eğitim Bilgileri");
        row = WriteKv(ws, row, "Üniversite", profile.University ?? "-");
        row = WriteKv(ws, row, "Fakülte", profile.Faculty ?? "-");
        row = WriteKv(ws, row, "Bölüm", profile.SchoolDepartment ?? "-");
        row = WriteKv(ws, row, "Sınıf", profile.ClassLevel ?? "-");
        row++;

        row = WriteSectionHeader(ws, row, "Kurum / Şube");
        row = WriteKv(ws, row, "Şube", profile.CurrentOrganizationUnit?.Name ?? "-");
        row++;

        row = WriteSectionHeader(ws, row, "Staj Dönemleri");
        if (periods.Count == 0)
        {
            ws.Cell(row, 1).Value = Sanitize("Kayıt bulunamadı");
            row++;
        }
        else
        {
            WriteHeaderRow(ws, row, "Başlangıç", "Bitiş", "Durum", "Danışman", "Tamamlanan Gün", "Zorunlu Gün");
            row++;
            foreach (var p in periods)
            {
                mentors.TryGetValue(p.MentorUserId, out var mentorName);
                ws.Cell(row, 1).Value = Sanitize(FormatDate(p.StartDate));
                ws.Cell(row, 2).Value = Sanitize(FormatDate(p.EndDate));
                ws.Cell(row, 3).Value = Sanitize(Of(p.Status));
                ws.Cell(row, 4).Value = Sanitize(mentorName ?? "-");
                ws.Cell(row, 5).Value = p.CompletedWorkDays;
                ws.Cell(row, 6).Value = p.RequiredWorkDays;
                row++;
            }
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteProjectsSheet(
        XLWorkbook workbook,
        List<(string Name, string OrganizationUnitName, string Status, int Progress, DateOnly StartDate, DateOnly? EndDate, string? Role, bool IsActive)> projects)
    {
        var ws = workbook.Worksheets.Add("Projeler");
        if (projects.Count == 0)
        {
            WriteEmpty(ws);
            return;
        }

        WriteHeaderRow(ws, 1, "Proje", "Şube", "Durum", "İlerleme (%)", "Başlangıç", "Bitiş", "Rol", "Aktif Atama");
        var row = 2;
        foreach (var p in projects)
        {
            ws.Cell(row, 1).Value = Sanitize(p.Name);
            ws.Cell(row, 2).Value = Sanitize(p.OrganizationUnitName);
            ws.Cell(row, 3).Value = Sanitize(p.Status);
            ws.Cell(row, 4).Value = p.Progress;
            ws.Cell(row, 5).Value = Sanitize(FormatDate(p.StartDate));
            ws.Cell(row, 6).Value = Sanitize(p.EndDate.HasValue ? FormatDate(p.EndDate.Value) : "-");
            ws.Cell(row, 7).Value = Sanitize(p.Role ?? "-");
            ws.Cell(row, 8).Value = Sanitize(p.IsActive ? "Evet" : "Hayır");
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private void WriteAttendanceSheet(XLWorkbook workbook, List<Domain.Entities.AttendanceDay> days)
    {
        var ws = workbook.Worksheets.Add("Devam");
        if (days.Count == 0)
        {
            WriteEmpty(ws);
            return;
        }

        WriteHeaderRow(ws, 1, "Tarih", "Durum", "Giriş", "Çıkış", "Çalışılan Dakika", "Geç", "Eksik", "Erken Çıkış");
        var row = 2;
        foreach (var d in days)
        {
            ws.Cell(row, 1).Value = Sanitize(FormatDate(d.WorkDate));
            ws.Cell(row, 2).Value = Sanitize(Of(d.Status));
            ws.Cell(row, 3).Value = Sanitize(FormatTime(d.FirstCheckInUtc));
            ws.Cell(row, 4).Value = Sanitize(FormatTime(d.LastCheckOutUtc));
            ws.Cell(row, 5).Value = d.TotalWorkedMinutes;
            ws.Cell(row, 6).Value = Sanitize(d.IsLate ? "Evet" : "Hayır");
            ws.Cell(row, 7).Value = Sanitize(d.IsIncomplete ? "Evet" : "Hayır");
            ws.Cell(row, 8).Value = Sanitize(d.IsEarlyLeave ? "Evet" : "Hayır");
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteReportsSheet(XLWorkbook workbook, List<Domain.Entities.DailyReport> reports)
    {
        var ws = workbook.Worksheets.Add("Günlük Raporlar");
        if (reports.Count == 0)
        {
            WriteEmpty(ws);
            return;
        }

        WriteHeaderRow(ws, 1, "Tarih", "Durum", "Genel Notlar", "Sorunlar", "Çözümler", "Yarın Planı", "Mentor Yorumu");
        var row = 2;
        foreach (var r in reports)
        {
            ws.Cell(row, 1).Value = Sanitize(FormatDate(r.ReportDate));
            ws.Cell(row, 2).Value = Sanitize(Of(r.Status));
            ws.Cell(row, 3).Value = Sanitize(r.GeneralNotes ?? "-");
            ws.Cell(row, 4).Value = Sanitize(r.ProblemsEncountered ?? "-");
            ws.Cell(row, 5).Value = Sanitize(r.SolutionsApplied ?? "-");
            ws.Cell(row, 6).Value = Sanitize(r.TomorrowPlan ?? "-");
            ws.Cell(row, 7).Value = Sanitize(r.MentorComment ?? "-");
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteLeavesSheet(XLWorkbook workbook, List<Domain.Entities.LeaveRequest> leaves)
    {
        var ws = workbook.Worksheets.Add("İzinler");
        if (leaves.Count == 0)
        {
            WriteEmpty(ws);
            return;
        }

        WriteHeaderRow(ws, 1, "Tür", "Başlangıç", "Bitiş", "Durum", "Gerekçe", "İnceleme Notu");
        var row = 2;
        foreach (var l in leaves)
        {
            ws.Cell(row, 1).Value = Sanitize(Of(l.LeaveType));
            ws.Cell(row, 2).Value = Sanitize(FormatDate(l.StartDate));
            ws.Cell(row, 3).Value = Sanitize(FormatDate(l.EndDate));
            ws.Cell(row, 4).Value = Sanitize(Of(l.Status));
            ws.Cell(row, 5).Value = Sanitize(l.Reason);
            ws.Cell(row, 6).Value = Sanitize(l.ReviewerNote ?? "-");
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteEvaluationsSheet(XLWorkbook workbook, List<Domain.Entities.Evaluation> evaluations)
    {
        var ws = workbook.Worksheets.Add("Değerlendirmeler");
        if (evaluations.Count == 0)
        {
            WriteEmpty(ws);
            return;
        }

        WriteHeaderRow(ws, 1,
            "Tarih", "Teknik", "Sorumluluk", "Takım Çalışması", "İletişim",
            "Problem Çözme", "Zaman Yönetimi", "Devam", "Ortalama", "Genel Yorum");
        var row = 2;
        foreach (var e in evaluations)
        {
            ws.Cell(row, 1).Value = Sanitize(FormatDate(e.EvaluationDate));
            ws.Cell(row, 2).Value = e.TechnicalKnowledgeScore;
            ws.Cell(row, 3).Value = e.ResponsibilityScore;
            ws.Cell(row, 4).Value = e.TeamworkScore;
            ws.Cell(row, 5).Value = e.CommunicationScore;
            ws.Cell(row, 6).Value = e.ProblemSolvingScore;
            ws.Cell(row, 7).Value = e.TimeManagementScore;
            ws.Cell(row, 8).Value = e.AttendanceScore;
            ws.Cell(row, 9).Value = Math.Round(e.AverageScore, 2);
            ws.Cell(row, 10).Value = Sanitize(e.GeneralComment ?? "-");
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static int WriteSectionHeader(IXLWorksheet ws, int row, string title)
    {
        var cell = ws.Cell(row, 1);
        cell.Value = Sanitize(title);
        cell.Style.Font.Bold = true;
        return row + 1;
    }

    private static int WriteKv(IXLWorksheet ws, int row, string key, string value)
    {
        ws.Cell(row, 1).Value = Sanitize(key);
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = Sanitize(value);
        return row + 1;
    }

    private static void WriteHeaderRow(IXLWorksheet ws, int row, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = Sanitize(headers[i]);
            cell.Style.Font.Bold = true;
        }
    }

    private static void WriteEmpty(IXLWorksheet ws)
    {
        ws.Cell(1, 1).Value = Sanitize("Kayıt bulunamadı");
        ws.Columns().AdjustToContents();
    }

    private string FormatTime(DateTime? utc)
    {
        if (utc is null) return "-";
        return _tz.ToLocal(utc.Value).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>Excel formula injection önlemi (=, +, -, @ ile başlayan hücreler).</summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@')
            return "'" + value;
        return value;
    }

    internal static string BuildFileName(string fullName, DateOnly localDate)
    {
        var safe = Regex.Replace(fullName.Trim(), @"[^\p{L}\p{N}]+", "_", RegexOptions.CultureInvariant);
        safe = safe.Trim('_');
        if (string.IsNullOrWhiteSpace(safe)) safe = "Stajyer";
        if (safe.Length > 60) safe = safe[..60];
        return $"Stajyer_{safe}_{localDate:yyyyMMdd}.xlsx";
    }

    private static string? MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId)) return null;
        if (nationalId.Length <= 5) return new string('*', nationalId.Length);
        return string.Concat(nationalId.AsSpan(0, 3), new string('*', nationalId.Length - 5), nationalId.AsSpan(nationalId.Length - 2));
    }

    private static string Of(InternshipStatus s) => s switch
    {
        InternshipStatus.Pending => "Beklemede",
        InternshipStatus.Active => "Aktif",
        InternshipStatus.Completed => "Tamamlandı",
        InternshipStatus.Terminated => "Sonlandırıldı",
        _ => s.ToString()
    };

    private static string Of(AttendanceStatus s) => s switch
    {
        AttendanceStatus.NotStarted => "Başlamadı",
        AttendanceStatus.Present => "Geldi",
        AttendanceStatus.Late => "Geç Kaldı",
        AttendanceStatus.Incomplete => "Eksik",
        AttendanceStatus.OnLeave => "İzinli",
        AttendanceStatus.Absent => "Gelmedi",
        _ => s.ToString()
    };

    private static string Of(DailyReportStatus s) => s switch
    {
        DailyReportStatus.Draft => "Taslak",
        DailyReportStatus.Submitted => "Gönderildi",
        DailyReportStatus.RevisionRequested => "Düzeltme İstendi",
        DailyReportStatus.Approved => "Onaylandı",
        DailyReportStatus.Rejected => "Reddedildi",
        _ => s.ToString()
    };

    private static string Of(ProjectStatus s) => s switch
    {
        ProjectStatus.Planned => "Planlandı",
        ProjectStatus.InProgress => "Devam Ediyor",
        ProjectStatus.OnHold => "Beklemede",
        ProjectStatus.Completed => "Tamamlandı",
        ProjectStatus.Cancelled => "İptal",
        _ => s.ToString()
    };

    private static string Of(LeaveRequestStatus s) => s switch
    {
        LeaveRequestStatus.Pending => "Beklemede",
        LeaveRequestStatus.Approved => "Onaylandı",
        LeaveRequestStatus.Rejected => "Reddedildi",
        LeaveRequestStatus.Cancelled => "İptal",
        _ => s.ToString()
    };

    private static string Of(LeaveType t) => t switch
    {
        LeaveType.Excuse => "Mazeret",
        LeaveType.Sick => "Sağlık",
        LeaveType.Administrative => "İdari",
        LeaveType.Other => "Diğer",
        _ => t.ToString()
    };
}
