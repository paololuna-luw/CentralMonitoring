using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CentralMonitoring.Infrastructure.Migrations
{
    [DbContext(typeof(MonitoringDbContext))]
    [Migration("20260211081000_AddAlertCooldown")]
    public partial class AddAlertCooldown : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastTriggerAtUtc",
                table: "AlertEvents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<double>(
                name: "LastTriggerValue",
                table: "AlertEvents",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Occurrences",
                table: "AlertEvents",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTriggerAtUtc",
                table: "AlertEvents");

            migrationBuilder.DropColumn(
                name: "LastTriggerValue",
                table: "AlertEvents");

            migrationBuilder.DropColumn(
                name: "Occurrences",
                table: "AlertEvents");
        }
    }
}
