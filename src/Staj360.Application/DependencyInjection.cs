using Microsoft.Extensions.DependencyInjection;
using Staj360.Application.Ai;
using Staj360.Application.Services.Attendance;
using Staj360.Application.Services.DailyReports;
using Staj360.Application.Services.Departments;
using Staj360.Application.Services.Evaluations;
using Staj360.Application.Services.Internships;
using Staj360.Application.Services.Leaves;
using Staj360.Application.Services.Projects;

namespace Staj360.Application;

public static class DependencyInjection
{
    /// <summary>Application katmanı iş servislerini kaydeder.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IDailyReportService, DailyReportService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IWorkScheduleService, WorkScheduleService>();
        services.AddScoped<IInternshipPeriodService, InternshipPeriodService>();
        // IInternService Identity join gerektirdiği için Infrastructure'da kaydedilir.
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectTaskService, ProjectTaskService>();
        services.AddScoped<ILeaveRequestService, LeaveRequestService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddSingleton<IReportSummaryPromptBuilder, DefaultReportSummaryPromptBuilder>();

        return services;
    }
}
