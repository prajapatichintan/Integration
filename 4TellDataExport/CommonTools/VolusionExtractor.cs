﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Diagnostics; //EventlogEntryType

namespace _4_Tell
{
	using IO; //data path
	using Utilities; //BoostLog

  public class VolusionExtractor : CartExtractor
  {
    private readonly string m_photoBaseUrl = string.Empty;
    private readonly string m_webServicesBaseUrl = string.Empty;
		private readonly string m_dataPath = string.Empty;

		private const string m_orderDetailsFileName = "OrderHistory.xml";
		private const string m_catalogExportFileName = "Catalog.xml";

		private string m_lastCatalogQuery = string.Empty;
		private bool m_apiAvailable = false;
		private IEnumerable<XElement> m_catalogXml = null;
		private IEnumerable<XElement> m_categoryXml = null;

		public XmlDocument GetXMLFromURL(string APIURL)
		{
			XmlDocument APIDoc = null;
			try
			{
				XmlTextReader reader = new XmlTextReader(APIURL);
				APIDoc = new XmlDocument();
				APIDoc.Load(reader);
				reader.Close();
			}
			catch (Exception e)
			{
				throw new Exception("Error reading API URL", e);
			}
			return APIDoc;
		}

		//Volusion Catalog Notes:
		//	Volusion has tiered pricing starting at Steel
		//	Only sites that are at least in the Voluion Gold level are allowed API access. 
		//	For lower tiered sites, we have created a saved export in the volusion admin 
		//	that must be run manually to create a Catalog.xml file.
		//  This file is placed in the client upload folder on our server.
		private void GetCatalogXml(String query = null)
    {
			if ((query == null) || (query.Length < 1))
				query = "EDI_Name=Generic\\all_products"; //generic catalog query

			if ((m_catalogXml != null) && (query == m_lastCatalogQuery))
				return; //no need to get it again

			//try to load from the site API
			m_catalogXml = null;
			m_lastCatalogQuery = query;
			XDocument catalogDoc = QueryVolusionApi(query);
			if ((catalogDoc != null) && (catalogDoc.Root != null))
			{
				m_apiAvailable = true;
				m_catalogXml = catalogDoc.Root.Descendants("Product");
				m_categoryXml = catalogDoc.Root.Descendants("Category");
			}

			if (m_catalogXml == null) //no luck with the API so look for a saved export (NOTE: currently no category names in saved export)
			{
				m_apiAvailable = false;
				catalogDoc = XDocument.Load(m_dataPath + m_catalogExportFileName);
				if (catalogDoc.Root != null)
					m_catalogXml = catalogDoc.Root.Descendants("SAVED_EXPORT");
			}
			if (m_catalogXml == null)
				throw new Exception("Unable to retrieve catalog");    
			return;
    }

		private XDocument QueryVolusionApi(string query)
		{
			XDocument result = null;

			// We need all three parameters to connect to the Volusion API
			if ((m_webServicesBaseUrl.Length > 0)
				&& (m_apiUserName.Length > 0)
				&& (m_apiKey.Length > 0))
			{
				string serviceURI = string.Format("{0}?Login={1}&EncryptedPassword={2}&{3}",
																					m_webServicesBaseUrl, m_apiUserName, m_apiKey, query);
				using (var xtr = new XmlTextReader(serviceURI))
				{
					result = XDocument.Load(xtr);
				}
			}
			return result;
		}

		private IEnumerable<XElement> m_salesXml = null;
		public IEnumerable<XElement> SalesHistory
		{
			get
			{
				if (m_salesXml == null)
				{
					try
					{
						var salesDoc = XDocument.Load(m_dataPath + m_orderDetailsFileName);
						if (salesDoc.Root != null)
							m_salesXml = salesDoc.Root.Descendants("SAVED_EXPORT");
					}
					catch (Exception ex)
					{
						string errMsg = "Error retrieving sales history: " + ex.Message;
						if (ex.InnerException != null)
							errMsg += "\n" + ex.InnerException.Message;
					}
				}
				return m_salesXml;
			}
			set { m_salesXml = value; }
		}

    public VolusionExtractor(string alias, XElement settings) : base(alias, settings)
    {
		  base.ParseSettings(settings);

			m_dataPath = DataPath.Instance.ClientDataPath(ref m_alias) + "upload\\";
			m_webServicesBaseUrl = m_storeLongUrl + "/net/WebService.aspx"; //Client.GetValue(settings, "webServicesBaseUrl");

			//in case of a CDN, client settings will hold the CDN base path 
			m_photoBaseUrl = Client.GetValue(settings, "photoBaseUrl");
			if (m_photoBaseUrl.Length < 1) m_photoBaseUrl = "//" + m_storeShortUrl;
			m_photoBaseUrl += "/v/vspfiles/photos/"; 
    }

