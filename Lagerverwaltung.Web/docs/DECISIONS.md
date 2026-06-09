# Architecture Decisions

## ADR-001: Keep one deployable app for now

Status: Accepted

Decision:

```text
Keep Lagerverwaltung as one ASP.NET Core / Blazor application for now.
```

Reason:

```text
The app is internal.
There is currently one main developer.
A separate frontend/backend split would add deployment, API, DTO, CORS, auth, and debugging overhead.
The current priority is to clean up the business model and add shared-database multi-site support, not to add deployment complexity.
```

Consequence:

```text
Program.cs can still host UI, API endpoints, auth, services, EF Core, and background workers.
The internal code should become more modular.
```

## ADR-002: Refactor toward a modular monolith

Status: Accepted

Decision:

```text
Use one app, but separate business areas clearly.
```

Target areas:

```text
Sites
User preferences
Site access
Catalog
Ordering
Fulfillment
Storage
Returns
Approvers
Notifications
Localization
```

Reason:

```text
The old request/storage/return model is becoming too coupled.
A modular monolith is easier for one developer than multiple deployed apps.
It still prepares the code for a possible future API split.
```

## ADR-003: Separate catalog from storage

Status: Accepted

Decision:

```text
RequestCatalogItem represents what users can request.
Position represents what physically exists in storage.
```

Reason:

```text
The big request list contains physical items, consumables, subscriptions, mutations, external orders, and return/cancellation actions.
Not all of these can or should be represented as Positions.
```

Consequence:

```text
The user-facing catalog must not be built only from Positions.
Catalog items can optionally link to a storage category through SiteCatalogItem.
```

## ADR-004: Add EquipmentOrder and EquipmentOrderLine

Status: Accepted

Decision:

```text
Replace the old long-term idea of EquipmentRequest with:

EquipmentOrder = one ticket/submission/approval
EquipmentOrderLine = one requested item inside the ticket
```

Reason:

```text
A user must be able to order multiple things in one ticket.
A supervisor should approve one ticket, not many duplicated single-item requests.
IT must still fulfill and return items individually.
```

Consequence:

```text
Approval happens on EquipmentOrder.
Fulfillment happens on EquipmentOrderLine.
```

## ADR-005: Make PositionId optional on order lines

Status: Accepted

Decision:

```text
EquipmentOrderLine.PositionId must be nullable.
```

Reason:

```text
Many order lines are not physical stock items.
Some lines are services, mutations, consumables, or external/manual tasks.
St. Niklaus and Frauenfeld do not use storage at all.
```

Consequence:

```text
No Issue is created when PositionId is null.
No storage count changes when PositionId is null.
```

## ADR-006: Do not reserve stock at order submission

Status: Accepted

Decision:

```text
Do not assign a physical Position when the user submits an order.
```

Reason:

```text
Unapproved orders should not block warehouse stock.
Stock should not go negative.
IT should decide fulfillment after approval.
Site stock policy must be applied during fulfillment, not during user submission.
```

Consequence:

```text
Stock is checked when IT prepares/assigns a line, unless the selected site is configured as NeverUseStorage.
If no stock exists, the line waits for external order or manual fulfillment.
```

## ADR-007: Approval at order level, fulfillment at line level

Status: Accepted

Decision:

```text
Supervisor approves or denies the whole order.
IT processes each line separately.
```

Reason:

```text
This matches how a basket/ticket should work.
It still allows mouse, keyboard, and headset to be fulfilled or returned independently.
```

## ADR-008: Returns are line-level

Status: Accepted

Decision:

```text
New returns must point to EquipmentOrderLine, not the whole order.
```

Reason:

```text
If a user ordered mouse, keyboard, and headset in one ticket, they must be able to return only the mouse.
```

Consequence:

```text
ReturnRequest should support EquipmentOrderLineId.
Legacy EquipmentRequestId can remain temporarily for old requests.
```

## ADR-009: Separate OrderedBy and RequestedFor

Status: Accepted

Decision:

```text
Orders store both the logged-in creator and the person receiving the equipment.
```

Reason:

```text
A worker may order equipment for a new hire before they start.
The equipment should be issued in the new hire's name, not the creator's name.
```

Consequence:

```text
OrderedBy comes from the logged-in user.
RequestedFor comes from the order form.
Issue.Username should use RequestedForName.
```

## ADR-010: Use shared-database multi-site support

Status: Accepted

Decision:

```text
Use one shared database for multiple sites.
Add a Site table.
Add SiteId to new orders.
Add SiteId to storage data.
Filter catalog, storage, orders, and admin views by selected site.
```

Reason:

```text
Management wants all locations in one shared system and one shared database.
Users must choose where they are before placing an order.
Different locations need different catalog items, stock behavior, and IT routing.
```

Consequence:

```text
The previous separate-database approach is replaced.
The app must always know the active site before creating orders or showing site-specific data.
Cross-site data leakage must be avoided.
```

