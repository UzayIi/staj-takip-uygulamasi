using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Attendance;

public record AttendanceActionContext(
    AttendanceSource Source = AttendanceSource.WebButton,
    string? IpAddress = null,
    string? DeviceInfo = null);

public record AttendanceDaySummary(
    DateOnly WorkDate,
    DateTime? FirstCheckInLocal,
    DateTime? LastCheckOutLocal,
    int TotalWorkedMinutes,
    AttendanceStatus Status,
    bool IsLate,
    bool IsEarlyLeave,
    bool IsIncomplete,
    bool HasOpenCheckIn);
