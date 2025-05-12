using System;
using System.Collections.Generic;
using QBFC16Lib;

namespace QB_ItemBills_Lib
{
    public static class BillAdder
    {
        public static void AddBills(List<ItemBill> bills)
        {
            using (var session = new QuickBooksSession("Bill Adder"))
            {
                var requestSet = session.CreateRequestSet();

                foreach (var bill in bills)
                {
                    var billAdd = requestSet.AppendBillAddRq();
                    billAdd.VendorRef.FullName.SetValue(bill.VendorName);
                    billAdd.TxnDate.SetValue(bill.BillDate);
                    billAdd.RefNumber.SetValue(bill.InvoiceNum);

                    if (!string.IsNullOrWhiteSpace(bill.Memo))
                        billAdd.Memo.SetValue(bill.Memo);

                    foreach (var line in bill.Lines)
                    {
                        var orItemLine = billAdd.ORItemLineAddList.Append();
                        var itemLine = orItemLine.ItemLineAdd;
                        itemLine.ItemRef.FullName.SetValue(line.PartName);
                        itemLine.Quantity.SetValue(line.Quantity);
                        itemLine.Cost.SetValue(line.UnitPrice);
                    }
                }

                var response = session.SendRequest(requestSet);

                for (int i = 0; i < bills.Count; i++)
                {
                    var resp = response.ResponseList.GetAt(i);
                    if (resp.StatusCode == 0)
                    {
                        var ret = (IBillRet)resp.Detail;
                        bills[i].TxnID = ret.TxnID.GetValue();
                        Console.WriteLine($"Bill {i + 1} added successfully: TxnID = {bills[i].TxnID}");
                    }
                    else
                    {
                        bills[i].TxnID = string.Empty;
                        Console.WriteLine($"Failed to add Bill {i + 1}: {resp.StatusMessage} (Code: {resp.StatusCode})");
                    }
                }
            }
        }
    }
}