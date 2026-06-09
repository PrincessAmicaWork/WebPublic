# Lagerverwaltung Database Notes

## Strategic database direction

The target architecture now uses one shared Oracle database for multiple locations.

This replaces the previous plan where each site would have its own detached database and app configuration.

The shared database must store site information and use `SiteId` to separate data.

Initial sites:

```text
SOLOTHURN   Solothurn      Uses storage
STNIKLAUS   St. Niklaus    No storage
FRAUENFELD  Frauenfeld     No storage
```

## Oracle naming note

The current Oracle setup uses max identifier length 30.

Keep table, index, and constraint names short enough for Oracle.

Recommended short table names:

```text
SITES
USER_PREFS
USER_SITE_ACCESS
REQUEST_CATALOG_ITEMS
SITE_CATALOG_ITEMS
CATALOG_ITEM_TEXTS
EQUIPMENT_ORDERS
EQUIPMENT_ORDER_LINES
RETURN_REQUESTS
```

Avoid too-long names such as:

```text
REQUEST_CATALOG_ITEM_TRANSLATIONS
```

Use this instead:

```text
CATALOG_ITEM_TEXTS
```

## Current schema concepts

### Categories

Storage categories.

Current model: `Category`

Important fields:

```text
ID
Name
Comment
MinimumAmount
NeedsNotification
RESTOCK_EMAIL
```

Target shared DB change:

```text
Add SiteId
```

Reason:

```text
Categories are storage-related.
Storage data must be separated by site.
Solothurn can have storage categories.
St. Niklaus and Frauenfeld should not see or use Solothurn categories.
```

Target important fields:

```text
ID
SITE_ID
Name
Comment
MinimumAmount
NeedsNotification
RESTOCK_EMAIL
```

Relationships:

```text
Site has many Categories
Category has many Positions
```

### Positions

Physical storage items.

Current model: `Position`

Important fields:

```text
ID
PurchaseDate
Supplier
Price
Description
OrderNumber
CategoryId
STOCK_CONDITION
```

Target shared DB change:

```text
Add SiteId
```

Reason:

```text
A Position is a real physical item located at one site.
Storage queries must not mix Solothurn items with other sites.
```

Target important fields:

```text
ID
SITE_ID
PurchaseDate
Supplier
Price
Description
OrderNumber
CategoryId
STOCK_CONDITION
```

Relationships:

```text
Site has many Positions
Position belongs to Category
Position has many Issues
```

Important behavior:

```text
A Position is available when it has no open Issue and is not reserved by an active order line.
```

For Phase 1 migration:

```text
All existing Categories -> SiteId = Solothurn
All existing Positions  -> SiteId = Solothurn
```

### EquipmentRequests

Old request table/model.

Current model: `EquipmentRequest`

Important fields:

```text
Id
PositionId
RequesterName
RequesterEmail
SupervisorName
SupervisorEmail
Reason
USED_ITEM_OK
TICKET_NUMBER
Status
RequestDate
DecisionDate
FULFILLMENT_TYPE
FULFILLMENT_DATE
BossComment
APPROVE_TOKEN
DENY_TOKEN
```

Current limitation:

```text
PositionId is required.
One EquipmentRequest equals one physical storage item.
```

Transition strategy:

```text
Do not delete EquipmentRequests immediately.
Keep legacy requests readable.
Build new multi-line orders beside the old model.
```

Optional later change:

```text
Add nullable SiteId to legacy EquipmentRequest for reporting only.
Existing legacy requests should be assigned to Solothurn if they belong to current storage history.
```

### Issue

Physical handover history.

Current model: `Issue`

Important fields:

```text
ID
PositionId
TicketNumber
Username
CostCentre
IssueDate
TakeBackDate
```

Meaning:

```text
TakeBackDate = null means currently issued.
```

Target shared DB behavior:

```text
Issue belongs to a Position.
Position belongs to a Site.
Therefore Issue is indirectly site-scoped through Position.
```

Optional later change:

```text
Add SiteId to Issue only if query performance or reporting needs it.
```

### RETURN_REQUESTS

Old return request table/model.

Current model: `ReturnRequest`

Important fields:

```text
ID
EQUIPMENT_REQUEST_ID
POSITION_ID
REQUESTER_NAME
REQUESTER_EMAIL
STATUS
REQUESTED_AT
CONFIRMED_AT
USER_COMMENT
ADMIN_COMMENT
```

Current limitation:

```text
ReturnRequest points to old EquipmentRequest.
For multi-item orders, returns must point to EquipmentOrderLine instead.
```

