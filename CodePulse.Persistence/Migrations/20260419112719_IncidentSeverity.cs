using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncidentSeverity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "Incidents",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Severity",
                table: "Incidents");
        }
    }
}
