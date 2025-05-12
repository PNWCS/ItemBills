using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using QBFC16Lib;
using QB_ItemBills_Lib;          // ItemBill, ItemBillLine, ItemBillStatus
//using QB_ItemBills_Lib;          // ItemBillComparator
using static QB_ItemBills_Test.CommonMethods;

namespace QB_ItemBills_Test
{
    [Collection("Sequential Tests")]
    public class ItemBillComparatorTests
    {
        [Fact]
        public void CompareItemBills_InMemoryScenario_Verify_All_Statuses()
        {
            
            // ─── 1️⃣  Log prep ──────────────────────────────────────────────────────
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            const int COMPANY_ID_START = 10_000;

            // ─── 2️⃣  Create QB fixtures (vendor + 2 items) ─────────────────────────
            var createdVendorIds = new List<string>();
            var createdItemIds = new List<string>();

            using (var qb = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                string vendorListId = AddVendor(qb, $"TestVendor_{Guid.NewGuid():N}".Substring(0, 8));
                createdVendorIds.Add(vendorListId);

                for (int i = 0; i < 2; i++)
                {
                    string itemListId = AddInventoryItem(qb, $"TestPart_{Guid.NewGuid():N}".Substring(0, 8));
                    createdItemIds.Add(itemListId);
                }
            }

            // ─── 3️⃣  Build initial company item-bills (5 total) ────────────────────
            var initialBills = new List<ItemBill>();

            for (int i = 0; i < 4; i++)           // VALID ⇒ Added
                initialBills.Add(BuildValidBill(i, COMPANY_ID_START,
                                                createdVendorIds[0], createdItemIds));

            var invalidBill = BuildInvalidBill(COMPANY_ID_START + 4);   // INVALID ⇒ FailedToAdd
            initialBills.Add(invalidBill);

            List<ItemBill> firstPass = new();
            List<ItemBill> secondPass = new();

            try
            {
                // ─── 4️⃣  First compare – expect Added & FailedToAdd ───────────────
                firstPass = ItemBillComparator.CompareItemBills(initialBills);

                foreach (var bill in firstPass.Where(b => b.InvoiceNum != invalidBill.InvoiceNum))
                {
                    Assert.Equal(ItemBillStatus.Added, bill.Status);
                    Assert.False(string.IsNullOrEmpty(bill.TxnID));
                }

                var failed = firstPass.Single(b => b.InvoiceNum == invalidBill.InvoiceNum);
                Assert.Equal(ItemBillStatus.FailedToAdd, failed.Status);
                Assert.True(string.IsNullOrEmpty(failed.TxnID));

                // ─── 5️⃣  Mutate list for second compare ──────────────────────────
                var updatedBills = new List<ItemBill>(initialBills);

                //   • Missing
                var billToRemove = updatedBills[0];
                updatedBills.Remove(billToRemove);

                //   • Different
                var billToModify = updatedBills[0];
                billToModify.Memo += "_MODIFIED";

                // ─── 6️⃣  Second compare – expect Missing / Different / Unchanged ─
                secondPass = ItemBillComparator.CompareItemBills(updatedBills);
                var secondDict = secondPass.ToDictionary(b => b.InvoiceNum);

                Assert.Equal(ItemBillStatus.Missing, secondDict[billToRemove.InvoiceNum].Status);
                Assert.Equal(ItemBillStatus.Different, secondDict[billToModify.InvoiceNum].Status);

                foreach (var inv in updatedBills
                         .Where(b => b.InvoiceNum != billToModify.InvoiceNum &&
                                     b.InvoiceNum != invalidBill.InvoiceNum)
                         .Select(b => b.InvoiceNum))
                {
                    Assert.Equal(ItemBillStatus.Unchanged, secondDict[inv].Status);
                }

                Assert.Equal(ItemBillStatus.FailedToAdd, secondDict[invalidBill.InvoiceNum].Status);
            }
            finally
            {
                // ─── 7️⃣  QB clean-up (bills → items → vendor) ─────────────────────
                using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);

                foreach (var bill in firstPass.Where(b => !string.IsNullOrEmpty(b.TxnID)))
                    DeleteBill(qb, bill.TxnID);

                foreach (var itemId in createdItemIds)
                    DeleteInventoryItem(qb, itemId);

                foreach (var vendorId in createdVendorIds)
                    DeleteVendor(qb, vendorId);
            }

