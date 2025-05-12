using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QBFC16Lib;
using QB_ItemBills_Lib; // for QuickBooksSession

namespace QB_ItemBills_Lib
{
    public static class VendorAdder
    {
        public static void AddVendors(List<string> vendorNames)
        {
            using (var session = new QuickBooksSession("Vendor Adder"))
            {
                var requestSet = session.CreateRequestSet();

                foreach (var vendorName in vendorNames)
                {
                    var vendorAdd = requestSet.AppendVendorAddRq();
                    vendorAdd.Name.SetValue(vendorName);
                }

                var responseSet = session.SendRequest(requestSet);
            }
        }
    }
}

