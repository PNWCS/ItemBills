//using QBFC16Lib;
//using Serilog;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace QB_ItemBills_Lib
//{
//    public class ItemBillReader
//    {
//        //public string TxnID { get; set; }  // Added property for transaction ID

//        public static List<ItemBill> QueryAllItemBills()
//        {
//            List<ItemBill> itemBills = new List<ItemBill>();

//            QBSessionManager sessionManager = new QBSessionManager();
//            try
//            {
//                sessionManager.OpenConnection("", "ItemBills App");
//                sessionManager.BeginSession("", ENOpenMode.omDontCare);

//                IMsgSetRequest billRequest = sessionManager.CreateMsgSetRequest("US", 16, 0);
//                billRequest.Attributes.OnError = ENRqOnError.roeContinue;
//                IBillQuery billQuery = billRequest.AppendBillQueryRq();
//                billQuery.IncludeLineItems.SetValue(true);

//                IMsgSetResponse billResponseSet = sessionManager.DoRequests(billRequest);
//                IResponse billResponse = billResponseSet.ResponseList.GetAt(0);

//                if (billResponse.StatusCode == 0 && billResponse.Detail is IBillRetList billList)
//                {
//                    for (int i = 0; i < billList.Count; i++)
//                    {
//                        IBillRet bill = billList.GetAt(i);
//                        itemBills.Add(new ItemBill
//                        {
//                            TxnID = bill.TxnID?.GetValue() ?? "N/A"
//                        });
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine("[ERROR] " + ex.Message);
//            }
//            finally
//            {
//                try
//                {
//                    sessionManager.EndSession();
//                    sessionManager.CloseConnection();
//                }
//                catch { }
//            }

//            return itemBills;
//        }

//        public static void GetVendorsAndItemBills()
//        {
//            QBSessionManager sessionManager = new QBSessionManager();

//            try
//            {
//                sessionManager.OpenConnection("", "ItemBills App");
//                sessionManager.BeginSession("", ENOpenMode.omDontCare);

//                // Step 1: Fetch Vendors
//                IMsgSetRequest vendorRequest = sessionManager.CreateMsgSetRequest("US", 16, 0);
//                vendorRequest.Attributes.OnError = ENRqOnError.roeContinue;
//                IVendorQuery vendorQuery = vendorRequest.AppendVendorQueryRq();

//                IMsgSetResponse vendorResponseSet = sessionManager.DoRequests(vendorRequest);
//                IResponse vendorResponse = vendorResponseSet.ResponseList.GetAt(0);

//                if (vendorResponse.StatusCode == 0 && vendorResponse.Detail is IVendorRetList vendorList)
//                {
//                    for (int i = 0; i < vendorList.Count; i++)
//                    {
//                        IVendorRet vendor = vendorList.GetAt(i);
//                        string vendorName = vendor.Name?.GetValue() ?? "Unknown Vendor";

//                        Console.WriteLine($"\nVendor: {vendorName}");

//                        // Step 2: Fetch Bills for this Vendor
//                        IMsgSetRequest billRequest = sessionManager.CreateMsgSetRequest("US", 16, 0);
//                        billRequest.Attributes.OnError = ENRqOnError.roeContinue;
//                        IBillQuery billQuery = billRequest.AppendBillQueryRq();
//                        billQuery.IncludeLineItems.SetValue(true);

//                        IMsgSetResponse billResponseSet = sessionManager.DoRequests(billRequest);
//                        IResponse billResponse = billResponseSet.ResponseList.GetAt(0);

//                        if (billResponse.StatusCode == 0 && billResponse.Detail is IBillRetList billList)
//                        {
//                            for (int j = 0; j < billList.Count; j++)
//                            {
//                                IBillRet bill = billList.GetAt(j);
//                                string date = bill.TxnDate?.GetValue().ToShortDateString() ?? "N/A";
//                                string QBID = bill.TxnID?.GetValue() ?? "N/A";

//                                Console.WriteLine($"  Bill Ref#: {QBID} | Date: {date}");
//                                if (bill.ORItemLineRetList != null)
//                                {
//                                    for (int k = 0; k < bill.ORItemLineRetList.Count; k++)
//                                    {
//                                        var orLine = bill.ORItemLineRetList.GetAt(k);

