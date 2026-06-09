using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lagerverwaltung.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Lagerverwaltung.Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Position> Positions { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Issue> Issues { get; set; } = null!;
        public DbSet<EquipmentRequest> EquipmentRequests { get; set; } = null!;
        public DbSet<Deleted> Deleted { get; set; } = null!;
        public DbSet<Destroyed> Destroyeds { get; set; } = null!;
        public DbSet<ReturnRequest> ReturnRequests { get; set; } = null!;
        public DbSet<LowStockNotificationLog> LowStockNotificationLogs { get; set; } = null!;
        public DbSet<Approver> Approvers { get; set; } = null!;
        public DbSet<RequestCatalogItem> RequestCatalogItems { get; set; } = null!;
        public DbSet<EquipmentOrder> EquipmentOrders { get; set; } = null!;
        public DbSet<EquipmentOrderLine> EquipmentOrderLines { get; set; } = null!;
        public DbSet<Site> Sites { get; set; } = null!;
        public DbSet<UserPreference> UserPreferences { get; set; } = null!;
        public DbSet<UserSiteAccess> UserSiteAccesses { get; set; } = null!;
        public DbSet<SiteCatalogItem> SiteCatalogItems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            mb.HasDefaultSchema("LAGERVERWALTUNG");
            mb.Model.SetMaxIdentifierLength(30);

            // Oracle-safe 0/1 integer converter.
            // Wichtig:
            // NICHT .HasColumnType("NUMBER(1)") verwenden.
            // Oracle/EF kann NUMBER(1) sonst als bool interpretieren.
            var int01Converter = new ValueConverter<int, int>(
                v => v == 0 ? 0 : 1,
                v => v == 0 ? 0 : 1);


            mb.Entity<Site>(e =>
            {
                e.ToTable("SITES");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID")
                    .HasColumnType("NUMBER(10)")
                    .ValueGeneratedOnAdd();

                e.Property(x => x.Code)
                    .HasColumnName("CODE")
                    .IsRequired()
                    .HasMaxLength(30);

                e.Property(x => x.Name)
                    .HasColumnName("NAME")
                    .IsRequired()
                    .HasMaxLength(100);

                e.Property(x => x.IsActiveValue)
                    .HasColumnName("IS_ACTIVE")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Property(x => x.StockPolicy)
                    .HasColumnName("STOCK_POLICY")
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.ItEmail)
                    .HasColumnName("IT_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.LowStockEmail)
                    .HasColumnName("LOW_STOCK_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.DefaultCulture)
                    .HasColumnName("DEFAULT_CULTURE")
                    .IsRequired()
                    .HasMaxLength(20);

                e.Property(x => x.AdminCulture)
                    .HasColumnName("ADMIN_CULTURE")
                    .IsRequired()
                    .HasMaxLength(20);

                e.Property(x => x.EntraGroupObjectId)
                    .HasColumnName("ENTRA_GROUP_ID")
                    .IsRequired()
                    .HasMaxLength(100);

                e.Property(x => x.CreatedAt)
                    .HasColumnName("CREATED_AT");

                e.Property(x => x.UpdatedAt)
                    .HasColumnName("UPDATED_AT");

                e.Ignore(x => x.IsActive);

                e.HasIndex(x => x.Code)
                    .IsUnique()
                    .HasDatabaseName("UX_SITES_CODE");


            });

            mb.Entity<UserPreference>(e =>
            {
                e.ToTable("USER_PREFS");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID")
                    .HasColumnType("NUMBER(10)")
                    .ValueGeneratedOnAdd();

                e.Property(x => x.UserEmail)
                    .HasColumnName("USER_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.UserEmailNormalized)
                    .HasColumnName("USER_EMAIL_N")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.LastSelectedSiteId)
                    .HasColumnName("LAST_SITE_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.PreferredCulture)
                    .HasColumnName("PREFERRED_CULTURE")
                    .IsRequired()
                    .HasMaxLength(20);

                e.Property(x => x.UpdatedAt)
                    .HasColumnName("UPDATED_AT");

                e.HasOne(x => x.LastSelectedSite)
                    .WithMany()
                    .HasForeignKey(x => x.LastSelectedSiteId)
                    .HasConstraintName("FK_PREF_SITE")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.UserEmailNormalized)
                    .IsUnique()
                    .HasDatabaseName("UX_USER_PREF_EMAIL");

                e.HasIndex(x => x.LastSelectedSiteId)
                    .HasDatabaseName("IX_PREF_SITE");
            });

            mb.Entity<UserSiteAccess>(e =>
            {
                e.ToTable("USER_SITE_ACCESS");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID")
                    .HasColumnType("NUMBER(10)")
                    .ValueGeneratedOnAdd();

                e.Property(x => x.UserEmail)
                    .HasColumnName("USER_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.UserEmailNormalized)
                    .HasColumnName("USER_EMAIL_N")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.SiteId)
                    .HasColumnName("SITE_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.CanOrderValue)
                    .HasColumnName("CAN_ORDER")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Property(x => x.CanFulfillValue)
                    .HasColumnName("CAN_FULFILL")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Property(x => x.IsAdminValue)
                    .HasColumnName("IS_ADMIN")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Property(x => x.IsDefaultValue)
                    .HasColumnName("IS_DEFAULT")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Property(x => x.IsActiveValue)
                    .HasColumnName("IS_ACTIVE")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Ignore(x => x.CanOrder);
                e.Ignore(x => x.CanFulfill);
                e.Ignore(x => x.IsAdmin);
                e.Ignore(x => x.IsDefault);
                e.Ignore(x => x.IsActive);

                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .HasConstraintName("FK_USA_SITE")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.UserEmailNormalized, x.SiteId })
                    .IsUnique()
                    .HasDatabaseName("UX_USA_EMAIL_SITE");

                e.HasIndex(x => x.SiteId)
                    .HasDatabaseName("IX_USA_SITE");
            });

            mb.Entity<Position>(e =>
            {
                e.ToTable("Positions");

                e.HasKey(x => x.ID);

                e.Property(x => x.SiteId)
                    .HasColumnName("SITE_ID")
                    .HasColumnType("NUMBER(10)");

                e.HasOne(x => x.Site)
                    .WithMany(x => x.Positions)
                    .HasForeignKey(x => x.SiteId)
                    .HasConstraintName("FK_POS_SITE")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.SiteId)
                    .HasDatabaseName("IX_POS_SITE");

                e.HasIndex(x => new { x.SiteId, x.CategoryId })
                    .HasDatabaseName("IX_POS_SITE_CAT");

                e.Property(x => x.StockCondition)
                    .HasColumnName("STOCK_CONDITION")
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.HasOne(x => x.Category)
                    .WithMany(x => x.Positions)
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasMany(x => x.Issued)
                    .WithOne()
                    .HasForeignKey(x => x.PositionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            mb.Entity<Category>(e =>
            {
                e.ToTable("Categories");

                e.HasKey(x => x.ID);

                e.Property(x => x.SiteId)
                    .HasColumnName("SITE_ID")
                    .HasColumnType("NUMBER(10)");

                e.HasOne(x => x.Site)
                    .WithMany(x => x.Categories)
                    .HasForeignKey(x => x.SiteId)
                    .HasConstraintName("FK_CAT_SITE")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.SiteId)
                    .HasDatabaseName("IX_CAT_SITE");

                e.Property(x => x.NeedsNotificationValue)
                    .HasColumnName("NeedsNotification")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Ignore(x => x.NeedsNotification);
                e.Ignore(x => x.CriticalAmount);
                e.Ignore(x => x.CurrentAmount);
                e.Ignore(x => x.AvaliableAmount);

                e.Property(x => x.Name)
                    .IsRequired();

                e.Property(x => x.Comment)
                    .IsRequired();

                e.Property(x => x.MinimumAmount)
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.RestockEmail)
                    .HasColumnName("RESTOCK_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);
            });



            mb.Entity<RequestCatalogItem>(e =>
            {
                e.ToTable("REQUEST_CATALOG_ITEMS");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID")
                    .HasColumnType("NUMBER(10)")
                    .ValueGeneratedOnAdd();

                e.Property(x => x.ActionCode)
                    .HasColumnName("ACTION_CODE")
                    .IsRequired()
                    .HasMaxLength(20);

                e.Property(x => x.CategoryName)
                    .HasColumnName("CATEGORY_NAME")
                    .IsRequired()
                    .HasMaxLength(100);

                e.Property(x => x.Manufacturer)
                    .HasColumnName("MANUFACTURER")
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.ItemName)
                    .HasColumnName("ITEM_NAME")
                    .IsRequired()
                    .HasMaxLength(500);

                e.Property(x => x.Currency)
                    .HasColumnName("CURRENCY")
                    .IsRequired()
                    .HasMaxLength(10);

                e.Property(x => x.Price)
                    .HasColumnName("PRICE")
                    .HasColumnType("NUMBER(18,2)");

                e.Property(x => x.BillingType)
                    .HasColumnName("BILLING_TYPE")
                    .IsRequired()
                    .HasMaxLength(50);

                e.Property(x => x.FulfillmentMode)
                    .HasColumnName("FULFILLMENT_MODE")
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.ReturnableValue)
                    .HasColumnName("RETURNABLE")
                    .HasConversion(int01Converter)
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.RequiresCommentValue)
                    .HasColumnName("REQUIRES_COMMENT")
                    .HasConversion(int01Converter)
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.IsActiveValue)
                    .HasColumnName("IS_ACTIVE")
                    .HasConversion(int01Converter)
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.StorageCategoryId)
                    .HasColumnName("STORAGE_CATEGORY_ID")
                    .HasColumnType("NUMBER(10)");

                e.Ignore(x => x.Returnable);
                e.Ignore(x => x.RequiresComment);
                e.Ignore(x => x.IsActive);
                e.Ignore(x => x.CanUseStorageByCatalogDefault);
                e.Ignore(x => x.DisplayName);

                e.HasOne(x => x.StorageCategory)
                    .WithMany()
                    .HasForeignKey(x => x.StorageCategoryId)
                    .HasConstraintName("FK_REQCAT_CATEGORY")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.ActionCode)
                    .IsUnique()
                    .HasDatabaseName("UX_REQCAT_ACTION");

                e.HasIndex(x => new { x.IsActiveValue, x.CategoryName })
                    .HasDatabaseName("IX_REQCAT_ACTIVE_CAT");

                e.HasIndex(x => x.StorageCategoryId)
                    .HasDatabaseName("IX_REQCAT_STOR_CAT");
            });


            mb.Entity<SiteCatalogItem>(e =>
            {
                e.ToTable("SITE_CATALOG_ITEMS");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID")
                    .HasColumnType("NUMBER(10)")
                    .ValueGeneratedOnAdd();

                e.Property(x => x.SiteId)
                    .HasColumnName("SITE_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.CatalogItemId)
                    .HasColumnName("CATALOG_ITEM_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.IsActiveValue)
                    .HasColumnName("IS_ACTIVE")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Property(x => x.FulfillmentModeOverride)
                    .HasColumnName("FULFILLMENT_MODE_OVERRIDE")
                    .HasConversion<int?>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.StorageCategoryId)
                    .HasColumnName("STORAGE_CATEGORY_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.PriceOverride)
                    .HasColumnName("PRICE_OVERRIDE")
                    .HasColumnType("NUMBER(18,2)");

                e.Property(x => x.BillingTypeOverride)
                    .HasColumnName("BILLING_TYPE_OVERRIDE")
                    .IsRequired()
                    .HasMaxLength(50);

                e.Property(x => x.SortOrder)
                    .HasColumnName("SORT_ORDER")
                    .HasColumnType("NUMBER(10)");

                e.Ignore(x => x.IsActive);
                e.Ignore(x => x.EffectiveCatalogFulfillmentMode);
                e.Ignore(x => x.EffectivePrice);
                e.Ignore(x => x.EffectiveBillingType);

                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .HasConstraintName("FK_SCAT_SITE")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.CatalogItem)
                    .WithMany()
                    .HasForeignKey(x => x.CatalogItemId)
                    .HasConstraintName("FK_SCAT_REQCAT")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.StorageCategory)
                    .WithMany()
                    .HasForeignKey(x => x.StorageCategoryId)
                    .HasConstraintName("FK_SCAT_CATEGORY")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.SiteId, x.CatalogItemId })
                    .IsUnique()
                    .HasDatabaseName("UX_SCAT_SITE_CATALOG");

                e.HasIndex(x => x.SiteId)
                    .HasDatabaseName("IX_SCAT_SITE");

                e.HasIndex(x => x.CatalogItemId)
                    .HasDatabaseName("IX_SCAT_CATALOG");
            });


            mb.Entity<EquipmentOrder>(e =>
            {
                e.ToTable("EQUIPMENT_ORDERS");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID")
                    .HasColumnType("NUMBER(10)")
                    .ValueGeneratedOnAdd();

                e.Property(x => x.TicketNumber)
                    .HasColumnName("TICKET_NUMBER")
                    .IsRequired()
                    .HasMaxLength(50);

                e.Property(x => x.OrderedByName)
                    .HasColumnName("ORDERED_BY_NAME")
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.OrderedByEmail)
                    .HasColumnName("ORDERED_BY_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.RequestedForName)
                    .HasColumnName("REQUESTED_FOR_NAME")
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.RequestedForEmail)
                    .HasColumnName("REQUESTED_FOR_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.PickupContactName)
                    .HasColumnName("PICKUP_CONTACT_NAME")
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.PickupContactEmail)
                    .HasColumnName("PICKUP_CONTACT_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.SupervisorName)
                    .HasColumnName("SUPERVISOR_NAME")
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.SupervisorEmail)
                    .HasColumnName("SUPERVISOR_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.Reason)
                    .HasColumnName("REASON")
                    .IsRequired()
                    .HasMaxLength(2000);

                e.Property(x => x.BossComment)
                    .HasColumnName("BOSS_COMMENT")
                    .IsRequired()
                    .HasMaxLength(2000);

                e.Property(x => x.Status)
                    .HasColumnName("STATUS")
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.CreatedAt)
                    .HasColumnName("CREATED_AT");

                e.Property(x => x.DecisionDate)
                    .HasColumnName("DECISION_DATE");

                e.Property(x => x.CompletedAt)
                    .HasColumnName("COMPLETED_AT");

                e.Property(x => x.ApproveToken)
                    .HasColumnName("APPROVE_TOKEN")
                    .IsRequired()
                    .HasMaxLength(100);

                e.Property(x => x.DenyToken)
                    .HasColumnName("DENY_TOKEN")
                    .IsRequired()
                    .HasMaxLength(100);

                e.Property(x => x.SiteId)
                    .HasColumnName("SITE_ID")
                    .HasColumnType("NUMBER(10)");

                e.HasOne(x => x.Site)
                    .WithMany()
                    .HasForeignKey(x => x.SiteId)
                    .HasConstraintName("FK_EQORD_SITE")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.SiteId, x.Status, x.CreatedAt })
                    .HasDatabaseName("IX_EQORD_SITE_STATUS");

                e.Ignore(x => x.DisplayTicketNumber);

                e.HasMany(x => x.Lines)
                    .WithOne(x => x.EquipmentOrder)
                    .HasForeignKey(x => x.EquipmentOrderId)
                    .HasConstraintName("FK_EQOL_ORDER")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.TicketNumber)
                    .IsUnique()
                    .HasDatabaseName("UX_EQORD_TICKET");

                e.HasIndex(x => new { x.Status, x.CreatedAt })
                    .HasDatabaseName("IX_EQORD_STATUS_CREATED");
            });

            mb.Entity<EquipmentOrderLine>(e =>
            {
                e.ToTable("EQUIPMENT_ORDER_LINES");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID")
                    .HasColumnType("NUMBER(10)")
                    .ValueGeneratedOnAdd();

                e.Property(x => x.EquipmentOrderId)
                    .HasColumnName("EQUIPMENT_ORDER_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.CatalogItemId)
                    .HasColumnName("CATALOG_ITEM_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.PositionId)
                    .HasColumnName("POSITION_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.Quantity)
                    .HasColumnName("QUANTITY")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.Status)
                    .HasColumnName("STATUS")
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.CatalogFulfillmentMode)
                    .HasColumnName("CATALOG_FULFILLMENT_MODE")
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.EffectiveFulfillmentMode)
                    .HasColumnName("EFFECTIVE_FULFILLMENT_MODE")
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.ReturnableValue)
                    .HasColumnName("RETURNABLE")
                    .HasConversion(int01Converter)
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.UsedItemOkValue)
                    .HasColumnName("USED_ITEM_OK")
                    .HasConversion(int01Converter)
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.UserComment)
                    .HasColumnName("USER_COMMENT")
                    .IsRequired()
                    .HasMaxLength(1000);

                e.Property(x => x.AdminComment)
                    .HasColumnName("ADMIN_COMMENT")
                    .IsRequired()
                    .HasMaxLength(1000);

                e.Property(x => x.FulfilledAt)
                    .HasColumnName("FULFILLED_AT");

                e.Property(x => x.ActionCode)
                    .HasColumnName("ACTION_CODE")
                    .IsRequired()
                    .HasMaxLength(20);

                e.Property(x => x.CategoryName)
                    .HasColumnName("CATEGORY_NAME")
                    .IsRequired()
                    .HasMaxLength(100);

                e.Property(x => x.Manufacturer)
                    .HasColumnName("MANUFACTURER")
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.ItemName)
                    .HasColumnName("ITEM_NAME")
                    .IsRequired()
                    .HasMaxLength(500);

                e.Property(x => x.Currency)
                    .HasColumnName("CURRENCY")
                    .IsRequired()
                    .HasMaxLength(10);

                e.Property(x => x.UnitPrice)
                    .HasColumnName("UNIT_PRICE")
                    .HasColumnType("NUMBER(18,2)");

                e.Property(x => x.BillingType)
                    .HasColumnName("BILLING_TYPE")
                    .IsRequired()
                    .HasMaxLength(50);

                e.Ignore(x => x.Returnable);
                e.Ignore(x => x.UsedItemOk);
                e.Ignore(x => x.HasPhysicalPosition);
                e.Ignore(x => x.DisplayName);

                e.HasOne(x => x.CatalogItem)
                    .WithMany()
                    .HasForeignKey(x => x.CatalogItemId)
                    .HasConstraintName("FK_EQOL_REQCAT")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Position)
                    .WithMany()
                    .HasForeignKey(x => x.PositionId)
                    .HasConstraintName("FK_EQOL_POSITION")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.EquipmentOrderId)
                    .HasDatabaseName("IX_EQOL_ORDER");

                e.HasIndex(x => x.CatalogItemId)
                    .HasDatabaseName("IX_EQOL_CATALOG");

                e.HasIndex(x => x.PositionId)
                    .HasDatabaseName("IX_EQOL_POSITION");

                e.HasIndex(x => x.Status)
                    .HasDatabaseName("IX_EQOL_STATUS");
            });

            mb.Entity<Issue>(e =>
            {
                e.ToTable("Issue");

                e.HasKey(x => x.ID);

                e.Property(x => x.TicketNumber)
                    .IsRequired();

                e.Property(x => x.Username)
                    .IsRequired();

                e.Property(x => x.CostCentre)
                    .IsRequired();
            });

            mb.Entity<Deleted>(e =>
            {
                e.ToTable("Deleted");

                e.HasKey(x => x.ID);

                e.Property(x => x.Comment)
                    .IsRequired();

                e.Property(x => x.OrderNumber)
                    .IsRequired();
            });

            mb.Entity<Destroyed>(e =>
            {
                e.ToTable("Destroyeds");

                e.HasKey(x => x.ID);

                e.Property(x => x.PositionOrderNumber)
                    .IsRequired();

                e.Property(x => x.Comment)
                    .IsRequired();
            });

            mb.Entity<ReturnRequest>(e =>
            {
                e.ToTable("RETURN_REQUESTS");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID");

                e.Property(x => x.EquipmentRequestId)
                    .HasColumnName("EQUIPMENT_REQUEST_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.EquipmentOrderLineId)
                    .HasColumnName("EQUIPMENT_ORDER_LINE_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.PositionId)
                    .HasColumnName("POSITION_ID")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.RequesterName)
                    .HasColumnName("REQUESTER_NAME")
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.RequesterEmail)
                    .HasColumnName("REQUESTER_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.Status)
                    .HasColumnName("STATUS")
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.RequestedAt)
                    .HasColumnName("REQUESTED_AT");

                e.Property(x => x.ConfirmedAt)
                    .HasColumnName("CONFIRMED_AT");

                e.Property(x => x.UserComment)
                    .HasColumnName("USER_COMMENT")
                    .IsRequired()
                    .HasMaxLength(1000);

                e.Property(x => x.AdminComment)
                    .HasColumnName("ADMIN_COMMENT")
                    .IsRequired()
                    .HasMaxLength(1000);

                e.HasOne(x => x.EquipmentRequest)
                    .WithMany()
                    .HasForeignKey(x => x.EquipmentRequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.EquipmentOrderLine)
                    .WithMany()
                    .HasForeignKey(x => x.EquipmentOrderLineId)
                    .HasConstraintName("FK_RET_ORDER_LINE")
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Position)
                    .WithMany()
                    .HasForeignKey(x => x.PositionId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.EquipmentRequestId)
                    .HasDatabaseName("IX_RETURN_REQ_EQREQ");

                e.HasIndex(x => x.EquipmentOrderLineId)
                    .HasDatabaseName("IX_RETURN_REQ_OLINE");

                e.HasIndex(x => x.Status)
                    .HasDatabaseName("IX_RETURN_REQ_STATUS");
            });

            mb.Entity<LowStockNotificationLog>(e =>
            {
                e.ToTable("LOW_STOCK_NOTIFICATION_LOGS");

                e.HasKey(x => x.Id);

                e.Property(x => x.Id)
                    .HasColumnName("ID");

                e.Property(x => x.CategoryId)
                    .HasColumnName("CATEGORY_ID");

                e.Property(x => x.SentAt)
                    .HasColumnName("SENT_AT");

                e.Property(x => x.AvailableAmount)
                    .HasColumnName("AVAILABLE_AMOUNT")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.MinimumAmount)
                    .HasColumnName("MINIMUM_AMOUNT")
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.RecipientEmail)
                    .HasColumnName("RECIPIENT_EMAIL")
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.TriggerReason)
                    .HasColumnName("TRIGGER_REASON")
                    .IsRequired()
                    .HasMaxLength(500);

                e.HasOne(x => x.Category)
                    .WithMany()
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.CategoryId, x.SentAt })
                    .HasDatabaseName("IX_LOW_STOCK_CAT_SENT");
            });

            mb.Entity<EquipmentRequest>(e =>
            {
                e.ToTable("EquipmentRequests");

                e.HasKey(x => x.Id);

                e.HasOne(x => x.Position)
                    .WithMany()
                    .HasForeignKey(x => x.PositionId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.PositionId)
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.RequesterName)
                    .IsRequired();

                e.Property(x => x.RequesterEmail)
                    .IsRequired();

                e.Property(x => x.Department)
                    .IsRequired();

                e.Property(x => x.SupervisorName)
                    .HasColumnName("SUPERVISOR_NAME")
                    .IsRequired();

                e.Property(x => x.SupervisorEmail)
                    .HasColumnName("SUPERVISOR_EMAIL")
                    .IsRequired();

                e.Property(x => x.Reason)
                    .IsRequired();

                e.Property(x => x.UsedItemOkValue)
                    .HasColumnName("USED_ITEM_OK")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Ignore(x => x.UsedItemOk);
                e.Ignore(x => x.UsedItemPreferenceLabel);
                e.Ignore(x => x.DisplayTicketNumber);

                e.Property(x => x.TicketNumber)
                    .HasColumnName("TICKET_NUMBER")
                    .IsRequired()
                    .HasMaxLength(50);

                e.Property(x => x.Status)
                    .HasConversion<int>()
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.FulfillmentType)
                    .HasColumnName("FULFILLMENT_TYPE")
                    .HasConversion(
                        v => v.HasValue ? (int?)(int)v.Value : null,
                        v => v.HasValue ? (RequestFulfillmentType?)(RequestFulfillmentType)v.Value : null)
                    .HasColumnType("NUMBER(10)");

                e.Property(x => x.FulfillmentDate)
                    .HasColumnName("FULFILLMENT_DATE");

                e.Property(x => x.BossComment)
                    .IsRequired();

                e.Property(x => x.ApproveToken)
                    .HasColumnName("APPROVE_TOKEN")
                    .IsRequired();

                e.Property(x => x.DenyToken)
                    .HasColumnName("DENY_TOKEN")
                    .IsRequired();

                e.HasIndex(x => x.TicketNumber)
                    .IsUnique()
                    .HasDatabaseName("UX_EQREQ_TICKET");
            });

            mb.Entity<Approver>(e =>
            {
                e.ToTable("Approver");

                e.HasKey(x => x.Id);

                e.Property(x => x.DisplayName)
                    .IsRequired()
                    .HasMaxLength(200);

                e.Property(x => x.Email)
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.EmailNormalized)
                    .IsRequired()
                    .HasMaxLength(320);

                e.Property(x => x.IsActiveValue)
                    .HasColumnName("IsActive")
                    .HasConversion(int01Converter)
                    .HasPrecision(1);

                e.Ignore(x => x.IsActive);

                e.Property(x => x.Source)
                    .IsRequired()
                    .HasMaxLength(50);

                e.HasIndex(x => x.EmailNormalized)
                    .IsUnique()
                    .HasDatabaseName("UX_APPROVER_EMAIL_N");
            });
        }

        public override int SaveChanges()
        {
            NormalizeAll();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            NormalizeAll();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            NormalizeAll();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            NormalizeAll();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void NormalizeAll()
        {
            NormalizeSites();
            NormalizeUserPreferences();
            NormalizeUserSiteAccesses();
            NormalizeEquipmentRequests();
            NormalizeApprovers();
            NormalizeCategories();
            NormalizePositions();
            NormalizeReturnRequests();
            NormalizeLowStockNotificationLogs();
            NormalizeRequestCatalogItems();
            NormalizeEquipmentOrders();
            NormalizeEquipmentOrderLines();
        }

        private void NormalizeSites()
        {
            var entries = ChangeTracker.Entries<Site>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.Code = string.IsNullOrWhiteSpace(entry.Entity.Code)
                    ? " "
                    : entry.Entity.Code.Trim().ToUpperInvariant();

                entry.Entity.Name = string.IsNullOrWhiteSpace(entry.Entity.Name)
                    ? " "
                    : entry.Entity.Name.Trim();

                entry.Entity.IsActiveValue = entry.Entity.IsActiveValue == 0 ? 0 : 1;

                entry.Entity.ItEmail = string.IsNullOrWhiteSpace(entry.Entity.ItEmail)
                    ? " "
                    : entry.Entity.ItEmail.Trim();

                entry.Entity.LowStockEmail = string.IsNullOrWhiteSpace(entry.Entity.LowStockEmail)
                    ? " "
                    : entry.Entity.LowStockEmail.Trim();

                entry.Entity.DefaultCulture = string.IsNullOrWhiteSpace(entry.Entity.DefaultCulture)
                    ? "de-CH"
                    : entry.Entity.DefaultCulture.Trim();

                entry.Entity.AdminCulture = string.IsNullOrWhiteSpace(entry.Entity.AdminCulture)
                    ? entry.Entity.DefaultCulture
                    : entry.Entity.AdminCulture.Trim();

                entry.Entity.EntraGroupObjectId = string.IsNullOrWhiteSpace(entry.Entity.EntraGroupObjectId)
                    ? " "
                    : entry.Entity.EntraGroupObjectId.Trim();

                if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                    entry.Entity.CreatedAt = DateTime.Now;

                if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = DateTime.Now;
            }
        }

        private void NormalizeUserPreferences()
        {
            var entries = ChangeTracker.Entries<UserPreference>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.UserEmail = entry.Entity.UserEmail?.Trim() ?? "";
                entry.Entity.UserEmailNormalized = string.IsNullOrWhiteSpace(entry.Entity.UserEmailNormalized)
                    ? entry.Entity.UserEmail.Trim().ToLowerInvariant()
                    : entry.Entity.UserEmailNormalized.Trim().ToLowerInvariant();

                entry.Entity.PreferredCulture = string.IsNullOrWhiteSpace(entry.Entity.PreferredCulture)
                    ? "de-CH"
                    : entry.Entity.PreferredCulture.Trim();

                entry.Entity.UpdatedAt = DateTime.Now;
            }
        }

        private void NormalizeUserSiteAccesses()
        {
            var entries = ChangeTracker.Entries<UserSiteAccess>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.UserEmail = entry.Entity.UserEmail?.Trim() ?? "";
                entry.Entity.UserEmailNormalized = string.IsNullOrWhiteSpace(entry.Entity.UserEmailNormalized)
                    ? entry.Entity.UserEmail.Trim().ToLowerInvariant()
                    : entry.Entity.UserEmailNormalized.Trim().ToLowerInvariant();

                entry.Entity.CanOrderValue = entry.Entity.CanOrderValue == 0 ? 0 : 1;
                entry.Entity.CanFulfillValue = entry.Entity.CanFulfillValue == 0 ? 0 : 1;
                entry.Entity.IsAdminValue = entry.Entity.IsAdminValue == 0 ? 0 : 1;
                entry.Entity.IsDefaultValue = entry.Entity.IsDefaultValue == 0 ? 0 : 1;
                entry.Entity.IsActiveValue = entry.Entity.IsActiveValue == 0 ? 0 : 1;
            }
        }

        private void NormalizeEquipmentRequests()
        {
            var entries = ChangeTracker.Entries<EquipmentRequest>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.RequesterName = entry.Entity.RequesterName?.Trim() ?? "";
                entry.Entity.RequesterEmail = entry.Entity.RequesterEmail?.Trim() ?? "";
                entry.Entity.Reason = entry.Entity.Reason?.Trim() ?? "";
                entry.Entity.UsedItemOkValue = entry.Entity.UsedItemOkValue == 0 ? 0 : 1;

                if (string.IsNullOrWhiteSpace(entry.Entity.TicketNumber))
                {
                    entry.Entity.TicketNumber = entry.Entity.Id > 0
                        ? EquipmentRequest.BuildTicketNumber(entry.Entity.Id)
                        : $"PENDING-{Guid.NewGuid():N}";
                }
                else
                {
                    entry.Entity.TicketNumber = entry.Entity.TicketNumber.Trim();
                }

                entry.Entity.ApproveToken = string.IsNullOrWhiteSpace(entry.Entity.ApproveToken)
                    ? Guid.NewGuid().ToString("N")
                    : entry.Entity.ApproveToken.Trim();

                entry.Entity.DenyToken = string.IsNullOrWhiteSpace(entry.Entity.DenyToken)
                    ? Guid.NewGuid().ToString("N")
                    : entry.Entity.DenyToken.Trim();

                if (string.IsNullOrWhiteSpace(entry.Entity.SupervisorName))
                    entry.Entity.SupervisorName = " ";
                else
                    entry.Entity.SupervisorName = entry.Entity.SupervisorName.Trim();

                if (string.IsNullOrWhiteSpace(entry.Entity.SupervisorEmail))
                    entry.Entity.SupervisorEmail = " ";
                else
                    entry.Entity.SupervisorEmail = entry.Entity.SupervisorEmail.Trim();

                if (string.IsNullOrWhiteSpace(entry.Entity.Department))
                    entry.Entity.Department = entry.Entity.SupervisorName;
                else
                    entry.Entity.Department = entry.Entity.Department.Trim();

                if (string.IsNullOrWhiteSpace(entry.Entity.BossComment))
                    entry.Entity.BossComment = " ";
                else
                    entry.Entity.BossComment = entry.Entity.BossComment.Trim();
            }
        }

        private void NormalizeApprovers()
        {
            var entries = ChangeTracker.Entries<Approver>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.DisplayName = entry.Entity.DisplayName?.Trim() ?? "";
                entry.Entity.Email = entry.Entity.Email?.Trim() ?? "";
                entry.Entity.EmailNormalized = entry.Entity.Email.ToLowerInvariant();
                entry.Entity.IsActiveValue = entry.Entity.IsActiveValue == 0 ? 0 : 1;

                entry.Entity.Source = string.IsNullOrWhiteSpace(entry.Entity.Source)
                    ? "CSV"
                    : entry.Entity.Source.Trim();

                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAt = DateTime.Now;

                entry.Entity.UpdatedAt = DateTime.Now;
            }
        }

        private void NormalizeCategories()
        {
            var entries = ChangeTracker.Entries<Category>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.Name = string.IsNullOrWhiteSpace(entry.Entity.Name)
                    ? " "
                    : entry.Entity.Name.Trim();

                entry.Entity.Comment = string.IsNullOrWhiteSpace(entry.Entity.Comment)
                    ? " "
                    : entry.Entity.Comment.Trim();

                entry.Entity.RestockEmail = string.IsNullOrWhiteSpace(entry.Entity.RestockEmail)
                    ? " "
                    : entry.Entity.RestockEmail.Trim();

                entry.Entity.MinimumAmount = Math.Max(0, entry.Entity.MinimumAmount);
                entry.Entity.NeedsNotificationValue = entry.Entity.NeedsNotificationValue == 0 ? 0 : 1;

                if (entry.Entity.SiteId <= 0)
                    entry.Entity.SiteId = 1;
            }
        }

        private void NormalizePositions()
        {
            var entries = ChangeTracker.Entries<Position>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.Description = string.IsNullOrWhiteSpace(entry.Entity.Description)
                    ? " "
                    : entry.Entity.Description.Trim();

                entry.Entity.OrderNumber = string.IsNullOrWhiteSpace(entry.Entity.OrderNumber)
                    ? " "
                    : entry.Entity.OrderNumber.Trim();

                entry.Entity.Supplier = string.IsNullOrWhiteSpace(entry.Entity.Supplier)
                    ? " "
                    : entry.Entity.Supplier.Trim();

                entry.Entity.Price = Math.Max(0, entry.Entity.Price);

                if (entry.Entity.SiteId <= 0)
                    entry.Entity.SiteId = 1;
            }
        }

        private void NormalizeReturnRequests()
        {
            var entries = ChangeTracker.Entries<ReturnRequest>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.RequesterName = string.IsNullOrWhiteSpace(entry.Entity.RequesterName)
                    ? " "
                    : entry.Entity.RequesterName.Trim();

                entry.Entity.RequesterEmail = string.IsNullOrWhiteSpace(entry.Entity.RequesterEmail)
                    ? " "
                    : entry.Entity.RequesterEmail.Trim();

                entry.Entity.UserComment = string.IsNullOrWhiteSpace(entry.Entity.UserComment)
                    ? " "
                    : entry.Entity.UserComment.Trim();

                entry.Entity.AdminComment = string.IsNullOrWhiteSpace(entry.Entity.AdminComment)
                    ? " "
                    : entry.Entity.AdminComment.Trim();
            }
        }


        private void NormalizeRequestCatalogItems()
        {
            var entries = ChangeTracker.Entries<RequestCatalogItem>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.ActionCode = string.IsNullOrWhiteSpace(entry.Entity.ActionCode)
                    ? " "
                    : entry.Entity.ActionCode.Trim();

                entry.Entity.CategoryName = string.IsNullOrWhiteSpace(entry.Entity.CategoryName)
                    ? " "
                    : entry.Entity.CategoryName.Trim();

                entry.Entity.Manufacturer = string.IsNullOrWhiteSpace(entry.Entity.Manufacturer)
                    ? " "
                    : entry.Entity.Manufacturer.Trim();

                entry.Entity.ItemName = string.IsNullOrWhiteSpace(entry.Entity.ItemName)
                    ? " "
                    : entry.Entity.ItemName.Trim();

                entry.Entity.Currency = string.IsNullOrWhiteSpace(entry.Entity.Currency)
                    ? "CHF"
                    : entry.Entity.Currency.Trim().ToUpperInvariant();

                entry.Entity.BillingType = string.IsNullOrWhiteSpace(entry.Entity.BillingType)
                    ? "ohne"
                    : entry.Entity.BillingType.Trim();

                entry.Entity.Price = Math.Max(0, entry.Entity.Price);
                entry.Entity.ReturnableValue = entry.Entity.ReturnableValue == 0 ? 0 : 1;
                entry.Entity.RequiresCommentValue = entry.Entity.RequiresCommentValue == 0 ? 0 : 1;
                entry.Entity.IsActiveValue = entry.Entity.IsActiveValue == 0 ? 0 : 1;

                if (entry.Entity.StorageCategoryId <= 0)
                    entry.Entity.StorageCategoryId = null;
            }
        }


        private void NormalizeEquipmentOrders()
        {
            var entries = ChangeTracker.Entries<EquipmentOrder>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.TicketNumber = string.IsNullOrWhiteSpace(entry.Entity.TicketNumber)
                    ? entry.Entity.Id > 0
                        ? EquipmentOrder.BuildTicketNumber(entry.Entity.Id)
                        : $"PENDING-{Guid.NewGuid():N}"
                    : entry.Entity.TicketNumber.Trim();

                entry.Entity.OrderedByName = string.IsNullOrWhiteSpace(entry.Entity.OrderedByName)
                    ? " "
                    : entry.Entity.OrderedByName.Trim();

                entry.Entity.OrderedByEmail = string.IsNullOrWhiteSpace(entry.Entity.OrderedByEmail)
                    ? " "
                    : entry.Entity.OrderedByEmail.Trim();

                entry.Entity.RequestedForName = string.IsNullOrWhiteSpace(entry.Entity.RequestedForName)
                    ? entry.Entity.OrderedByName
                    : entry.Entity.RequestedForName.Trim();

                entry.Entity.RequestedForEmail = string.IsNullOrWhiteSpace(entry.Entity.RequestedForEmail)
                    ? entry.Entity.OrderedByEmail
                    : entry.Entity.RequestedForEmail.Trim();

                entry.Entity.PickupContactName = string.IsNullOrWhiteSpace(entry.Entity.PickupContactName)
                    ? entry.Entity.OrderedByName
                    : entry.Entity.PickupContactName.Trim();

                entry.Entity.PickupContactEmail = string.IsNullOrWhiteSpace(entry.Entity.PickupContactEmail)
                    ? entry.Entity.OrderedByEmail
                    : entry.Entity.PickupContactEmail.Trim();

                entry.Entity.SupervisorName = string.IsNullOrWhiteSpace(entry.Entity.SupervisorName)
                    ? " "
                    : entry.Entity.SupervisorName.Trim();

                entry.Entity.SupervisorEmail = string.IsNullOrWhiteSpace(entry.Entity.SupervisorEmail)
                    ? " "
                    : entry.Entity.SupervisorEmail.Trim();

                entry.Entity.Reason = string.IsNullOrWhiteSpace(entry.Entity.Reason)
                    ? " "
                    : entry.Entity.Reason.Trim();

                entry.Entity.BossComment = string.IsNullOrWhiteSpace(entry.Entity.BossComment)
                    ? " "
                    : entry.Entity.BossComment.Trim();

                entry.Entity.ApproveToken = string.IsNullOrWhiteSpace(entry.Entity.ApproveToken)
                    ? Guid.NewGuid().ToString("N")
                    : entry.Entity.ApproveToken.Trim();

                entry.Entity.DenyToken = string.IsNullOrWhiteSpace(entry.Entity.DenyToken)
                    ? Guid.NewGuid().ToString("N")
                    : entry.Entity.DenyToken.Trim();

                if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                    entry.Entity.CreatedAt = DateTime.Now;

                if (entry.Entity.SiteId <= 0)
                    entry.Entity.SiteId = 1;
            }
        }

        private void NormalizeEquipmentOrderLines()
        {
            var entries = ChangeTracker.Entries<EquipmentOrderLine>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Entity.PositionId.HasValue && entry.Entity.PositionId.Value <= 0)
                    entry.Entity.PositionId = null;

                entry.Entity.Quantity = Math.Max(1, entry.Entity.Quantity);
                entry.Entity.ReturnableValue = entry.Entity.ReturnableValue == 0 ? 0 : 1;
                entry.Entity.UsedItemOkValue = entry.Entity.UsedItemOkValue == 0 ? 0 : 1;

                entry.Entity.UserComment = string.IsNullOrWhiteSpace(entry.Entity.UserComment)
                    ? " "
                    : entry.Entity.UserComment.Trim();

                entry.Entity.AdminComment = string.IsNullOrWhiteSpace(entry.Entity.AdminComment)
                    ? " "
                    : entry.Entity.AdminComment.Trim();

                entry.Entity.ActionCode = string.IsNullOrWhiteSpace(entry.Entity.ActionCode)
                    ? " "
                    : entry.Entity.ActionCode.Trim();

                entry.Entity.CategoryName = string.IsNullOrWhiteSpace(entry.Entity.CategoryName)
                    ? " "
                    : entry.Entity.CategoryName.Trim();

                entry.Entity.Manufacturer = string.IsNullOrWhiteSpace(entry.Entity.Manufacturer)
                    ? " "
                    : entry.Entity.Manufacturer.Trim();

                entry.Entity.ItemName = string.IsNullOrWhiteSpace(entry.Entity.ItemName)
                    ? " "
                    : entry.Entity.ItemName.Trim();

                entry.Entity.Currency = string.IsNullOrWhiteSpace(entry.Entity.Currency)
                    ? "CHF"
                    : entry.Entity.Currency.Trim().ToUpperInvariant();

                entry.Entity.BillingType = string.IsNullOrWhiteSpace(entry.Entity.BillingType)
                    ? "ohne"
                    : entry.Entity.BillingType.Trim();

                entry.Entity.UnitPrice = Math.Max(0, entry.Entity.UnitPrice);
            }
        }

        private void NormalizeLowStockNotificationLogs()
        {
            var entries = ChangeTracker.Entries<LowStockNotificationLog>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.RecipientEmail = string.IsNullOrWhiteSpace(entry.Entity.RecipientEmail)
                    ? " "
                    : entry.Entity.RecipientEmail.Trim();

                entry.Entity.TriggerReason = string.IsNullOrWhiteSpace(entry.Entity.TriggerReason)
                    ? " "
                    : entry.Entity.TriggerReason.Trim();
            }
        }
    }
}