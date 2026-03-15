using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralMonitoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Step3_ApiV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetricSamples_HostId",
                table: "MetricSamples");

            migrationBuilder.RenameColumn(
                name: "Timestamp",
                table: "MetricSamples",
                newName: "TimestampUtc");

            migrationBuilder.AlterColumn<string>(
                name: "MetricKey",
                table: "MetricSamples",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "LabelsJson",
                table: "MetricSamples",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Hosts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Hosts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Hosts",
                type: "character varying(45)",
                maxLength: 45,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Hosts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Hosts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetricSamples_HostId_MetricKey_TimestampUtc",
                table: "MetricSamples",
                columns: new[] { "HostId", "MetricKey", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricSamples_HostId_TimestampUtc",
                table: "MetricSamples",
                columns: new[] { "HostId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Hosts_IpAddress",
                table: "Hosts",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_Hosts_Name",
                table: "Hosts",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetricSamples_HostId_MetricKey_TimestampUtc",
                table: "MetricSamples");

            migrationBuilder.DropIndex(
                name: "IX_MetricSamples_HostId_TimestampUtc",
                table: "MetricSamples");

            migrationBuilder.DropIndex(
                name: "IX_Hosts_IpAddress",
                table: "Hosts");

            migrationBuilder.DropIndex(
                name: "IX_Hosts_Name",
                table: "Hosts");

            migrationBuilder.DropColumn(
                name: "LabelsJson",
                table: "MetricSamples");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Hosts");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Hosts");

            migrationBuilder.RenameColumn(
                name: "TimestampUtc",
                table: "MetricSamples",
                newName: "Timestamp");

            migrationBuilder.AlterColumn<string>(
                name: "MetricKey",
                table: "MetricSamples",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Hosts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Hosts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Hosts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(45)",
                oldMaxLength: 45);

            migrationBuilder.CreateIndex(
                name: "IX_MetricSamples_HostId",
                table: "MetricSamples",
                column: "HostId");
        }
    }
}
