using System.Collections.Generic;

namespace GLUploadIssue_ACU2023R2;

public class GLTransactionDetail
{
    public string Branch { get; set; }
    public string Account { get; set; }
    public string Subaccount { get; set; }
    public string TransactionDescription { get; set; }
    public string Project { get; set; }
    public string ProjectTask { get; set; }
    public string CostCode { get; set; }
    public string ReferenceNbr { get; set; }
    public decimal? Qty { get; set; }
    public string Uom { get; set; }
    public decimal? DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    public bool NonBillable { get; set; }
    public string TaxCategory { get; set; }
    public string TaxID { get; set; }
    public Dictionary<string, object> CustomColumns { get; set; }
}