## ADR-011: User selects active site before ordering

Status: Accepted

Decision:

```text
A user must select the active site before seeing or creating an order.
The selected site is stored in UserPreference as LastSelectedSiteId.
```

Reason:

```text
The selected site decides which catalog items exist, which storage policy applies, and which IT email receives notifications.
The app cannot safely show the order catalog without a site context.
```

Consequence:

```text
A CurrentSiteService is required.
Pages and services must use CurrentSiteService instead of manually reading site values.
```

## ADR-012: Sites control stock policy

Status: Accepted

Decision:

```text
StockPolicy belongs to Site.
```

Initial site policies:

```text
Solothurn: RequireStorageForStockItems
St. Niklaus: NeverUseStorage
Frauenfeld: NeverUseStorage
```

Reason:

```text
Solothurn uses the warehouse/storage module.
St. Niklaus and Frauenfeld process orders without storage.
```

Consequence:

```text
Solothurn can assign Positions to stock-managed order lines.
St. Niklaus and Frauenfeld keep PositionId null and process lines manually or externally.
Low-stock checks only apply to storage-enabled sites.
```

## ADR-013: Sites control catalog visibility

Status: Accepted

Decision:

```text
REQUEST_CATALOG_ITEMS is the global item master.
SITE_CATALOG_ITEMS controls which item is visible at which site and how it behaves there.
```

Reason:

```text
The same item can exist at multiple sites with different fulfillment behavior.
Some items may exist only at one site.
Duplicating the complete catalog per site would create unnecessary maintenance work.
```

Consequence:

```text
Catalog queries must filter by CurrentSiteId through SITE_CATALOG_ITEMS.
Site-specific overrides can be added without changing the global catalog item.
```

## ADR-014: Sites control IT email routing

Status: Accepted

Decision:

```text
IT notification email is stored on Site.
```

Reason:

```text
An order from Solothurn should be handled by the Solothurn IT team.
An order from St. Niklaus or Frauenfeld may need a different IT recipient.
Hard-coded IT emails would break shared-database multi-site behavior.
```

Consequence:

```text
After approval, the IT notification uses EquipmentOrder.Site.ItEmail.
Low-stock notifications use Site.LowStockEmail or a defined fallback.
```

## ADR-015: Keep site and language separate

Status: Accepted

Decision:

```text
Site and Culture are separate values.
Site controls business behavior.
Culture controls display and email language.
```

Supported planned cultures:

```text
de-CH
en
fr
it
```

Reason:

```text
A user can work at St. Niklaus but prefer French or English UI.
Business rules must not be inferred from language.
```

Consequence:

```text
EquipmentOrder stores both SiteId and UiCulture.
UserPreference stores LastSelectedSiteId and PreferredCulture.
```

## ADR-016: Preserve legacy EquipmentRequest history during transition

Status: Accepted

Decision:

```text
Do not immediately delete or rewrite old EquipmentRequests.
```

Reason:

```text
The current app already has request, issue, and return history.
A big migration increases risk.
```

Recommended path:

```text
Add new site and order tables beside old EquipmentRequests.
Build new flow on new tables.
Keep old requests read-only or legacy-visible.
Later decide whether to migrate old requests into one-line orders.
```

## ADR-017: Phase 1 only builds the shared-site foundation

Status: Accepted

Decision:

```text
Phase 1 adds the site foundation only.
It does not build the full new order flow yet.
```

Phase 1 includes:

```text
SITES
USER_PREFS
USER_SITE_ACCESS
SiteId on Category
SiteId on Position
Seed Solothurn, St. Niklaus, Frauenfeld
Assign existing storage data to Solothurn
CurrentSiteService
Site selection UI
StorageService filters by CurrentSiteId
Storage UI disabled or hidden for NeverUseStorage sites
```

Reason:

```text
The shared database must be safe before catalog and order functionality is rebuilt.
A missing SiteId can cause data leakage between locations.
```

Consequence:

```text
Catalog and EquipmentOrder work starts after the site foundation is stable.
```

## ADR-018: New code must be localization-ready

Status: Accepted

Decision:

```text
New screens, dialogs, validation messages, and emails should be built with localization keys where practical.
German/de-CH remains the first complete fallback language.
```

Reason:

```text
Full translation can be filled later, but new hard-coded German UI creates avoidable rework.
```

Consequence:

```text
Do not stop Phase 1 for full translation.
But do not create large new flows with hard-coded German text if they are already being rebuilt.
```

## Superseded decisions

The following earlier ideas are superseded by ADR-010:

```text
Use app-instance stock policy instead of database Site policy.
Keep multi-site database support out of scope.
No Site table.
No SiteId on orders.
Separate app/database per site.
```

They are replaced by:

```text
One shared app/database.
Site table required.
SiteId required on new orders and storage data.
Site-specific stock policy.
Site-specific catalog visibility.
Site-specific IT routing.
```
