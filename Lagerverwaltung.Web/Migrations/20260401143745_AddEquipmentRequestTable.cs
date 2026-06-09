using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentRequestTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "LAGERVERWALTUNG");

            // migrationBuilder.CreateTable(
            // name: "Categories",
            // schema: "LAGERVERWALTUNG",
            // columns: table => new
            // {
            // ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
            // .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
            // Name = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
            // Comment = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
            // MinimumAmount = table.Column<int>(type: "NUMBER(10)", nullable: false),
            // NeedsNotification = table.Column<bool>(type: "BOOLEAN", nullable: false)
            // },
            // constraints: table =>
            // {
            // table.PrimaryKey("PK_Categories", x => x.ID);
            // });

            // migrationBuilder.CreateTable(
            // name: "Positions",
            // schema: "LAGERVERWALTUNG",
            // columns: table => new
            // {
            // ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
            // .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
            // PurchaseDate = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
            // Supplier = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
            // Price = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
            // Description = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
            // OrderNumber = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
            // CategoryId = table.Column<int>(type: "NUMBER(10)", nullable: false)
            // },
            // constraints: table =>
            // {
            // table.PrimaryKey("PK_Positions", x => x.ID);
            // table.ForeignKey(
            // name: "FK_Positions_Categories_Categ~",
            // column: x => x.CategoryId,
            // principalSchema: "LAGERVERWALTUNG",
            // principalTable: "Categories",
            // principalColumn: "ID",
            // onDelete: ReferentialAction.Cascade);
            // });

            migrationBuilder.CreateTable(
                name: "EquipmentRequests",
                schema: "LAGERVERWALTUNG",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PositionId = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RequesterName = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    RequesterEmail = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Department = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Reason = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    BossComment = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    APPROVE_TOKEN = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    DENY_TOKEN = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentRequests_Position~",
                        column: x => x.PositionId,
                        principalSchema: "LAGERVERWALTUNG",
                        principalTable: "Positions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            // migrationBuilder.CreateTable(
            // name: "Issue",
            // schema: "LAGERVERWALTUNG",
            // columns: table => new
            // {
            // ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
            // .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
            // PositionId = table.Column<int>(type: "NUMBER(10)", nullable: false),
            // TicketNumber = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
            // Username = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
            // CostCentre = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
            // IssueDate = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
            // TakeBackDate = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
            // },
            // constraints: table =>
            // {
            // table.PrimaryKey("PK_Issue", x => x.ID);
            // table.ForeignKey(
            // name: "FK_Issue_Positions_PositionId",
            // column: x => x.PositionId,
            // principalSchema: "LAGERVERWALTUNG",
            // principalTable: "Positions",
            // principalColumn: "ID",
            // onDelete: ReferentialAction.Cascade);
            // });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRequests_Position~",
                schema: "LAGERVERWALTUNG",
                table: "EquipmentRequests",
                column: "PositionId");
        }

            // migrationBuilder.CreateIndex(
            // name: "IX_Issue_PositionId",
            // schema: "LAGERVERWALTUNG",
            // table: "Issue",
            // column: "PositionId");
            
            // migrationBuilder.CreateIndex(
            // name: "IX_Positions_CategoryId",
            // schema: "LAGERVERWALTUNG",
            // table: "Positions",
            // column: "CategoryId");
            // }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentRequests",
                schema: "LAGERVERWALTUNG");

            migrationBuilder.DropTable(
                name: "Issue",
                schema: "LAGERVERWALTUNG");

            migrationBuilder.DropTable(
                name: "Positions",
                schema: "LAGERVERWALTUNG");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "LAGERVERWALTUNG");
        }
    }
}
