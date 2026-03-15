using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralMonitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HostId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TriggerValue = table.Column<double>(type: "double precision", nullable: false),
                    Threshold = table.Column<double>(type: "double precision", nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertEvents_Hosts_HostId",
                        column: x => x.HostId,
                        principalTable: "Hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_HostId_MetricKey_IsResolved",
                table: "AlertEvents",
                columns: new[] { "HostId", "MetricKey", "IsResolved" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertEvents");
        }
    }
}
