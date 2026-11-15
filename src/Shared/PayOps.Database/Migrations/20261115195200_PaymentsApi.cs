using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayOps.Database.Migrations
{
    /// <inheritdoc />
    public partial class PaymentsApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_ReconStatusId",
                table: "PaymentSummary");

            migrationBuilder.AddColumn<int>(
                name: "HeldByUserId",
                table: "PaymentSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HeldDate",
                table: "PaymentSummary",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HoldReason",
                table: "PaymentSummary",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnHold",
                table: "PaymentSummary",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ModifiedByUserId",
                table: "PaymentSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReconStatusChangedByUserId",
                table: "PaymentSummary",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconStatusChangedDate",
                table: "PaymentSummary",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            // PaymentSummary is system-versioned: with versioning on, SQL Server adds the column to the history table too and
            // fails on existing history rows. So add RowVersion (auto-filled) to the history table first, then to the table.
            migrationBuilder.Sql("ALTER TABLE [PaymentSummary] SET (SYSTEM_VERSIONING = OFF);");
            migrationBuilder.Sql("ALTER TABLE [PaymentSummaryHistory] ADD [RowVersion] rowversion NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE [PaymentSummary] ADD [RowVersion] rowversion NOT NULL;");
            migrationBuilder.Sql("ALTER TABLE [PaymentSummary] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[PaymentSummaryHistory]));");

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedByUserId",
                table: "PaymentRejection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedDate",
                table: "PaymentRejection",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PaymentRejection",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_HeldByUserId",
                table: "PaymentSummary",
                column: "HeldByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_IsOnHold",
                table: "PaymentSummary",
                column: "IsOnHold",
                filter: "[IsOnHold] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_ModifiedByUserId",
                table: "PaymentSummary",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_ReconStatusChangedByUserId",
                table: "PaymentSummary",
                column: "ReconStatusChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_ReconStatusId_PaymentBatchId",
                table: "PaymentSummary",
                columns: new[] { "ReconStatusId", "PaymentBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRejection_ConfirmedByUserId",
                table: "PaymentRejection",
                column: "ConfirmedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRejection_AppUser_ConfirmedByUserId",
                table: "PaymentRejection",
                column: "ConfirmedByUserId",
                principalTable: "AppUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSummary_AppUser_HeldByUserId",
                table: "PaymentSummary",
                column: "HeldByUserId",
                principalTable: "AppUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSummary_AppUser_ModifiedByUserId",
                table: "PaymentSummary",
                column: "ModifiedByUserId",
                principalTable: "AppUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentSummary_AppUser_ReconStatusChangedByUserId",
                table: "PaymentSummary",
                column: "ReconStatusChangedByUserId",
                principalTable: "AppUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRejection_AppUser_ConfirmedByUserId",
                table: "PaymentRejection");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSummary_AppUser_HeldByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSummary_AppUser_ModifiedByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentSummary_AppUser_ReconStatusChangedByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_HeldByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_IsOnHold",
                table: "PaymentSummary");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_ModifiedByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_ReconStatusChangedByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropIndex(
                name: "IX_PaymentSummary_ReconStatusId_PaymentBatchId",
                table: "PaymentSummary");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRejection_ConfirmedByUserId",
                table: "PaymentRejection");

            migrationBuilder.DropColumn(
                name: "HeldByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "HeldDate",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "HoldReason",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "IsOnHold",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "ReconStatusChangedByUserId",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "ReconStatusChangedDate",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PaymentSummary");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "PaymentRejection");

            migrationBuilder.DropColumn(
                name: "ConfirmedDate",
                table: "PaymentRejection");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PaymentRejection");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_ReconStatusId",
                table: "PaymentSummary",
                column: "ReconStatusId");
        }
    }
}
