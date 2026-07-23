using Staj360.Application.Services.DailyReports;
using Staj360.Domain.Enums;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.DailyReports;

public class DailyReportServiceTests
{
    private static DailyReportService CreateService(Staj360.Infrastructure.Persistence.AppDbContext db, DateTime utc) =>
        new(db, new TestClock(utc), new NoOpAuditLogService());

    [Fact]
    public async Task Create_DuplicateSameDay_IsRejected()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, userId, _, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var service = CreateService(db, utc);
            var date = new DateOnly(2026, 7, 13);
            Assert.True((await service.CreateAsync(userId, new CreateDailyReportCommand(date, null, null, null, null))).Success);
            var second = await service.CreateAsync(userId, new CreateDailyReportCommand(date, "ikinci", null, null, null));
            Assert.False(second.Success);
            Assert.Equal("DUPLICATE_REPORT", second.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task Submit_WithoutWorkItems_IsRejected()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, userId, _, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var service = CreateService(db, utc);
            var created = await service.CreateAsync(userId, new CreateDailyReportCommand(new DateOnly(2026, 7, 13), null, null, null, null));
            var submit = await service.SubmitAsync(userId, created.Data!.Id);
            Assert.False(submit.Success);
            Assert.Equal("NO_WORK_ITEM", submit.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task ApprovedReport_CannotBeEditedByIntern()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, userId, mentorId, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var service = CreateService(db, utc);
            var created = await service.CreateAsync(userId, new CreateDailyReportCommand(new DateOnly(2026, 7, 13), "not", null, null, null));
            await service.AddWorkItemAsync(userId, new AddWorkItemCommand(created.Data!.Id, null, null, "İş", "açıklama", 60, "C#", null, null));
            Assert.True((await service.SubmitAsync(userId, created.Data.Id)).Success);
            Assert.True((await service.ReviewAsync(mentorId, new ReviewDailyReportCommand(created.Data.Id, ReviewDecision.Approve, "OK"))).Success);
            var update = await service.UpdateAsync(userId, new UpdateDailyReportCommand(created.Data.Id, "değişiklik", null, null, null));
            Assert.False(update.Success);
            Assert.Equal("NOT_EDITABLE", update.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task Intern_CannotAccess_OtherInternReport()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, userId, _, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var service = CreateService(db, utc);
            var created = await service.CreateAsync(userId, new CreateDailyReportCommand(new DateOnly(2026, 7, 13), null, null, null, null));
            var other = await service.GetForInternAsync(Guid.NewGuid(), created.Data!.Id);
            Assert.False(other.Success);
            Assert.Equal("NOT_FOUND", other.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task Mentor_CannotReview_OtherMentorsIntern()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, userId, mentorId, _, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var service = CreateService(db, utc);
            var created = await service.CreateAsync(userId, new CreateDailyReportCommand(new DateOnly(2026, 7, 13), null, null, null, null));
            await service.AddWorkItemAsync(userId, new AddWorkItemCommand(created.Data!.Id, null, null, "İş", null, 30, null, null, null));
            Assert.True((await service.SubmitAsync(userId, created.Data.Id)).Success);
            var review = await service.ReviewAsync(Guid.NewGuid(), new ReviewDailyReportCommand(created.Data.Id, ReviewDecision.Approve, null));
            Assert.False(review.Success);
            Assert.Equal("FORBIDDEN", review.ErrorCode);
            Assert.True((await service.GetForMentorAsync(mentorId, created.Data.Id)).Success);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }
}
