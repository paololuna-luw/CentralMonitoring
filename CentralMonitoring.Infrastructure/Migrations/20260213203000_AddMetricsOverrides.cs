using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralMonitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MonitoringDbContext))]
    [Migration("20260213203000_AddMetricsOverrides")]
    public partial class AddMetricsOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetricsJson",
                table: "SnmpTargets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetricsJson",
                table: "Hosts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetricsJson",
                table: "SnmpTargets");

            migrationBuilder.DropColumn(
                name: "MetricsJson",
                table: "Hosts");
        }
    }
}