    private string BuildRequestUrl(string service)
    {
        return string.Format("{0}?Login={1}&EncryptedPassword={2}&EDI_Name={3}", m_webServicesBaseUrl, m_apiUserName, m_apiKey, service);
    }

    public override void LogSalesOrder(string orderId)
    {
        throw new NotImplementedException();
    }

    protected override string GetCatalog()
    {
			string result = "\n" + CatalogFilename + ": ";
			ProgressText += result + "Extracting from web service...";
			var stopWatch = new StopWatch(true);
			var products = new List<ProductRecord>();

			//Note: the following attempts at getting custom results did not return category ids
			//			only the generic request was able to get the ids
			//			downside is that the generic request does not get all fields, so we may need to make a second request per item to get missing fields
			//string query = "EDI_Name=Generic\\Products&SELECT_Columns=p.HideProduct,p.IsChildOfProductCode,p.ProductCode,p.ProductID,p.ProductName,p.ProductPopularity,p.StockStatus,p.ProductUrl,p.PhotoUrl,pe.Hide_When_OutOfStock,pe.ProductCategory,pe.ProductManufacturer,pe.PhotoURL_Small,pe.ProductPrice,pe.SalePrice,pe.UPC_code,pe.Google_Gender";
			//string query = "EDI_Name=Generic\\Products&SELECT_Columns=*";
			string query = null; 
			try
			{
				GetCatalogXml(query);
			}
			catch (Exception ex)
			{
				result += ex.Message;
				if (ex.InnerException != null)
					result += Environment.NewLine + ex.InnerException.Message;
				return result;
			}
			int numItems = (m_catalogXml == null)? 0 : m_catalogXml.Count();
			if (numItems == 0)
			{
				result += "There are no products in the catalog.";
				return result;
			}

			ProgressText += string.Format("completed ({0}){1}Parsing Product Catalog ({2} rows)...",
																		stopWatch.Lap(), Environment.NewLine, numItems);
			string tempDisplay = ProgressText;

			//any extra fields not in the standard export will need to be requested from the API for each product
			string extraFields = "p.Photos_Cloned_From";  //special case needed to find images for some products
			List<string> extraFieldList = GetRuleFields();
			if (extraFieldList.Count > 0)
			{
				//use the first product to find standard attributes and remove them from the list
				extraFieldList = extraFieldList.Where(x => (m_catalogXml.First().Descendants().Where(
														y => y.Name.LocalName.Equals(x, StringComparison.CurrentCultureIgnoreCase)
														).DefaultIfEmpty(null).Single() == null)).ToList<string>();
				extraFields += ",pe." + extraFieldList.Aggregate((w, j) => string.Format("{0},pe.{1}", w, j));
				//Note:	Currently assuming that all fields are in pe table --true so far
				//A more robust solution would be to compile local lists of fields in each Volusion table and check against the lists
			}

			int rows = 0;
			foreach (var product in m_catalogXml)
      {
				var p = new ProductRecord
                {
                    ProductId = Client.GetValue(product, "ProductCode"),
										Name =  Client.GetValue(product, "ProductName"),
										Att2Id = string.Empty,
										Price =  Client.GetValue(product, "ProductPrice"),
										SalePrice =  Client.GetValue(product, "SalePrice"),
										Filter = string.Empty
                };

				if (m_secondAttEnabled)
					p.Att2Id = Client.GetValue(product, "ProductManufacturer");
        p.Link = string.Format("{0}/ProductDetails.asp?ProductCode={1}", m_storeLongUrl, p.ProductId);
				p.Rating =  Client.GetValue(product, "ProductPopularity");
				p.StandardCode = Client.GetValue(product, "UPC_code");
				if (p.StandardCode.Length < 1)
					p.StandardCode = p.ProductId;

				var categories = product.Descendants().Where(x => x.Name.LocalName.Equals("Categories", StringComparison.CurrentCultureIgnoreCase)).SingleOrDefault();
        if(categories != null)
            p.Att1Id = categories.Descendants("Category").Aggregate(string.Empty, (current, cat) => current + string.Format("{0},", cat.Descendants().Where(x => x.Name.LocalName.Equals("CategoryID")).Select(x => x.Value).DefaultIfEmpty("").Single())).TrimEnd(',');
        else
					p.Att1Id = Client.GetValue(product, "CategoryIDs");

				//If extra fields not in the current export are required, call api to get the missing fields 
				if (extraFields.Length > 0)
				{
					try
					{
						query = string.Format("API_Name=Generic\\Products&SELECT_Columns={0}&WHERE_Column=p.ProductCode&WHERE_Value={1}", 
																		extraFields, p.ProductId);
						XDocument extraXml = QueryVolusionApi(query);
						if ((extraXml != null) && (extraXml.Root != null))
						{
							IEnumerable<XElement> ix = extraXml.Root.Elements("Products");
							if (ix != null)
								//remove any repetitive fields such as the product code
								product.Add(ix.Descendants().Where(x => Client.GetValue(product, x.Name.ToString()).Length < 1));
						}
					}
					catch
					{ }
				}
				//get the image link
				p.ImageLink = string.Empty;
				string imageID = Client.GetValue(product,"Photos_Cloned_From");
#if DEBUG
				if (imageID.Length > 0) // cloned image found!
					p.ImageLink = string.Empty; //just something to put a breakpoint on
#endif
				if (imageID.Length < 1) //not using cloned image
				{
					//try to pull image from dtabase fields (these appear to never have the image)
					imageID = p.ProductId;
					p.ImageLink = Client.GetValue(product, "PhotoURL_Small");
					if (p.ImageLink.Length < 1)
						p.ImageLink = Client.GetValue(product, "PhotoURL");
				}
				if (p.ImageLink.Length < 1) //build the image link from the imageID
				{
					//replace illegal characters
					string encodedID = imageID.Replace("/", "-fslash-").Replace(" ", "%20");
					p.ImageLink = string.Format("{0}{1}-1.jpg", m_photoBaseUrl, encodedID);
				}

				//check category conditions, exclusions, and filters
				ApplyRules(ref p, product);

				products.Add(p);
				ProgressText = string.Format("{0}{1} rows completed ({2})", tempDisplay, ++rows, stopWatch.Lap());
			}
			ProgressText = string.Format("{0}Completed ({1}){2}Uploading to server...",
																		tempDisplay, stopWatch.Lap(), Environment.NewLine);

			result += m_boostService.WriteTable(m_alias, CatalogFilename, products);
			ProgressText = string.Format("{0}({1})", result, stopWatch.Stop());
			return result; 
		}