Target change:

```text
ReturnRequest supports both legacy EquipmentRequestId and new EquipmentOrderLineId.
```

## New shared multi-site tables

### SITES

Represents a location in the shared database.

Proposed model:

```csharp
public class Site
{
    public int Id { get; set; }

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public AppStockPolicy StockPolicy { get; set; }

    public string ItEmail { get; set; } = "";
    public string LowStockEmail { get; set; } = "";

    public string DefaultCulture { get; set; } = "de-CH";
    public string AdminCulture { get; set; } = "de-CH";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}
```

Proposed enum:

```csharp
public enum AppStockPolicy
{
    UseCatalogDefault = 0,
    NeverUseStorage = 1,
    PreferStorageButAllowExternal = 2,
    RequireStorageForStockItems = 3
}
```

Initial seed data:

```text
Id: 1
Code: SOLOTHURN
Name: Solothurn
StockPolicy: RequireStorageForStockItems
ItEmail: placeholder until real email is known
LowStockEmail: placeholder until real email is known
DefaultCulture: de-CH
AdminCulture: de-CH

Id: 2
Code: STNIKLAUS
Name: St. Niklaus
StockPolicy: NeverUseStorage
ItEmail: placeholder until real email is known
LowStockEmail: empty or placeholder
DefaultCulture: de-CH
AdminCulture: de-CH

Id: 3
Code: FRAUENFELD
Name: Frauenfeld
StockPolicy: NeverUseStorage
ItEmail: placeholder until real email is known
LowStockEmail: empty or placeholder
DefaultCulture: de-CH
AdminCulture: de-CH
```

### USER_PREFS

Stores default settings per user.

Proposed model:

```csharp
public class UserPreference
{
    public int Id { get; set; }

    public string UserEmail { get; set; } = "";
    public string UserEmailNormalized { get; set; } = "";

    public int? LastSelectedSiteId { get; set; }
    public Site? LastSelectedSite { get; set; }

    public string PreferredCulture { get; set; } = "de-CH";

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
```

Purpose:

```text
Remember the user's last selected site.
Remember the user's preferred language.
Avoid forcing the user to select the same site on every visit.
```

Recommended unique index:

```text
UX_USER_PREFS_EMAIL on USER_EMAIL_NORMALIZED
```

### USER_SITE_ACCESS

Controls which user may access which site.

This table is optional for the first UI prototype, but recommended from the beginning because it prevents later security rework.

Proposed model:

```csharp
public class UserSiteAccess
{
    public int Id { get; set; }

    public string UserEmail { get; set; } = "";
    public string UserEmailNormalized { get; set; } = "";

    public int SiteId { get; set; }
    public Site? Site { get; set; }

    public bool CanOrder { get; set; } = true;
    public bool CanFulfill { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}
```

Purpose:

```text
Normal users can order for allowed sites.
Site IT can fulfill only allowed sites.
Global admins can switch between all sites.
```

Recommended unique index:

```text
UX_USA_EMAIL_SITE on USER_EMAIL_NORMALIZED, SITE_ID
```

## Target catalog tables

### REQUEST_CATALOG_ITEMS

The global request catalog from the big list.

This table describes what an item is. It does not decide at which site it is visible.

Proposed model:

```csharp
public class RequestCatalogItem
{
    public int Id { get; set; }

    public string ActionCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string ItemName { get; set; } = "";

    public string Currency { get; set; } = "CHF";
    public decimal Price { get; set; }
    public string BillingType { get; set; } = "";

    public CatalogFulfillmentMode FulfillmentMode { get; set; }

    public bool Returnable { get; set; }
    public bool RequiresComment { get; set; }
    public bool IsActive { get; set; } = true;
}
```

Proposed enum:

```csharp
public enum CatalogFulfillmentMode
{
    StockManaged = 0,
    StockOrExternalOrder = 1,
    ExternalOrderOnly = 2,
    ConsumableNoStock = 3,
    ServiceChange = 4,
    ReturnAction = 5
}
```

Important rule:

```text
Catalog item describes default behavior.
SiteCatalogItem and Site.StockPolicy decide effective behavior at a selected site.
```

### SITE_CATALOG_ITEMS

Connects a global catalog item to one site.

This table decides what a user sees after selecting a site.

Proposed model:

