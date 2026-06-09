# Lagerverwaltung Flows

## Current flow: old single-item request

```text
User selects one item from a catalog built from Positions.
EquipmentService searches matching Positions.
EquipmentService immediately selects one available Position.
EquipmentRequest is created with required PositionId.
Supervisor approves or denies the request.
IT marks the request as preparing.
IT completes handover.
Issue is created for the Position.
User can request return for that EquipmentRequest.
IT confirms return and closes the open Issue.
```

Current problem:

```text
The request itself is already a storage reservation.
```

This is too limited for the new request list and for shared-database multi-site behavior.

## Target first step: select site

Before a user sees the request catalog or creates an order, the app must know the active site.

Flow:

```text
User opens app.
App reads logged-in user.
App reads UserPreference.
If LastSelectedSiteId exists and user still has access:
  use that site.
Else:
  show site selection.
User selects site.
App stores LastSelectedSiteId.
App loads catalog and rules for selected site.
```

Initial site choices:

```text
Solothurn
St. Niklaus
Frauenfeld
```

Initial stock behavior:

```text
Solothurn: uses storage
St. Niklaus: no storage
Frauenfeld: no storage
```

Important rule:

```text
No site context = no order catalog and no order submission.
```

## Optional same screen: select language

Site and language are separate.

Recommended selection screen:

```text
Where are you located?
  Solothurn
  St. Niklaus
  Frauenfeld

Language:
  Deutsch
  English
  Francais
  Italiano
```

Storage/catalog behavior comes from Site.

UI/email language comes from Culture.

## Target flow: shared DB multi-line order

```text
User opens app.
User selects active site if needed.
User opens request catalog.
Catalog is filtered by selected site.
User adds one or more catalog items to a cart/order.
User selects whether the order is for self or someone else.
User selects supervisor.
User submits one order.
Order stores SiteId and UiCulture.
Supervisor approves or denies the whole order.
After approval, IT notification goes to the selected site's IT email.
IT processes each order line separately.
Each line can be assigned to storage, externally ordered, completed manually, cancelled, or fulfilled.
```

## Target catalog loading

Catalog is not loaded from Positions.

Target:

```text
REQUEST_CATALOG_ITEMS = global item master
SITE_CATALOG_ITEMS = site-specific visibility and behavior
```

Catalog load flow:

```text
Get CurrentSiteId.
Load active SITE_CATALOG_ITEMS for CurrentSiteId.
Join REQUEST_CATALOG_ITEMS.
Apply language text from CATALOG_ITEM_TEXTS if available.
Show only active items.
```

Example:

```text
USB Mouse
  Solothurn: visible, can use storage
  St. Niklaus: visible, manual/no-storage
  Frauenfeld: visible, manual/no-storage

27 inch Monitor
  Solothurn: visible, can use storage
  St. Niklaus: hidden
  Frauenfeld: visible, manual/no-storage
```

## Target order submission

At submit time:

```text
Validate selected site exists and is active.
Validate user may order for selected site.
Validate catalog items are active for selected site.
Validate supervisor exists and is active.
Create EquipmentOrder with SiteId and UiCulture.
Create EquipmentOrderLines.
Snapshot catalog data into order lines.
Generate approval/deny tokens for the order.
Send one approval email for the whole order.
Do not assign a physical Position yet.
Do not make stock negative.
```

Important submit rule:

```text
Order submission does not reserve stock.
```

Reason:

```text
Unapproved orders should not block warehouse stock.
IT chooses the physical item after approval.
```

## Target approval

Approval is order-level.

```text
Supervisor approves order WEB-123.
Order becomes Approved.
All lines move from WaitingForApproval to Open, unless the order is denied.
IT receives one notification email with all lines.
```

IT email routing:

```text
Use EquipmentOrder.Site.ItEmail.
```

Examples:

```text
Solothurn order -> Solothurn IT email
St. Niklaus order -> St. Niklaus IT email
Frauenfeld order -> Frauenfeld IT email
```

If denied:

```text
Order becomes Denied.
Lines become Cancelled or Denied.
Requester/contact is notified.
No stock is reserved.
No Issue is created.
```

## Target IT fulfillment

Fulfillment is line-level.

For each order line, IT can do one of these:

```text
Assign Position from storage
Mark waiting for external order
Mark manual/service item as completed
Cancel one line
Mark line as fulfilled
```

Effective behavior depends on selected site and catalog item.

## Target stock assignment: Solothurn

Solothurn uses storage.

Recommended rule:

```text
Submit order:
  no physical Position is reserved

Supervisor approval:
  still no forced Position reservation

IT prepares/assigns line:
  if line is stock-managed and stock is available:
    assign PositionId
  if line is stock-managed and no stock is available:
    mark WaitingForExternalOrder or ManualNoStorage depending on process
  if line is no-stock/service:
    PositionId stays null

IT handover:
  create Issue only if PositionId exists
```

Important:

```text
RequireStorageForStockItems does not mean every catalog item uses storage.
It only means catalog items that are stock-managed must use storage.
Service items, mutations, subscriptions, and consumables can still be no-stock lines.
```

## Target no-storage behavior: St. Niklaus and Frauenfeld

St. Niklaus and Frauenfeld do not use storage.

For these sites:

```text
Site.StockPolicy = NeverUseStorage
PositionId always stays null
No Issue is created
No storage availability is changed
No low-stock check is triggered
IT fulfills lines manually or externally
```

Example:

