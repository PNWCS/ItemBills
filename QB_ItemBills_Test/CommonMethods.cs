using QB_ItemBills_CLI;
using QBFC16Lib;
using Serilog;

namespace QB_ItemBills_Test
{
    public class CommonMethods
    {
        public static void EnsureLogFileClosed()
        {
            Log.CloseAndFlush();
            Thread.Sleep(1000);
        }

        public static void DeleteOldLogFiles()
        {
            string logDirectory = "logs";
            string logPattern = "qb_sync*.log";

            if (Directory.Exists(logDirectory))
            {
                foreach (var logFile in Directory.GetFiles(logDirectory, logPattern, SearchOption.TopDirectoryOnly))
                {
                    TryDeleteFile(logFile);
                }
            }
        }

        public static void EnsureLogFileExists(string logFile)
        {
            const int MAX_RETRIES = 10;
            int delayMilliseconds = 200;

            for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
            {
                if (File.Exists(logFile)) return;
                Thread.Sleep(delayMilliseconds);
            }

            throw new FileNotFoundException($"Log file '{logFile}' was not created after test execution.");
        }

        public static void TryDeleteFile(string filePath)
        {
            const int maxRetries = 10;
            int delayMilliseconds = 200;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    File.Delete(filePath);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMilliseconds);
                    delayMilliseconds += 200;
                }
            }
        }

        public static string GetLatestLogFile()
        {
            string logDirectory = "logs";
            string logPattern = "qb_sync*.log";
            string[] logFiles = Directory.GetFiles(logDirectory, logPattern, SearchOption.TopDirectoryOnly);

            if (logFiles.Length == 0)
                throw new FileNotFoundException("No log files found after test run.");

            return logFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
        }

        public static void ResetLogger()
        {
            LoggerConfig.ResetLogger();
            LoggerConfig.ConfigureLogging();
        }

        public static List<string> CreateMultipleVendors(QuickBooksSession session, int count)
        {
            var vendorNames = new List<string>();

            for (int i = 0; i < count; i++)
            {
                string baseName = $"TestVendor_{Guid.NewGuid():N}".Substring(0, 8);
                string uniqueName = baseName;
                int attempt = 1;

                while (DoesVendorExist(session, uniqueName))
                {
                    uniqueName = $"{baseName}_{attempt}";
                    attempt++;
                }

                var rq = session.CreateRequestSet();
                var add = rq.AppendVendorAddRq();
                add.Name.SetValue(uniqueName);
                var rs = session.SendRequest(rq).ResponseList.GetAt(0);
                if (rs.StatusCode != 0) throw new Exception(rs.StatusMessage);

                vendorNames.Add(uniqueName);
            }

            return vendorNames;
        }

        public static string AddVendor(QuickBooksSession session, string baseName)
        {
            string uniqueName = baseName;
            int attempt = 1;

            while (DoesVendorExist(session, uniqueName))
            {
                uniqueName = $"{baseName}_{attempt}";
                attempt++;
            }

            var rq = session.CreateRequestSet();
            var add = rq.AppendVendorAddRq();
            add.Name.SetValue(uniqueName);

            var rs = session.SendRequest(rq).ResponseList.GetAt(0);
            if (rs.StatusCode != 0) throw new Exception(rs.StatusMessage);

            return uniqueName;
        }

        public static bool DoesVendorExist(QuickBooksSession session, string name)
        {
            var rq = session.CreateRequestSet();
            var query = rq.AppendVendorQueryRq();
            query.ORVendorListQuery.VendorListFilter.ORNameFilter.NameFilter.MatchCriterion.SetValue(ENMatchCriterion.mcStartsWith);
            query.ORVendorListQuery.VendorListFilter.ORNameFilter.NameFilter.Name.SetValue(name);

            var rs = session.SendRequest(rq).ResponseList.GetAt(0);
            return rs.StatusCode == 0 && rs.Detail != null;
        }

        public static string AddInventoryItem(QuickBooksSession session, string baseName)
        {
            string uniqueName = baseName;
            int attempt = 1;

            while (DoesInventoryItemExist(session, uniqueName))
            {
                uniqueName = $"{baseName}_{attempt}";
                attempt++;
            }

            var rq = session.CreateRequestSet();
            var add = rq.AppendItemInventoryAddRq();
            add.Name.SetValue(uniqueName);
            add.IncomeAccountRef.FullName.SetValue("Sales");
            add.COGSAccountRef.FullName.SetValue("Cost of Goods Sold");
            add.AssetAccountRef.FullName.SetValue("Inventory Asset");

            var rs = session.SendRequest(rq).ResponseList.GetAt(0);
            if (rs.StatusCode != 0) throw new Exception(rs.StatusMessage);

            return uniqueName;
        }

        public static bool DoesInventoryItemExist(QuickBooksSession session, string name)
        {
            var rq = session.CreateRequestSet();
            var query = rq.AppendItemInventoryQueryRq();
            query.ORListQueryWithOwnerIDAndClass.ListWithClassFilter.ORNameFilter.NameFilter.MatchCriterion.SetValue(ENMatchCriterion.mcStartsWith);
            query.ORListQueryWithOwnerIDAndClass.ListWithClassFilter.ORNameFilter.NameFilter.Name.SetValue(name);

            var rs = session.SendRequest(rq).ResponseList.GetAt(0);
            return rs.StatusCode == 0 && rs.Detail != null;
        }

    }
}
