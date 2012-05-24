//#define MAGENTO_API_AVAILABLE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;			//XmlWriter
using System.Xml.Linq; //XElement
using System.Xml.Serialization;
using System.Web;
using System.Text;			//StringBuilder
using System.Net;				//HttpWebRequest/Response
using System.IO;				//StreamReader
using System.Threading; //Thread
using System.Diagnostics; //EventlogEntryType

namespace _4_Tell
{
	//using GoStore_wsTea; //DEBUG
	//using GoStore_MageGo;
	//using GoStore;
	//using GoStore_TheLuvo;
	using GoStore_condomania;
	//using GoStore_Buticul;
	using Utilities;
	using Utilities.DynamicProxyLibrary;

	/// <summary>
	/// CartExtractor implementation for Magento shopping carts
	/// </summary>
	public class MagentoExtractorStatic: CartExtractor
	{
		private readonly string m_thumbnailFormat = string.Empty;

		public MagentoExtractorStatic(string alias, XElement settings)
			: base(alias, settings)
		{
			//Add any Magento specific construction here
//#if DEBUG
//      XElement x = null;
//      ApiTest(ref x);
//#endif
			base.ParseSettings(settings);

			m_thumbnailFormat = "//s4e787b99edc22.img.gostorego.com/802754/cdn/media/s4/e7/87/b9/9e/dc/22/catalog/product/cache/1/small_image/306x392/9df78eab33525d08d6e5fb8d27136e95/i/m/img_{0}_detail_1.jpg";
		}

		public override void LogSalesOrder(string orderID)
		{
			StopWatch stopWatch = new StopWatch(true);
			string result = "AutoSalesLog: ";
			ProgressText = result + "Exporting single sale...";
			XElement xmlResult = null;

			try
			{
				//access Magento store API
			}
			catch (Exception ex)
			{
				string errMsg = ex.Message;
				if (xmlResult != null)
				{
					errMsg += "\nMagento Result: ";
					if (xmlResult.Name.LocalName.Equals("Error"))
					{
						if (xmlResult.Element("Id") != null)
						{
							errMsg += "Id = " + xmlResult.Element("Id");
							if (xmlResult.Element("Description") != null)
								errMsg += " Description = " + xmlResult.Element("Description");
						}
						else if (xmlResult.Value != null)
							errMsg += xmlResult.Value;
					}
					else if (xmlResult.Value != null)
						errMsg += xmlResult.Value;
				}
				if (ex.InnerException != null)
					errMsg += "\nInner Exception: " + ex.InnerException.Message;

				result = ProgressText + "\n" + errMsg;
			}
			finally
			{
				stopWatch.Stop();
				result += "\nTotal Time: " + stopWatch.TotalTime;
				ProgressText = result;
				//m_log.WriteEntry(result, System.Diagnostics.EventLogEntryType.Information, m_alias);
			}
		}