    protected override string GetSalesMonth(DateTime exportDate, string filename)
    {
			string result = "\n" + filename + ": ";
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			if ((SalesHistory == null) || (SalesHistory.Count() == 0))		
				return result + "No orders in sales history";
			
			var orders = new List<SalesRecord>();
			DateTime first = new DateTime(exportDate.Year, exportDate.Month, 1);
			DateTime last = first.AddMonths(1);
				//new DateTime(exportDate.Year, exportDate.Month, DateTime.DaysInMonth(exportDate.Year, exportDate.Month));
			foreach (var order in SalesHistory)
      {
				string sDate = Client.GetValue(order, "OrderDate");
				var orderDate = DateTime.Parse(sDate);
        if (DateTime.Compare(orderDate, first) < 0 || 
            DateTime.Compare(orderDate, last) >= 0)
          continue;

				var o = new SalesRecord
                    {
                        ProductId = Client.GetValue(order, "ProductCode"),
                        CustomerId = Client.GetValue(order, "CustomerId"),
                        Quantity = Client.GetValue(order, "Quantity"),
                        Date = orderDate.ToShortDateString()
                    };

        orders.Add(o);
      }

      ProgressText = tempDisplay + string.Format("{0}Uploading to server...", Environment.NewLine);

			result += m_boostService.WriteTable(m_alias, filename, orders);
			if (orders.Count == 0)
				result += "(no data)"; //still need an empty placeholder file uploaded, but nice to know 
			return result; //final result should be in the format of "Filename: success/failure status"
    }

    protected override string GetExclusions()
    {
			//NOTE: GetCatalog shoudld be called before GetExclusions in case there are any categories excluded

			if (!m_exclusionsEnabled)
				return "Exclusions disabled"; //nothing to do
			m_exclusionsEnabled = false;
			if ((m_exclusions == null) && (m_catConditions == null))
				return "No exclusions"; //nothing to do

			string result = "\n" + ExclusionFilename + ": ";
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			List<string> catExclusions = null;
			if (m_catConditions != null)
				catExclusions = m_catConditions.GetExcludedItems();

			//compile exclusion list
			var exclusions = new List<ExclusionRecord>();
			if ((catExclusions != null) && (catExclusions.Count > 0))
			{
				foreach (string id in catExclusions.Distinct()) //distinct to avoid duplicates
					exclusions.Add(new ExclusionRecord { Id = id });
				m_exclusionsEnabled = true;
			}

			if (!m_exclusionsEnabled)
				return result + "(no data)"; //no data matches rules

			//upload to 4-Tell
			ProgressText = tempDisplay + result + "Uploading...";
			result += m_boostService.WriteTable(m_alias, ExclusionFilename, exclusions);

			ProgressText = tempDisplay + result;
			return result;
		}

