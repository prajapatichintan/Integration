using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _4_Tell.MivaMerchant
{
    public class SaleItem
    {
        public string OrderNum { get; set; }

        public string Date { get; set; } // Required by 4-Tell

        public string CustomerName { get; set; }

        public string CustomerEmail { get; set; }

        public string Status { get; set; }

        public string Total { get; set; }

        /// <summary>
        /// These fields are required by 4-Tell
        /// </summary>
        public string FourTell_ProductID { get; set; }
        public string FourTell_CustomerID { get; set; }
        public string FourTell_Quantity { get; set; }
    }
}