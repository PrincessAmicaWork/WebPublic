using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "Positions",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "Categories",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SITES",
                schema: "LAGERVERWALTUNG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 4 INCREMENT BY 1"),
                    CODE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    IS_ACTIVE = table.Column<int>(type: "NUMBER(1)", precision: 1, nullable: false),
                    STOCK_POLICY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IT_EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    LOW_STOCK_EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    DEFAULT_CULTURE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ADMIN_CULTURE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ENTRA_GROUP_ID = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UPDATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SITES", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "USER_PREFS",
                schema: "LAGERVERWALTUNG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USER_EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    USER_EMAIL_N = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    LAST_SITE_ID = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    PREFERRED_CULTURE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    UPDATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USER_PREFS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PREF_SITE",
                        column: x => x.LAST_SITE_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "SITES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "USER_SITE_ACCESS",
                schema: "LAGERVERWALTUNG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    USER_EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    USER_EMAIL_N = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    SITE_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CAN_ORDER = table.Column<int>(type: "NUMBER(1)", precision: 1, nullable: false),
                    CAN_FULFILL = table.Column<int>(type: "NUMBER(1)", precision: 1, nullable: false),
                    IS_ADMIN = table.Column<int>(type: "NUMBER(1)", precision: 1, nullable: false),
                    IS_DEFAULT = table.Column<int>(type: "NUMBER(1)", precision: 1, nullable: false),
                    IS_ACTIVE = table.Column<int>(type: "NUMBER(1)", precision: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USER_SITE_ACCESS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_USA_SITE",
                        column: x => x.SITE_ID,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "SITES",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            // Raw SQL seed is intentional.
            // Oracle EF Core can fail when InsertData contains NUMBER(1) bool values as ints.
            migrationBuilder.Sql(@"
INSERT INTO LAGERVERWALTUNG.SITES
    (ID, ADMIN_CULTURE, CODE, CREATED_AT, DEFAULT_CULTURE, ENTRA_GROUP_ID, IS_ACTIVE, IT_EMAIL, LOW_STOCK_EMAIL, NAME, STOCK_POLICY, UPDATED_AT)
VALUES
    (1, 'de-CH', 'SOLOTHURN', TIMESTAMP '2026-01-01 00:00:00', 'de-CH', 'ef374943-f32c-4455-9b34-261749223f05', 1, 'CH.ITService@bosch.com', 'pol2sn@bosch.com', 'Solothurn', 3, NULL)");

            migrationBuilder.Sql(@"
INSERT INTO LAGERVERWALTUNG.SITES
    (ID, ADMIN_CULTURE, CODE, CREATED_AT, DEFAULT_CULTURE, ENTRA_GROUP_ID, IS_ACTIVE, IT_EMAIL, LOW_STOCK_EMAIL, NAME, STOCK_POLICY, UPDATED_AT)
VALUES
    (2, 'de-CH', 'STNIKLAUS', TIMESTAMP '2026-01-01 00:00:00', 'de-CH', ' ', 1, 'CH.ITService@bosch.com', ' ', 'St. Niklaus', 1, NULL)");

            migrationBuilder.Sql(@"
INSERT INTO LAGERVERWALTUNG.SITES
    (ID, ADMIN_CULTURE, CODE, CREATED_AT, DEFAULT_CULTURE, ENTRA_GROUP_ID, IS_ACTIVE, IT_EMAIL, LOW_STOCK_EMAIL, NAME, STOCK_POLICY, UPDATED_AT)
VALUES
    (3, 'de-CH', 'FRAUENFELD', TIMESTAMP '2026-01-01 00:00:00', 'de-CH', ' ', 1, 'CH.ITService@bosch.com', ' ', 'Frauenfeld', 1, NULL)");

            migrationBuilder.Sql(
                "UPDATE LAGERVERWALTUNG.\"Categories\" SET SITE_ID = 1 WHERE SITE_ID IS NULL");

            migrationBuilder.Sql(
                "UPDATE LAGERVERWALTUNG.\"Positions\" SET SITE_ID = 1 WHERE SITE_ID IS NULL");

            migrationBuilder.AlterColumn<int>(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "Positions",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "Categories",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_POS_SITE",
                schema: "LAGERVERWALTUNG",
                table: "Positions",
                column: "SITE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_POS_SITE_CAT",
                schema: "LAGERVERWALTUNG",
                table: "Positions",
                columns: new[] { "SITE_ID", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CAT_SITE",
                schema: "LAGERVERWALTUNG",
                table: "Categories",
                column: "SITE_ID");

            migrationBuilder.CreateIndex(
                name: "UX_SITES_CODE",
                schema: "LAGERVERWALTUNG",
                table: "SITES",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PREF_SITE",
                schema: "LAGERVERWALTUNG",
                table: "USER_PREFS",
                column: "LAST_SITE_ID");

            migrationBuilder.CreateIndex(
                name: "UX_USER_PREF_EMAIL",
                schema: "LAGERVERWALTUNG",
                table: "USER_PREFS",
                column: "USER_EMAIL_N",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USA_SITE",
                schema: "LAGERVERWALTUNG",
                table: "USER_SITE_ACCESS",
                column: "SITE_ID");

            migrationBuilder.CreateIndex(
                name: "UX_USA_EMAIL_SITE",
                schema: "LAGERVERWALTUNG",
                table: "USER_SITE_ACCESS",
                columns: new[] { "USER_EMAIL_N", "SITE_ID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CAT_SITE",
                schema: "LAGERVERWALTUNG",
                table: "Categories",
                column: "SITE_ID",
                principalSchema: "LAGERVERWALTUNG",
                principalTable: "SITES",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POS_SITE",
                schema: "LAGERVERWALTUNG",
                table: "Positions",
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
                name: "FK_CAT_SITE",
                schema: "LAGERVERWALTUNG",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_POS_SITE",
                schema: "LAGERVERWALTUNG",
                table: "Positions");

            migrationBuilder.DropTable(
                name: "USER_PREFS",
                schema: "LAGERVERWALTUNG");

            migrationBuilder.DropTable(
                name: "USER_SITE_ACCESS",
                schema: "LAGERVERWALTUNG");

            migrationBuilder.DropTable(
                name: "SITES",
                schema: "LAGERVERWALTUNG");

            migrationBuilder.DropIndex(
                name: "IX_POS_SITE",
                schema: "LAGERVERWALTUNG",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_POS_SITE_CAT",
                schema: "LAGERVERWALTUNG",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_CAT_SITE",
                schema: "LAGERVERWALTUNG",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "SITE_ID",
                schema: "LAGERVERWALTUNG",
                table: "Categories");
        }
    }
}
