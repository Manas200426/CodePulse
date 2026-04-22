using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePulse.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncidentCorrelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DownstreamIncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpstreamIncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DownstreamServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpstreamServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    TimeDifferenceMinutes = table.Column<double>(type: "double precision", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentCorrelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentCorrelations_Incidents_DownstreamIncidentId",
                        column: x => x.DownstreamIncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentCorrelations_Incidents_UpstreamIncidentId",
                        column: x => x.UpstreamIncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentCorrelations_DownstreamIncidentId_UpstreamIncidentId",
                table: "IncidentCorrelations",
                columns: new[] { "DownstreamIncidentId", "UpstreamIncidentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentCorrelations_UpstreamIncidentId",
                table: "IncidentCorrelations",
                column: "UpstreamIncidentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IncidentCorrelations");
        }
    }
}
