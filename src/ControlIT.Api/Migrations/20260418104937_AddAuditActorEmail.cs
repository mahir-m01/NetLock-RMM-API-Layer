using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlIT.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActorEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "actor_email",
                table: "controlit_audit_log",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actor_email",
                table: "controlit_audit_log");
        }
    }
}