		protected override string GetCatalog()
		{
			string result = "\n" + CatalogFilename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Exporting catalog...";
			StopWatch exportWatch = new StopWatch(true);

			Mage_Api_Model_Server_V2_HandlerPortTypeClient Mclient = new Mage_Api_Model_Server_V2_HandlerPortTypeClient();
			string MsessionId = "";

			//---------------------CATALOG EXPORT----------------------------
			//errors caught by calling function

			//login
			MsessionId = Mclient.login(m_apiUserName, m_apiKey);

			//Set product attributes to fetch
			catalogProductRequestAttributes prodAttributes = new catalogProductRequestAttributes();
			string[] attributes = { "sku", "url_key", "price", "special_price", "special_from_date", "special_to_date", "parent_item_id" };
			prodAttributes.attributes = attributes;

			//loop through all products to build the list
			var products = new List<ProductRecord>();
			catalogProductEntity[] plist;
			Mclient.catalogProductList(out plist, MsessionId, null, "");
			int maxCid = 0;
			foreach (catalogProductEntity product in plist)
			{
				var p = new ProductRecord
				{
					ProductId = product.product_id,
					Name = product.name,
				};
				p.Att1Id = "";
				bool first = true;
				foreach (string cid in product.category_ids)
				{
					if (first) first = false;
					else p.Att1Id += ",";
					p.Att1Id += cid;
					int id = Convert.ToInt32(cid);
					if (id > maxCid) maxCid = id;
				}

				p.Att2Id = "";
				catalogProductReturnEntity pinfo = Mclient.catalogProductInfo(MsessionId, p.ProductId, "", prodAttributes, "id");
				p.Price = pinfo.price; ;
				if ((pinfo.special_from_date != null) && (pinfo.special_to_date != null))
				{
					DateTime saleStart = DateTime.Parse(pinfo.special_from_date);
					DateTime saleEnd = DateTime.Parse(pinfo.special_to_date);
					DateTime now = DateTime.Now;
					if (now >= saleStart && now <= saleEnd)
						p.Price = pinfo.special_price;
				}
				p.Filter = "";
				p.Link = pinfo.url_key;
				p.ImageLink = "";
				catalogProductImageEntity[] pimageinfo = null; 
				try 
				{
					pimageinfo = Mclient.catalogProductAttributeMediaList(MsessionId, pinfo.sku, "default", null); 
				} 
				catch { }
				if ((pimageinfo != null) && (pimageinfo.Length > 0))
					p.ImageLink = pimageinfo[0].url;
				else
				{
					p.ImageLink = string.Format(m_thumbnailFormat, pinfo.sku);
				}
				p.StandardCode = pinfo.sku;

			}

			ProgressText = tempDisplay + string.Format("Completed ({0}){1}Uploading to server...",
																									exportWatch.Lap(), Environment.NewLine);
			var sb = new StringBuilder(CommonHeader + ProductRecord.Header());
			foreach (var product in products)
			{
				sb.Append(string.Format("{0}\t", product.ProductId));
				sb.Append(string.Format("{0}\t", product.Name));
				sb.Append(string.Format("{0}\t", product.Att1Id));
				sb.Append(string.Format("{0}\t", product.Att2Id));
				sb.Append(string.Format("{0}\t", product.Price));
				sb.Append(string.Format("{0}\t", product.SalePrice));
				sb.Append(string.Format("{0}\t", product.Rating));
				sb.Append(string.Format("{0}\t", product.Filter));
				sb.Append(string.Format("{0}\t", product.Link));
				sb.Append(string.Format("{0}\t", product.ImageLink));
				sb.Append(string.Format("{0}\r\n", product.StandardCode));
			}
			result += m_boostService.WriteTable(m_alias, CatalogFilename, sb);

			//get cat info
			result += "\n" + Att1Filename + ": ";
			ProgressText = tempDisplay + result + "Exporting category names...";
			catalogCategoryInfo cinfo;
			StringBuilder csb = new StringBuilder(CommonHeader + AttributeRecord.Header());
			for (int cid = 1; cid <= maxCid; cid++)
			{
				try
				{
					cinfo = Mclient.catalogCategoryInfo(MsessionId, cid, "", null);
					csb.Append(cid.ToString() + "\t" + cinfo.name + "\r\n");
				}
				catch
				{
					csb.Append(cid.ToString() + "\t" + cid.ToString() + "\r\n");
				}
			}
			result += m_boostService.WriteTable(m_alias, Att1Filename, csb);
			ProgressText = tempDisplay + result;

			return result;
		}

