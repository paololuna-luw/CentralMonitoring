using System;
using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralMonitoring.Infrastructure.Migrations
{
    [DbContext(typeof(MonitoringDbContext))]
    [Migration("20260212183000_AddAlertDispatchFields")]
    public partial class AddAlertDispatchFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DispatchAttempts",
                table: "AlertEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchedAtUtc",
                table: "AlertEvents",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DispatchAttempts",
                table: "AlertEvents");

            migrationBuilder.DropColumn(
                name: "DispatchedAtUtc",
                table: "AlertEvents");
        }
    }
}
