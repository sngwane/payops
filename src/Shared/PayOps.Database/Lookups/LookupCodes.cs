namespace PayOps.Database.Lookups;

public enum BatchStatusCode : byte { Created = 0, Sent = 1, Ack = 2, Nack = 3, Duplicate = 4 }

public enum ReconStatusCode : byte { Reconciled = 1, Unreconciled = 2, Discrepancies = 3 }

public enum TransactionStatusCode : byte { Pending = 1, Transmitted = 2, Success = 3, Rejected = 4, PendingUnreconciled = 5 }

public enum ServiceTypeCode : byte { SDV = 1, RTL = 2, OneDay = 3, TwoDay = 4, DW = 5 }

public enum AccountTypeCode : byte { Nominated = 1, Charges = 2 }

public enum ApprovalStatusCode : byte { Pending = 1, Approved = 2, Rejected = 3 }