```csharp
public class SiteCatalogItem
{
    public int Id { get; set; }

    public int SiteId { get; set; }
    public Site? Site { get; set; }

    public int CatalogItemId { get; set; }
    public RequestCatalogItem? CatalogItem { get; set; }

    public bool IsActive { get; set; } = true;

    public CatalogFulfillmentMode? FulfillmentModeOverride { get; set; }

    public int? StorageCategoryId { get; set; }
    public Category? StorageCategory { get; set; }

    public decimal? PriceOverride { get; set; }
    public string BillingTypeOverride { get; set; } = "";

    public int SortOrder { get; set; }
}
```

Rules:

```text
Only active SiteCatalogItems are shown.
StorageCategoryId must belong to the same SiteId if set.
For NeverUseStorage sites, StorageCategoryId should normally be null.
For Solothurn stock-managed items, StorageCategoryId can point to a Solothurn Category.
```

Recommended unique index:

```text
UX_SITE_CAT_ITEM on SITE_ID, CATALOG_ITEM_ID
```

### CATALOG_ITEM_TEXTS

Stores translated catalog display text.

This table is for database-driven item names/descriptions.

Proposed model:

```csharp
public class CatalogItemText
{
    public int Id { get; set; }

    public int CatalogItemId { get; set; }
    public RequestCatalogItem? CatalogItem { get; set; }

    public string CultureCode { get; set; } = "de-CH";

    public string CategoryName { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string Description { get; set; } = "";
}
```

Supported planned culture codes:

```text
de-CH
en
fr
it
```

Recommended unique index:

```text
UX_CAT_TEXT_CULT on CATALOG_ITEM_ID, CULTURE_CODE
```

## Target new order tables/models

### EQUIPMENT_ORDERS

One submitted ticket/order.

Shared DB target model:

```csharp
public class EquipmentOrder
{
    public int Id { get; set; }

    public int SiteId { get; set; }
    public Site? Site { get; set; }

    public string UiCulture { get; set; } = "de-CH";

    public string TicketNumber { get; set; } = "";

    public string OrderedByName { get; set; } = "";
    public string OrderedByEmail { get; set; } = "";

    public string RequestedForName { get; set; } = "";
    public string RequestedForEmail { get; set; } = "";

    public string PickupContactName { get; set; } = "";
    public string PickupContactEmail { get; set; } = "";

    public string SupervisorName { get; set; } = " ";
    public string SupervisorEmail { get; set; } = " ";

    public string Reason { get; set; } = "";
    public string BossComment { get; set; } = " ";

    public OrderStatus Status { get; set; } = OrderStatus.PendingApproval;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DecisionDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string ApproveToken { get; set; } = Guid.NewGuid().ToString("N");
    public string DenyToken { get; set; } = Guid.NewGuid().ToString("N");

    public List<EquipmentOrderLine> Lines { get; set; } = new();
}
```

Proposed enum:

```csharp
public enum OrderStatus
{
    PendingApproval = 0,
    Approved = 1,
    Denied = 2,
    PartiallyFulfilled = 3,
    Completed = 4,
    Cancelled = 5
}
```

Important rules:

```text
SiteId is required.
UiCulture is required.
SiteId controls catalog, storage, IT email, admin visibility.
UiCulture controls display language and user/supervisor email language.
```

### EQUIPMENT_ORDER_LINES

One requested item inside an order.

Shared DB target model:

```csharp
public class EquipmentOrderLine
{
    public int Id { get; set; }

    public int EquipmentOrderId { get; set; }
    public EquipmentOrder? EquipmentOrder { get; set; }

    public int SiteCatalogItemId { get; set; }
    public SiteCatalogItem? SiteCatalogItem { get; set; }

    public int CatalogItemId { get; set; }
    public RequestCatalogItem? CatalogItem { get; set; }

    public int? PositionId { get; set; }
    public Position? Position { get; set; }

    public int Quantity { get; set; } = 1;

    public OrderLineStatus Status { get; set; } = OrderLineStatus.WaitingForApproval;
    public CatalogFulfillmentMode CatalogFulfillmentMode { get; set; }
    public EffectiveFulfillmentMode EffectiveFulfillmentMode { get; set; }

    public bool Returnable { get; set; }
    public bool UsedItemOk { get; set; }

    public string UserComment { get; set; } = " ";
    public string AdminComment { get; set; } = " ";

    public DateTime? FulfilledAt { get; set; }

    public string ActionCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string Currency { get; set; } = "CHF";
    public decimal UnitPrice { get; set; }
    public string BillingType { get; set; } = "";
}
```

Proposed enum:

```csharp
public enum OrderLineStatus
{
    WaitingForApproval = 0,
    Open = 1,
    Preparing = 2,
    WaitingForExternalOrder = 3,
    ReadyForPickup = 4,
    Fulfilled = 5,
    Cancelled = 6,
    Returned = 7
}
```

