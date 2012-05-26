using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;
// used by MivaMerchantJsonBridge
using System.Collections;
//using System.Collections.ObjectModel;
//using System.Web.UI.WebControls;
using System.Runtime.Serialization.Json;

namespace _4_Tell
{
	using Utilities;
	using IO;
    using System.Net;

    public class MivaMerchantExtractor : CartExtractor
    {
        private const string m_orderDetailsFileName = "Orders.dat";
        private const string m_catalogExportFileName = "Catalog.xml";
        private const string m_categoriesFileName = "Categories.csv";
        private readonly string m_dataPath = string.Empty;

        private readonly string m_catalogFilePath = string.Empty;
        private readonly string m_photoBaseUrl = string.Empty;
        private readonly string m_categoriesFilePath = string.Empty;
        private readonly string m_ordersFilePath = string.Empty;

        private IEnumerable<SalesRecord> m_orderHistory = null;
        private IEnumerable<SalesRecord> OrderHistory
        {
            get
            {
                if (m_orderHistory == null)
                {
                    m_orderHistory = LoadTabDelimitedFile(m_ordersFilePath).Select(order => new SalesRecord
                    {
                        //OrderId = order["ORDER_ID"],
                        ProductId = order["PROD_CODE"],
                        CustomerId = GetHash(order["BILL_EMAIL"]),
                        Quantity = order["PROD_QUANT"],
                        Date = order["ORDER_DATE"]
                    }).ToList();
                }
                return m_orderHistory;
            }
            set { m_orderHistory = value; }
        }
        //dictionary version for csv file had problems
        //private IEnumerable<Dictionary<string, string>> m_catalogDictionary = null;
        private IEnumerable<Dictionary<string, string>> m_categoryDictionary = null;
        //public IEnumerable<Dictionary<string, string>> Catalog
        //{
        //  get
        //  {
        //    //TODO: get live catalog from site

        //    //If no live catalog avaialable, look for a catalog.xml file instead
        //    //WARNING: Delete catalog.xml once live catalog is available so connection issue doesn't cause a revert to old data
        //    if (m_catalogDictionary == null)
        //      m_catalogDictionary = LoadTabDelimitedFile(m_productsFilePath);
        //    if (m_categoryDictionary == null)
        //      m_categoryDictionary = LoadTabDelimitedFile(m_categoriesFilePath);

        //    return m_catalogDictionary;
        //  }
        //  set { m_catalogDictionary = value; }
        //}
        private XElement m_catalogXml = null;
        //private IEnumerable<XElement> m_categoryXml = null;
        public XElement Catalog
        {
            get
            {
                //TODO: get live catalog from site
                

                //If no live catalog avaialable, look for a catalog.xml file instead
                //WARNING: Delete catalog.xml once live catalog is available so connection issue doesn't cause a revert to old data
                if (m_catalogXml == null)
                    m_catalogXml = XElement.Load(m_catalogFilePath);
                //if (m_categoryXml == null)
                //  m_categoryXml = LoadXmlFile(m_categoriesFilePath);
                if (m_categoryDictionary == null)
                    m_categoryDictionary = LoadTabDelimitedFile(m_categoriesFilePath);

                return m_catalogXml;
            }
            set { m_catalogXml = value; }
        }


        public MivaMerchantExtractor(string alias, XElement settings)
            : base(alias, settings)
        {
            base.ParseSettings(settings);

            m_dataPath = DataPath.Instance.ClientDataPath(ref m_alias) + "upload\\";
            m_ordersFilePath = m_dataPath + m_orderDetailsFileName;
            m_catalogFilePath = m_dataPath + m_catalogExportFileName;
            m_categoriesFilePath = m_dataPath + m_categoriesFileName;
            m_photoBaseUrl = Client.GetValue(settings, "photoBaseUrl");
            if (m_photoBaseUrl.Length < 1)
                m_photoBaseUrl = m_storeLongUrl + "/mm5/";
        }

        private static IEnumerable<Dictionary<string, string>> LoadTabDelimitedFile(string path)
        {
            var contents = new List<Dictionary<string, string>>();
            var keys = new List<string>();

            using (var sr = new StreamReader(path))
            {
                string line;
                if (!string.IsNullOrEmpty(line = sr.ReadLine()))
                    keys.AddRange(line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));

                int keyCount = keys.Count();
                while (!string.IsNullOrEmpty(line = sr.ReadLine()))
                {
                    var content = new Dictionary<string, string>();
                    var values = line.Replace("<br>", "").Split(new[] { '\t' });

                    int columnCount = values.Length;
                    if (columnCount > keyCount)
                    { //Extra tabs in the description column cause problems 
                        int extraColumns = values.Length - keyCount;
                        for (int i = 8; i < keyCount; i++) //descrptions are in column 7
                            values[i] = values[i + extraColumns];
                        columnCount = keyCount;
                    }

                    for (var i = 0; i < columnCount; i++)
                    {
                        content.Add(keys[i], values[i].Trim());
                    }

                    contents.Add(content);
                }
            }

