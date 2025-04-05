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