		protected string ApiTest()
		{
			string result = "\n" + CatalogFilename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Rows to export...";
			StopWatch exportWatch = new StopWatch(true);


#if MAGENTO_API_AVAILABLE			
			#region Static Trevor
			if (static_proxy && m_alias.Equals("Trevor"))
			{
			  MagentoService Mclient = new MagentoService();
			  string MsessionId = "";

			  //---------------------CATALOG EXPORT----------------------------
			  try
			  {
			    MsessionId = Mclient.login(m_apiUserName, m_apiKey);
			    catalogProductEntity[] plist = Mclient.catalogProductList(MsessionId, null, "");
			    if (plist.Length < 1)
			      throw new Exception("No products available");

			    //TODO: create catalog file header
			    string type = "";
			    foreach (catalogProductEntity p in plist)
			    {
			      string pid = p.product_id;
			      if (p.type.Equals("simple")) //only export combined items or else simple items with no parents
			      {
			        bool isChild = false;
			        catalogProductLinkEntity[] plinks = Mclient.catalogProductLinkList(MsessionId, "grouped", pid, "id");
			        foreach (catalogProductLinkEntity pl in plinks)
			          if (pl.type.Equals("configurable"))
			          {
			            isChild = true;
			            break;
			          }
			        if (isChild) continue;
			      }
			      else
			        type += p.type + " ";

			      string pname = p.name;
			      string patt1 = "";
			      bool first = true;
			      foreach (string cid in p.category_ids)
			      {
			        if (first) first = false;
			        else patt1 += ",";
			        patt1 += cid;
			      }

			      catalogProductReturnEntity pinfo = Mclient.catalogProductInfo(MsessionId, pid, "", null, "id");
			      catalogProductReturnEntity pPriceInfo = Mclient.catalogProductGetSpecialPrice(MsessionId, pid, "", "id");
			      string patt2 = "";
			      string pprice = pPriceInfo.price; ;
			      if ((pPriceInfo.special_from_date != null) && (pinfo.special_to_date != null))
			      {
			        DateTime saleStart = DateTime.Parse(pPriceInfo.special_from_date);
			        DateTime saleEnd = DateTime.Parse(pPriceInfo.special_to_date);
			        DateTime now = DateTime.Now;
			        if (now >= saleStart && now <= saleEnd)
			          pprice = pPriceInfo.special_price;
			      }
			      string pfilter = "";
			      string plink = pinfo.url_key;
			      string pimage = "";
			      string psku = pinfo.sku;
			      catalogProductImageEntity pimageinfo = null;
			      try
			      {
			        pimageinfo = Mclient.catalogProductAttributeMediaInfo(MsessionId, pid, "", "", "id");
			      }
			      catch { }
			      if (pimageinfo != null)
			      {
			        pimage = pimageinfo.url;
			      }
			    }
			  }
			  catch { }

			  //---------------------SALES EXPORT----------------------------
			  try
			  {
			    //salesOrderEntity[] sorders = Mclient.salesOrderList(MsessionId, null);
			    salesOrderEntity[] sorders = Mclient.salesOrderList(MsessionId, null);
			    if (sorders.Length > 0)
			    {
			      //TODO: create header line for sales export
			      foreach (salesOrderEntity s in sorders)
			      {
			        string customerid = s.customer_id;
			        if (s.customer_is_guest.Equals("1"))
			        {
			          customerid = s.customer_email;
			          if (customerid == null || customerid.Length < 1)
			            customerid = s.increment_id;
			        }
			        string date = s.created_at;
			        salesOrderEntity sinfo = Mclient.salesOrderInfo(MsessionId, s.increment_id);
			        foreach (salesOrderItemEntity item in sinfo.items)
			        {
			          string productid = item.product_id;
			          string quantity = item.qty_ordered;
			          int len = quantity.IndexOf(".");
			          if (len > 0)
			            quantity = quantity.Substring(0, len); //remove fractional part

			          //TODO: add line to sales data here
			        }
			      }
			      //TODO: upload sales data
			    }
			  }
			  catch { }
			}
			#endregion
#endif

			#region Static GoStore
			  Mage_Api_Model_Server_V2_HandlerPortTypeClient Mclient = new Mage_Api_Model_Server_V2_HandlerPortTypeClient();
			  string MsessionId = "";

			  //---------------------CATALOG EXPORT----------------------------
			  try
			  {
			    //login
					//MsessionId = Mclient.login(m_apiUserName, m_apiKey);
					MsessionId = Mclient.login("4Tell", "4tellsoftware"); //condomania

					//Get API calls available
					apiEntity[] resources = Mclient.resources(MsessionId);
					//resultObj = proxy.CallMethod("resources", sessionID);
					//Type t = resultObj.GetType();
					//XmlSerializer xs = new XmlSerializer(t);
					//XElement resources = xs.SerializeAsXElement(resultObj);
					//TODO: check each CallMethod to make sure it is in this list...
					
					//Set product attributes to fetch
			    catalogProductRequestAttributes prodAttributes = new catalogProductRequestAttributes();
					string[] attributes = { "sku", "url_key", "price", "special_price", "special_from_date", "special_to_date", "parent_item_id" };
			    prodAttributes.attributes = attributes;

					//filters prodFilters = new filters();
					//associativeEntity[] filterList = new associativeEntity[1];
					//filterList[0].key = "";
					//filterList[0].value = "";

			    //loop through all products
					StringBuilder data = new StringBuilder(CommonHeader + ProductRecord.Header());
			    catalogProductEntity[] plist;
			    Mclient.catalogProductList(out plist, MsessionId, null, "");
			    string type = "";
			    int maxCid = 0;
			    foreach (catalogProductEntity p in plist)
			    {
			      string pid = p.product_id;

						if (p.type.Equals("simple")) //only export combined items or else simple items with no parents
						{
							//bool isChild = false;
							//catalogProductLinkEntity[] plinks = Mclient.catalogProductLinkList(MsessionId, "grouped", pid, "id");
							//foreach (catalogProductLinkEntity pl in plinks)
							//  if (pl.type.Equals("configurable"))
							//  {
							//    isChild = true;
							//    break;
							//  }
							//if (isChild) continue;
						}
						else
							type += p.type + " ";

			      string pname = p.name;
			      string patt1 = "";
			      bool first = true;
			      foreach (string cid in p.category_ids)
			      {
			        if (first) first = false;
			        else patt1 += ",";
			        patt1 += cid;
			        int id = Convert.ToInt32(cid);
			        if (id > maxCid) maxCid = id;
			      }

			      string patt2 = "";
			      catalogProductReturnEntity pinfo = Mclient.catalogProductInfo(MsessionId, pid, "", prodAttributes, "id");
			      string pprice = pinfo.price; ;
			      if ((pinfo.special_from_date != null) && (pinfo.special_to_date != null))
			      {
			        DateTime saleStart = DateTime.Parse(pinfo.special_from_date);
			        DateTime saleEnd = DateTime.Parse(pinfo.special_to_date);
			        DateTime now = DateTime.Now;
			        if (now >= saleStart && now <= saleEnd)
			          pprice = pinfo.special_price;
			      }
			      string pfilter = "";
			      string plink = pinfo.url_key;
			      string pimage = "";
			      string psku = pinfo.sku;
			      catalogProductImageEntity pimageinfo = null;
						try
						{
							pimageinfo = Mclient.catalogProductAttributeMediaInfo(MsessionId, pid, "", "", "id");
						}
						catch
						{
							try
							{
								pimageinfo = Mclient.catalogProductAttributeMediaInfo(MsessionId, psku, "", "", "sku");
							}
							catch
							{
							}
						}
						if (pimageinfo != null)
						{
							pimage = pimageinfo.url;
						}
			      data.Append(pid + "\t" + pname + "\t" + patt1 + "\t" + patt2 + "\t" + pprice + "\t" + pfilter + "\t" + plink + "\t" + pimage + "\t" + psku + "\r\n");
			    }
					result += m_boostService.WriteTable(m_alias, CatalogFilename, data);

			    //get cat info
			    result += "\n" + Att1Filename + ": ";
			    ProgressText = tempDisplay + result + "Exporting...";
			    catalogCategoryInfo cinfo;
					StringBuilder csb = new StringBuilder(CommonHeader + AttributeRecord.Header());
			    for (int cid = 1; cid <= maxCid; cid++)
			    {
			      try
			      {
			        cinfo = Mclient.catalogCategoryInfo(MsessionId, cid, "", null);
			        csb.Append(cid.ToString() + "\t" + cinfo.name + "\r\n");
			      }
			      catch
			      {
			        csb.Append(cid.ToString() + "\t" + cid.ToString() + "\r\n");
			      }
			    }
					result += m_boostService.WriteTable(m_alias, Att1Filename, csb);
			    ProgressText = tempDisplay + result;
			  }
			  catch { }
				//try
				//{
				//  //catalogCategoryTree ctree = Mclient.catalogCategoryTree(MsessionId, "", "");
				//  catalogCategoryTree ctree = Mclient.catalogCategoryTree(MsessionId, "0", "");
				//}
				//catch { }
				//try
				//{
				//  //catalogCategoryEntityNoChildren[] clist = Mclient.catalogCategoryLevel(MsessionId, "", "", "");
				//  catalogCategoryEntityNoChildren[] clist = Mclient.catalogCategoryLevel(MsessionId, "", "", "");
				//}
				//catch { }
				//try
				//{
				//  //catalogCategoryEntityNoChildren[] clist = Mclient.catalogCategoryLevel(MsessionId, "", "", "");
				//  catalogCategoryEntityNoChildren[] clist = Mclient.catalogCategoryLevel(MsessionId, "", "", "");
				//}
				//catch { }
				//try
				//{
				//  //catalogCategoryInfo cinfo = Mclient.catalogCategoryInfo(MsessionId, 0, "CurrentView", null);
				//  catalogCategoryInfo cinfo = Mclient.catalogCategoryInfo(MsessionId, 4, "", null);
				//}
				//catch { }
				//try
				//{
				//  //catalogProductAttributeSetEntity[] pasList = Mclient.catalogProductAttributeSetList(MsessionId);
				//  //...this one works!
				//  catalogProductAttributeSetEntity[] pasList = Mclient.catalogProductAttributeSetList(MsessionId);
				//}
				//catch { }

			  //---------------------SALES EXPORT----------------------------
			  try
			  {
					DateTime exportDate = DateTime.Now; //pass this date in
					string salesFileName = string.Format(SalesFilenameFormat, exportDate.ToString("yyyy-MM"));
					result += "\n" + salesFileName + ": ";
					StringBuilder salesData = new StringBuilder(CommonHeader + SalesRecord.Header());

					//create filter to get sales for this month only
					string fromDate = string.Format("{0:0000}-{1:00}-01 00:00:00", exportDate.Year, exportDate.Month);
					string toDate = string.Format("{0:0000}-{1:00}-01 00:00:00", exportDate.Year, exportDate.Month + 1);
					filters monthFilter = new filters();
					monthFilter.complex_filter = new complexFilter[2];
					monthFilter.complex_filter[0] = new complexFilter();
					monthFilter.complex_filter[0].key = "created_at";
					monthFilter.complex_filter[0].value = new associativeEntity();
					monthFilter.complex_filter[0].value.key = "from";
					monthFilter.complex_filter[0].value.value = fromDate;
					monthFilter.complex_filter[1] = new complexFilter();
					monthFilter.complex_filter[1].key = "created_at";
					monthFilter.complex_filter[1].value = new associativeEntity();
					monthFilter.complex_filter[1].value.key = "to";
					monthFilter.complex_filter[1].value.value = toDate;

					//get list of sales orders
			    salesOrderEntity[] sorders = Mclient.salesOrderList(MsessionId, monthFilter);
			    if (sorders.Length > 0)
			    {
			      //TODO: create header line for sales export
			      foreach (salesOrderEntity s in sorders)
			      {
			        string customerid = s.customer_id;
			        if (s.customer_is_guest.Equals("1"))
			        {
			          customerid = s.customer_email;
			          if (customerid == null || customerid.Length < 1)
			            customerid = s.increment_id;
			        }
			        string date = s.created_at;
							//get list of items purchased on each sales order
			        salesOrderEntity sinfo = Mclient.salesOrderInfo(MsessionId, s.increment_id);
			        foreach (salesOrderItemEntity item in sinfo.items)
			        {
			          string productid = item.product_id;
			          string quantity = item.qty_ordered;
			          int len = quantity.IndexOf(".");
			          if (len > 0)
			            quantity = quantity.Substring(0, len); //remove fractional part

			          //add line to sales data
								salesData.Append(customerid + "\t" + productid + "\t" + quantity + "\t" + date + "\r\n");  
			        }
			      }
			      //upload sales data
						result += m_boostService.WriteTable(m_alias, salesFileName, salesData);

			    }
			  }
			  catch { }
			#endregion

			return result;
		}

