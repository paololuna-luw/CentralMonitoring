using System;
using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralMonitoring.Infrastructure.Migrations
{
    [DbContext(typeof(MonitoringDbContext))]
    [Migration("20260212182000_AddSnmpTargetGrace")]
    public partial class AddSnmpTargetGrace : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "SnmpTargets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFailureUtc",
                table: "SnmpTargets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSuccessUtc",
                table: "SnmpTargets",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "SnmpTargets");

            migrationBuilder.DropColumn(
                name: "LastFailureUtc",
                table: "SnmpTargets");

            migrationBuilder.DropColumn(
                name: "LastSuccessUtc",
                table: "SnmpTargets");
        }
    }
}
