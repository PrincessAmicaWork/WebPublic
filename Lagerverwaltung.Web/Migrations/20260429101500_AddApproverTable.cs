using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddApproverTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "LAGERVERWALTUNG");

            migrationBuilder.CreateTable(
    name: "Approver",
    schema: "LAGERVERWALTUNG",
    columns: table => new
    {
        Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
            .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),

        DisplayName = table.Column<string>(
            type: "NVARCHAR2(200)",
            maxLength: 200,
            nullable: false),

        Email = table.Column<string>(
            type: "NVARCHAR2(320)",
            maxLength: 320,
            nullable: false),

        EmailNormalized = table.Column<string>(
            type: "NVARCHAR2(320)",
            maxLength: 320,
            nullable: false),

        // FIXED:
        // C# property is bool, Oracle storage is NUMBER(1).
        // No int defaultValue here.
        IsActive = table.Column<bool>(
            type: "NUMBER(1)",
            nullable: false),

        Source = table.Column<string>(
            type: "NVARCHAR2(50)",
            maxLength: 50,
            nullable: false),

        CreatedAt = table.Column<DateTime>(
            type: "TIMESTAMP(7)",
            nullable: false),

        UpdatedAt = table.Column<DateTime>(
            type: "TIMESTAMP(7)",
            nullable: false),

        LastSyncedAt = table.Column<DateTime>(
            type: "TIMESTAMP(7)",
            nullable: true)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_Approver", x => x.Id);
    });

            migrationBuilder.CreateIndex(
                name: "UX_APPROVER_EMAIL_N",
                schema: "LAGERVERWALTUNG",
                table: "Approver",
                column: "EmailNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Approver",
                schema: "LAGERVERWALTUNG");
        }
    }
}
