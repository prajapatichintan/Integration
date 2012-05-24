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
	using Utilities;
	using Utilities.DynamicProxyLibrary;
	//using Utilities.GoStore;
	//using WSTeaService; //DEBUG
	//using GoStore_condomania;
	using GoStore_Buticul;

	/// <summary>
	/// CartExtractor implementation for Magento shopping carts
	/// </summary>
	public class MagentoExtractor: CartExtractor
	{

		public MagentoExtractor(string alias, XElement settings)
			: base(alias, settings)
		{
			//Add any Magento specific construction here
			ParseSettings(settings);
		}

		protected override void ParseSettings(XElement settings)
		{
			base.ParseSettings(settings);
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
				DynamicProxy proxy = GetSoapProxy(m_storeLongUrl + "/api/v2_soap/?wsdl", "Mage_Api_Model_Server_V2_HandlerPortType");
				if (proxy == null)
					throw new Exception("Unable to create SOAP proxy for Magento");

				//login to get a session id
				object resultObj = proxy.CallMethod("login", m_apiUserName, m_apiKey);
				string sessionID = resultObj.ToString();

				//Get order details
				//Get order details  sales_order.info  salesOrderInfo
				resultObj = proxy.CallMethod("sales_order.info", sessionID, orderID);
				XElement orderInfo = XmlSerializerExtension.SerializeAsXElement(resultObj, "salesOrderEntity");
				
				//Type t = resultObj.GetType();
				//if (!t.Name.Equals("salesOrderEntity"))
				//  throw new Exception("Illegal response from magento service");
				//XmlSerializer xs = new XmlSerializer(t);
				//XElement orderInfo = xs.SerializeAsXElement(resultObj);
				xmlResult = orderInfo;

				//salesOrderEntity orderInfo = (salesOrderEntity)resultObj;
				string customerID = Client.GetValue(orderInfo, "customer_id"); //orderInfo.customer_id;
				bool isGuest = Client.GetValue(orderInfo, "customer_is_guest").Equals("1");//orderInfo.customer_is_guest.Equals("1");
				if (isGuest)
					customerID = Client.GetValue(orderInfo, "customer_email"); //orderInfo.customer_email;
				//foreach (salesOrderItemEntity item in orderInfo.items)
				foreach (XElement items in orderInfo.Elements("items"))
					foreach (XElement x in items.Elements("salesOrderItemEntity"))
					{
						string productID = Client.GetValue(x, "product_id"); //item.product_id;
						string q = Client.GetValue(x, "qty_ordered");
						int len = q.IndexOf(".");
						if (len > 0)
							q = q.Substring(0, len);
						int quantity = Convert.ToInt32(q); //item.qty_ordered);
						string d = Client.GetValue(x, "created_at");
						DateTime date = DateTime.Parse(d);
						UsageLog.Instance.LogSingleAction(m_alias, productID, customerID, quantity, date);
					}
				result += "Complete";
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
			string h2 = "";
			StringBuilder data = null;

			string filename = "Catalog.txt";
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Rows to export...";
			StopWatch exportWatch = new StopWatch(true);

			//--------DEBUG USING STATIC------
			bool static_proxy = true;
			//--------------------------------

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
			if (static_proxy && m_alias.Equals("Buticul"))
			{				
				Mage_Api_Model_Server_V2_HandlerPortTypeClient Mclient = new Mage_Api_Model_Server_V2_HandlerPortTypeClient();
				string MsessionId = "";

				//---------------------CATALOG EXPORT----------------------------
				try
				{
					//login
					MsessionId = Mclient.login(m_apiUserName, m_apiKey);

					//Set product attributes to fetch
					catalogProductRequestAttributes prodAttributes = new catalogProductRequestAttributes();
					string[] attributes = { "sku", "url_key", "price", "special_price", "special_from_date", "special_to_date", "parent_item_id" };
					prodAttributes.attributes = attributes;

					//filters prodFilters = new filters();
					//associativeEntity[] filterList = new associativeEntity[1];
					//filterList[0].key = "";
					//filterList[0].value = "";

					//loop through all products
					h2 = "Product ID\tName\tAtt1 ID\tAtt2 ID\tPrice\tFilter\tLink\tImage Link\tStandard Code\r\n"; //product catalog second header line
					data = new StringBuilder(CommonHeader + h2);
					catalogProductEntity[] plist;
					Mclient.catalogProductList(out plist, MsessionId, null, "");
					//string type = "";
					int maxCid = 0;
					foreach (catalogProductEntity p in plist)
					{
						string pid = p.product_id;
						catalogProductLinkEntity[] plinks = Mclient.catalogProductLinkList(MsessionId, "parent", pid, "id");
						if (plinks != null)
							continue; //is a child

						//if (p.type.Equals("simple")) //only export combined items or else simple items with no parents
						//{
						//  bool isChild = false;
						//  catalogProductLinkEntity[] plinks = Mclient.catalogProductLinkList(MsessionId, "grouped", pid, "id");
						//  foreach (catalogProductLinkEntity pl in plinks)
						//    if (pl.type.Equals("configurable"))
						//    {
						//      isChild = true;
						//      break;
						//    }
						//  if (isChild) continue;
						//}
						//else
						//  type += p.type + " ";

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
						catch { }
						if (pimageinfo != null)
						{
							pimage = pimageinfo.url;
						}
						data.Append(pid + "\t" + pname + "\t" + patt1 + "\t" + patt2 + "\t" + pprice + "\t" + pfilter + "\t" + plink + "\t" + pimage + "\t" + psku + "\r\n");
					}
					result += m_boostService.WriteTable(m_alias, filename, data);

					//get cat info
					filename = "Attribute1Names.txt";
					result += "\n" + filename + ": ";
					ProgressText = tempDisplay + result + "Exporting...";
					catalogCategoryInfo cinfo;
					h2 = "Att ID\tName\r\n";
					StringBuilder csb = new StringBuilder(CommonHeader + h2);
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
					result += m_boostService.WriteTable(m_alias, filename, csb);
					ProgressText = tempDisplay + result;
				}
				catch { }
			}
			#endregion

			#region Dynamic GoStore

			if (!static_proxy)
			{
				//http://whitesalmontea.gostorego.com/api/v2_soap/?wsdl
				DynamicProxy proxy = GetSoapProxy(m_storeLongUrl + "/api/v2_soap/?wsdl", "Mage_Api_Model_Server_V2_HandlerPortType");
				if (proxy == null)
					throw new Exception("Unable to create SOAP proxy for Magento");

				//login to get a session id
				object resultObj = proxy.CallMethod("login", m_apiUserName, m_apiKey);
				string sessionID = resultObj.ToString();
				//Type t;
				//XmlSerializer xs;

				//---------------------CATALOG EXPORT----------------------------
				try
				{

					//Get catalog details
					resultObj = proxy.CallMethod("resources", sessionID);
					XElement resources = XmlSerializerExtension.SerializeAsXElement(resultObj);
					//t = resultObj.GetType();
					//xs = new XmlSerializer(t);
					//XElement resources = xs.SerializeAsXElement(resultObj);
					//TODO: check each CallMethod to make sure it is in this list...


					//catalog_product.list  catalogProductList
					resultObj = proxy.CallMethod("catalog_product.list", sessionID);
					XElement products = XmlSerializerExtension.SerializeAsXElement(resultObj); //catalogProductEntity[]
					//t = resultObj.GetType();
					//xs = new XmlSerializer(t);
					//XElement products = xs.SerializeAsXElement(resultObj); //catalogProductEntity[]
					foreach (XElement product in products.Elements("catalogProductEntity"))
					{
						string pid = "";
						string pname = "";
						string patt1 = "";
						try
						{
							pid = Client.GetValue(product, "product_id");
							pname = Client.GetValue(product, "name");
						}
						catch (Exception ex) { }

						try
						{
							bool first = true;
							foreach (XElement cat in product.Elements("category_ids"))
							{
								if (first) first = false;
								else patt1 += ",";
								patt1 += Client.GetValue(cat, "id");
							}
						}
						catch (Exception ex) { }

						XElement pinfo = null;
						//#if MAGENTO_API_AVAILABLE			
						catalogProductRequestAttributes a = new catalogProductRequestAttributes();
						string[] attributes = { "sku", "url_key", "price", "special_price", "special_from_date", "special_to_date" };
						a.attributes = attributes;
						XElement request = XmlSerializerExtension.SerializeAsXElement(a);
						//t = a.GetType();
						//xs = new XmlSerializer(t);
						//XElement request = xs.SerializeAsXElement(a);
						string patt2 = "";
						string pprice = "";
						try
						{
							//catalog_product.info  catalogProductInfo
							resultObj = proxy.CallMethod("catalog_product.info", sessionID, pid, a, "id");
							pinfo = XmlSerializerExtension.SerializeAsXElement(resultObj); //catalogProductReturnEntity
							//t = resultObj.GetType();
							//xs = new XmlSerializer(t);
							//pinfo = xs.SerializeAsXElement(resultObj); //catalogProductReturnEntity

							pprice = Client.GetValue(pinfo, "price");
							XElement xFromDate = pinfo.Element("special_from_date");
							XElement xToDate = pinfo.Element("special_from_date");
							if ((xFromDate != null) && (xToDate != null))
							{
								DateTime saleStart = DateTime.Parse(xFromDate.Value);
								DateTime saleEnd = DateTime.Parse(xToDate.Value);
								DateTime now = DateTime.Now;
								if (now >= saleStart && now <= saleEnd)
									pprice = Client.GetValue(pinfo, "special_price");
							}
						}
						catch (Exception ex) { }
						//#endif

						string pfilter = "";
						string plink = "";
						string psku = "";
						string pimage = "";
						try
						{
							plink = Client.GetValue(pinfo, "url_key");
							psku = Client.GetValue(pinfo, "sku");
							XElement pimageinfo = null;
							//catalog_product_attribute_media.info  catalogProductAttributeMediaInfo
							resultObj = proxy.CallMethod("catalog_product_attribute_media.info", sessionID, pid, "", "", "id");
							pimageinfo = XmlSerializerExtension.SerializeAsXElement(resultObj);		//catalogProductImageEntity
							//t = resultObj.GetType();
							//xs = new XmlSerializer(t);
							//pimageinfo = xs.SerializeAsXElement(resultObj);		//catalogProductImageEntity
							if (pimageinfo != null)
							{
								pimage = Client.GetValue(pimageinfo, "url");
							}
						}
						catch { }

					}
				}
				catch (Exception ex)
				{
					string errMsg = "Error extracting catalog: " + ex.Message;
					if (ex.InnerException != null)
						errMsg += "\nInner Exception" + ex.InnerException.Message;
					result += "\n" + errMsg;
				}
			}
			#endregion

			return result;
		}

		protected void APITest()
		{
			//static test
			Mage_Api_Model_Server_V2_HandlerPortTypeClient Mclient = new Mage_Api_Model_Server_V2_HandlerPortTypeClient();
			string MsessionId = "";

			try
			{
				//login
				MsessionId = Mclient.login(m_apiUserName, m_apiKey);
			}
			catch { }
			try
			{
				catalogCategoryTree ctree = Mclient.catalogCategoryTree(MsessionId, "0", "");
			}
			catch { }
			try
			{
				catalogCategoryEntityNoChildren[] clist = Mclient.catalogCategoryLevel(MsessionId, "", "", "");
			}
			catch { }
			try
			{
				catalogCategoryEntityNoChildren[] clist = Mclient.catalogCategoryLevel(MsessionId, "", "", "");
			}
			catch { }
			try
			{
				catalogCategoryInfo cinfo = Mclient.catalogCategoryInfo(MsessionId, 4, "", null);
			}
			catch { }
			try
			{
				catalogProductAttributeSetEntity[] pasList = Mclient.catalogProductAttributeSetList(MsessionId);
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

			//dynamic test
			DynamicProxy proxy = GetSoapProxy(m_storeLongUrl + "/api/v2_soap/?wsdl", "Mage_Api_Model_Server_V2_HandlerPortType");
			if (proxy == null)
				throw new Exception("Unable to create SOAP proxy for Magento");

			//login to get a session id
			object resultObj = proxy.CallMethod("login", m_apiUserName, m_apiKey);
			string sessionID = resultObj.ToString();
			Type t;
			XmlSerializer xs;

			try
			{
				//catalog_category.info  catalogCategoryInfo
				resultObj = proxy.CallMethod("catalog_category.info", sessionID);
				XElement categories = XmlSerializerExtension.SerializeAsXElement(resultObj);
				//t = resultObj.GetType();
				//xs = new XmlSerializer(t);
				//XElement categories = xs.SerializeAsXElement(resultObj);
				foreach (XElement category in categories.Elements("catalogCategoryEntity"))
				{
					string test = category.Element("").Value;
				}
			}
			catch (Exception ex)
			{
				string errMsg = "Error extracting catalog: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception" + ex.InnerException.Message;
			}

			try
			{
				//catalog_category.tree  catalogCategoryTree
				resultObj = proxy.CallMethod("catalog_category.tree", sessionID);
				XElement categoryTree = XmlSerializerExtension.SerializeAsXElement(resultObj);
				//t = resultObj.GetType();
				//xs = new XmlSerializer(t);
				//XElement categoryTree = xs.SerializeAsXElement(resultObj);
				foreach (XElement category in categoryTree.Elements("catalogCategoryEntity"))
				{
					string test = category.Element("").Value;
				}
			}
			catch (Exception ex)
			{
				string errMsg = "Error extracting catalog: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception" + ex.InnerException.Message;
			}

			try
			{
				//product_attribute_set.list  catalogProductAttributeSetList
				resultObj = proxy.CallMethod("product_attribute_set.list", sessionID);
				XElement attributes = XmlSerializerExtension.SerializeAsXElement(resultObj);
				//t = resultObj.GetType();
				//xs = new XmlSerializer(t);
				//XElement attributes = xs.SerializeAsXElement(resultObj);
				foreach (XElement attribute in attributes.Elements("catalogProductAttributeSetEntity"))
				{
					string test = attribute.Element("").Value;
				}
			}
			catch (Exception ex)
			{
				string errMsg = "Error extracting catalog: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception" + ex.InnerException.Message;
			}

			//---------------------SALES EXPORT----------------------------
			try
			{
				//sales_order.list  salesOrderList
				resultObj = proxy.CallMethod("sales_order.list", sessionID);
				XElement orders = XmlSerializerExtension.SerializeAsXElement(resultObj);
				//t = resultObj.GetType();
				//xs = new XmlSerializer(t);
				//XElement orders = xs.SerializeAsXElement(resultObj);
				foreach (XElement order in orders.Elements("salesOrderEntity"))
				{
					string orderID = Client.GetValue(order, "order_id");
					string date = Client.GetValue(order, "created_at");

					//Get order details  sales_order.info  salesOrderInfo
					resultObj = proxy.CallMethod("sales_order.info", sessionID, orderID);
					XElement orderInfo = XmlSerializerExtension.SerializeAsXElement(resultObj, "salesOrderInfo");
					//t = resultObj.GetType();
					//if (!t.Name.Equals("salesOrderInfo"))
					//  throw new Exception("Illegal response from magento service");
					//xs = new XmlSerializer(t);
					//XElement orderInfo = xs.SerializeAsXElement(resultObj);

					//salesOrderEntity orderInfo = (salesOrderEntity)resultObj;
					string customerID = Client.GetValue(orderInfo, "customer_id"); //orderInfo.customer_id;
					bool isGuest = Client.GetValue(orderInfo, "customer_is_guest").Equals("1");//orderInfo.customer_is_guest.Equals("1");
					if (isGuest)
						customerID = Client.GetValue(orderInfo, "customer_email"); //orderInfo.customer_email;
					//foreach (salesOrderItemEntity item in orderInfo.items)
					foreach (XElement items in orderInfo.Elements("items"))
						foreach (XElement x in items.Elements("salesOrderItemEntity"))
						{
							string productID = Client.GetValue(x, "product_id"); //item.product_id;
							string q = Client.GetValue(x, "qty_ordered");
							int len = q.IndexOf(".");
							if (len > 0)
								q = q.Substring(0, len);
							int quantity = Convert.ToInt32(q); //item.qty_ordered);
							//TODO: add line to sales data here
						}
				}
				//TODO: upload sales data (check that string has some data in it)
			}
			catch (Exception ex)
			{
				string errMsg = "Error extracting catalog: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception" + ex.InnerException.Message;
			}
		}

		protected override string GetSalesMonth(DateTime exportDate, string filename)
		{
			//access Magento store API
			DynamicProxy proxy = GetSoapProxy(m_storeLongUrl + "/api/v2_soap/?wsdl", "Mage_Api_Model_Server_V2_HandlerPortType");
			if (proxy == null)
				throw new Exception("Unable to create SOAP proxy for Magento");

			//login to get a session id
			object resultObj = proxy.CallMethod("login", m_apiUserName, m_apiKey);
			string sessionID = resultObj.ToString();
			//Type t;
			//XmlSerializer xs;

#if DEBUG
			//apiEntity[] resources = 
			resultObj = proxy.CallMethod("resources", sessionID);
			XElement resources = XmlSerializerExtension.SerializeAsXElement(resultObj);
#endif

			//Get the sales records for the requested month
			int year = exportDate.Year;
			int month = exportDate.Month;
			DateTime start = new DateTime(year, month, 1);
			DateTime end = start.AddMonths(1);
			string result = filename + ": ";
			StringBuilder data = new StringBuilder();
			try
			{
				//sales_order.list  salesOrderList
				resultObj = proxy.CallMethod("sales_order.list", sessionID);
				XElement orders = XmlSerializerExtension.SerializeAsXElement(resultObj);
				//t = resultObj.GetType();
				//xs = new XmlSerializer(t);
				//XElement orders = xs.SerializeAsXElement(resultObj);
				foreach (XElement order in orders.Elements("salesOrderEntity"))
				{
					string orderID = Client.GetValue(order, "order_id");
					string date = Client.GetValue(order, "created_at");

					//Get order details  sales_order.info  salesOrderInfo
					resultObj = proxy.CallMethod("sales_order.info", sessionID, orderID);
					XElement orderInfo = XmlSerializerExtension.SerializeAsXElement(resultObj, "salesOrderInfo");
					//t = resultObj.GetType();
					//if (!t.Name.Equals("salesOrderInfo"))
					//  throw new Exception("Illegal response from magento service");
					//xs = new XmlSerializer(t);
					//XElement orderInfo = xs.SerializeAsXElement(resultObj);

					//salesOrderEntity orderInfo = (salesOrderEntity)resultObj;
					string customerID = Client.GetValue(orderInfo, "customer_id"); //orderInfo.customer_id;
					bool isGuest = Client.GetValue(orderInfo, "customer_is_guest").Equals("1");//orderInfo.customer_is_guest.Equals("1");
					if (isGuest)
						customerID = Client.GetValue(orderInfo, "customer_email"); //orderInfo.customer_email;
					//foreach (salesOrderItemEntity item in orderInfo.items)
					foreach (XElement items in orderInfo.Elements("items"))
						foreach (XElement x in items.Elements("salesOrderItemEntity"))
						{
							string productID = Client.GetValue(x, "product_id"); //item.product_id;
							string q = Client.GetValue(x, "qty_ordered");
							int len = q.IndexOf(".");
							if (len > 0)
								q = q.Substring(0, len);
							int quantity = Convert.ToInt32(q); //item.qty_ordered);
							//TODO: add line to sales data here
						}
				}
				//TODO: upload sales data (check that string has some data in it)
				if (data.Length > 0)
					result += m_boostService.WriteTable(m_alias, filename, data, false);
			}
			catch (Exception ex)
			{
				string errMsg = "Error extracting catalog: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception" + ex.InnerException.Message;
			}

			return result;
		}

		protected override string GetExclusions()
		{
			if (m_exclusions == null)
				return "No exclusions"; //nothing to do

			string h2 = "";
			StringBuilder data = null;
			string filename = "Exclusions.txt";
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Exporting...";
			h2 = "Product ID\r\n"; //product catalog second header line
			data = new StringBuilder(CommonHeader + h2);
			int emptyLen = data.Length;

//#if MAGENTO_API_AVAILABLE			
			Mage_Api_Model_Server_V2_HandlerPortTypeClient Mclient = new Mage_Api_Model_Server_V2_HandlerPortTypeClient();
			string MsessionId = "";

			//login
			MsessionId = Mclient.login(m_apiUserName, m_apiKey);

			//Set product attributes to fetch
			string[] attributes = new string[m_exclusions.Count];
			int i = 0;
			foreach (Condition c in m_exclusions)
				attributes[i++] = c.FieldName;
			catalogProductRequestAttributes prodAttributes = new catalogProductRequestAttributes();
			prodAttributes.attributes = attributes;

			//WSTeaService.filters prodFilters = new WSTeaService.filters();
			//prodFilters.filter = new WSTeaService.associativeEntity[1];
			//prodFilters.filter[0].key = "";
			//prodFilters.filter[0].value = "";

			//loop through all products
			catalogProductEntity[] plist;
			Mclient.catalogProductList(out plist, MsessionId, null, "");
			foreach (catalogProductEntity p in plist)
			{
				string pid = p.product_id;

				catalogProductReturnEntity pinfo = Mclient.catalogProductInfo(MsessionId, pid, "", prodAttributes, "id");
				XElement xpInfo = XmlSerializerExtension.SerializeAsXElement(pinfo);
				//Type t = pinfo.GetType();
				//XmlSerializer xs = new XmlSerializer(t);
				//XElement xpInfo = xs.SerializeAsXElement(pinfo);
				foreach (Condition c in m_exclusions)
				{
					string actual = Client.GetValue(xpInfo, c.FieldName);
					bool match = c.Compare(actual);
					if (match)
						data.Append(pid + "\r\n");
				}
				if (data.Length > emptyLen)
				{
					ProgressText = tempDisplay + result + "Uploading...";
					result += m_boostService.WriteTable(m_alias, filename, data);
				}
			}
//#endif
			return result;
		}

		protected override string GetReplacements()
		{
			return "Not Implemented";
		}

		protected override string GetAtt1Names()
		{
			return "Not Implemented";
			//throw new NotImplementedException();
		}

		protected override string GetAtt2Names()
		{
			return "Not Implemented";
			//throw new NotImplementedException();
		}

		private DynamicProxy GetSoapProxy(string serviceUri, string contract)
		{
			DynamicProxyFactory factory = null;
			DynamicProxy proxy = null;
			try
			{
				factory = new DynamicProxyFactory(serviceUri);
				proxy = factory.CreateProxy(contract);
			}
			catch (Exception ex)
			{
				string errMsg = "Error creating Soap Proxy: " + ex.Message + "\n";
				if (ex.InnerException != null)
					errMsg += "Inner Exception = " + ex.InnerException.Message + "\n";
				errMsg += "\nserviceUri: " + serviceUri;
				errMsg += "\ncontract: " + contract;
				m_log.WriteEntry(errMsg, EventLogEntryType.Error);
			}
			return proxy;
		}



	} //class MagentoExtractor

	static class XmlSerializerExtension
	{
		public static XElement SerializeAsXElement(object o, string requiredName = "")
		{
			Type t = o.GetType();
			if ((requiredName.Length > 0) && (!t.Name.Equals(requiredName)))
					throw new Exception("Illegal response from magento service");

			XmlSerializer xs = new XmlSerializer(t);
			XDocument d = new XDocument();
			using (XmlWriter w = d.CreateWriter()) xs.Serialize(w, o);
			XElement e = d.Root;
			e.Remove();
			return e;
		}
 
		//public static XElement SerializeAsXElement(this XmlSerializer xs, object o)
		//{
		//  XDocument d = new XDocument();
		//  using (XmlWriter w = d.CreateWriter()) xs.Serialize(w, o);
		//  XElement e = d.Root;
		//  e.Remove();
		//  return e;
		//}
	}

} //namespace