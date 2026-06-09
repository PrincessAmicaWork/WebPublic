# Lagerverwaltung AI Context

## Purpose

Lagerverwaltung is an internal IT warehouse and equipment ordering app.

The current app was originally built around this rule:

```text
1 equipment request = 1 physical storage position
```

The target model is:

```text
1 equipment order/ticket = many requested lines
1 order line = one requested thing
1 order line may or may not be connected to a physical storage position
```

This is needed because the request list contains stock items, consumables, subscriptions, mutations, returns, cancellations, and manual/external order items.

## Strategic direction

The app will now use one shared application and one shared database for multiple locations.

This replaces the previous idea that each site receives its own separate app copy and database.

The shared database must support site separation:

```text
One app
One database
Multiple sites
Site-specific catalog visibility
Site-specific stock behavior
Site-specific IT notification email
Site-specific admin/fulfillment views
```

Current initial sites:

```text
Solothurn
  Code: SOLOTHURN
  Uses storage: yes
  StockPolicy: RequireStorageForStockItems

St. Niklaus
  Code: STNIKLAUS
  Uses storage: no
  StockPolicy: NeverUseStorage

Frauenfeld
  Code: FRAUENFELD
  Uses storage: no
  StockPolicy: NeverUseStorage
```

## Current technology

- ASP.NET Core
- Blazor / Razor components
- MudBlazor
- Entity Framework Core
- Oracle database
- Microsoft Entra ID / Azure AD authentication
- SMTP email notifications
- Serilog logging
- Swagger / minimal API endpoints
- Hosted approver CSV import worker

## Current important services

- `EquipmentService`: current single-item request flow, approval, preparation, handover, emails
- `StorageService`: storage dashboard, categories, positions, availability, delete/destroy logic
- `ReturnService`: current return request flow for old single-item requests
- `LowStockNotificationService`: minimum stock checks and restock emails
- `ApproverService`: supervisor list and approver validation
- `EmailService`: SMTP email sending
- `CurrentUserService`: reads logged-in user name/email from claims

## New important services

### CurrentSiteService

Central service that determines the active site for the logged-in user.

Responsibilities:

```text
Read logged-in user
Load available sites for user
Load last selected site from UserPreference
Set/change current site
Validate that the user may use the selected site
Expose CurrentSiteId and CurrentSite to other services
```

No page or business service should manually guess the active site.

### SiteCatalogService

Loads catalog items that are visible for the current site.

Responsibilities:

```text
Filter catalog by CurrentSiteId
Apply SITE_CATALOG_ITEMS rules
Apply language/culture text lookup later
Return only active items for the selected site
```

### OrderingService

Creates multi-line EquipmentOrders.

Responsibilities:

```text
Require selected site before order submission
Store SiteId on the order
Store UiCulture on the order
Create EquipmentOrderLines
Snapshot catalog item data into lines
Send approval email
Do not reserve stock on submission
```

### FulfillmentService

Processes approved order lines.

Responsibilities:

```text
Use Site.StockPolicy to decide whether storage may be used
For Solothurn stock-managed items can use Position
For St. Niklaus and Frauenfeld PositionId stays null
Create Issue only when PositionId has a value
Send IT/user notifications using site-specific routing
```

## Current important models

- `Category`: storage category
- `Position`: one physical storage item
- `EquipmentRequest`: current old request model, tied to one `PositionId`
- `Issue`: physical handover/issued record for a `Position`
- `ReturnRequest`: current old return model, tied to one `EquipmentRequest` and one `Position`
- `Approver`: supervisor/approver record
- `LowStockNotificationLog`: restock notification history
- `Deleted`: delete log
- `Destroyed`: destroyed item log

## New important models

### Site

Represents one location/site in the shared database.

Examples:

```text
Solothurn
St. Niklaus
Frauenfeld
```

A site controls:

```text
Which catalog items are visible
Which stock policy applies
Which storage data is visible/usable
Which IT email receives fulfillment notifications
Which low-stock email receives restock notifications
Which admins can process orders
```

### UserPreference

Stores per-user defaults.

Examples:

```text
LastSelectedSiteId
PreferredCulture
```

This lets the app remember the user's last selected site and language.

