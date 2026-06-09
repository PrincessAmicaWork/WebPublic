using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lagerverwaltung.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedSiteCatalogItemsAllSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert one SITE_CATALOG_ITEMS row for every combination of active site and
            // catalog item that does not already have an entry.
            // IS_ACTIVE = 1, SORT_ORDER = 0, BILLING_TYPE_OVERRIDE = ' ' (single space)
            // matches the model defaults so that item visibility falls back to the
            // global catalog defaults until overridden per-site in the admin UI.
            migrationBuilder.Sql(@"
INSERT INTO LAGERVERWALTUNG.SITE_CATALOG_ITEMS
    (SITE_ID, CATALOG_ITEM_ID, IS_ACTIVE, BILLING_TYPE_OVERRIDE, SORT_ORDER)
SELECT s.ID, c.ID, 1, ' ', 0
FROM LAGERVERWALTUNG.SITES s
CROSS JOIN LAGERVERWALTUNG.REQUEST_CATALOG_ITEMS c
WHERE NOT EXISTS (
    SELECT 1
    FROM LAGERVERWALTUNG.SITE_CATALOG_ITEMS sci
    WHERE sci.SITE_ID = s.ID
      AND sci.CATALOG_ITEM_ID = c.ID
)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove only the rows that were created by this seed migration
            // (i.e. rows with all-default values that have no overrides set).
            migrationBuilder.Sql(@"
DELETE FROM LAGERVERWALTUNG.SITE_CATALOG_ITEMS
WHERE FULFILLMENT_MODE_OVERRIDE IS NULL
  AND STORAGE_CATEGORY_ID IS NULL
  AND PRICE_OVERRIDE IS NULL
  AND BILLING_TYPE_OVERRIDE = ' '
  AND SORT_ORDER = 0");
        }
    }
}