		protected override string GetSalesMonth(DateTime exportDate, string filename)
		{
			//access Magento store API

			//Get the sales records for the requested month
			DateTime fromDate = new DateTime(exportDate.Year, exportDate.Month, 1);
			DateTime toDate = fromDate.AddMonths(1);
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Exporting...";
			StringBuilder data = new StringBuilder();
			string h2 = "Product ID\tCustomer ID\tQuantity\tDate\r\n"; //sales file second header line
			StringBuilder salesData = new StringBuilder(CommonHeader + h2);

			Mage_Api_Model_Server_V2_HandlerPortTypeClient Mclient = new Mage_Api_Model_Server_V2_HandlerPortTypeClient();
			string MsessionId = "";

			//---------------------SALES MONTH EXPORT----------------------------
			//errors caught by calling function

			//login
			MsessionId = Mclient.login(m_apiUserName, m_apiKey);

			//create filter to get sales for this month only
			filters monthFilter = new filters();
			monthFilter.complex_filter = new complexFilter[2];
			monthFilter.complex_filter[0] = new complexFilter();
			monthFilter.complex_filter[0].key = "created_at";
			monthFilter.complex_filter[0].value = new associativeEntity();
			monthFilter.complex_filter[0].value.key = "from";
			monthFilter.complex_filter[0].value.value = fromDate.ToString("yyyy-MM-dd HH:mm:ss");
			monthFilter.complex_filter[1] = new complexFilter();
			monthFilter.complex_filter[1].key = "created_at";
			monthFilter.complex_filter[1].value = new associativeEntity();
			monthFilter.complex_filter[1].value.key = "to";
			monthFilter.complex_filter[1].value.value = toDate.ToString("yyyy-MM-dd HH:mm:ss");

			//get list of sales orders
			salesOrderEntity[] sorders = Mclient.salesOrderList(MsessionId, monthFilter);
			if (sorders.Length > 0)
			{
				foreach (salesOrderEntity s in sorders)
				{
					string customerid = s.customer_id;
					if (s.customer_is_guest.Equals("1"))
					{
						customerid = s.customer_email;
						if (customerid == null || customerid.Length < 1)
							customerid = s.increment_id;
					}
					string date = s.created_at;
					//get list of items purchased on each sales order
					salesOrderEntity sinfo = Mclient.salesOrderInfo(MsessionId, s.increment_id);
					foreach (salesOrderItemEntity item in sinfo.items)
					{
						string productid = item.product_id;
						//TODO: Need to get parent_item_id here if it exists

						string quantity = item.qty_ordered;
						int len = quantity.IndexOf(".");
						if (len > 0)
							quantity = quantity.Substring(0, len); //remove fractional part

						//add line to sales data
						salesData.Append(productid + "\t" + customerid + "\t" + quantity + "\t" + date + "\r\n");
					}
				}
			}
			//upload sales data
			ProgressText = tempDisplay + result + "Uploading...";
			result += m_boostService.WriteTable(m_alias, filename, data);
			ProgressText = tempDisplay + result;
			return result;
		}

