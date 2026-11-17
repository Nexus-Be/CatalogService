using System;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tracks whether each SKU has its inventory row in FulfillmentService, so a failed call during approval is retried in the background.
    /// Active SKUs of active Regular products are marked Pending: approval used to ignore failures, so the worker re-checks them once.
    /// The Fulfillment call is idempotent, so a SKU that already has its row simply becomes Done.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261116130000_AddVariantInventoryInitState")]
    public partial class AddVariantInventoryInitState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryInitState",
                table: "ProductVariants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InventoryInitAttemptCount",
                table: "ProductVariants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InventoryInitNextRetryAt",
                table: "ProductVariants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryInitLastError",
                table: "ProductVariants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_InventoryInitState_InventoryInitNextRetryAt",
                table: "ProductVariants",
                columns: new[] { "InventoryInitState", "InventoryInitNextRetryAt" });

            // Product.Status 1 = Active, Product.SaleType 0 = Regular, ProductVariant.Status 0 = Active, InventoryInitState 1 = Pending.
            migrationBuilder.Sql(@"
UPDATE ""ProductVariants"" AS v
SET ""InventoryInitState"" = 1, ""InventoryInitNextRetryAt"" = now()
FROM ""Products"" AS p
WHERE v.""ProductId"" = p.""Id""
  AND v.""IsDeleted"" = false
  AND v.""Status"" = 0
  AND p.""IsDeleted"" = false
  AND p.""Status"" = 1
  AND p.""SaleType"" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_InventoryInitState_InventoryInitNextRetryAt",
                table: "ProductVariants");

            migrationBuilder.DropColumn(name: "InventoryInitState", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "InventoryInitAttemptCount", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "InventoryInitNextRetryAt", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "InventoryInitLastError", table: "ProductVariants");
        }
    }
}
