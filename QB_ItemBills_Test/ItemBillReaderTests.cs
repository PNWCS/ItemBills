using System.Diagnostics;
using Serilog;
using QBFC16Lib;
using static QB_ItemBills_Test.CommonMethods; // Reuse your shared helpers

namespace QB_ItemBills_Test
{
    [Collection("Sequential Tests")]
    public class ItemBillReaderTests
    {
        private const int BILL_COUNT = 2; // We'll create n=2 vendors, 2*n=4 parts, and 2 bills

        [Fact]
        public void CreateAndDelete_Vendors_Parts_Bills()
        {
            var createdVendorListIDs = new List<string>();
            var createdPartListIDs = new List<string>();
            var createdBillTxnIDs = new List<string>();

            // We'll store random vendor names and random part names/prices
            var randomVendorNames = new List<string>();
            var randomPartNames = new List<string>();
            var randomPartPrices = new List<double>();

            // Each Bill's "test info" so we can verify after reading from QB
            var billTestData = new List<ItemBillTestInfo>();

            try
            {
                // 1) Clean logs, etc.
                EnsureLogFileClosed();
                DeleteOldLogFiles();
                ResetLogger();

                // 2) Create 2 vendors
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < BILL_COUNT; i++)
                    {
                        string vendorName = "RandVend_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        string vendorListID = AddVendor(qbSession, vendorName);
                        createdVendorListIDs.Add(vendorListID);
                        randomVendorNames.Add(vendorName);
                    }
                }