		protected override string GetExclusions()
		{
			//-------temporarily disabled-------------
			//m_exclusionsEnabled = false;
			//-------temporarily disabled-------------

			if ((m_exclusions == null) || !m_exclusionsEnabled)
			{
				m_exclusionsEnabled = false;
				return "\nNo exclusions"; //nothing to do
			}

			string h2 = "";
			XElement xmlResult = null;
			StringBuilder data = null;
			string filename = "Exclusions.txt";
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Exporting...";
			h2 = "Product ID\r\n"; //product catalog second header line
			data = new StringBuilder(CommonHeader + h2);
			int emptyLen = data.Length;

			Mage_Api_Model_Server_V2_HandlerPortTypeClient Mclient = new Mage_Api_Model_Server_V2_HandlerPortTypeClient();
			string MsessionId = "";

			//login
			MsessionId = Mclient.login(m_apiUserName, m_apiKey);

			//Set product attributes to fetch
			//TODO: confirm that attributes are in the 
			string[] attributes = new string[m_exclusions.Count];
			int i = 0;
			foreach (Condition c in m_exclusions)
				attributes[i++] = c.FieldName;
			catalogProductRequestAttributes prodAttributes = new catalogProductRequestAttributes();
			prodAttributes.attributes = attributes;

			//loop through all products
			//TODO: creeate a filter to just get ptoducts that match the conditions
			catalogProductEntity[] plist;
			Mclient.catalogProductList(out plist, MsessionId, null, "");
			foreach (catalogProductEntity p in plist)
			{
				string pid = p.product_id;

				catalogProductReturnEntity pinfo = Mclient.catalogProductInfo(MsessionId, pid, "", prodAttributes, "id");
				XElement xpInfo = XmlSerializerExtension.SerializeAsXElement(pinfo);
				foreach (Condition c in m_exclusions)
				{
					string actual = Client.GetValue(xpInfo, c.FieldName);
					bool match = c.Compare(actual);
					if (match)
						data.Append(pid + "\r\n");
				}
			}
			if (data.Length > emptyLen)
			{
				ProgressText = tempDisplay + result + "Uploading...";
				result += m_boostService.WriteTable(m_alias, filename, data);
			}
			else
			{
				m_exclusionsEnabled = false;
				result += "No data matches conditions";
			}
			ProgressText = tempDisplay + result;
			return result;
		}

		protected override string GetReplacements()
		{
			return "Not Implemented";
		}

		protected override string GetAtt1Names()
		{
			string filename = "Attribute1Names.txt";
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Exporting...";

			result += "Uploaded with catalog";

			return result;
			//throw new NotImplementedException();
		}

		protected override string GetAtt2Names()
		{
			string filename = "Attribute1Names.txt";
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Exporting...";

			result += "Not Implemented";

			return result;
			//throw new NotImplementedException();
		}


	} //class MagentoExtractorStatic

} //namespace