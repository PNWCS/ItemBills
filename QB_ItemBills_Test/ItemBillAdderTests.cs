using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QBFC16Lib;
using QB_ItemBills_Lib;
using static QB_ItemBills_Test.CommonMethods;

namespace QB_ItemBills_Test
{
    [Collection("Sequential Tests")]
    public class ItemBillAdderTests
    {
        [Fact]
        public void AddItemBills_ShouldWriteToQuickBooks_AndBeVerifiable()
        {
            var vendors = new List<(string Name, string ListID)>();
            var parts = new List<(string Name, double Price, string ListID)>();
            var bills = new List<ItemBill>();
            var createdTxnIDs = new List<string>();

            const int BILL_COUNT = 2;

            try
            {
                // 1. Add Vendors
                using (var session = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < BILL_COUNT; i++)
                    {
                        string name = "Vend_" + Guid.NewGuid().ToString("N")[..6];
                        string listID = AddVendor(session, name);
                        vendors.Add((name, listID));
                    }
                }

                // 2. Add Inventory Items
                using (var session = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < BILL_COUNT * 2; i++)
                    {
                        string name = "Part_" + Guid.NewGuid().ToString("N")[..6];
                        double price = 10 + i;
                        string listID = AddInventoryPart(session, name, price);
                        parts.Add((name, price, listID));
                    }
                }

                // 3. Build ItemBills
                for (int i = 0; i < BILL_COUNT; i++)
                {
                    var vendor = vendors[i];
                    int companyID = 1000 + i;
                    string invoiceNum = $"INV_{companyID}";
                    var bill = new ItemBill
                    {
                        VendorName = vendor.Name,
                        BillDate = DateTime.Today,
                        InvoiceNum = invoiceNum,
                        Memo = companyID.ToString(),
                        QBID = companyID,
                        Lines = new List<ItemBillLine>
                        {
                            new ItemBillLine { PartName = parts[2*i].Name, UnitPrice = parts[2*i].Price, Quantity = 2 },
                            new ItemBillLine { PartName = parts[2*i+1].Name, UnitPrice = parts[2*i+1].Price, Quantity = 3 }
                        }
                    };
                    bills.Add(bill);
                }

                // 4. Call the Adder
                ItemBillAdder.AddItemBills(bills);

                // 5. Validate TxnIDs populated
                foreach (var bill in bills)
                {
                    Assert.False(string.IsNullOrWhiteSpace(bill.TxnID), "TxnID should be set after AddItemBills");
                    createdTxnIDs.Add(bill.TxnID);
                }

                // 6. Query QB directly to verify each bill
                using (var session = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var bill in bills)
                    {
                        var retrieved = QueryBillByTxnID(session, bill.TxnID);
                        Assert.NotNull(retrieved);
                        Assert.Equal(bill.VendorName, retrieved.VendorName);
                        Assert.Equal(bill.InvoiceNum, retrieved.InvoiceNum);
                        Assert.Equal(bill.BillDate.Date, retrieved.BillDate.Date);
                        Assert.Equal(bill.Lines.Count, retrieved.Lines.Count);
                    }
                }
            }
            finally
            {
                // Cleanup: Delete Bills
                using (var session = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var txnID in createdTxnIDs)
                        DeleteBill(session, txnID);
                }

                using (var session = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var part in parts)
                        DeleteListObject(session, part.ListID, ENListDelType.ldtItemInventory);
                }

                using (var session = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var vendor in vendors)
                        DeleteListObject(session, vendor.ListID, ENListDelType.ldtVendor);
                }
            }
        }

        // Reuse Query logic
        private ItemBill QueryBillByTxnID(QuickBooksSession session, string txnID)
        {
            var request = session.CreateRequestSet();
            var query = request.AppendBillQueryRq();
            query.ORBillQuery.TxnIDList.Add(txnID);
            query.IncludeLineItems.SetValue(true);

            var response = session.SendRequest(request);
            var ret = response.ResponseList.GetAt(0)?.Detail as IBillRet;
            if (ret == null) return null;

            var result = new ItemBill
            {
                TxnID = ret.TxnID.GetValue(),
                VendorName = ret.VendorRef?.FullName?.GetValue() ?? "",
                InvoiceNum = ret.RefNumber?.GetValue() ?? "",
                BillDate = ret.TxnDate.GetValue(),
                Memo = ret.Memo?.GetValue() ?? "",
                Lines = new()
            };

            if (ret.ORItemLineRetList != null)
            {
                foreach (IORItemLineRet line in ret.ORItemLineRetList)
                {
                    var item = line.ItemLineRet;
                    if (item == null) continue;
                    result.Lines.Add(new ItemBillLine
                    {
                        PartName = item.ItemRef?.FullName?.GetValue() ?? "",
                        Quantity = (int)item.Quantity?.GetValue(),
                        UnitPrice = item.Cost?.GetValue() ?? 0
                    });
                }
            }

            return result;
        }
    }
}
