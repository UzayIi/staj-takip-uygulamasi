using Staj360.Domain.Enums;

namespace Staj360.Web.Helpers;

/// <summary>Enum ve durum değerlerinin arayüzde gösterilecek Türkçe karşılıkları.</summary>
public static class DisplayText
{
    public static string Of(InternshipStatus s) => s switch
    {
        InternshipStatus.Pending => "Beklemede",
        InternshipStatus.Active => "Aktif",
        InternshipStatus.Completed => "Tamamlandı",
        InternshipStatus.Terminated => "Sonlandırıldı",
        _ => s.ToString()
    };

    public static string Of(AttendanceStatus s) => s switch
    {
        AttendanceStatus.NotStarted => "Başlamadı",
        AttendanceStatus.Present => "Geldi",
        AttendanceStatus.Late => "Geç Kaldı",
        AttendanceStatus.Incomplete => "Eksik",
        AttendanceStatus.OnLeave => "İzinli",
        AttendanceStatus.Absent => "Gelmedi",
        _ => s.ToString()
    };

    public static string Of(DailyReportStatus s) => s switch
    {
        DailyReportStatus.Draft => "Taslak",
        DailyReportStatus.Submitted => "Gönderildi",
        DailyReportStatus.RevisionRequested => "Düzeltme İstendi",
        DailyReportStatus.Approved => "Onaylandı",
        DailyReportStatus.Rejected => "Reddedildi",
        _ => s.ToString()
    };

    public static string Of(ProjectStatus s) => s switch
    {
        ProjectStatus.Planned => "Planlandı",
        ProjectStatus.InProgress => "Devam Ediyor",
        ProjectStatus.OnHold => "Beklemede",
        ProjectStatus.Completed => "Tamamlandı",
        ProjectStatus.Cancelled => "İptal",
        _ => s.ToString()
    };

    public static string Of(ProjectTaskStatus s) => s switch
    {
        ProjectTaskStatus.Todo => "Yapılacak",
        ProjectTaskStatus.InProgress => "Devam Ediyor",
        ProjectTaskStatus.InReview => "İncelemede",
        ProjectTaskStatus.Done => "Tamamlandı",
        ProjectTaskStatus.Cancelled => "İptal",
        _ => s.ToString()
    };

    public static string Of(TaskPriority p) => p switch
    {
        TaskPriority.Low => "Düşük",
        TaskPriority.Medium => "Orta",
        TaskPriority.High => "Yüksek",
        TaskPriority.Critical => "Kritik",
        _ => p.ToString()
    };

    public static string Of(LeaveRequestStatus s) => s switch
    {
        LeaveRequestStatus.Pending => "Beklemede",
        LeaveRequestStatus.Approved => "Onaylandı",
        LeaveRequestStatus.Rejected => "Reddedildi",
        LeaveRequestStatus.Cancelled => "İptal",
        _ => s.ToString()
    };

    public static string Of(LeaveType t) => t switch
    {
        LeaveType.Excuse => "Mazeret",
        LeaveType.Sick => "Sağlık",
        LeaveType.Administrative => "İdari",
        LeaveType.Other => "Diğer",
        _ => t.ToString()
    };

    public static string Of(TransferRequestStatus s) => s switch
    {
        TransferRequestStatus.Pending => "Beklemede",
        TransferRequestStatus.Approved => "Onaylandı",
        TransferRequestStatus.Rejected => "Reddedildi",
        TransferRequestStatus.Cancelled => "İptal",
        _ => s.ToString()
    };

    public static string Of(AiSummaryType t) => t switch
    {
        AiSummaryType.Weekly => "Haftalık",
        AiSummaryType.Monthly => "Aylık",
        AiSummaryType.CustomRange => "Tarih Aralığı",
        AiSummaryType.FinalOverall => "Staj Sonu Genel",
        _ => t.ToString()
    };

    public static string Of(AiSummaryStatus s) => s switch
    {
        AiSummaryStatus.Pending => "Bekliyor",
        AiSummaryStatus.Completed => "Tamamlandı",
        AiSummaryStatus.Failed => "Başarısız",
        AiSummaryStatus.Disabled => "Devre Dışı",
        _ => s.ToString()
    };

    // Durum renkleri için Bootstrap badge sınıfı.
    public static string BadgeClass(InternshipStatus s) => s switch
    {
        InternshipStatus.Pending => "bg-secondary",
        InternshipStatus.Active => "bg-success",
        InternshipStatus.Completed => "bg-primary",
        InternshipStatus.Terminated => "bg-danger",
        _ => "bg-secondary"
    };

    public static string BadgeClass(DailyReportStatus s) => s switch
    {
        DailyReportStatus.Draft => "bg-secondary",
        DailyReportStatus.Submitted => "bg-info text-dark",
        DailyReportStatus.RevisionRequested => "bg-warning text-dark",
        DailyReportStatus.Approved => "bg-success",
        DailyReportStatus.Rejected => "bg-danger",
        _ => "bg-secondary"
    };

    public static string BadgeClass(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => "bg-success",
        AttendanceStatus.Late => "bg-warning text-dark",
        AttendanceStatus.Incomplete => "bg-warning text-dark",
        AttendanceStatus.OnLeave => "bg-info text-dark",
        AttendanceStatus.Absent => "bg-danger",
        _ => "bg-secondary"
    };

    public static string BadgeClass(ProjectStatus s) => s switch
    {
        ProjectStatus.Planned => "bg-secondary",
        ProjectStatus.InProgress => "bg-primary",
        ProjectStatus.OnHold => "bg-warning text-dark",
        ProjectStatus.Completed => "bg-success",
        ProjectStatus.Cancelled => "bg-danger",
        _ => "bg-secondary"
    };

    public static string BadgeClass(ProjectTaskStatus s) => s switch
    {
        ProjectTaskStatus.Todo => "bg-secondary",
        ProjectTaskStatus.InProgress => "bg-primary",
        ProjectTaskStatus.InReview => "bg-info text-dark",
        ProjectTaskStatus.Done => "bg-success",
        ProjectTaskStatus.Cancelled => "bg-danger",
        _ => "bg-secondary"
    };

    public static string BadgeClass(LeaveRequestStatus s) => s switch
    {
        LeaveRequestStatus.Pending => "bg-warning text-dark",
        LeaveRequestStatus.Approved => "bg-success",
        LeaveRequestStatus.Rejected => "bg-danger",
        LeaveRequestStatus.Cancelled => "bg-secondary",
        _ => "bg-secondary"
    };

    public static string BadgeClass(TransferRequestStatus s) => s switch
    {
        TransferRequestStatus.Pending => "bg-warning text-dark",
        TransferRequestStatus.Approved => "bg-success",
        TransferRequestStatus.Rejected => "bg-danger",
        TransferRequestStatus.Cancelled => "bg-secondary",
        _ => "bg-secondary"
    };
}
