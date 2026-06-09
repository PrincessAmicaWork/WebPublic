using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "EQUIPMENT_REQUEST_ID",
                schema: "LAGERVERWALTUNG",
                table: "RETURN_REQUESTS",
                type: "NUMBER(10)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AddColumn<int>(
                name: "EQUIPMENT_ORDER_LINE_ID",
                schema: "LAGERVERWALTUNG",
                table: "RETURN_REQUESTS",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RETURN_REQ_OLINE",
                schema: "LAGERVERWALTUNG",
                table: "RETURN_REQUESTS",
                column: "EQUIPMENT_ORDER_LINE_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_RET_ORDER_LINE",
                schema: "LAGERVERWALTUNG",
                table: "RETURN_REQUESTS",
                column: "EQUIPMENT_ORDER_LINE_ID",
                principalSchema: "LAGERVERWALTUNG",
                principalTable: "EQUIPMENT_ORDER_LINES",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RET_ORDER_LINE",
                schema: "LAGERVERWALTUNG",
                table: "RETURN_REQUESTS");

            migrationBuilder.DropIndex(
                name: "IX_RETURN_REQ_OLINE",
                schema: "LAGERVERWALTUNG",
                table: "RETURN_REQUESTS");

            migrationBuilder.DropColumn(
                name: "EQUIPMENT_ORDER_LINE_ID",
                schema: "LAGERVERWALTUNG",
                table: "RETURN_REQUESTS");

            migrationBuilder.AlterColumn<int>(
                name: "EQUIPMENT_REQUEST_ID",
                schema: "LAGERVERWALTUNG",
                table: "RETURN_REQUESTS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)",
                oldNullable: true);
        }
    }
}
