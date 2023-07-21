using System;
using System.Collections.Generic;

namespace GLUploadIssue_ACU2023R2;

public class GLTransaction
{
    public bool Hold { get; set; }
    public bool Release { get; set; }
    public string Branch { get; set; }
    public string Ledger { get; set; }
    public string Currency { get; set; }
    public bool AutoReverse { get; set; }
    public bool CreateTaxTransactions { get; set; }
    public bool SkipTaxAmountValidation { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string PostPeriod { get; set; }
    public string Description { get; set; }
    public List<GLTransactionDetail> Details { get; set; } = new();

    public string BatchNbr { get; set; }
    public string ErrorMessage { get; set; }
}