using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PayOps.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;",
                suppressTransaction: true);

            migrationBuilder.CreateTable(
                name: "AccountType",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalStatus",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppUser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bank",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BatchStatus",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrimaryClient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UniqueClientId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AutoProcess = table.Column<bool>(type: "bit", nullable: false),
                    LogoFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrimaryClient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconStatus",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceType",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionStatus",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankClientProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    ClientProfileNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClientIdentifier = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RunningSequence = table.Column<long>(type: "bigint", nullable: false),
                    DailySequence = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankClientProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankClientProfile_Bank_BankId",
                        column: x => x.BankId,
                        principalTable: "Bank",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentBatch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RunDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BatchStatusId = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    ApprovalStatusId = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentBatch", x => x.Id);
                    table.CheckConstraint("CK_PaymentBatch_MakerChecker", "[ApprovedByUserId] IS NULL OR [ApprovedByUserId] <> [CreatedByUserId]");
                    table.ForeignKey(
                        name: "FK_PaymentBatch_AppUser_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBatch_AppUser_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBatch_ApprovalStatus_ApprovalStatusId",
                        column: x => x.ApprovalStatusId,
                        principalTable: "ApprovalStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBatch_Bank_BankId",
                        column: x => x.BankId,
                        principalTable: "Bank",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentBatch_BatchStatus_BatchStatusId",
                        column: x => x.BatchStatusId,
                        principalTable: "BatchStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SecondaryClient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrimaryClientId = table.Column<int>(type: "int", nullable: false),
                    UniqueClientId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecondaryClient", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecondaryClient_PrimaryClient_PrimaryClientId",
                        column: x => x.PrimaryClientId,
                        principalTable: "PrimaryClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRole_AppUser_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRole_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSummary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentBatchId = table.Column<int>(type: "int", nullable: true),
                    SecondaryClientId = table.Column<int>(type: "int", nullable: false),
                    ServiceTypeId = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RejectionAccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RejectionBranchCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RejectionAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    UniqueReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReconStatusId = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)2),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    ValidTo = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSummary", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentSummary_AppUser_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentSummary_PaymentBatch_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalTable: "PaymentBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentSummary_ReconStatus_ReconStatusId",
                        column: x => x.ReconStatusId,
                        principalTable: "ReconStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentSummary_SecondaryClient_SecondaryClientId",
                        column: x => x.SecondaryClientId,
                        principalTable: "SecondaryClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentSummary_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PaymentSummaryHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.CreateTable(
                name: "PaymentRecord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentSummaryId = table.Column<int>(type: "int", nullable: false),
                    BankFileReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmployeeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Initials = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IdOrPassport = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Cell = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BranchCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransactionStatusId = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecord", x => x.Id);
                    table.CheckConstraint("CK_PaymentRecord_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentRecord_PaymentSummary_PaymentSummaryId",
                        column: x => x.PaymentSummaryId,
                        principalTable: "PaymentSummary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRecord_TransactionStatus_TransactionStatusId",
                        column: x => x.TransactionStatusId,
                        principalTable: "TransactionStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSummaryComment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentSummaryId = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSummaryComment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentSummaryComment_AppUser_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentSummaryComment_PaymentSummary_PaymentSummaryId",
                        column: x => x.PaymentSummaryId,
                        principalTable: "PaymentSummary",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRejection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentRecordId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentBatchId = table.Column<int>(type: "int", nullable: true),
                    ServiceTypeId = table.Column<byte>(type: "tinyint", nullable: true),
                    AccountName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankFileReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransactionStatusId = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)5),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRejection", x => x.Id);
                    table.CheckConstraint("CK_PaymentRejection_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentRejection_PaymentBatch_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalTable: "PaymentBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRejection_PaymentRecord_PaymentRecordId",
                        column: x => x.PaymentRecordId,
                        principalTable: "PaymentRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRejection_ServiceType_ServiceTypeId",
                        column: x => x.ServiceTypeId,
                        principalTable: "ServiceType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRejection_TransactionStatus_TransactionStatusId",
                        column: x => x.TransactionStatusId,
                        principalTable: "TransactionStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRecordStatusHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentRecordId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentRejectionId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionStatusId = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentBatchId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: true),
                    ChangedDate = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecordStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRecordStatusHistory_AppUser_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRecordStatusHistory_PaymentBatch_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalTable: "PaymentBatch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRecordStatusHistory_PaymentRecord_PaymentRecordId",
                        column: x => x.PaymentRecordId,
                        principalTable: "PaymentRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRecordStatusHistory_PaymentRejection_PaymentRejectionId",
                        column: x => x.PaymentRejectionId,
                        principalTable: "PaymentRejection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRecordStatusHistory_TransactionStatus_TransactionStatusId",
                        column: x => x.TransactionStatusId,
                        principalTable: "TransactionStatus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AccountType",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { (byte)1, "Nominated", true, "Nominated Account" },
                    { (byte)2, "Charges", true, "Charges Account" }
                });

            migrationBuilder.InsertData(
                table: "ApprovalStatus",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { (byte)1, "Pending", true, "Pending approval" },
                    { (byte)2, "Approved", true, "Approved" },
                    { (byte)3, "Rejected", true, "Rejected" }
                });

            migrationBuilder.InsertData(
                table: "BatchStatus",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { (byte)0, "Created", true, "Created" },
                    { (byte)1, "Sent", true, "Sent" },
                    { (byte)2, "Ack", true, "Acknowledged" },
                    { (byte)3, "Nack", true, "Not acknowledged" },
                    { (byte)4, "Duplicate", true, "Duplicate" }
                });

            migrationBuilder.InsertData(
                table: "ReconStatus",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { (byte)1, "Reconciled", true, "Reconciled" },
                    { (byte)2, "Unreconciled", true, "Unreconciled" },
                    { (byte)3, "Discrepancies", true, "Discrepancies" }
                });

            migrationBuilder.InsertData(
                table: "ServiceType",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { (byte)1, "SDV", true, "Same Day Value" },
                    { (byte)2, "RTL", true, "Real Time Payments" },
                    { (byte)3, "OneDay", true, "One Day" },
                    { (byte)4, "TwoDay", true, "Two Day" },
                    { (byte)5, "DW", true, "Direct Withdrawal" }
                });

            migrationBuilder.InsertData(
                table: "TransactionStatus",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { (byte)1, "Pending", true, "Pending" },
                    { (byte)2, "Transmitted", true, "Transmitted" },
                    { (byte)3, "Success", true, "Success" },
                    { (byte)4, "Rejected", true, "Rejected" },
                    { (byte)5, "PendingUnreconciled", true, "Pending unreconciled" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountType_Code",
                table: "AccountType",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalStatus_Code",
                table: "ApprovalStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_Email",
                table: "AppUser",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_ExternalId",
                table: "AppUser",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_Username",
                table: "AppUser",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bank_Code",
                table: "Bank",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankClientProfile_BankId_ClientProfileNumber",
                table: "BankClientProfile",
                columns: new[] { "BankId", "ClientProfileNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatchStatus_Code",
                table: "BatchStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_ApprovalStatusId",
                table: "PaymentBatch",
                column: "ApprovalStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_ApprovedByUserId",
                table: "PaymentBatch",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_BankId",
                table: "PaymentBatch",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_BatchStatusId",
                table: "PaymentBatch",
                column: "BatchStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_CreatedByUserId",
                table: "PaymentBatch",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_Reference",
                table: "PaymentBatch",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentBatch_RunDate",
                table: "PaymentBatch",
                column: "RunDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecord_BankFileReference",
                table: "PaymentRecord",
                column: "BankFileReference");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecord_PaymentSummaryId",
                table: "PaymentRecord",
                column: "PaymentSummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecord_TransactionStatusId",
                table: "PaymentRecord",
                column: "TransactionStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecordStatusHistory_ChangedByUserId",
                table: "PaymentRecordStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecordStatusHistory_PaymentBatchId",
                table: "PaymentRecordStatusHistory",
                column: "PaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecordStatusHistory_PaymentRecordId",
                table: "PaymentRecordStatusHistory",
                column: "PaymentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecordStatusHistory_PaymentRejectionId",
                table: "PaymentRecordStatusHistory",
                column: "PaymentRejectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecordStatusHistory_TransactionStatusId_ChangedDate",
                table: "PaymentRecordStatusHistory",
                columns: new[] { "TransactionStatusId", "ChangedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRejection_BankFileReference",
                table: "PaymentRejection",
                column: "BankFileReference");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRejection_PaymentBatchId",
                table: "PaymentRejection",
                column: "PaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRejection_PaymentRecordId",
                table: "PaymentRejection",
                column: "PaymentRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRejection_ServiceTypeId",
                table: "PaymentRejection",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRejection_TransactionStatusId",
                table: "PaymentRejection",
                column: "TransactionStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_CreatedByUserId",
                table: "PaymentSummary",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_PaymentBatchId",
                table: "PaymentSummary",
                column: "PaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_PaymentDate",
                table: "PaymentSummary",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_ReconStatusId",
                table: "PaymentSummary",
                column: "ReconStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_SecondaryClientId_SequenceNumber",
                table: "PaymentSummary",
                columns: new[] { "SecondaryClientId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_ServiceTypeId",
                table: "PaymentSummary",
                column: "ServiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummary_UniqueReference",
                table: "PaymentSummary",
                column: "UniqueReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummaryComment_CreatedByUserId",
                table: "PaymentSummaryComment",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSummaryComment_PaymentSummaryId",
                table: "PaymentSummaryComment",
                column: "PaymentSummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_PrimaryClient_UniqueClientId",
                table: "PrimaryClient",
                column: "UniqueClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconStatus_Code",
                table: "ReconStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Role_Name",
                table: "Role",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecondaryClient_PrimaryClientId",
                table: "SecondaryClient",
                column: "PrimaryClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SecondaryClient_UniqueClientId",
                table: "SecondaryClient",
                column: "UniqueClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceType_Code",
                table: "ServiceType",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionStatus_Code",
                table: "TransactionStatus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_AppUserId_RoleId",
                table: "UserRole",
                columns: new[] { "AppUserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRole_RoleId",
                table: "UserRole",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountType");

            migrationBuilder.DropTable(
                name: "BankClientProfile");

            migrationBuilder.DropTable(
                name: "PaymentRecordStatusHistory");

            migrationBuilder.DropTable(
                name: "PaymentSummaryComment");

            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "PaymentRejection");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "PaymentRecord");

            migrationBuilder.DropTable(
                name: "PaymentSummary")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "PaymentSummaryHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "ValidTo")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "ValidFrom");

            migrationBuilder.DropTable(
                name: "TransactionStatus");

            migrationBuilder.DropTable(
                name: "PaymentBatch");

            migrationBuilder.DropTable(
                name: "ReconStatus");

            migrationBuilder.DropTable(
                name: "SecondaryClient");

            migrationBuilder.DropTable(
                name: "ServiceType");

            migrationBuilder.DropTable(
                name: "AppUser");

            migrationBuilder.DropTable(
                name: "ApprovalStatus");

            migrationBuilder.DropTable(
                name: "Bank");

            migrationBuilder.DropTable(
                name: "BatchStatus");

            migrationBuilder.DropTable(
                name: "PrimaryClient");
        }
    }
}
