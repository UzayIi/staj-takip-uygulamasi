using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Staj360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditMessagingAndManagerProjectEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedStartDate",
                table: "InternTransferRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationUnitId",
                table: "DailyReports",
                type: "uniqueidentifier",
                nullable: true);

            // Mevcut raporlara stajyerin güncel birimini snapshot olarak yaz.
            migrationBuilder.Sql("""
                UPDATE dr
                SET dr.OrganizationUnitId = ip.CurrentOrganizationUnitId
                FROM DailyReports dr
                INNER JOIN InternshipPeriods p ON p.Id = dr.InternshipPeriodId
                INNER JOIN InternProfiles ip ON ip.Id = p.InternProfileId
                WHERE dr.OrganizationUnitId IS NULL;
                """);

            // Hâlâ boş kalanlar (orphan) için ilk aktif şubeyi kullan.
            migrationBuilder.Sql("""
                UPDATE DailyReports
                SET OrganizationUnitId = (
                    SELECT TOP 1 Id FROM OrganizationUnits
                    WHERE UnitType = 1 AND IsDeleted = 0 AND IsActive = 1
                    ORDER BY DisplayOrder, Name)
                WHERE OrganizationUnitId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationUnitId",
                table: "DailyReports",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorNameSnapshot",
                table: "AuditLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorRoleSnapshot",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorUserId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReasonCode",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuccessful",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationUnitId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestMethod",
                table: "AuditLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPath",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafeDescription",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AuditLogs",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StaffMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ParentMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArchivedBySender = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedByRecipient = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffMessages_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffMessages_StaffMessages_ParentMessageId",
                        column: x => x.ParentMessageId,
                        principalTable: "StaffMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternTransferRequests_PlannedStartDate",
                table: "InternTransferRequests",
                column: "PlannedStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailyReports_OrganizationUnitId",
                table: "DailyReports",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_IpAddress",
                table: "AuditLogs",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationUnitId",
                table: "AuditLogs",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_OrganizationUnitId",
                table: "StaffMessages",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_ParentMessageId",
                table: "StaffMessages",
                column: "ParentMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_RecipientUserId",
                table: "StaffMessages",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_RecipientUserId_IsRead",
                table: "StaffMessages",
                columns: new[] { "RecipientUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_SenderUserId",
                table: "StaffMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_SentAtUtc",
                table: "StaffMessages",
                column: "SentAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StaffMessages_ThreadId",
                table: "StaffMessages",
                column: "ThreadId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyReports_OrganizationUnits_OrganizationUnitId",
                table: "DailyReports",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyReports_OrganizationUnits_OrganizationUnitId",
                table: "DailyReports");

            migrationBuilder.DropTable(
                name: "StaffMessages");

            migrationBuilder.DropIndex(
                name: "IX_InternTransferRequests_PlannedStartDate",
                table: "InternTransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_DailyReports_OrganizationUnitId",
                table: "DailyReports");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_IpAddress",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_OrganizationUnitId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PlannedStartDate",
                table: "InternTransferRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId",
                table: "DailyReports");

            migrationBuilder.DropColumn(
                name: "ActorNameSnapshot",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorRoleSnapshot",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "FailureReasonCode",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IsSuccessful",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RequestMethod",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RequestPath",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "SafeDescription",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "AuditLogs");
        }
    }
}
