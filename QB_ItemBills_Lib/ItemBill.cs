using System;
using System.Collections.Generic;

namespace QB_ItemBills_Lib
{
    public class ItemBill
    {
        public string TxnID { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public string InvoiceNum { get; set; } = string.Empty; // This maps to RefNumber from the test
        public string Memo { get; set; } = string.Empty; // Stores the CompanyID
        public int QBID { get; set; } // This is likely used as an internal identifier
        public List<ItemBillLine> Lines { get; set; } = new List<ItemBillLine>();
        public ItemBillStatus Status { get; set; } = ItemBillStatus.Unchanged;
        //public ItemBillStatus Status { get; set; }
    }

    public class ItemBillLine
    {
        public string PartName { get; set; } = string.Empty;
        public double UnitPrice { get; set; }
        public int Quantity { get; set; }
        public ItemBillStatus Status { get; set; }
    }
}


