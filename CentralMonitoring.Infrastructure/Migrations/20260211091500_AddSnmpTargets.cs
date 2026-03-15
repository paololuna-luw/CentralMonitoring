using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

using CentralMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CentralMonitoring.Infrastructure.Migrations
{
    [DbContext(typeof(MonitoringDbContext))]
    [Migration("20260211091500_AddSnmpTargets")]
    public partial class AddSnmpTargets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SnmpTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Version = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Community = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SecurityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AuthProtocol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AuthPassword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PrivProtocol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PrivPassword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Profile = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnmpTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SnmpTargets_Enabled",
                table: "SnmpTargets",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_SnmpTargets_IpAddress",
                table: "SnmpTargets",
                column: "IpAddress");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SnmpTargets");
        }
    }
}