            return contents;
        }

        private static IEnumerable<XElement> LoadXmlFile(string path)
        {
            XDocument catalogDoc = new XDocument();
            IEnumerable<XElement> resultXml = null;

            catalogDoc = XDocument.Load(path);
            resultXml = catalogDoc.Ancestors();

            return resultXml;
        }

        protected override string GetCatalog()
        {
            string result = "\n" + CatalogFilename + ": ";
            ProgressText += result + "Extracting...";
            var stopWatch = new StopWatch(true);
            var products = new List<ProductRecord>();

            IEnumerable<XElement> items = Catalog.Elements("Product_Add");
            int numItems = (items == null) ? 0 : items.Count();
            if (numItems == 0)
            {
                result += " (no data)";
                ProgressText = result;
                return result;
            }
            IEnumerable<XElement> customFields = Catalog.Elements("Product_CustomField");
            IEnumerable<XElement> catAssignments = Catalog.Elements("CategoryProduct_Assign");

            result += string.Format("({0} items) ", numItems);
            ProgressText += result + string.Format("Extracting...completed ({0}){1}Parsing Product Catalog...",
                                                                            stopWatch.Lap(), Environment.NewLine);
            string tempDisplay = ProgressText;

            int rows = 0, errors = 0;
            foreach (var product in items)
            {
                try
                {
                    var p = new ProductRecord
                    {
                        ProductId = Client.GetValue(product, "Code"),
                        Name = Client.GetValue(product, "Name"),
                        Att1Id = string.Empty,
                        Att2Id = string.Empty,
                        Price = Client.GetValue(product, "Price"),
                        SalePrice = string.Empty,
                        Filter = string.Empty,
                        ImageLink = Client.GetValue(product, "ThumbnailImage"),
                    };
                    if (p.ImageLink.Length < 1)
                        p.ImageLink = Client.GetValue(product, "FullSizeImage");

                    p.Link = string.Format("{0}/product/{1}.html", m_storeLongUrl, p.ProductId);
                    p.Rating = string.Empty;

                    //parse custom fields
                    IEnumerable<XElement> cfSubset = customFields.Where(x => Client.GetAttribute(x, "product").Equals(p.ProductId));
                    p.StandardCode = cfSubset.Where(x => Client.GetAttribute(x, "field").Equals("UPC")).Select(x => x.Value).DefaultIfEmpty(string.Empty).Single();
                    if (p.StandardCode.Length < 1)
                        p.StandardCode = p.ProductId;
                    p.Att2Id = cfSubset.Where(x => Client.GetAttribute(x, "field").Equals("brand")).Select(x => x.Value).DefaultIfEmpty(string.Empty).Single();

                    //get category list
                    var categories = catAssignments.Where(x => Client.GetAttribute(x, "product_code").Equals(p.ProductId)).Select(x => Client.GetAttribute(x, "category_code"));
                    if (categories != null)
                        //{
                        //  bool first = true;
                        //  foreach (string id in categories)
                        //  {
                        //    if (first) first = false;
                        //    else p.Attr1 += ",";
                        //    p.Attr1 += id;
                        //  }
                        //}
                        //  p.Attr1 = categories.Aggregate(string.Empty, (current, cat) => current + string.Format("{0},", cat.Descendants().Where(x => x.Name.LocalName.Equals("CategoryID")).Select(x => x.Value).DefaultIfEmpty("").Single())).TrimEnd(',');
                        p.Att1Id = categories.Aggregate((w, j) => string.Format("{0},{1}", w, j));

                    //check category conditions, exclusions and filters
                    ApplyRules(ref p, product);
                    products.Add(p);

                    //This is now handled in ApplyRules
                    ////Need to take care of exclusions here since we have access to all the product attributes
                    ////if (Client.GetValue(product, "Active").Equals("No"))
                    ////  m_catConditions.AddExcludedItem(p.ProductId);
                    //if (m_exclusions != null)
                    //{
                    //  foreach (var c in m_exclusions)
                    //  {
                    //    string value = Client.GetValue(product, c.FieldName);
                    //    if (value.Length < 1) //not found so check custom fields
                    //      value = cfSubset.Where(x => Client.GetAttribute(x, "field").Equals(c.FieldName)).Select(x => x.Value).DefaultIfEmpty(string.Empty).Single();
                    //    if (value.Length > 0)
                    //      if (c.Compare(value))
                    //      {
                    //        m_catConditions.AddExcludedItem(p.ProductId);
                    //        break;
                    //      }
                    //  }
                    //}

                }
                catch { errors++; }
                ProgressText = string.Format("{0}{1} products, {2} errors ({3})", tempDisplay, ++rows, errors, stopWatch.Lap());
            }
            ProgressText = string.Format("{0}Completed ({1}){2}Uploading to server...",
                                                                        tempDisplay, stopWatch.Lap(), Environment.NewLine);

            result += m_boostService.WriteTable(m_alias, CatalogFilename, products);
            stopWatch.Stop();
            return result;
        }