                // 3) Create 2*n=4 parts (inventory items)
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < (2 * BILL_COUNT); i++)
                    {
                        string partName = "RandPart_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        double partPrice = 5.0 + i; // vary the price slightly
                        string partListID = AddInventoryPart(qbSession, partName, partPrice);

                        createdPartListIDs.Add(partListID);
                        randomPartNames.Add(partName);
                        randomPartPrices.Add(partPrice);
                    }
                }

                // 4) Create n=2 bills, each referencing:
                //    - The i-th vendor
                //    - 2 line items from the part array
                //    - numeric CompanyID in the Bill's Memo
                //    - vendor invoice number in RefNumber
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < BILL_COUNT; i++)
                    {
                        string vendorListID = createdVendorListIDs[i];
                        string vendorName = randomVendorNames[i];

                        // We'll pick item indices 2*i and 2*i+1
                        int partIndex1 = 2 * i;
                        int partIndex2 = 2 * i + 1;

                        string partListID1 = createdPartListIDs[partIndex1];
                        string partName1 = randomPartNames[partIndex1];
                        double price1 = randomPartPrices[partIndex1];

                        string partListID2 = createdPartListIDs[partIndex2];
                        string partName2 = randomPartNames[partIndex2];
                        double price2 = randomPartPrices[partIndex2];

                        int companyID = 100 + i;  // numeric ID for Bill's memo
                        DateTime billDate = DateTime.Today;
                        string vendorInvoiceNum = "INV_" + (200 + i); // vendor's invoice number

                        // We'll assume quantity=2 for each line
                        int qtyPerLine = 2;

                        string billTxnID = AddItemBill(
                            qbSession,
                            vendorListID,
                            vendorName,
                            billDate,
                            vendorInvoiceNum,
                            companyID,
                            partListID1,
                            partName1,
                            price1,
                            qtyPerLine,
                            partListID2,
                            partName2,
                            price2,
                            qtyPerLine
                        );

                        createdBillTxnIDs.Add(billTxnID);

                        // Record for final asserts
                        billTestData.Add(new ItemBillTestInfo
                        {
                            TxnID = billTxnID,
                            CompanyID = companyID,
                            VendorName = vendorName,
                            BillDate = billDate,
                            RefNumber = vendorInvoiceNum,
                            Lines = new List<ItemBillLine>
                            {
                                new ItemBillLine { PartName = partName1, UnitPrice = price1, Quantity = qtyPerLine },
                                new ItemBillLine { PartName = partName2, UnitPrice = price2, Quantity = qtyPerLine }
                            }
                        });
                    }
                }

                // 5) Query & verify bills
                var allBills = ItemBillReader.QueryAllItemBills();
                // This is your custom method returning a list of item bills from QuickBooks.

                foreach (var bill in billTestData)
                {
                    var matchingBill = allBills.FirstOrDefault(x => x.QBID == bill.TxnID);
                    Assert.NotNull(matchingBill);

                    Assert.Equal(bill.CompanyID.ToString(), matchingBill.Memo); // numeric in Bill's memo
                    Assert.Equal(bill.VendorName, matchingBill.VendorName);
                    Assert.Equal(bill.BillDate.Date, matchingBill.BillDate.Date);
                    Assert.Equal(bill.RefNumber, matchingBill.InvoiceNum);

                    Assert.Equal(2, matchingBill.Lines.Count);

                    // We'll assume lines are in the same order. If not, you might need to match them by part name.
                    Assert.Equal(bill.Lines[0].PartName, matchingBill.Lines[0].PartName);
                    Assert.Equal(bill.Lines[0].UnitPrice, matchingBill.Lines[0].UnitPrice);
                    Assert.Equal(bill.Lines[0].Quantity, matchingBill.Lines[0].Quantity);

                    Assert.Equal(bill.Lines[1].PartName, matchingBill.Lines[1].PartName);
                    Assert.Equal(bill.Lines[1].UnitPrice, matchingBill.Lines[1].UnitPrice);
                    Assert.Equal(bill.Lines[1].Quantity, matchingBill.Lines[1].Quantity);
                }
            }
            finally
            {
                // 6) Cleanup: delete bills first, then parts, then vendors
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var billID in createdBillTxnIDs)
                    {
                        DeleteBill(qbSession, billID);
                    }
                }

                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var partID in createdPartListIDs)
                    {
                        DeleteListObject(qbSession, partID, ENListDelType.ldtItemInventory);
                    }
                }

                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var vendID in createdVendorListIDs)
                    {
                        DeleteListObject(qbSession, vendID, ENListDelType.ldtVendor);
                    }
                }
            }
        }

        //------------------------------------------------------------------------------
        // Create vendor
        //------------------------------------------------------------------------------

        private string AddVendor(QuickBooksSession qbSession, string vendorName)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IVendorAdd vendAdd = request.AppendVendorAddRq();

            vendAdd.Name.SetValue(vendorName);
            // Optional fields like vendAdd.CompanyName.SetValue(...)

            IMsgSetResponse response = qbSession.SendRequest(request);
            return ExtractVendorListID(response);
        }

        //------------------------------------------------------------------------------
        // Create inventory part
        //------------------------------------------------------------------------------

        private string AddInventoryPart(QuickBooksSession qbSession, string partName, double price)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IItemInventoryAdd itemAdd = request.AppendItemInventoryAddRq();

            itemAdd.Name.SetValue(partName);
            // For an inventory item, we must set these accounts (adapt to your file):
            itemAdd.IncomeAccountRef.FullName.SetValue("Sales");
            itemAdd.AssetAccountRef.FullName.SetValue("Inventory Asset");
            itemAdd.COGSAccountRef.FullName.SetValue("Cost of Goods Sold");

            // If you want to store a default purchase cost, or sales price, do so:
            itemAdd.SalesPrice.SetValue(price);

            var resp = qbSession.SendRequest(request);
            return ExtractPartListID(resp);
        }

        //------------------------------------------------------------------------------
        // Create an item-based bill
        //------------------------------------------------------------------------------

        private string AddItemBill(
            QuickBooksSession qbSession,
            string vendorListID,
            string vendorName,
            DateTime billDate,
            string refNumber,
            int companyID,
            string partListID1,
            string partName1,
            double price1,
            int qty1,
            string partListID2,
            string partName2,
            double price2,
            int qty2
        )
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IBillAdd billAddRq = request.AppendBillAddRq();

            // Required: VendorRef
            billAddRq.VendorRef.ListID.SetValue(vendorListID);
            // Optionally set vendor address, or rely on default

            // Bill date
            billAddRq.TxnDate.SetValue(billDate);

            // The vendor invoice number (RefNumber)
            billAddRq.RefNumber.SetValue(refNumber);

            // CompanyID in the Bill's Memo
            billAddRq.Memo.SetValue(companyID.ToString());

            // Add the first item line
            var lineAdd1 = billAddRq.ORItemLineAddList.Append().ItemLineAdd;
            lineAdd1.ItemRef.ListID.SetValue(partListID1);
            lineAdd1.Quantity.SetValue(qty1);
            lineAdd1.Cost.SetValue(price1);  // the cost per unit

            // Add the second item line
            var lineAdd2 = billAddRq.ORItemLineAddList.Append().ItemLineAdd;
            lineAdd2.ItemRef.ListID.SetValue(partListID2);
            lineAdd2.Quantity.SetValue(qty2);
            lineAdd2.Cost.SetValue(price2);

            var resp = qbSession.SendRequest(request);
            return ExtractBillTxnID(resp);
        }

        //------------------------------------------------------------------------------
        // Deletion
        //------------------------------------------------------------------------------

        private void DeleteBill(QuickBooksSession qbSession, string txnID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var txnDel = request.AppendTxnDelRq();
            txnDel.TxnDelType.SetValue(ENTxnDelType.tdtBill);
            txnDel.TxnID.SetValue(txnID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting Bill TxnID: {txnID}");
        }

        private void DeleteListObject(QuickBooksSession qbSession, string listID, ENListDelType listDelType)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var listDel = request.AppendListDelRq();
            listDel.ListDelType.SetValue(listDelType);
            listDel.ListID.SetValue(listID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting {listDelType} {listID}");
        }

        //------------------------------------------------------------------------------
        // Extractors
        //------------------------------------------------------------------------------

        private string ExtractVendorListID(IMsgSetResponse resp)
        {
            var respList = resp.ResponseList;
            if (respList == null || respList.Count == 0)
                throw new Exception("No response from VendorAdd.");

            var firstResp = respList.GetAt(0);
            if (firstResp.StatusCode != 0)
                throw new Exception($"VendorAdd failed: {firstResp.StatusMessage}");

            var vendRet = firstResp.Detail as IVendorRet;
            if (vendRet == null)
                throw new Exception("No IVendorRet returned from vendor add.");

            return vendRet.ListID.GetValue();
        }

        private string ExtractPartListID(IMsgSetResponse resp)
        {
            var respList = resp.ResponseList;
            if (respList == null || respList.Count == 0)
                throw new Exception("No response from ItemInventoryAdd.");

            var firstResp = respList.GetAt(0);
            if (firstResp.StatusCode != 0)
                throw new Exception($"ItemInventoryAdd failed: {firstResp.StatusMessage}");

            var itemRet = firstResp.Detail as IItemInventoryRet;
            if (itemRet == null)
                throw new Exception("No IItemInventoryRet returned.");

            return itemRet.ListID.GetValue();
        }

        private string ExtractBillTxnID(IMsgSetResponse resp)
        {
            var respList = resp.ResponseList;
            if (respList == null || respList.Count == 0)
                throw new Exception("No response from BillAddRq.");

            var firstResp = respList.GetAt(0);
            if (firstResp.StatusCode != 0)
                throw new Exception($"BillAdd failed: {firstResp.StatusMessage}");

            var billRet = firstResp.Detail as IBillRet;
            if (billRet == null)
                throw new Exception("No IBillRet returned.");

            return billRet.TxnID.GetValue();
        }

        //------------------------------------------------------------------------------
        // Error Handler
        //------------------------------------------------------------------------------

        private void CheckForError(IMsgSetResponse resp, string context)
        {
            if (resp?.ResponseList == null || resp.ResponseList.Count == 0)
                return;

            var firstResp = resp.ResponseList.GetAt(0);
            if (firstResp.StatusCode != 0)
            {
                throw new Exception($"Error {context}: {firstResp.StatusMessage}. Status code: {firstResp.StatusCode}");
            }
            else
            {
                Debug.WriteLine($"OK: {context}");
            }
        }

        //------------------------------------------------------------------------------
        // POCO CLASSES
        //------------------------------------------------------------------------------

        private class ItemBillTestInfo
        {
            public string TxnID { get; set; } = "";
            public int CompanyID { get; set; }
            public string VendorName { get; set; } = "";
            public DateTime BillDate { get; set; }
            public string RefNumber { get; set; } = "";
            public List<ItemBillLine> Lines { get; set; } = new();
        }

        private class ItemBillLine
        {
            public string PartName { get; set; } = "";
            public double UnitPrice { get; set; }
            public int Quantity { get; set; }
        }
    }
}
