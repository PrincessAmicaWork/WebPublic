using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EQUIPMENT_ORDERS",
                schema: "LAGERVERWALTUNG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TICKET_NUMBER = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ORDERED_BY_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ORDERED_BY_EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    REQUESTED_FOR_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    REQUESTED_FOR_EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    PICKUP_CONTACT_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PICKUP_CONTACT_EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    SUPERVISOR_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SUPERVISOR_EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    REASON = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    BOSS_COMMENT = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DECISION_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    COMPLETED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    APPROVE_TOKEN = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DENY_TOKEN = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQUIPMENT_ORDERS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "EQUIPMENT_ORDER_LINES",
                schema: "LAGERVERWALTUNG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    EQUIPMENT_ORDER_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CATALOG_ITEM_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    POSITION_ID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    QUANTITY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CATALOG_FULFILLMENT_MODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    EFFECTIVE_FULFILLMENT_MODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RETURNABLE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    USED_ITEM_OK = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    USER_COMMENT = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    ADMIN_COMMENT = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    FULFILLED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ACTION_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    CATEGORY_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    MANUFACTURER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ITEM_NAME = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    CURRENCY = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    UNIT_PRICE = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    BILLING_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQUIPMENT_ORDER_LINES", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EQOL_ORDER",
                        column: x => x.EQUIPMENT_ORDER_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "EQUIPMENT_ORDERS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EQOL_POSITION",
                        column: x => x.POSITION_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "Positions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EQOL_REQCAT",
                        column: x => x.CATALOG_ITEM_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "REQUEST_CATALOG_ITEMS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EQOL_CATALOG",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDER_LINES",
                column: "CATALOG_ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EQOL_ORDER",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDER_LINES",
                column: "EQUIPMENT_ORDER_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EQOL_POSITION",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDER_LINES",
                column: "POSITION_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EQOL_STATUS",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDER_LINES",
                column: "STATUS");

            migrationBuilder.CreateIndex(
                name: "IX_EQORD_STATUS_CREATED",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS",
                columns: new[] { "STATUS", "CREATED_AT" });

            migrationBuilder.CreateIndex(
                name: "UX_EQORD_TICKET",
                schema: "LAGERVERWALTUNG",
                table: "EQUIPMENT_ORDERS",
                column: "TICKET_NUMBER",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EQUIPMENT_ORDER_LINES",
                schema: "LAGERVERWALTUNG");

            migrationBuilder.DropTable(
                name: "EQUIPMENT_ORDERS",
                schema: "LAGERVERWALTUNG");
        }
    }
}
