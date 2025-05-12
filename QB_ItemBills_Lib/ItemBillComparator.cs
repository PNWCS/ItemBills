using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using Serilog;

namespace QB_ItemBills_Lib
{
    public enum ItemBillStatus
    {
        MATCHED,
        MISSING_IN_QB,
        MISSING_IN_EXCEL,
        CONFLICT,
        Added,
        FailedToAdd,
        Missing,
        Different,
        Unchanged
    }

    public class ItemBillComparisonResult
    {
        public ItemBill ExcelBill { get; set; } = null!;
        public ItemBill? QuickBooksBill { get; set; }
        public ItemBillStatus Status { get; set; }
    }

    public static class ItemBillComparator
    {
        public static List<ItemBill> ReadBillsFromExcel(string filePath)
        {
            var bills = new List<ItemBill>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var sheet = package.Workbook.Worksheets[0];
                int rowCount = sheet.Dimension.End.Row;

                for (int row = 2; row <= rowCount; row++)
                {
                    var bill = new ItemBill
                    {
                        VendorName = sheet.Cells[row, 1].Text.Trim(),
                        InvoiceNum = sheet.Cells[row, 2].Text.Trim(),
                        BillDate = DateTime.TryParse(sheet.Cells[row, 3].Text, out var date) ? date : DateTime.MinValue,
                        Memo = sheet.Cells[row, 4].Text.Trim(),
                        Lines = new List<ItemBillLine>
                        {
                            new ItemBillLine
                            {
                                PartName = sheet.Cells[row, 5].Text.Trim(),
                                Quantity = int.TryParse(sheet.Cells[row, 6].Text, out var qty) ? qty : 0,
                                UnitPrice = double.TryParse(sheet.Cells[row, 7].Text, out var price) ? price : 0.0
                            }
                        }
                    };
                    bills.Add(bill);
                }
            }

            return bills;
        }

        public static List<ItemBill> CompareItemBills(List<ItemBill> companyBills)
        {
            Log.Information("ItemBillComparator Initialized");

            var qbBills = ItemBillReader.QueryAllItemBills();
            var results = new List<ItemBill>();

            foreach (var bill in companyBills)
            {
                try
                {
                    var match = qbBills.FirstOrDefault(q =>
                        q.InvoiceNum.Equals(bill.InvoiceNum, StringComparison.OrdinalIgnoreCase));

                    if (match == null)
                    {
                        BillAdder.AddBills(new List<ItemBill> { bill });

                        if (!string.IsNullOrEmpty(bill.TxnID))
                        {
                            bill.Status = ItemBillStatus.Added;
                        }
                        else
                        {
                            bill.Status = ItemBillStatus.FailedToAdd;
                        }
                    }
                    else if (BillsAreEqual(match, bill))
                    {
                        bill.TxnID = match.TxnID ?? string.Empty;
                        bill.Status = ItemBillStatus.Unchanged;
                    }
                    else
                    {
                        bill.TxnID = match.TxnID ?? string.Empty;
                        bill.Status = ItemBillStatus.Different;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("QuickBooks error: {Message}", ex.Message);
                    bill.Status = ItemBillStatus.FailedToAdd;
                }

                Log.Information("ItemBill {InvoiceNum} is {Status}.", bill.InvoiceNum, bill.Status);
                results.Add(bill);
            }

            foreach (var qbBill in qbBills)
            {
                if (!companyBills.Any(b =>
                    b.InvoiceNum.Equals(qbBill.InvoiceNum, StringComparison.OrdinalIgnoreCase)))
                {
                    qbBill.Status = ItemBillStatus.Missing;
                    Log.Information("ItemBill {InvoiceNum} is {Status}.", qbBill.InvoiceNum, qbBill.Status);
                    results.Add(qbBill);
                }
            }

            Log.Information("ItemBillComparator Completed");
            return results;
        }

        private static bool BillsAreEqual(ItemBill a, ItemBill b)
        {
            if (!a.VendorName.Equals(b.VendorName, StringComparison.OrdinalIgnoreCase)) return false;
            if (!a.InvoiceNum.Equals(b.InvoiceNum, StringComparison.OrdinalIgnoreCase)) return false;
            if (a.BillDate.Date != b.BillDate.Date) return false;
            if (a.Memo != b.Memo) return false;
            if (a.Lines.Count != b.Lines.Count) return false;

            for (int i = 0; i < a.Lines.Count; i++)
            {
                var lineA = a.Lines[i];
                var lineB = b.Lines[i];
                if (!lineA.PartName.Equals(lineB.PartName, StringComparison.OrdinalIgnoreCase)) return false;
                if (lineA.Quantity != lineB.Quantity) return false;
                if (Math.Abs(lineA.UnitPrice - lineB.UnitPrice) > 0.01) return false;
            }

            return true;
        }

        public static List<ItemBillComparisonResult> CompareBothDirections(List<ItemBill> excelBills, List<ItemBill> qbBills)
        {
            var results = new List<ItemBillComparisonResult>();

            foreach (var excelBill in excelBills)
            {
                var match = qbBills.FirstOrDefault(b =>
                    b.VendorName.Equals(excelBill.VendorName, StringComparison.OrdinalIgnoreCase)
                    && b.InvoiceNum.Equals(excelBill.InvoiceNum, StringComparison.OrdinalIgnoreCase));

                ItemBillStatus status;
                if (match == null)
                {
                    status = ItemBillStatus.MISSING_IN_QB;
                }
                else if (BillsAreEqual(match, excelBill))
                {
                    status = ItemBillStatus.MATCHED;
                }
                else
                {
                    status = ItemBillStatus.CONFLICT;
                }

                results.Add(new ItemBillComparisonResult
                {
                    ExcelBill = excelBill,
                    QuickBooksBill = match,
                    Status = status
                });
            }

            foreach (var qbBill in qbBills)
            {
                var match = excelBills.FirstOrDefault(b =>
                    b.VendorName.Equals(qbBill.VendorName, StringComparison.OrdinalIgnoreCase)
                    && b.InvoiceNum.Equals(qbBill.InvoiceNum, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    results.Add(new ItemBillComparisonResult
                    {
                        ExcelBill = null!,
                        QuickBooksBill = qbBill,
                        Status = ItemBillStatus.MISSING_IN_EXCEL
                    });
                }
            }

            return results;
        }

        public static void AddMissingBillsToQB(List<ItemBillComparisonResult> results)
        {
            var billsToAdd = results
                .Where(r => r.Status == ItemBillStatus.MISSING_IN_QB && r.ExcelBill != null)
                .Select(r => r.ExcelBill!)
                .ToList();

            if (billsToAdd.Count == 0)
            {
                Log.Information("No bills were added due to errors or missing vendor/items.");
                return;
            }

            try
            {
                BillAdder.AddBills(billsToAdd);
                Log.Information("Missing bills added to QuickBooks successfully.");
            }
            catch (Exception ex)
            {
                Log.Error("Error while adding bills: {Message}", ex.Message);
            }

        }
    }
}