### UserSiteAccess

Optional but recommended access table.

It defines which sites a user can access and which role they have per site.

Examples:

```text
CanOrder
CanFulfill
IsAdmin
IsDefault
```

### RequestCatalogItem

A global requestable item from the big request list.

Examples:

- 24 inch monitor
- 27 inch monitor
- USB mouse
- USB keyboard
- headset
- notebook bag
- iPhone cable
- NATEL subscription
- printer mutation
- monitor return
- iPad

The global catalog item describes the item itself.

### SiteCatalogItem

Connects a global RequestCatalogItem to a Site.

This table decides whether a catalog item exists at a site and how it behaves there.

Example:

```text
USB Mouse
  Solothurn: visible, stock-managed, mapped to storage category
  St. Niklaus: visible, no-storage/manual
  Frauenfeld: visible, no-storage/manual
```

### EquipmentOrder

One submitted order/ticket.

The supervisor approves this level.

Important target fields:

```text
SiteId
UiCulture
OrderedBy
RequestedFor
PickupContact
Supervisor
Status
```

### EquipmentOrderLine

One requested thing inside an order.

IT fulfills this level.

Each line may have:

```text
PositionId = some value   physical storage item assigned
PositionId = null         no physical storage item assigned
```

### Position

One real physical item in storage.

In the shared database every Position must belong to a Site.

For the initial migration, existing Positions belong to Solothurn.

Examples:

- actual monitor
- actual iPad
- actual phone
- actual docking station

### Category

A storage category.

In the shared database every Category must belong to a Site.

For the initial migration, existing Categories belong to Solothurn.

### Issue

A physical item was handed out to a person.

Only create an `Issue` when a real `Position` is handed out.

No Issue is created for no-storage/manual lines.

### ReturnRequest

A request to return one fulfilled order line.

Returns must be line-level, not whole-order-level.

Legacy return requests can still point to old EquipmentRequest during transition.

## Current limitation

The old request model cannot cleanly represent:

- multiple items in one ticket
- return one item from a multi-item ticket
- catalog/service items that do not have storage
- external/manual orders without a physical `Position`
- ordering for another person
- multiple sites in one shared database
- site-specific catalog visibility
- site-specific stock behavior
- site-specific IT email routing

The old model forces `EquipmentRequest.PositionId` to exist.

## Site and language separation

Site and language are independent concepts.

```text
Site = business location, catalog, stock policy, storage, IT routing
Culture = UI language, email language, translated catalog display text
```

Supported cultures planned:

```text
de-CH
en
fr
it
```

Initial fallback culture:

```text
de-CH
```

A user can select a site and a language separately.

Example:

```text
Selected site: St. Niklaus
Selected language: French
```

This means the user sees the St. Niklaus catalog and rules, but UI/email text can be French.

## Target modules inside one deployable app

Do not split into separate frontend/backend apps yet.

Use a modular monolith:

```text
Lagerverwaltung.Web
  Blazor pages/components
  API endpoints
  Program.cs/auth setup

Application/services
  current site service
  site access service
  catalog service
  ordering service
  fulfillment logic
  storage service
  return service
  approval logic
  notification logic
  localization support

Domain/models
  Site
  UserPreference
  UserSiteAccess
  RequestCatalogItem
  SiteCatalogItem
  EquipmentOrder
  EquipmentOrderLine
  enums and core rules

Infrastructure/data
  AppDbContext
  EF Core mappings
  email
  Oracle-specific setup
  CSV import worker
```

The app can stay as one deployed ASP.NET/Blazor application.

## Key target rules

```text
User must select a site before creating an order.
Every new EquipmentOrder must have SiteId.
Every storage Category and Position must have SiteId.
Catalog visibility is controlled by SiteCatalogItem.
Order approval happens at order level.
IT fulfillment happens at line level.
Physical storage is optional per line.
No physical Position is assigned when the user submits an order.
No negative stock.
No Issue without a Position.
No whole-order return for physical items.
Solothurn can use storage.
St. Niklaus and Frauenfeld must not use storage.
Site controls IT notification email.
Culture controls display and email language.
Legacy EquipmentRequests stay readable during transition.
```
