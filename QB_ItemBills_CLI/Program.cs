using System;
using System.Collections.Generic;
using System.IO;
using QB_ItemBills_Lib;

namespace QB_ItemBills_CLI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("QuickBooks Item Bill CLI");
            Console.WriteLine("=========================");
            Console.WriteLine("Select an action:");
            Console.WriteLine("1 - Query and Add Item Bills manually");
            Console.WriteLine("2 - Compare Excel and QuickBooks (both directions), and Add Missing Bills");
            Console.Write("Choice (1 or 2): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    ManualBillAddFlow();
                    break;
                case "2":
                    CompareBothDirectionsFlow();
                    break;
                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }

            Console.WriteLine("\nProcess complete. Press any key to exit.");
            Console.ReadKey();
        }

        private static void ManualBillAddFlow()
        {
            const string excelPath = @"C:\Users\NagavarapuS\ItemBills\ItemBills.xlsx";
            if (!File.Exists(excelPath))
            {
                Console.WriteLine("Excel file not found at hardcoded path.");
                return;
            }


            var bills = ItemBillComparator.ReadBillsFromExcel(excelPath);
            Console.WriteLine($"Read {bills.Count} item bills from Excel.");

            Console.Write("Add all these to QuickBooks? (y/n): ");
            var confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm == "y")
            {
                BillAdder.AddBills(bills);
                Console.WriteLine("Bills added to QuickBooks.");
            }
            else
            {
                Console.WriteLine("No bills were added.");
            }
        }

        private static void CompareBothDirectionsFlow()
        {
            const string excelPath = @"C:\Users\NagavarapuS\ItemBills\ItemBills.xlsx";
            if (!File.Exists(excelPath))
            {
                Console.WriteLine("Excel file not found at hardcoded path.");
                return;
            }

            Console.WriteLine("\nQuickBooks Item Bill Comparator");
            Console.WriteLine("===============================");

            var excelBills = ItemBillComparator.ReadBillsFromExcel(excelPath);
            var qbBills = ItemBillReader.QueryAllItemBills();

            Console.WriteLine($"Read {excelBills.Count} item bills from Excel.");
            Console.WriteLine($"Read {qbBills.Count} item bills from QuickBooks.");

            var comparisonResults = ItemBillComparator.CompareBothDirections(excelBills, qbBills);

            Console.WriteLine("Comparison Results:");
            foreach (var result in comparisonResults)
            {
                Console.WriteLine($"Vendor: {result.ExcelBill?.VendorName ?? result.QuickBooksBill?.VendorName}, " +
                                  $"Invoice: {result.ExcelBill?.InvoiceNum ?? result.QuickBooksBill?.InvoiceNum}, " +
                                  $"Status: {result.Status}");
            }

            Console.Write("Do you want to add bills that are missing in QuickBooks? (y/n): ");
            var answer = Console.ReadLine()?.Trim().ToLower();
            if (answer == "y")
            {
                ItemBillComparator.AddMissingBillsToQB(comparisonResults);
            }
            else
            {
                Console.WriteLine("No bills were added.");
            }
        }
    }
}