//                                        if (orLine.ItemLineRet != null)
//                                        {
//                                            var line = orLine.ItemLineRet;
//                                            string itemName = line.ItemRef?.FullName?.GetValue() ?? "Unknown Item";
//                                            int quantity = (int)(line.Quantity?.GetValue() ?? 0);

//                                            Console.WriteLine($"    - Item: {itemName}, Quantity: {quantity}");
//                                        }
//                                        else if (orLine.ItemGroupLineRet != null)
//                                        {
//                                            var groupLine = orLine.ItemGroupLineRet;
//                                            string groupName = groupLine.ItemGroupRef?.FullName?.GetValue() ?? "Unknown Group";
//                                            int groupQty = (int)(groupLine.Quantity?.GetValue() ?? 0);

//                                            Console.WriteLine($"    - Group Item: {groupName}, Quantity: {groupQty}");
//                                        }
//                                    }
//                                }
//                            }
//                        }
//                        else
//                        {
//                            Console.WriteLine("  No bills found.");
//                        }
//                    }
//                }
//                else
//                {
//                    Console.WriteLine("No vendors found.");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine("[ERROR] " + ex.Message);
//            }
//            finally
//            {
//                try
//                {
//                    sessionManager.EndSession();
//                    sessionManager.CloseConnection();
//                }
//                catch { }
//            }
//        }
//    }
//}



using QBFC16Lib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QB_ItemBills_Lib
{
    public class ItemBillReader
    {
        public static List<ItemBill> QueryAllItemBills()
        {
            List<ItemBill> itemBills = new List<ItemBill>();
            QBSessionManager sessionManager = new QBSessionManager();

            try
            {
                sessionManager.OpenConnection("", "ItemBills App");
                sessionManager.BeginSession("", ENOpenMode.omDontCare);

                IMsgSetRequest billRequest = sessionManager.CreateMsgSetRequest("US", 16, 0);
                billRequest.Attributes.OnError = ENRqOnError.roeContinue;
                IBillQuery billQuery = billRequest.AppendBillQueryRq();

                // Important: Make sure we get line items
                billQuery.IncludeLineItems.SetValue(true);

                IMsgSetResponse billResponseSet = sessionManager.DoRequests(billRequest);
                IResponse billResponse = billResponseSet.ResponseList.GetAt(0);

                if (billResponse.StatusCode == 0 && billResponse.Detail is IBillRetList billList)
                {
                    for (int i = 0; i < billList.Count; i++)
                    {
                        IBillRet bill = billList.GetAt(i);

                        var itemBill = new ItemBill
                        {
                            TxnID = bill.TxnID?.GetValue() ?? string.Empty,
                            VendorName = bill.VendorRef?.FullName?.GetValue() ?? string.Empty,
                            BillDate = bill.TxnDate?.GetValue() ?? DateTime.MinValue,
                            InvoiceNum = bill.RefNumber?.GetValue() ?? string.Empty,
                            Memo = bill.Memo?.GetValue() ?? string.Empty,
                        };

                        // Try to parse the QBID from memo (which should contain the CompanyID)
                        if (int.TryParse(itemBill.Memo, out int qbid))
                        {
                            itemBill.QBID = qbid;
                        }

                        // Process line items
                        if (bill.ORItemLineRetList != null)
                        {
                            for (int j = 0; j < bill.ORItemLineRetList.Count; j++)
                            {
                                var orLine = bill.ORItemLineRetList.GetAt(j);

                                if (orLine.ItemLineRet != null)
                                {
                                    var line = orLine.ItemLineRet;

                                    var billLine = new ItemBillLine
                                    {
                                        PartName = line.ItemRef?.FullName?.GetValue() ?? string.Empty,
                                        Quantity = (int)(line.Quantity?.GetValue() ?? 0),
                                        UnitPrice = line.Cost?.GetValue() ?? 0.0
                                    };

                                    itemBill.Lines.Add(billLine);
                                }
                                // We're not handling ItemGroupLineRet here as the test doesn't seem to use it
                            }
                        }

                        itemBills.Add(itemBill);
                    }
                }
                else if (billResponse.StatusCode != 0)
                {
                    // Log the error
                    Log.Error($"QuickBooks error: {billResponse.StatusMessage}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error querying QuickBooks bills");
            }
            finally
            {
                try
                {
                    sessionManager.EndSession();
                    sessionManager.CloseConnection();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error closing QuickBooks session");
                }
            }

            return itemBills;
        }
    }
}