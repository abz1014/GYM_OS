using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRecords_Suppliers_SupplierId",
                table: "PurchaseRecords");

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                table: "InventoryItems",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "InventoryItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId_Sku",
                table: "InventoryItems",
                columns: new[] { "TenantId", "Sku" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRecords_Suppliers_SupplierId",
                table: "PurchaseRecords",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRecords_Suppliers_SupplierId",
                table: "PurchaseRecords");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_TenantId_Sku",
                table: "InventoryItems");

            migrationBuilder.AlterColumn<string>(
                name: "Sku",
                table: "InventoryItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "InventoryItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRecords_Suppliers_SupplierId",
                table: "PurchaseRecords",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");
        }
    }
}