Proposed enum:

```csharp
public enum EffectiveFulfillmentMode
{
    UseStorage = 0,
    ManualNoStorage = 1,
    ExternalOrder = 2,
    ServiceChange = 3,
    ReturnAction = 4
}
```

Important rules:

```text
PositionId is nullable.
Issue is only created when PositionId has a value.
For St. Niklaus and Frauenfeld, PositionId stays null.
For Solothurn, stock-managed lines can receive a PositionId during IT fulfillment.
Returnable physical items should usually be one line per physical item.
Snapshot fields preserve old order history even if catalog data changes later.
```

## Target return changes

Existing `ReturnRequest` should support new order lines while preserving legacy requests.

Possible migration target:

```csharp
public class ReturnRequest
{
    public int Id { get; set; }

    public int? EquipmentRequestId { get; set; }        // legacy
    public EquipmentRequest? EquipmentRequest { get; set; }

    public int? EquipmentOrderLineId { get; set; }      // new
    public EquipmentOrderLine? EquipmentOrderLine { get; set; }

    public int? PositionId { get; set; }
    public Position? Position { get; set; }

    public string RequesterName { get; set; } = " ";
    public string RequesterEmail { get; set; } = " ";
    public ReturnRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string UserComment { get; set; } = " ";
    public string AdminComment { get; set; } = " ";
}
```

This keeps legacy requests possible while new returns use `EquipmentOrderLineId`.

## Target low-stock changes

Current low-stock checks old `EquipmentRequests.PositionId` for reserved items.

Target:

```text
Reserved physical storage = order lines where PositionId is not null and status is Preparing/ReadyForPickup/Open-reserved.
No-stock/manual lines must not affect stock count.
Low-stock checks only run for sites that use storage.
For initial setup this means Solothurn only.
```

Low-stock email routing:

```text
Use Site.LowStockEmail if set.
Fallback to Category.RestockEmail if required.
Do not send low-stock emails for NeverUseStorage sites.
```

## Site-specific data rules

### Solothurn

```text
Site.Code = SOLOTHURN
StockPolicy = RequireStorageForStockItems
Storage categories allowed
Positions allowed
Low-stock checks allowed
Stock-managed catalog items can map to StorageCategoryId
Issues are created only when PositionId exists
```

### St. Niklaus

```text
Site.Code = STNIKLAUS
StockPolicy = NeverUseStorage
No storage categories required
No positions required
No low-stock checks
No Issues for manual/no-storage lines
Orders are still tracked and fulfilled manually
```

### Frauenfeld

```text
Site.Code = FRAUENFELD
StockPolicy = NeverUseStorage
No storage categories required
No positions required
No low-stock checks
No Issues for manual/no-storage lines
Orders are still tracked and fulfilled manually
```

## Target migration strategy

Do not delete old tables immediately.

Recommended:

```text
1. Add SITES, USER_PREFS, USER_SITE_ACCESS.
2. Seed Solothurn, St. Niklaus, Frauenfeld.
3. Add SiteId to Categories.
4. Add SiteId to Positions.
5. Set all existing Categories and Positions to Solothurn.
6. Add CurrentSiteService in code.
7. Filter StorageService by CurrentSiteId.
8. Hide or disable storage UI for NeverUseStorage sites.
9. Add REQUEST_CATALOG_ITEMS and SITE_CATALOG_ITEMS.
10. Add EQUIPMENT_ORDERS and EQUIPMENT_ORDER_LINES.
11. Build new order flow using new tables.
12. Keep old EquipmentRequests readable for history during transition.
13. Later decide whether to migrate old requests into one-line orders.
```

## Phase 1 database scope

Phase 1 is only the foundation.

Create or change:

```text
SITES
USER_PREFS
USER_SITE_ACCESS
Category.SiteId
Position.SiteId
```

Seed:

```text
Solothurn
St. Niklaus
Frauenfeld
```

Migrate existing storage data:

```text
Existing Categories -> Solothurn
Existing Positions -> Solothurn
```

Do not yet build the full catalog/order migration in Phase 1.

## Rules to preserve

```text
No negative stock.
No Issue without a Position.
No whole-order return for physical items.
Approval is order-level.
Fulfillment is line-level.
Catalog data is snapshotted into order lines.
Legacy EquipmentRequest history stays readable.
Every new order must have SiteId.
Every storage query must filter by SiteId.
NeverUseStorage sites must not consume storage.
```