            // ─── 8️⃣  Log assertions ──────────────────────────────────────────────
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);
            string logs = File.ReadAllText(logFile);

            Assert.Contains("ItemBillComparator Initialized", logs);
            Assert.Contains("ItemBillComparator Completed", logs);

            foreach (var bill in firstPass.Concat(secondPass))
                Assert.Contains($"ItemBill {bill.InvoiceNum} is {bill.Status}.", logs);
        }

        // ──────────────────────────── Helpers ─────────────────────────────────────
        private ItemBill BuildValidBill(int idx, int companyStart, string vendorName, List<string> partNames) =>
            new()
            {
                VendorName = vendorName,
                BillDate = DateTime.Today,
                InvoiceNum = $"INV_{Guid.NewGuid():N}".Substring(0, 10),
                Memo = (companyStart + idx).ToString(),
                Lines = new()
                {
                    new ItemBillLine { PartName = partNames[0], Quantity = 2, UnitPrice = 15.5 },
                    new ItemBillLine { PartName = partNames[1], Quantity = 1, UnitPrice =  9.9 }
                }
            };

        private ItemBill BuildInvalidBill(int companyId) =>
            new()
            {
                VendorName = $"BadVendor_{Guid.NewGuid():N}".Substring(0, 6),
                BillDate = DateTime.Today,
                InvoiceNum = $"INV_BAD_{Guid.NewGuid():N}".Substring(0, 10),
                Memo = companyId.ToString(),
                Lines = new() { new ItemBillLine { PartName = "BadItem", Quantity = 1, UnitPrice = 1.0 } }
            };

        // —— QuickBooks CRUD helpers (Vendor / Item / Bill) ————————————————
        private string AddVendor(QuickBooksSession s, string name)
        {
            var rq = s.CreateRequestSet();
            var add = rq.AppendVendorAddRq();
            add.Name.SetValue(name);
            var rs = s.SendRequest(rq).ResponseList.GetAt(0);
            if (rs.StatusCode != 0) throw new Exception(rs.StatusMessage);
            return ((IVendorRet)rs.Detail).ListID.GetValue();
        }

        private string AddInventoryItem(QuickBooksSession s, string name)
        {
            var rq = s.CreateRequestSet();
            var add = rq.AppendItemInventoryAddRq();
            add.Name.SetValue(name);
            add.IncomeAccountRef.FullName.SetValue("Sales");
            add.COGSAccountRef.FullName.SetValue("Cost of Goods Sold");
            add.AssetAccountRef.FullName.SetValue("Inventory Asset");
            var rs = s.SendRequest(rq).ResponseList.GetAt(0);
            if (rs.StatusCode != 0) throw new Exception(rs.StatusMessage);
            return ((IItemInventoryRet)rs.Detail).ListID.GetValue();
        }

        private void DeleteBill(QuickBooksSession s, string txnId)
        {
            var rq = s.CreateRequestSet();
            var del = rq.AppendTxnDelRq();
            del.TxnDelType.SetValue(ENTxnDelType.tdtBill);
            del.TxnID.SetValue(txnId);
            s.SendRequest(rq);
        }

        private void DeleteVendor(QuickBooksSession s, string listId) =>
            DeleteListObj(s, ENListDelType.ldtVendor, listId);

        private void DeleteInventoryItem(QuickBooksSession s, string listId) =>
            DeleteListObj(s, ENListDelType.ldtItemInventory, listId);

        private void DeleteListObj(QuickBooksSession s, ENListDelType type, string listId)
        {
            var rq = s.CreateRequestSet();
            var del = rq.AppendListDelRq();
            del.ListDelType.SetValue(type);
            del.ListID.SetValue(listId);
            s.SendRequest(rq);
        }
    }
}
//second version
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using QBFC16Lib;
using QB_ItemBills_Lib;          // ItemBill, ItemBillLine, ItemBillStatus
using QB_ItemBills_Lib;          // ItemBillComparator
using static QB_ItemBills_Test.CommonMethods;

