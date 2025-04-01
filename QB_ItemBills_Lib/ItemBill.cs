//using Microsoft.VisualBasic;
//using System;
//using System.Collections.Generic;
//using QBFC16Lib;

//namespace QB_ItemBills_Lib
//{
//    //public class PartInfo
//    //{
//    //    public string PartName { get; set; }
//    //    public int Quantity { get; set; }

//    //    public PartInfo(string partName, int quantity)
//    //    {
//    //        PartName = partName;
//    //        Quantity = quantity;
//    //    }
//    //}

//    public class ItemBill
//    {
//        public string VendorName { get; set; }
//        public string TxnDate { get; set; }
//        public string TxnID { get; set;  }
//        public int QBID { get; set; }
//        public string RefNumber { get; set; }

//    }
//        //public List<PartInfo> Parts { get; set; }

//        //    public ItemBill(string vendorName, string txnDate, int qbid, string refNumber, List<PartInfo> parts)
//        //    {
//        //        VendorName = vendorName;
//        //        TxnDate = txnDate;
//        //        QBID = qbid;
//        //        RefNumber = refNumber;
//        //        Parts = parts;
//        //    }
//        //}


//        }



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
    }

    public class ItemBillLine
    {
        public string PartName { get; set; } = string.Empty;
        public double UnitPrice { get; set; }
        public int Quantity { get; set; }
    }
}


