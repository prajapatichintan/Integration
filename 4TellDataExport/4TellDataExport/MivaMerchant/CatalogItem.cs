using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _4_Tell.MivaMerchant
{
    public class CatalogItem
    {
        public string Code { get; set; }

        public string SKU { get; set; }

        public string CanonicalCategoryCode { get; set; } // Same as FourTell_CategoryID?

        public string AlternateDisplayPage { get; set; } // Required by 4-Tell

        public string Name { get; set; }  // Required by 4-Tell

        public string Price { get; set; }  // Required by 4-Tell

        public string Cost { get; set; }

        public string Weight { get; set; }

        public string Description { get; set; }

        public string Taxable { get; set; }

        public string ThumbnailImage { get; set; }

        public string FullSizedImage { get; set; }

        public string Active { get; set; } // Required by 4-Tell

        public string Current_Stock { get; set; } // Required by 4-Tell

        /// <summary>
        /// These fields are required by 4-Tell
        /// </summary>
        public string FourTell_ProductID { get { return Name; } } // unique product identifier, listed at the parent product.

        public string[] FourTell_CategoryIDs
        {
            get
            {
                var retVal = from s in CanonicalCategoryCode.Split(',')
                             select s;

                return retVal.ToArray();
            }
        }

        public string FourTell_BrandID { get; set; } // manufacturers or brands for each product
        public decimal FourTell_SalePrice { get; set; }  // Should only contain a value if the item is currently on sale. Otherwise, it should contain an empty string.
        public string FourTell_Link { get; set; } // link to the product page for the product that the recommendation
        public string FourTell_ImageLink { get { return ThumbnailImage; } } // link to the thumbnail image of the product that should displayed with the recommendation.
        public string FourTell_StandardCode { get { return SKU; } } // Required: UPC, ISBN, or any other standard code.
        public string FourTell_ActiveFlag { get { return Active; } } // tracks whether or not an item is active in the store
        public string FourTell_StockLevel { get { return Current_Stock; } } // If the store actively tracks inventory, then this field should show the number of items available for this product.
    }
}