        public override void LogSalesOrder(string orderID)
        {
            throw new NotImplementedException();
        }

        protected override string GetSalesMonth(DateTime exportDate, string filename)
        {
            string result = Environment.NewLine + filename + ": ";
            ProgressText += result;
            string tempDisplay = ProgressText;
            ProgressText += "Exporting...";
            var stopWatch = new StopWatch(true);

            if ((OrderHistory == null) || (OrderHistory.Count() == 0))
                return string.Format("No Sales for: {0}", exportDate.ToShortDateString());

            List<SalesRecord> orders = new List<SalesRecord>();
            foreach (var order in OrderHistory)
            {
                //check date
                var orderDate = DateTime.Parse(order.Date);
                if (DateTime.Compare(orderDate, new DateTime(exportDate.Year, exportDate.Month, 1)) < 0 ||
                        DateTime.Compare(orderDate, new DateTime(exportDate.Year, exportDate.Month, DateTime.DaysInMonth(exportDate.Year, exportDate.Month))) > 0)
                    continue;

                orders.Add(new SalesRecord
                                    {
                                        ProductId = order.ProductId,
                                        CustomerId = order.CustomerId,
                                        Quantity = order.Quantity,
                                        Date = order.Date
                                    });
            }

            var countDisplay = string.Format("({0} orders) ", orders.Count);
            ProgressText = tempDisplay + countDisplay + "Uploading...";
            result += countDisplay + m_boostService.WriteTable(m_alias, filename, orders);
            stopWatch.Stop();
            return result;
        }

        protected override string GetExclusions()
        {
            if (!m_exclusionsEnabled)
                return Environment.NewLine + "Exclusions disabled";

            m_exclusionsEnabled = false;
            if (m_exclusions == null && m_catConditions == null)
                return Environment.NewLine + "Exclusions disabled";

            var result = string.Format("{0}{1}: ", Environment.NewLine, ExclusionFilename);
            ProgressText += result;
            string tempDisplay = ProgressText;
            ProgressText += "Exporting...";

            var exclusions = new List<ExclusionRecord>();
            if (m_catConditions != null)
                exclusions = m_catConditions.GetExcludedItems().Distinct().Select(x =>
                                                new ExclusionRecord { Id = x }).ToList();

            //TODO: enable other exclusion rules for Miva
            //if (m_exclusions != null)
            //{
            //  foreach(var c in m_exclusions)
            //  {
            //    var subList = c.Evaluate(items, "Code").Select(x =>
            //																new ExclusionRecord { Id = x }).ToList();
            //    if(subList.Count > 0)
            //      exclusions = exclusions.Union(subList).ToList();
            //  }
            //}
            if (exclusions.Count < 1)
                return result + "(no data)";

            m_exclusionsEnabled = true;
            var countDisplay = string.Format("({0} items) ", exclusions.Count);
            ProgressText = tempDisplay + countDisplay + "Uploading...";
            result += countDisplay + m_boostService.WriteTable(m_alias, ExclusionFilename, exclusions);
            return result;
        }

        protected override string GetReplacements()
        {
            if (!m_replacementsEnabled)
                return Environment.NewLine + "Replacements disabled";
            m_replacementsEnabled = false;
            if (m_replacements == null || m_replacements.Count == 0)
                return Environment.NewLine + "No replacements";

            var result = string.Format("{0}{1}: ", Environment.NewLine, ReplacementFilename);
            ProgressText += result;
            string tempDisplay = ProgressText;
            ProgressText += "Exporting...";

            var replacements = new List<ReplacementRecord>();
            if (!m_replacements[0].Type.Equals(ReplacementCondition.repType.catalog))
            {
                foreach (var rc in m_replacements)
                {
                    if (!rc.Type.Equals(ReplacementCondition.repType.item))
                    {
                        m_log.WriteEntry(string.Format("Invalid replacement condition: {0}", rc), EventLogEntryType.Warning, m_alias);
                        continue;
                    }

                    replacements.Add(new ReplacementRecord { OldId = rc.OldName, NewId = rc.NewName });
                }
            }
            else
            {
                var products = LoadTabDelimitedFile(m_catalogFilePath).Select(x => new ProductRecord { ProductId = x["PRODUCT_CODE"], Name = x["PRODUCT_NAME"] });
                if (products.Count() == 0)
                    return result + " ( no data)";

                foreach (var p in products)
                {
                    var rr = new ReplacementRecord
                        {
                            OldId = products.Where(x => x.Name.Equals(m_replacements[0].OldName)).Select(y => y.ProductId).DefaultIfEmpty("").Single(),
                            NewId = products.Where(x => x.Name.Equals(m_replacements[0].NewName)).Select(y => y.ProductId).DefaultIfEmpty("").Single()
                        };
                    replacements.Add(rr);
                }
            }
            if (replacements.Count < 1)
                return result + "(no data)";

            m_replacementsEnabled = true;
            var countDisplay = string.Format("({0} items) ", replacements.Count);
            ProgressText = tempDisplay + countDisplay + "Uploading...";
            result += countDisplay + m_boostService.WriteTable(m_alias, ReplacementFilename, replacements);
            return result;
        }

