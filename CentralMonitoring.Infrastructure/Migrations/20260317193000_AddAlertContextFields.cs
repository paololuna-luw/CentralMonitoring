using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralMonitoring.Infrastructure.Migrations
{
    [DbContext(typeof(MonitoringDbContext))]
    [Migration("20260317193000_AddAlertContextFields")]
    public partial class AddAlertContextFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContextKey",
                table: "AlertEvents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabelsJson",
                table: "AlertEvents",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_AlertEvents_HostId_MetricKey_IsResolved",
                table: "AlertEvents");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_HostId_MetricKey_ContextKey_IsResolved",
                table: "AlertEvents",
                columns: new[] { "HostId", "MetricKey", "ContextKey", "IsResolved" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertEvents_HostId_MetricKey_ContextKey_IsResolved",
                table: "AlertEvents");

            migrationBuilder.DropColumn(
                name: "ContextKey",
                table: "AlertEvents");

            migrationBuilder.DropColumn(
                name: "LabelsJson",
                table: "AlertEvents");

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_HostId_MetricKey_IsResolved",
                table: "AlertEvents",
                columns: new[] { "HostId", "MetricKey", "IsResolved" });
        }
    }
}
