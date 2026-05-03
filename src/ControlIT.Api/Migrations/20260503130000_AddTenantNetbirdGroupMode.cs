using System;
using ControlIT.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlIT.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ControlItDbContext))]
    [Migration("20260503130000_AddTenantNetbirdGroupMode")]
    public partial class AddTenantNetbirdGroupMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "controlit_managed",
                table: "controlit_tenant_netbird_group",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "group_mode",
                table: "controlit_tenant_netbird_group",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "managed")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "controlit_tenant_netbird_group",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.CreateIndex(
                name: "idx_tenant_netbird_group_group",
                table: "controlit_tenant_netbird_group",
                column: "netbird_group_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_tenant_netbird_group_group",
                table: "controlit_tenant_netbird_group");

            migrationBuilder.DropColumn(
                name: "controlit_managed",
                table: "controlit_tenant_netbird_group");

            migrationBuilder.DropColumn(
                name: "group_mode",
                table: "controlit_tenant_netbird_group");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "controlit_tenant_netbird_group");
        }
    }
}
