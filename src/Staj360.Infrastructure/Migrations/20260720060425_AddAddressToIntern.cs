using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Staj360.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressToIntern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "InternProfiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "InternProfiles");
        }
    }
}
