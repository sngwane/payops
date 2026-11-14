using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayOps.Database.Migrations
{
    /// <inheritdoc />
    public partial class LegacyImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "SecondaryClient",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "PrimaryClient",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "PaymentSummaryComment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyBatchId",
                table: "PaymentSummary",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "PaymentSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "PaymentRejection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "PaymentRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "PaymentBatch",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "ClientBankAccount",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "BankClientProfile",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyId",
                table: "Bank",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecondaryClient_LegacyId",
                table: "SecondaryClient",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PrimaryClient_LegacyId",
                table: "PrimaryClient",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummaryComment_LegacyId",
                table: "PaymentSummaryComment",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_LegacyBatchId",
                table: "PaymentSummary",
                column: "LegacyBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_LegacyId",
                table: "PaymentSummary",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRejection_LegacyId",
                table: "PaymentRejection",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecord_LegacyId",
                table: "PaymentRecord",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_LegacyId",
                table: "PaymentBatch",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBankAccount_PrimaryClientId_AccountTypeId_LegacyId",
                table: "ClientBankAccount",
                columns: new[] { "PrimaryClientId", "AccountTypeId", "LegacyId" },
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankClientProfile_LegacyId",
                table: "BankClientProfile",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bank_LegacyId",
                table: "Bank",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SecondaryClient_LegacyId",
                table: "SecondaryClient");

            migrationBuilder.DropIndex(
                name: "IX_PrimaryClient_LegacyId",
                table: "PrimaryClient");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSummaryComment_LegacyId",
                table: "PaymentSummaryComment");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_LegacyBatchId",
                table: "PaymentSummary");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_LegacyId",
                table: "PaymentSummary");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRejection_LegacyId",
                table: "PaymentRejection");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRecord_LegacyId",
                table: "PaymentRecord");

            migrationBuilder.DropIndex(
                name: "IX_PaymentBatch_LegacyId",
                table: "PaymentBatch");

            migrationBuilder.DropIndex(
                name: "IX_ClientBankAccount_PrimaryClientId_AccountTypeId_LegacyId",
                table: "ClientBankAccount");

            migrationBuilder.DropIndex(
                name: "IX_BankClientProfile_LegacyId",
                table: "BankClientProfile");

            migrationBuilder.DropIndex(
                name: "IX_Bank_LegacyId",
                table: "Bank");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "SecondaryClient");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "PrimaryClient");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "PaymentSummaryComment");

            migrationBuilder.DropColumn(
                name: "LegacyBatchId",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "PaymentRejection");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "PaymentRecord");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "PaymentBatch");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "ClientBankAccount");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "BankClientProfile");

            migrationBuilder.DropColumn(
                name: "LegacyId",
                table: "Bank");
        }
    }
}
