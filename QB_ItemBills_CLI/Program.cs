//using System;
//using QB_ItemBills_Lib;

//namespace QB_ItemBills_CLI
//{
//    class Program
//    {
//        static void Main()
//        {
//            ItemBillReader.QueryAllItemBills();

//            //foreach (var bill in bills)
//            //{
//            //    Console.WriteLine("-------------------------------------------------");
//            //    Console.WriteLine($"Vendor:         {bill.Vendor}");
//            //    Console.WriteLine($"Date:           {bill.Date}");
//            //    Console.Write("Part's names:   ");
//            //    Console.WriteLine(string.Join(", ", bill.Parts.ConvertAll(p => p.Name)));
//            //    Console.Write("Part's quantity:");
//            //    Console.WriteLine(" " + string.Join(", ", bill.Parts.ConvertAll(p => p.Quantity.ToString())));
//            //    Console.WriteLine($"Ref Number:     {bill.RefNumber}");
//            //    Console.WriteLine("-------------------------------------------------\n");
//            //}

//            //Console.WriteLine("Press any key to exit...");
//            //Console.ReadKey();
//        }
//    }
//}

using System;
using System.Collections.Generic;
using QB_ItemBills_Lib;
using System;
using QB_ItemBills_Lib;

//namespace QB_ItemBills_CLI
//{
//    class Program
//    {
//        static void Main()
//        {
//            Console.WriteLine("WARNING: This will delete ALL bills in QuickBooks!");
//            Console.WriteLine("Make sure you have a backup of your QuickBooks file.");
//            Console.Write("Do you want to continue? (Y/N): ");

//            var key = Console.ReadKey();
//            Console.WriteLine();

//            if (key.Key == ConsoleKey.Y)
//            {
//                Console.WriteLine("\nDeleting all bills in QuickBooks...");
//                BillDeleter.DeleteAllBills();
//            }
//            else
//            {
//                Console.WriteLine("\nOperation cancelled.");
//            }

//            Console.WriteLine("\nPress any key to exit...");
//            Console.ReadKey();
//        }
//    }
//}
namespace QB_ItemBills_CLI
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Querying QuickBooks for Item Bills...");
            Console.WriteLine("====================================");

            try
            {
                // Call the QueryAllItemBills method
                List<ItemBill> bills = ItemBillReader.QueryAllItemBills();

                // Display the results
                if (bills.Count > 0)
                {
                    Console.WriteLine($"Found {bills.Count} bills in QuickBooks:\n");

                    foreach (var bill in bills)
                    {
                        Console.WriteLine("-------------------------------------------------");
                        Console.WriteLine($"Vendor:        {bill.VendorName}");
                        Console.WriteLine($"Date:          {bill.BillDate.ToShortDateString()}");
                        Console.WriteLine($"Invoice #:     {bill.InvoiceNum}");
                        Console.WriteLine($"Company ID:    {bill.QBID}");

                        if (bill.Lines.Count > 0)
                        {
                            Console.Write("Part's names:  ");
                            Console.WriteLine(string.Join(", ", bill.Lines.ConvertAll(p => p.PartName)));

                            Console.Write("Part's qty:    ");
                            Console.WriteLine(string.Join(", ", bill.Lines.ConvertAll(p => p.Quantity.ToString())));

                            Console.Write("Part's price:  ");
                            Console.WriteLine(string.Join(", ", bill.Lines.ConvertAll(p => $"${p.UnitPrice:F2}")));
                        }

                        Console.WriteLine($"Transaction ID: {bill.TxnID}");
                        Console.WriteLine("-------------------------------------------------\n");
                    }
                }
                else
                {
                    Console.WriteLine("No item bills found in QuickBooks.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}