using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayOps.Database.Migrations
{
    /// <inheritdoc />
    public partial class BatchReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientReference",
                table: "PaymentSummary",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE [PaymentSummary] SET [ClientReference] = [UniqueReference] WHERE [ClientReference] = N'';");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_SecondaryClientId_ClientReference",
                table: "PaymentSummary",
                columns: new[] { "SecondaryClientId", "ClientReference" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_SecondaryClientId_ClientReference",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "ClientReference",
                table: "PaymentSummary");
        }
    }
}
