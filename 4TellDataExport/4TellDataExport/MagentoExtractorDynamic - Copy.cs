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
	//using WSTeaService; //DEBUG
	//using GoStore_TheLuvo;
	using GoStore_condomania;
	using Utilities;
	using Utilities.DynamicProxyLibrary;

	/// <summary>
	/// CartExtractor implementation for Magento shopping carts
	/// </summary>
	public class MagentoExtractorDynamic: CartExtractor
	{

		public MagentoExtractorDynamic(string dataPath, BoostLog log, string alias, XElement settings)
			: base(dataPath, log, alias, settings)
		{
			//Add any Magento specific construction here
			ParseSettings(settings);

#if DEBUG
			XElement x = null;
			ApiTest(ref x);
#endif
		}

		protected override void ParseSettings(XElement settings)
		{
			base.ParseSettings(settings);

			if (m_storeUrl.Length > 0)
			{
				if (!m_storeUrl.StartsWith("http"))
					m_storeUrl = "http://" + m_storeUrl; //add prefix if missing
				if (m_storeUrl.EndsWith("/"))
					m_storeUrl.Remove(m_storeUrl.Length - 1); //remove final slash
			}
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
				DynamicProxy proxy = GetSoapProxy(m_storeUrl + "/api/v2_soap/?wsdl", "Mage_Api_Model_Server_V2_HandlerPortType");
				if (proxy == null)
					throw new Exception("Unable to create SOAP proxy for Magento");

				//login to get a session id
				object resultObj = proxy.CallMethod("login", m_apiUserName, m_apiKey);
				string sessionID = resultObj.ToString();

				//Get order details
				resultObj = proxy.CallMethod("salesOrderInfo", sessionID, orderID);
				Type t = resultObj.GetType();
				if (!t.Name.Equals("salesOrderEntity"))
					throw new Exception("Illegal response from magento service");
				XmlSerializer xs = new XmlSerializer(t);
				XElement orderInfo = xs.SerializeAsXElement(resultObj);
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
						m_log.LogSingleSale(m_alias, customerID, productID, quantity, date);
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

		protected override string GetCatalog(ref XElement xmlResult)
		{
			string h2 = "";
			xmlResult = null;
			StringBuilder data = null;

			string filename = "ProductDetails.txt";
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Rows to export...";
			StopWatch exportWatch = new StopWatch(true);

			Mage_Api_Model_Server_V2_HandlerPortTypeClient Mclient = new Mage_Api_Model_Server_V2_HandlerPortTypeClient();
			string MsessionId = "";

			//---------------------CATALOG EXPORT----------------------------
			try
			{
			}
			catch { }

			return result;
		}

		protected string ApiTest(ref XElement xmlResult)
		{
			string h2 = "";
			xmlResult = null;
			StringBuilder data = null;

			string filename = "ProductDetails.txt";
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Rows to export...";
			StopWatch exportWatch = new StopWatch(true);

			#region Dynamic
			{
			  //http://whitesalmontea.gostorego.com/api/v2_soap/?wsdl
			  DynamicProxy proxy = GetSoapProxy(m_storeUrl + "/api/v2_soap/?wsdl", "Mage_Api_Model_Server_V2_HandlerPortType");
			  if (proxy == null)
			    throw new Exception("Unable to create SOAP proxy for Magento");

			  //login to get a session id
			  object resultObj = proxy.CallMethod("login", m_apiUserName, m_apiKey);
			  string sessionID = resultObj.ToString();
			  Type t;
			  XmlSerializer xs;

			  //---------------------CATALOG EXPORT----------------------------
			  try
			  {

			    //Get catalog details
			    resultObj = proxy.CallMethod("resources", sessionID);
			    t = resultObj.GetType();
			    xs = new XmlSerializer(t);
			    XElement resources = xs.SerializeAsXElement(resultObj);
			    //TODO: check each CallMethod to make sure it is in this list...


			    //catalog_product.list  catalogProductList
			    resultObj = proxy.CallMethod("catalogProductList", sessionID);
			    t = resultObj.GetType();
			    xs = new XmlSerializer(t);
			    XElement products = xs.SerializeAsXElement(resultObj); //catalogProductEntity[]
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
			        resultObj = proxy.CallMethod("catalogProductAttributeMediaInfo", sessionID, pid, "", "", "id");
			        t = resultObj.GetType();
			        xs = new XmlSerializer(t);
			        pimageinfo = xs.SerializeAsXElement(resultObj);		//catalogProductImageEntity
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
			  }

			  try
			  {
			    //catalog_category.info  catalogCategoryInfo
			    resultObj = proxy.CallMethod("catalogCategoryInfo", sessionID);
			    t = resultObj.GetType();
			    xs = new XmlSerializer(t);
			    XElement categories = xs.SerializeAsXElement(resultObj);
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
			    resultObj = proxy.CallMethod("catalogCategoryTree", sessionID);
			    t = resultObj.GetType();
			    xs = new XmlSerializer(t);
			    XElement categoryTree = xs.SerializeAsXElement(resultObj);
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
			    resultObj = proxy.CallMethod("catalogProductAttributeSetList", sessionID);
			    t = resultObj.GetType();
			    xs = new XmlSerializer(t);
			    XElement attributes = xs.SerializeAsXElement(resultObj);
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
			    resultObj = proxy.CallMethod("salesOrderList", sessionID);
			    t = resultObj.GetType();
			    xs = new XmlSerializer(t);
			    XElement orders = xs.SerializeAsXElement(resultObj);
			    foreach (XElement order in orders.Elements("salesOrderEntity"))
			    {
			      string orderID = Client.GetValue(order, "order_id");
			      string date = Client.GetValue(order, "created_at");

			      //Get order details  sales_order.info  salesOrderInfo
			      resultObj = proxy.CallMethod("salesOrderInfo", sessionID, orderID);
			      t = resultObj.GetType();
			      if (!t.Name.Equals("salesOrderInfo"))
			        throw new Exception("Illegal response from magento service");
			      xs = new XmlSerializer(t);
			      XElement orderInfo = xs.SerializeAsXElement(resultObj);

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
			    result += "\n" + errMsg;
			  }
			}
			#endregion

			return result;
		}

		protected override string GetSalesMonth(ref XElement xmlResult, DateTime exportDate, string filename)
		{
			//access Magento store API
			DynamicProxy proxy = GetSoapProxy(m_storeUrl + "/api/v2_soap/?wsdl", "Mage_Api_Model_Server_V2_HandlerPortType");
			if (proxy == null)
				throw new Exception("Unable to create SOAP proxy for Magento");

			//login to get a session id
			object resultObj = proxy.CallMethod("login", m_apiUserName, m_apiKey);
			string sessionID = resultObj.ToString();

			//Get API calls available
			resultObj = proxy.CallMethod("resources", sessionID);
			Type t = resultObj.GetType();
			XmlSerializer xs = new XmlSerializer(t);
			XElement resources = xs.SerializeAsXElement(resultObj);
			//TODO: check each CallMethod to make sure it is in this list...

			//Get the sales records for the requested month
			DateTime start = new DateTime(exportDate.Year, exportDate.Month, 1);
			DateTime end = start.AddMonths(1);
			string result = filename + ": ";
			StringBuilder data = new StringBuilder();
			try
			{
				//sales_order.list  salesOrderList
				resultObj = proxy.CallMethod("salesOrderList", sessionID); //PROBLEM: this gets all orders, not just the ones for this month
				t = resultObj.GetType();
				xs = new XmlSerializer(t);
				XElement orders = xs.SerializeAsXElement(resultObj);
				foreach (XElement order in orders.Elements("salesOrderEntity"))
				{
					string orderID = Client.GetValue(order, "order_id");
					string date = Client.GetValue(order, "created_at");

					//Get order details  sales_order.info  salesOrderInfo
					resultObj = proxy.CallMethod("salesOrderInfo", sessionID, orderID);
					t = resultObj.GetType();
					if (!t.Name.Equals("salesOrderInfo"))
						throw new Exception("Illegal response from magento service");
					xs = new XmlSerializer(t);
					XElement orderInfo = xs.SerializeAsXElement(resultObj);

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
					result += m_boostService.UploadFileTo4Tell(filename, data, false);
			}
			catch (Exception ex)
			{
				string errMsg = "Error extracting catalog: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception" + ex.InnerException.Message;
			}

			return result;
		}

		protected override string GetExclusions(ref XElement xmlResult)
		{
			if (m_exclusions == null)
				return "No exclusions"; //nothing to do

			string h2 = "";
			xmlResult = null;
			StringBuilder data = null;
			string filename = "DoNotRecommend.txt";
			string result = "\n" + filename + ": ";
			string tempDisplay = ProgressText;
			ProgressText += result + "Exporting...";
			h2 = "Product ID\r\n"; //product catalog second header line
			data = new StringBuilder(m_headerRow1 + h2);
			int emptyLen = data.Length;

			return result;
		}

		protected override string GetAtt1Names(ref XElement xmlResult)
		{
			return "Not Implemented";
			//throw new NotImplementedException();
		}

		protected override string GetAtt2Names(ref XElement xmlResult)
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
		public static XElement SerializeAsXElement(this XmlSerializer xs, object o)
		{
			XDocument d = new XDocument();
			using (XmlWriter w = d.CreateWriter()) xs.Serialize(w, o);
			XElement e = d.Root;
			e.Remove();
			return e;
		}
	}

} //namespace