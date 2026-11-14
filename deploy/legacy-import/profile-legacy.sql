-- Read-only profile of the legacy BulkPayments data before importing it into PayOps.
-- Run against the legacy database: sqlcmd -C -I -b -S <server> -d BulkPayments -i profile-legacy.sql
SET NOCOUNT ON;

SELECT 'PrimaryClient' AS [Table], COUNT(*) AS [Rows] FROM PrimaryClient
UNION ALL SELECT 'SecondaryClient', COUNT(*) FROM SecondaryClient
UNION ALL SELECT 'BankAccount links', COUNT(*) FROM PrimaryClientBankAccountType
UNION ALL SELECT 'Bank', COUNT(*) FROM Bank
UNION ALL SELECT 'BankClientProfile', COUNT(*) FROM BankClientProfile
UNION ALL SELECT 'BankRun', COUNT(*) FROM BankRun
UNION ALL SELECT 'Header', COUNT(*) FROM Header
UNION ALL SELECT 'Transactions', COUNT(*) FROM Transactions
UNION ALL SELECT 'Rejections', COUNT(*) FROM Rejections
UNION ALL SELECT 'HeaderComment', COUNT(*) FROM HeaderComment
UNION ALL SELECT 'PublicHoliday', COUNT(*) FROM PublicHoliday
UNION ALL SELECT 'BillingRates', COUNT(*) FROM BillingRates;

SELECT 'Transactions amount' AS Total, SUM(Amount) AS Amount FROM Transactions
UNION ALL SELECT 'Header TotalValue', SUM(TotalValue) FROM Header;

WITH ids AS (
    SELECT LTRIM(RTRIM(UniqueClientId)) AS Id, 'Primary' AS Kind FROM PrimaryClient
    UNION ALL SELECT LTRIM(RTRIM(UniqueClientId)), 'Secondary' FROM SecondaryClient)
SELECT 'Client IDs empty' AS [Check], COUNT(*) AS [Rows] FROM ids WHERE ISNULL(Id, '') = ''
UNION ALL SELECT 'Client IDs longer than 20', COUNT(*) FROM ids WHERE LEN(Id) > 20
UNION ALL SELECT 'Client IDs with spaces', COUNT(*) FROM ids WHERE Id LIKE '% %'
UNION ALL SELECT 'Client IDs used more than once', COUNT(*) FROM (SELECT Id FROM ids WHERE ISNULL(Id, '') <> '' GROUP BY Id HAVING COUNT(*) > 1) d
UNION ALL SELECT 'Secondaries with unknown primary', COUNT(*) FROM SecondaryClient s WHERE NOT EXISTS (SELECT 1 FROM PrimaryClient p WHERE p.PrimaryClientId = s.PrimaryClientId)
UNION ALL SELECT 'Headers with no matching secondary', COUNT(*) FROM Header h WHERE NOT EXISTS (SELECT 1 FROM SecondaryClient s WHERE s.UniqueClientId = h.UniqueClientId)
UNION ALL SELECT 'Headers with bad ServiceTypeId', COUNT(*) FROM Header WHERE ISNULL(ServiceTypeId, 0) NOT BETWEEN 1 AND 5
UNION ALL SELECT 'Headers with no PaymentDate', COUNT(*) FROM Header WHERE PaymentDate IS NULL
UNION ALL SELECT 'Headers sharing a BatchId', COUNT(*) FROM Header WHERE BatchId IN (SELECT BatchId FROM Header GROUP BY BatchId HAVING COUNT(*) > 1)
UNION ALL SELECT 'Duplicate UniquePaymentReference', COUNT(*) FROM Header WHERE UniquePaymentReference IN (SELECT UniquePaymentReference FROM Header GROUP BY UniquePaymentReference HAVING COUNT(*) > 1)
UNION ALL SELECT 'Duplicate SequenceNumber per client', COUNT(*) FROM Header h WHERE SequenceNumber IS NULL OR EXISTS (SELECT 1 FROM Header o WHERE o.UniqueClientId = h.UniqueClientId AND o.SequenceNumber = h.SequenceNumber AND o.HeaderId <> h.HeaderId)
UNION ALL SELECT 'Transactions with no Header', COUNT(*) FROM Transactions t WHERE NOT EXISTS (SELECT 1 FROM Header h WHERE h.BatchId = t.BatchId)
UNION ALL SELECT 'Transactions with amount <= 0 or empty', COUNT(*) FROM Transactions WHERE ISNULL(Amount, 0) <= 0
UNION ALL SELECT 'Transactions missing surname/account/branch', COUNT(*) FROM Transactions WHERE ISNULL(EmployeeSurname, '') = '' OR ISNULL(AccountNumber, '') = '' OR ISNULL(BranchCode, '') = ''
UNION ALL SELECT 'Rejections with unknown transaction', COUNT(*) FROM Rejections r WHERE NOT EXISTS (SELECT 1 FROM Transactions t WHERE t.TransactionId = r.TransactionId)
UNION ALL SELECT 'Transactions with more than one rejection', COUNT(*) FROM (SELECT TransactionId FROM Rejections GROUP BY TransactionId HAVING COUNT(*) > 1) d
UNION ALL SELECT 'Distinct BillingRates.DebtorsCode', COUNT(DISTINCT DebtorsCode) FROM BillingRates;
