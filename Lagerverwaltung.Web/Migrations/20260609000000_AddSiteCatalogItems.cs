using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SITE_CATALOG_ITEMS",
                schema: "LAGERVERWALTUNG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    SITE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CATALOG_ITEM_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IS_ACTIVE = table.Column<int>(type: "NUMBER(1)", precision: 1, nullable: false),
                    FULFILLMENT_MODE_OVERRIDE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    STORAGE_CATEGORY_ID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PRICE_OVERRIDE = table.Column<decimal>(type: "NUMBER(18,2)", nullable: true),
                    BILLING_TYPE_OVERRIDE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SITE_CATALOG_ITEMS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SCAT_SITE",
                        column: x => x.SITE_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "SITES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SCAT_REQCAT",
                        column: x => x.CATALOG_ITEM_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "REQUEST_CATALOG_ITEMS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SCAT_CATEGORY",
                        column: x => x.STORAGE_CATEGORY_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "Categories",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_SCAT_SITE_CATALOG",
                schema: "LAGERVERWALTUNG",
                table: "SITE_CATALOG_ITEMS",
                columns: new[] { "SITE_ID", "CATALOG_ITEM_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCAT_SITE",
                schema: "LAGERVERWALTUNG",
                table: "SITE_CATALOG_ITEMS",
                column: "SITE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SCAT_CATALOG",
                schema: "LAGERVERWALTUNG",
                table: "SITE_CATALOG_ITEMS",
                column: "CATALOG_ITEM_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SITE_CATALOG_ITEMS",
                schema: "LAGERVERWALTUNG");
        }
    }
}