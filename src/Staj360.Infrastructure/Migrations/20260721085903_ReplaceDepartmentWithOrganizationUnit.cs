using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Staj360.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDepartmentWithOrganizationUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) FK'ları kaldır (Departments tablosu henüz silinmez — veri korunur).
            migrationBuilder.DropForeignKey(
                name: "FK_InternProfiles_Departments_DepartmentId",
                table: "InternProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Departments_DepartmentId",
                table: "Projects");

            // 2) OrganizationUnits oluştur ve varsayılan şubeyi ekle (mevcut satırları map etmek için).
            migrationBuilder.CreateTable(
                name: "OrganizationUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    UnitType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationUnits_OrganizationUnits_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_Code",
                table: "OrganizationUnits",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_DisplayOrder",
                table: "OrganizationUnits",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_ParentId",
                table: "OrganizationUnits",
                column: "ParentId");

            var dirId = new Guid("A1000000-0000-0000-0000-000000000001");
            var branchId = new Guid("A1000000-0000-0000-0000-000000000002");
            var now = DateTime.UtcNow;

            migrationBuilder.InsertData(
                table: "OrganizationUnits",
                columns: new[] { "Id", "Code", "Name", "UnitType", "ParentId", "DisplayOrder", "IsActive", "CreatedAtUtc", "IsDeleted" },
                values: new object[] { dirId, "BILGI_ISLEM", "Bilgi İşlem Dairesi Başkanlığı", "Directorate", null, 40, true, now, false });

            migrationBuilder.InsertData(
                table: "OrganizationUnits",
                columns: new[] { "Id", "Code", "Name", "UnitType", "ParentId", "DisplayOrder", "IsActive", "CreatedAtUtc", "IsDeleted" },
                values: new object[] { branchId, "BILGI_TEKNOLOJILERI", "Bilgi Teknolojileri Şube Müdürlüğü", "Branch", dirId, 10, true, now, false });

            // 3) Kolonları yeniden adlandır ve mevcut FK değerlerini varsayılan şubeye map et.
            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "Projects",
                newName: "OrganizationUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_DepartmentId",
                table: "Projects",
                newName: "IX_Projects_OrganizationUnitId");

            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "InternProfiles",
                newName: "CurrentOrganizationUnitId");

            migrationBuilder.RenameIndex(
                name: "IX_InternProfiles_DepartmentId",
                table: "InternProfiles",
                newName: "IX_InternProfiles_CurrentOrganizationUnitId");

            migrationBuilder.Sql($"UPDATE InternProfiles SET CurrentOrganizationUnitId = '{branchId}'");
            migrationBuilder.Sql($"UPDATE Projects SET OrganizationUnitId = '{branchId}'");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationUnitId",
                table: "LeaveRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql($@"
