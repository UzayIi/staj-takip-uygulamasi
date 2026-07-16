namespace Staj360.Domain.Enums;

/// <summary>Staj döneminin genel durumu.</summary>
public enum InternshipStatus
{
    Pending = 0,
    Active = 1,
    Completed = 2,
    Terminated = 3
}

/// <summary>Bir iş gününün devam durumu.</summary>
public enum AttendanceStatus
{
    NotStarted = 0,
    Present = 1,
    Late = 2,
    Incomplete = 3,
    OnLeave = 4,
    Absent = 5
}

public enum AttendanceEventType
{
    CheckIn = 0,
    CheckOut = 1
}

/// <summary>Giriş-çıkış kaydının kaynağı. QR ve Kiosk gelecek için ayrılmıştır.</summary>
public enum AttendanceSource
{
    WebButton = 0,
    AdminCorrection = 1,
    Qr = 2,
    Kiosk = 3
}

public enum CorrectionRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum DailyReportStatus
{
    Draft = 0,
    Submitted = 1,
    RevisionRequested = 2,
    Approved = 3,
    Rejected = 4
}

public enum ProjectStatus
{
    Planned = 0,
    InProgress = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ProjectTaskStatus
{
    Todo = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3,
    Cancelled = 4
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum LeaveRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

public enum LeaveType
{
    Excuse = 0,
    Sick = 1,
    Administrative = 2,
    Other = 3
}

public enum AiSummaryStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Disabled = 3
}

public enum AiSummaryType
{
    Weekly = 0,
    Monthly = 1,
    CustomRange = 2,
    FinalOverall = 3
}

public enum NotificationType
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3
}
