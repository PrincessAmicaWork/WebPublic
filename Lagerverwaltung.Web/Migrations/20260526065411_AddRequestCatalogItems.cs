using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "REQUEST_CATALOG_ITEMS",
                schema: "LAGERVERWALTUNG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ACTION_CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    CATEGORY_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    MANUFACTURER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ITEM_NAME = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    CURRENCY = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: false),
                    PRICE = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    BILLING_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    FULFILLMENT_MODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RETURNABLE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    REQUIRES_COMMENT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_ACTIVE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STORAGE_CATEGORY_ID = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REQUEST_CATALOG_ITEMS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_REQCAT_CATEGORY",
                        column: x => x.STORAGE_CATEGORY_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "Categories",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REQCAT_ACTIVE_CAT",
                schema: "LAGERVERWALTUNG",
                table: "REQUEST_CATALOG_ITEMS",
                columns: new[] { "IS_ACTIVE", "CATEGORY_NAME" });

            migrationBuilder.CreateIndex(
                name: "IX_REQCAT_STOR_CAT",
                schema: "LAGERVERWALTUNG",
                table: "REQUEST_CATALOG_ITEMS",
                column: "STORAGE_CATEGORY_ID");

            migrationBuilder.CreateIndex(
                name: "UX_REQCAT_ACTION",
                schema: "LAGERVERWALTUNG",
                table: "REQUEST_CATALOG_ITEMS",
                column: "ACTION_CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REQUEST_CATALOG_ITEMS",
                schema: "LAGERVERWALTUNG");
        }
    }
}
