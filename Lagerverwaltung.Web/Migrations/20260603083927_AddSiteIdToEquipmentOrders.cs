using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteIdToEquipmentOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE LAGERVERWALTUNG.EQUIPMENT_ORDERS SET SITE_ID = 1 WHERE SITE_ID IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EQORD_SITE_STATUS",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS",
                columns: new[] { "SITE_ID", "STATUS", "CREATED_AT" });

            migrationBuilder.AddForeignKey(
                name: "FK_EQORD_SITE",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS",
                column: "SITE_ID",
                principalSchema: "LAGERVERWALTUNG",
                principalTable: "SITES",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EQORD_SITE",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS");

            migrationBuilder.DropIndex(
                name: "IX_EQORD_SITE_STATUS",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS");

            migrationBuilder.DropColumn(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS");
        }
    }
}
