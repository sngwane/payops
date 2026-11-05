using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayOps.Database.Migrations
{
    /// <inheritdoc />
    public partial class ClientApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresReview",
                table: "SecondaryClient",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SecondaryClient",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PrimaryClient",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "ClientApiKey",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrimaryClientId = table.Column<int>(type: "int", nullable: true),
                    SecondaryClientId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KeyPrefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    KeyHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RevokedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LastUsedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientApiKey", x => x.Id);
                    table.CheckConstraint("CK_ClientApiKey_Owner", "([PrimaryClientId] IS NULL AND [SecondaryClientId] IS NOT NULL) OR ([PrimaryClientId] IS NOT NULL AND [SecondaryClientId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ClientApiKey_PrimaryClient_PrimaryClientId",
                        column: x => x.PrimaryClientId,
                        principalTable: "PrimaryClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientApiKey_SecondaryClient_SecondaryClientId",
                        column: x => x.SecondaryClientId,
                        principalTable: "SecondaryClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientBankAccount",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrimaryClientId = table.Column<int>(type: "int", nullable: false),
                    AccountTypeId = table.Column<byte>(type: "tinyint", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GlAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientBankAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientBankAccount_AccountType_AccountTypeId",
                        column: x => x.AccountTypeId,
                        principalTable: "AccountType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientBankAccount_PrimaryClient_PrimaryClientId",
                        column: x => x.PrimaryClientId,
                        principalTable: "PrimaryClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientApiKey_KeyHash",
                table: "ClientApiKey",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientApiKey_PrimaryClientId",
                table: "ClientApiKey",
                column: "PrimaryClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientApiKey_SecondaryClientId",
                table: "ClientApiKey",
                column: "SecondaryClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBankAccount_AccountTypeId",
                table: "ClientBankAccount",
                column: "AccountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientBankAccount_PrimaryClientId_AccountTypeId",
                table: "ClientBankAccount",
                columns: new[] { "PrimaryClientId", "AccountTypeId" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientApiKey");

            migrationBuilder.DropTable(
                name: "ClientBankAccount");

            migrationBuilder.DropColumn(
                name: "RequiresReview",
                table: "SecondaryClient");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SecondaryClient");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PrimaryClient");
        }
    }
}