namespace QB_ItemBills_Test
{
    [Collection("Sequential Tests")]
    public class ItemBillComparatorTests
    {
        [Fact]
        public void CompareItemBills_InMemoryScenario_Verify_All_Statuses()
        {
            // ─── 1️⃣  Log prep ──────────────────────────────────────────────────────
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            const int COMPANY_ID_START = 10_000;

            // ─── 2️⃣  Create QB fixtures (vendor + 2 items) ─────────────────────────
            var createdVendorIds = new List<string>();
            var createdItemIds   = new List<string>();

            using (var qb = new QuickBooksSession(AppConfig.QB_APP_NAME))
            {
                string vendorListId = AddVendor(qb, $"TestVendor_{Guid.NewGuid():N}".Substring(0, 8));
                createdVendorIds.Add(vendorListId);

                for (int i = 0; i < 2; i++)
                {
                    string itemListId = AddInventoryItem(qb, $"TestPart_{Guid.NewGuid():N}".Substring(0, 8));
                    createdItemIds.Add(itemListId);
                }
            }

            // ─── 3️⃣  Build initial company item-bills (5 total) ────────────────────
            var initialBills = new List<ItemBill>();

            for (int i = 0; i < 4; i++)           // VALID ⇒ Added
                initialBills.Add(BuildValidBill(i, COMPANY_ID_START, 
                                                createdVendorIds[0], createdItemIds));

            var invalidBill = BuildInvalidBill(COMPANY_ID_START + 4);   // INVALID ⇒ FailedToAdd
            initialBills.Add(invalidBill);

            List<ItemBill> firstPass  = new();
            List<ItemBill> secondPass = new();

            try
            {
                // ─── 4️⃣  First compare – expect Added & FailedToAdd ───────────────
                firstPass = ItemBillComparator.CompareItemBills(initialBills);

                foreach (var bill in firstPass.Where(b => b.InvoiceNum != invalidBill.InvoiceNum))
                {
                    Assert.Equal(ItemBillStatus.Added, bill.Status);
                    Assert.False(string.IsNullOrEmpty(bill.TxnID));
                }

                var failed = firstPass.Single(b => b.InvoiceNum == invalidBill.InvoiceNum);
                Assert.Equal(ItemBillStatus.FailedToAdd, failed.Status);
                Assert.True(string.IsNullOrEmpty(failed.TxnID));

                // ─── 5️⃣  Mutate list for second compare ──────────────────────────
                var updatedBills = new List<ItemBill>(initialBills);

                //   • Missing
                var billToRemove = updatedBills[0];     
                updatedBills.Remove(billToRemove);

                //   • Different
                var billToModify = updatedBills[0];
                billToModify.Memo += "_MODIFIED";

                // ─── 6️⃣  Second compare – expect Missing / Different / Unchanged ─
                secondPass = ItemBillComparator.CompareItemBills(updatedBills);
                var secondDict = secondPass.ToDictionary(b => b.InvoiceNum);

                Assert.Equal(ItemBillStatus.Missing,   secondDict[billToRemove.InvoiceNum].Status);
                Assert.Equal(ItemBillStatus.Different, secondDict[billToModify.InvoiceNum].Status);

                foreach (var inv in updatedBills
                         .Where(b => b.InvoiceNum != billToModify.InvoiceNum &&
                                     b.InvoiceNum != invalidBill.InvoiceNum)
                         .Select(b => b.InvoiceNum))
                {
                    Assert.Equal(ItemBillStatus.Unchanged, secondDict[inv].Status);
                }

                Assert.Equal(ItemBillStatus.FailedToAdd, secondDict[invalidBill.InvoiceNum].Status);
            }
            finally
            {
                // ─── 7️⃣  QB clean-up (bills → items → vendor) ─────────────────────
                using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);

                foreach (var bill in firstPass.Where(b => !string.IsNullOrEmpty(b.TxnID)))
                    DeleteBill(qb, bill.TxnID);

                foreach (var itemId in createdItemIds)
                    DeleteInventoryItem(qb, itemId);

                foreach (var vendorId in createdVendorIds)
                    DeleteVendor(qb, vendorId);
            }