    protected override string GetReplacements()
    {
			string result = "\n" + ReplacementFilename + ": ";
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			var replacements = new List<ReplacementRecord>(); 

			//first check for any child products in the catalog
			var childItems = m_catalogXml.Where(x => Client.GetValue(x, "IsChildOfProductCode").Length > 0).Select(x =>
				new ReplacementRecord
				{
					OldId = Client.GetValue(x, "ProductCode"),
					NewId = Client.GetValue(x, "IsChildOfProductCode")
				}).ToList();

			if ((childItems != null) && (childItems.Count() > 0))
				replacements = replacements.Union(childItems).ToList(); //union to avoid duplicates in final list
			//else
			//{	
			//  //Assumption was that all child products have a product code that is in the form of {parentCode}-{childCode}
			//	//Would be nice but it looks like this is not always true
			//  var childCodes = Catalog.Where(x => Client.GetValue(x, "ProductCode").Contains('-')).Select(x =>
			//    Client.GetValue(x, "ProductCode"));
			//  foreach (string code in childCodes)
			//  //foreach (var item in Catalog)
			//  {
			//    //string code = Client.GetValue(item, "ProductCode");
			//    int index = code.IndexOf('-');
			//    //if (index < 0) continue; //not a child

			//    string parent = code.Substring(0, index);
			//    replacements.Add(new replacementRecord
			//                  {
			//                    OldId = code,
			//                    NewId = parent
			//                  });
			//  }
			//}

			//now check for any replacement rules
			if (m_replacements != null)
			{
				if (m_replacements[0].Type != ReplacementCondition.repType.catalog) //individual item replacement
				{
					foreach (ReplacementCondition rc in m_replacements)
					{
						if (rc.Type != ReplacementCondition.repType.item)
						{
							m_log.WriteEntry("Invalid replacement condition: " + rc.ToString(), EventLogEntryType.Warning, m_alias);
							continue;  //ignore any invalid entries 
						}
						var r = new ReplacementRecord();
						r.OldId = rc.OldName;
						r.NewId = rc.NewName;
						replacements.Add(r);
					}
				}
				else //full catalog replacement -- need to retrieve from 3dCart
				{
					//Query the catalog for the data
					string oldfield = m_replacements[0].OldName;
					string newfield = m_replacements[0].NewName;

					if ((m_catalogXml != null) && (m_catalogXml.Count() > 0))
					{
						foreach (XElement item in m_catalogXml)
						{
							var r = new ReplacementRecord();
							r.OldId = Client.GetValue(item, oldfield);
							r.NewId = Client.GetValue(item, newfield);
							replacements.Add(r);
						}
					}
				}
			}
			if (replacements.Count < 1)
			{
				m_replacementsEnabled = false;
				return result + "(no data)"; //no data matches rules
			}
			m_replacementsEnabled = true;

			//upload to 4-Tell
			ProgressText = tempDisplay + result + "Uploading...";
			result += m_boostService.WriteTable(m_alias, ReplacementFilename, replacements);
			ProgressText = tempDisplay + result;

			return result;
		}

    protected override string GetAtt1Names()
    {
			if ((m_categoryXml == null) || (m_categoryXml.Count() == 0))
				return Environment.NewLine + "No category names exported";

			string result = Environment.NewLine + Att1Filename + ": ";
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			ProgressText = tempDisplay + string.Format("Parsing Categories ({0})...", m_categoryXml.Count());
			var categories = new List<AttributeRecord>();
			foreach (var category in m_categoryXml)
			{
				var id = category.Descendants().Where(x => x.Name.LocalName.Equals("CategoryID")).Select(x => x.Value.Replace(",", "")).DefaultIfEmpty("").Single();
				if (categories.Select(x => x.Id).Contains(id) || string.IsNullOrEmpty(id))
					continue;

				categories.Add(new AttributeRecord
											{
												Id = id,
												Name = category.Descendants().Where(x => x.Name.LocalName.Equals("CategoryName")).Select(x => x.Value.Replace(",", "")).DefaultIfEmpty("").Single()
											});
			}

			ProgressText = tempDisplay + "Uploading to server...";
			result += m_boostService.WriteTable(m_alias, Att1Filename, categories);
			return result; //final result should be in the format of "Filename: success/failure status"
		}

    protected override string GetAtt2Names()
    {
			return Environment.NewLine + "No Manufacturer names exported (names match ids in Volusion)";
		}

  }
}