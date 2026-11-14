using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayOps.Database.Migrations
{
    /// <inheritdoc />
    public partial class BatchFileApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FeeTotal",
                table: "PaymentSummary",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "BatchContact",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentSummaryId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CellNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchContact", x => x.Id);
                    table.CheckConstraint("CK_BatchContact_Channel", "[Email] IS NOT NULL OR [CellNumber] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_BatchContact_PaymentSummary_PaymentSummaryId",
                        column: x => x.PaymentSummaryId,
                        principalTable: "PaymentSummary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BatchUpload",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SecondaryClientId = table.Column<int>(type: "int", nullable: true),
                    ClientApiKeyId = table.Column<int>(type: "int", nullable: true),
                    UniqueClientId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UniqueReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Format = table.Column<byte>(type: "tinyint", nullable: false),
                    IsAccepted = table.Column<bool>(type: "bit", nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    PaymentSummaryId = table.Column<int>(type: "int", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchUpload", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchUpload_ClientApiKey_ClientApiKeyId",
                        column: x => x.ClientApiKeyId,
                        principalTable: "ClientApiKey",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchUpload_PaymentSummary_PaymentSummaryId",
                        column: x => x.PaymentSummaryId,
                        principalTable: "PaymentSummary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BatchUpload_SecondaryClient_SecondaryClientId",
                        column: x => x.SecondaryClientId,
                        principalTable: "SecondaryClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillingRate",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceTypeId = table.Column<byte>(type: "tinyint", nullable: false),
                    FeePerRecord = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingRate", x => x.Id);
                    table.CheckConstraint("CK_BillingRate_FeePerRecord", "[FeePerRecord] >= 0");
                    table.ForeignKey(
                        name: "FK_BillingRate_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobLease",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LastRunDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobLease", x => x.JobName);
                });

            migrationBuilder.CreateTable(
                name: "NotificationMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SecondaryClientId = table.Column<int>(type: "int", nullable: true),
                    PaymentSummaryId = table.Column<int>(type: "int", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    SentDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationMessage_PaymentSummary_PaymentSummaryId",
                        column: x => x.PaymentSummaryId,
                        principalTable: "PaymentSummary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationMessage_SecondaryClient_SecondaryClientId",
                        column: x => x.SecondaryClientId,
                        principalTable: "SecondaryClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicHoliday",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHoliday", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchContact_PaymentSummaryId_Purpose",
                table: "BatchContact",
                columns: new[] { "PaymentSummaryId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_BatchUpload_ClientApiKeyId",
                table: "BatchUpload",
                column: "ClientApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchUpload_PaymentSummaryId",
                table: "BatchUpload",
                column: "PaymentSummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_BatchUpload_ReceivedDate",
                table: "BatchUpload",
                column: "ReceivedDate");

            migrationBuilder.CreateIndex(
                name: "IX_BatchUpload_SecondaryClientId_ReceivedDate",
                table: "BatchUpload",
                columns: new[] { "SecondaryClientId", "ReceivedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingRate_ServiceTypeId_EffectiveFrom",
                table: "BillingRate",
                columns: new[] { "ServiceTypeId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessage_DeduplicationKey",
                table: "NotificationMessage",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessage_PaymentSummaryId",
                table: "NotificationMessage",
                column: "PaymentSummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessage_SecondaryClientId",
                table: "NotificationMessage",
                column: "SecondaryClientId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessage_Status_NextAttemptDate",
                table: "NotificationMessage",
                columns: new[] { "Status", "NextAttemptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicHoliday_HolidayDate",
                table: "PublicHoliday",
                column: "HolidayDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchContact");

            migrationBuilder.DropTable(
                name: "BatchUpload");

            migrationBuilder.DropTable(
                name: "BillingRate");

            migrationBuilder.DropTable(
                name: "JobLease");

            migrationBuilder.DropTable(
                name: "NotificationMessage");

            migrationBuilder.DropTable(
                name: "PublicHoliday");

            migrationBuilder.DropColumn(
                name: "FeeTotal",
                table: "PaymentSummary");
        }
    }
}
