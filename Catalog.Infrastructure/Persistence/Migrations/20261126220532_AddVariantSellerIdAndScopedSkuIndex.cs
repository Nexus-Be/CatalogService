using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantSellerIdAndScopedSkuIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_SkuCode",
                table: "ProductVariants");

            migrationBuilder.AddColumn<Guid>(
                name: "SellerId",
                table: "ProductVariants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE "ProductVariants" pv
                SET "SellerId" = p."SellerId"
                FROM "Products" p
                WHERE pv."ProductId" = p."Id";
                """);

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("11100000-0000-0000-0000-000000000001"),
                column: "SellerId",
                value: new Guid("cccccccc-3333-3333-3333-333333333333"));

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("22100000-0000-0000-0000-000000000002"),
                column: "SellerId",
                value: new Guid("cccccccc-3333-3333-3333-333333333333"));

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("33100000-0000-0000-0000-000000000003"),
                column: "SellerId",
                value: new Guid("cccccccc-3333-3333-3333-333333333333"));

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("44100000-0000-0000-0000-000000000004"),
                column: "SellerId",
                value: new Guid("cccccccc-3333-3333-3333-333333333333"));

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("55100000-0000-0000-0000-000000000005"),
                column: "SellerId",
                value: new Guid("cccccccc-3333-3333-3333-333333333333"));

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_ProductVariants_SellerId_SkuCode"
                ON "ProductVariants" ("SellerId", lower("SkuCode"))
                WHERE "IsDeleted" = false;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_SkuCode",
                table: "ProductVariants",
                column: "SkuCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_SellerId_SkuCode",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_SkuCode",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "ProductVariants");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_SkuCode",
                table: "ProductVariants",
                column: "SkuCode",
                unique: true);
        }
    }
}
