using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QBFC16Lib;

namespace QB_ItemBills_Lib
{

        public class BillDeleter
        {
            public static void DeleteAllBills()
            {
                QBSessionManager sessionManager = new QBSessionManager();
                List<string> billTxnIDs = new List<string>();

                try
                {
                    // Step 1: Open a connection to QuickBooks
                    sessionManager.OpenConnection("", "ItemBills App");
                    sessionManager.BeginSession("", ENOpenMode.omDontCare);

                    // Step 2: Query all existing bills to get their TxnIDs
                    IMsgSetRequest billRequest = sessionManager.CreateMsgSetRequest("US", 16, 0);
                    billRequest.Attributes.OnError = ENRqOnError.roeContinue;
                    IBillQuery billQuery = billRequest.AppendBillQueryRq();

                    IMsgSetResponse billResponseSet = sessionManager.DoRequests(billRequest);
                    IResponse billResponse = billResponseSet.ResponseList.GetAt(0);

                    if (billResponse.StatusCode == 0 && billResponse.Detail is IBillRetList billList)
                    {
                        Console.WriteLine($"Found {billList.Count} bills to delete");

                        // Collect all TxnIDs first
                        for (int i = 0; i < billList.Count; i++)
                        {
                            IBillRet bill = billList.GetAt(i);
                            string txnID = bill.TxnID?.GetValue() ?? string.Empty;
                            string refNum = bill.RefNumber?.GetValue() ?? "No Ref";
                            string vendor = bill.VendorRef?.FullName?.GetValue() ?? "Unknown";

                            if (!string.IsNullOrEmpty(txnID))
                            {
                                billTxnIDs.Add(txnID);
                                Console.WriteLine($"Queued for deletion: Bill #{i + 1} - TxnID: {txnID} | Ref: {refNum} | Vendor: {vendor}");
                            }
                        }
                    }
                    else if (billResponse.StatusCode != 0)
                    {
                        Console.WriteLine($"QuickBooks error during query: {billResponse.StatusMessage}");
                        return;
                    }

                    // Step 3: Delete each bill
                    int successCount = 0;
                    foreach (string txnID in billTxnIDs)
                    {
                        IMsgSetRequest delRequest = sessionManager.CreateMsgSetRequest("US", 16, 0);
                        ITxnDel txnDel = delRequest.AppendTxnDelRq();
                        txnDel.TxnDelType.SetValue(ENTxnDelType.tdtBill);
                        txnDel.TxnID.SetValue(txnID);

                        IMsgSetResponse delResponse = sessionManager.DoRequests(delRequest);
                        IResponse response = delResponse.ResponseList.GetAt(0);

                        if (response.StatusCode == 0)
                        {
                            successCount++;
                            Console.WriteLine($"Successfully deleted bill with TxnID: {txnID}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to delete bill with TxnID: {txnID}. Error: {response.StatusMessage}");
                        }
                    }

                    Console.WriteLine($"Successfully deleted {successCount} out of {billTxnIDs.Count} bills");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        // Always close the session and connection
                        sessionManager.EndSession();
                        sessionManager.CloseConnection();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error closing QuickBooks session: {ex.Message}");
                    }
                }
            }
        }
    }