UPDATE lr SET lr.OrganizationUnitId = ip.CurrentOrganizationUnitId
FROM LeaveRequests lr
INNER JOIN InternshipPeriods p ON p.Id = lr.InternshipPeriodId
INNER JOIN InternProfiles ip ON ip.Id = p.InternProfileId");

            migrationBuilder.Sql($"UPDATE LeaveRequests SET OrganizationUnitId = '{branchId}' WHERE OrganizationUnitId IS NULL");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationUnitId",
                table: "LeaveRequests",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // 4) Eski Departments tablosunu kaldır (stajyer/proje/izin satırları korunur).
            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.CreateTable(
                name: "InternFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReplyMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RepliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEscalated = table.Column<bool>(type: "bit", nullable: false),
                    EscalatedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternFeedbacks_InternProfiles_InternProfileId",
                        column: x => x.InternProfileId,
                        principalTable: "InternProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // OrganizationUnits tablosu yukarıda oluşturuldu.

            migrationBuilder.CreateTable(
                name: "AdvisorUnitAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorUnitAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvisorUnitAssignments_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InternTransferRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceOrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetOrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetAdvisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternTransferRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternTransferRequests_InternProfiles_InternProfileId",
                        column: x => x.InternProfileId,
                        principalTable: "InternProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternTransferRequests_OrganizationUnits_SourceOrganizationUnitId",
                        column: x => x.SourceOrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternTransferRequests_OrganizationUnits_TargetOrganizationUnitId",
                        column: x => x.TargetOrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InternUnitAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdvisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternUnitAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternUnitAssignments_InternProfiles_InternProfileId",
                        column: x => x.InternProfileId,
                        principalTable: "InternProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternUnitAssignments_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManagerUnitAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerUnitAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagerUnitAssignments_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_OrganizationUnitId",
                table: "LeaveRequests",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorUnitAssignments_AdvisorUserId_OrganizationUnitId",
                table: "AdvisorUnitAssignments",
                columns: new[] { "AdvisorUserId", "OrganizationUnitId" },
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorUnitAssignments_OrganizationUnitId",
                table: "AdvisorUnitAssignments",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InternFeedbacks_AdvisorUserId",
                table: "InternFeedbacks",
                column: "AdvisorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InternFeedbacks_InternProfileId",
                table: "InternFeedbacks",
                column: "InternProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InternFeedbacks_Status",
                table: "InternFeedbacks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InternTransferRequests_InternProfileId",
                table: "InternTransferRequests",
                column: "InternProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InternTransferRequests_SourceOrganizationUnitId",
                table: "InternTransferRequests",
                column: "SourceOrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InternTransferRequests_Status",
                table: "InternTransferRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InternTransferRequests_TargetOrganizationUnitId",
                table: "InternTransferRequests",
                column: "TargetOrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InternUnitAssignments_AdvisorUserId",
                table: "InternUnitAssignments",
                column: "AdvisorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InternUnitAssignments_InternProfileId_IsActive",
                table: "InternUnitAssignments",
                columns: new[] { "InternProfileId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InternUnitAssignments_OrganizationUnitId",
                table: "InternUnitAssignments",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerUnitAssignments_ManagerUserId_OrganizationUnitId",
                table: "ManagerUnitAssignments",
                columns: new[] { "ManagerUserId", "OrganizationUnitId" },
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerUnitAssignments_OrganizationUnitId",
                table: "ManagerUnitAssignments",
                column: "OrganizationUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_InternProfiles_OrganizationUnits_CurrentOrganizationUnitId",
                table: "InternProfiles",
                column: "CurrentOrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_OrganizationUnits_OrganizationUnitId",
                table: "LeaveRequests",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_OrganizationUnits_OrganizationUnitId",
                table: "Projects",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InternProfiles_OrganizationUnits_CurrentOrganizationUnitId",
                table: "InternProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_OrganizationUnits_OrganizationUnitId",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_OrganizationUnits_OrganizationUnitId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "AdvisorUnitAssignments");

            migrationBuilder.DropTable(
                name: "InternFeedbacks");

            migrationBuilder.DropTable(
                name: "InternTransferRequests");

            migrationBuilder.DropTable(
                name: "InternUnitAssignments");

            migrationBuilder.DropTable(
                name: "ManagerUnitAssignments");

            migrationBuilder.DropTable(
                name: "OrganizationUnits");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_OrganizationUnitId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId",
                table: "LeaveRequests");

            migrationBuilder.RenameColumn(
                name: "OrganizationUnitId",
                table: "Projects",
                newName: "DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_OrganizationUnitId",
                table: "Projects",
                newName: "IX_Projects_DepartmentId");

            migrationBuilder.RenameColumn(
                name: "CurrentOrganizationUnitId",
                table: "InternProfiles",
                newName: "DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_InternProfiles_CurrentOrganizationUnitId",
                table: "InternProfiles",
                newName: "IX_InternProfiles_DepartmentId");

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_InternProfiles_Departments_DepartmentId",
                table: "InternProfiles",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Departments_DepartmentId",
                table: "Projects",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