            // ─── 8️⃣  Log assertions ──────────────────────────────────────────────
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);
            string logs = File.ReadAllText(logFile);

            Assert.Contains("ItemBillComparator Initialized", logs);
            Assert.Contains("ItemBillComparator Completed",   logs);

            foreach (var bill in firstPass.Concat(secondPass))
                Assert.Contains($"ItemBill {bill.InvoiceNum} is {bill.Status}.", logs);
        }

        // ──────────────────────────── Helpers ─────────────────────────────────────
        private ItemBill BuildValidBill(int idx, int companyStart, string vendorName, List<string> partNames) =>
            new()
            {
                VendorName = vendorName,
                BillDate   = DateTime.Today,
                InvoiceNum = $"INV_{Guid.NewGuid():N}".Substring(0, 10),
                Memo       = (companyStart + idx).ToString(),
                Lines = new()
                {
                    new ItemBillLine { PartName = partNames[0], Quantity = 2, UnitPrice = 15.5 },
                    new ItemBillLine { PartName = partNames[1], Quantity = 1, UnitPrice =  9.9 }
                }
            };

        private ItemBill BuildInvalidBill(int companyId) =>
            new()
            {
                VendorName = $"BadVendor_{Guid.NewGuid():N}".Substring(0, 6),
                BillDate   = DateTime.Today,
                InvoiceNum = $"INV_BAD_{Guid.NewGuid():N}".Substring(0, 10),
                Memo       = companyId.ToString(),
                Lines      = new() { new ItemBillLine { PartName = "BadItem", Quantity = 1, UnitPrice = 1.0 } }
            };

        // —— QuickBooks CRUD helpers (Vendor / Item / Bill) ————————————————
        private string AddVendor(QuickBooksSession s, string name)
        {
            var rq  = s.CreateRequestSet();
            var add = rq.AppendVendorAddRq();
            add.Name.SetValue(name);
            var rs  = s.SendRequest(rq).ResponseList.GetAt(0);
            if (rs.StatusCode != 0) throw new Exception(rs.StatusMessage);
            return ((IVendorRet)rs.Detail).ListID.GetValue();
        }

        private string AddInventoryItem(QuickBooksSession s, string name)
        {
            var rq  = s.CreateRequestSet();
            var add = rq.AppendItemInventoryAddRq();
            add.Name.SetValue(name);
            add.IncomeAccountRef.FullName.SetValue("Sales");
            add.COGSAccountRef.FullName.SetValue("Cost of Goods Sold");
            add.AssetAccountRef.FullName.SetValue("Inventory Asset");
            var rs  = s.SendRequest(rq).ResponseList.GetAt(0);
            if (rs.StatusCode != 0) throw new Exception(rs.StatusMessage);
            return ((IItemInventoryRet)rs.Detail).ListID.GetValue();
        }

        private void DeleteBill(QuickBooksSession s, string txnId)
        {
            var rq = s.CreateRequestSet();
            var del = rq.AppendTxnDelRq();
            del.TxnDelType.SetValue(ENTxnDelType.tdtBill);
            del.TxnID.SetValue(txnId);
            s.SendRequest(rq);
        }

        private void DeleteVendor(QuickBooksSession s, string listId) =>
            DeleteListObj(s, ENListDelType.ldtVendor, listId);

        private void DeleteInventoryItem(QuickBooksSession s, string listId) =>
            DeleteListObj(s, ENListDelType.ldtItemInventory, listId);

        private void DeleteListObj(QuickBooksSession s, ENListDelType type, string listId)
        {
            var rq  = s.CreateRequestSet();
            var del = rq.AppendListDelRq();
            del.ListDelType.SetValue(type);
            del.ListID.SetValue(listId);
            s.SendRequest(rq);
        }
    }
}