        protected override string GetAtt1Names()
        {
            if (m_categoryDictionary == null)
                return Environment.NewLine + "No Category data.";

            var stopWatch = new StopWatch(true);
            string filename = "Attribute1Names.txt";
            string result = string.Format("{0}{1}: ", Environment.NewLine, filename);
            ProgressText += result;
            string tempDisplay = ProgressText;
            ProgressText += "Exporting...";

            var categories = m_categoryDictionary.Select(category => new AttributeRecord
                                                                                                        {
                                                                                                            Id = category["CATEGORY_CODE"],
                                                                                                            Name = category["CATEGORY_NAME"]
                                                                                                        }).ToList();

            var countDisplay = string.Format("({0} categories) ", categories.Count);
            ProgressText = tempDisplay + countDisplay + "Uploading...";
            result += countDisplay + m_boostService.WriteTable(m_alias, Att1Filename, categories);
            return result;
        }

        protected override string GetAtt2Names()
        {
            return Environment.NewLine + "No Manufacturer names exported (names match ids in Miva)";
        }
    }//END class MivaMerhcatExtractor


   
    /// <summary>
    /// Class to provide JSON implomentation
    ///  
    /// * A MivaMerchat test store to experiment with: https://4-tell.coolcommerce.net/mm5/admin.mvc
    /// * You'll have to allow the self-signed certificate error through your browser.
    /// * 
    /// * Username: Administrator
    /// * Password: s2zML469w$
    /// * 
    /// * JSON address: https://4-tell.coolcommerce.net/mm5/merchant.mvc?Screen=4Tell&ClientAlias=MivaMerc&ServiceKey=W0C4CDD14A%217DAKEaEc561J7&DataGroup=?
    /// * If you specify 'Sales' you must specify DateRange. All the orders have a creation date of 8/7/2008 so when exporting the Sales keep that in mind for the DateRange
    /// * 
    ///            
    /// </summary>
    internal class MivaMerchantJsonBridge
    {
        string storeUrl = @"https://4-tell.coolcommerce.net/mm5/merchant.mvc?Screen=4Tell&ClientAlias=MivaMerc&ServiceKey=W0C4CDD14A!7DAKEaEc561J7&DataGroup=?";

        enum DataGroup { Categories = "CBED", Catalog = "PBED", Sales = "MORD" }; 

        internal void GetCatalogData()
        {
            try
            {
                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(CatalogItem));
                using (FileStream fs = File.OpenRead(@"c:\jsonText.txt"))
                {
                    //string json = @"{""Name"" : ""My Product""}";
                    //MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json));
                    CatalogItem catatlogItem = ser.ReadObject(fs) as CatalogItem;
                    
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.Read();
            }

        }

    }//END class MivaMerchantJsonBridge

    [Serializable]
    class CatalogItem
    {
        public string Code {get; set; }

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
        public string FourTell_ProductID { get { return Name;} } // unique product identifier, listed at the parent product.
        
        public string[] FourTell_CategoryIDs {
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
        public string FourTell_ImageLink {get { return ThumbnailImage; } } // link to the thumbnail image of the product that should displayed with the recommendation.
        public string FourTell_StandardCode { get{ return SKU; }  } // Required: UPC, ISBN, or any other standard code.
        public string FourTell_ActiveFlag { get { return Active; } } // tracks whether or not an item is active in the store
        public string FourTell_StockLevel { get { return Current_Stock; } } // If the store actively tracks inventory, then this field should show the number of items available for this product.
    }

    [Serializable]
    class Category
    {
        public string Code { get; set; }

        public string Name { get; set; }

        public string Active { get; set; }

        public string CategoryHeader { get; set; }

        public string CategoryFooter { get; set; }

        public string METAKeywords { get; set; }

        public string METADescription { get; set; }
    }

    [Serializable]
    class Sales
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