```text
User selects St. Niklaus.
User orders USB Mouse.
Order line is created.
After approval, IT completes the line manually.
PositionId remains null.
No Issue is created.
Solothurn storage is not touched.
```

## Target no-stock behavior

Some catalog items never need storage, even in Solothurn.

Examples:

```text
mobile subscription
mobile cancellation
printer mutation
monitor mutation
possibly cables/adapters/USB sticks if treated as consumables
manual external order lines
```

For these lines:

```text
PositionId = null
No Issue is created
No storage availability is changed
Line can still be tracked and completed
```

## Target storage views

Storage dashboard and storage item lists must be site-scoped.

Flow:

```text
Get CurrentSiteId.
Get CurrentSite.StockPolicy.
If CurrentSite.StockPolicy is NeverUseStorage:
  hide storage module or show a clear disabled message.
If CurrentSite uses storage:
  load only Categories and Positions where SiteId = CurrentSiteId.
```

Initial result:

```text
Solothurn sees existing storage.
St. Niklaus sees no storage module or disabled storage view.
Frauenfeld sees no storage module or disabled storage view.
```

Storage query rule:

```text
Every storage query must filter by SiteId.
```

## Target low-stock flow

Low-stock checks apply only to sites that use storage.

For Solothurn:

```text
Check Solothurn Categories and Positions.
Send low-stock notification to Site.LowStockEmail or fallback recipient.
```

For St. Niklaus and Frauenfeld:

```text
Skip low-stock checks.
No storage exists.
No low-stock email is sent.
```

## Target return flow

Returns must be line-level.

```text
User sees issued/fulfilled items individually.
User clicks Return on one item/line.
ReturnRequest is created for one EquipmentOrderLine.
IT confirms the return.
If the line has PositionId:
  close open Issue
  mark Position as Used
  make Position available again
If the line has no PositionId:
  complete manual return/ticket action
  no storage count changes
```

Important rule:

```text
Returning a mouse must not return the keyboard and headset from the same order.
```

## Target order-for-someone-else flow

The app must separate the creator of an order from the person receiving the equipment.

```text
OrderedBy = logged-in person who created the order
RequestedFor = person who receives/uses the equipment
PickupContact = person IT contacts when equipment is ready
Supervisor = normally the supervisor of RequestedFor
```

Example:

```text
OrderedBy: Sarah Worker
RequestedFor: Max Newhire
PickupContact: Sarah Worker
Supervisor: Max's supervisor
```

Issues should be created for `RequestedFor`, not necessarily `OrderedBy`.

## Target user views

### Site selection

Shown when there is no valid current site.

```text
Select site:
  Solothurn
  St. Niklaus
  Frauenfeld

Optional language:
  de-CH
  en
  fr
  it
```

### My Orders

Shows submitted orders/tickets for the user.

Recommended user rule:

```text
Show orders created by the user or requested for the user.
Show site name on each order.
```

Example:

```text
WEB-123 | Solothurn | Approved | 3 lines
WEB-124 | St. Niklaus | Pending Approval | 1 line
```

### My Equipment

Shows individual issued/fulfilled returnable lines.

```text
Mouse       Solothurn       Return
Keyboard    Solothurn       Return
Headset     St. Niklaus     Return/manual action if returnable
```

## Target admin views

Admin should see one order row with expandable lines.

Site admins should only see their allowed sites.

```text
WEB-123 | Solothurn | RequestedFor Max Newhire | Approved | 3 lines

  Line 1 | Mouse    | Open      | Assign Position
  Line 2 | Keyboard | Open      | Assign Position
  Line 3 | Headset  | Waiting   | External order
```

For no-storage sites:

```text
WEB-200 | St. Niklaus | RequestedFor Max Newhire | Approved | 2 lines

  Line 1 | Mouse       | Open | Complete manually
  Line 2 | Subscription| Open | Complete service change
```

Admin filtering rule:

```text
Admin order lists must filter by allowed SiteIds.
Global admins may switch site or view all sites if explicitly designed.
```

## Phase 1 flow

Phase 1 is the foundation only.

Flow to build first:

```text
App starts.
User is authenticated.
CurrentSiteService checks UserPreference.
If no valid site exists, show site selection.
User selects Solothurn, St. Niklaus, or Frauenfeld.
Selection is saved.
Storage dashboard uses CurrentSiteId.
Solothurn can view storage.
St. Niklaus and Frauenfeld cannot use storage.
```

Phase 1 does not require the new catalog/order flow yet.

## Phase 2 flow

Catalog becomes site-aware.

```text
User has CurrentSiteId.
App loads SITE_CATALOG_ITEMS for CurrentSiteId.
User sees only items available at that site.
```

## Phase 3 flow

New order flow becomes site-aware.

```text
User submits EquipmentOrder with SiteId and UiCulture.
Lines reference SiteCatalogItem and CatalogItem.
Approval email is order-level.
IT email goes to Site.ItEmail.
Fulfillment follows Site.StockPolicy.
```

## Rules to preserve

```text
User must select a site before creating an order.
Every new order has SiteId.
Every storage query filters by SiteId.
No negative stock.
No Issue without a Position.
No whole-order return for physical items.
Approval is order-level.
Fulfillment is line-level.
NeverUseStorage sites never consume storage.
Catalog data is snapshotted into order lines.
Site controls catalog, storage behavior, admin visibility, and IT email routing.
Culture controls UI and email language only.
Legacy EquipmentRequests stay readable during transition.
```
