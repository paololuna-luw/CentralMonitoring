using System;
using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralMonitoring.Infrastructure.Migrations
{
    [DbContext(typeof(MonitoringDbContext))]
    [Migration("20260211104500_AddRules")]
    public partial class AddRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Operator = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Threshold = table.Column<double>(type: "double precision", nullable: false),
                    WindowMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HostId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnmpIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    LabelContains = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rules_Enabled_MetricKey",
                table: "Rules",
                columns: new[] { "Enabled", "MetricKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Rules_HostId",
                table: "Rules",
                column: "HostId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rules");
        }
    }
}
