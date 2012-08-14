using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using _4_Tell.Utilities;
using _4_Tell.IO;


namespace _4_Tell
{
  public class BigCommerceExtractor : CartExtractor
  {
		private readonly string m_webServicesBaseUrl;
		private readonly string m_dataPath = string.Empty;
  	private const string m_orderDetailsFileName = "OrderHistory.xml";
  	private readonly Dictionary<string, IEnumerable<XElement>> _xml = new Dictionary<string, IEnumerable<XElement>>();

    public BigCommerceExtractor(string alias, XElement settings) : base(alias, settings)
    {
			base.ParseSettings(settings);

			m_dataPath = DataPath.Instance.ClientDataPath(ref m_alias) + "upload\\";
			m_webServicesBaseUrl = string.Format("https://{0}/api/v2/", m_storeShortUrl);
    }

		public IEnumerable<XElement> CallCartApi(string service, string tagName = null)
    {
				//NOTE: Johnny wrote this to allow multiple methods to request the same data without hitting the serice each time
				//			It strikes me that this could prevent future calls
        if (_xml.Keys.Contains(service))
            return _xml.Where(x => x.Key.Equals(service)).Select(x => x.Value).SingleOrDefault();

				var request = WebRequest.Create(string.Format("{0}{1}", m_webServicesBaseUrl, service)) as HttpWebRequest;
        if(request == null)
            return null;

        request.Method = "GET";
        request.Credentials = new NetworkCredential(m_apiUserName, m_apiKey);

        using(var response = request.GetResponse() as HttpWebResponse)
        {
            if(response == null || !response.StatusCode.Equals(HttpStatusCode.OK))
                return null;

            var doc = XDocument.Load(response.GetResponseStream());
						if (tagName == null) tagName = service;
						XName name = tagName;
						var elements = doc.Elements(name);
            _xml.Add(service, elements);

            return elements;
        }
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
							m_salesXml = salesDoc.Root.Elements("order");
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

    #region Overrides of CartExtractor

    public override void LogSalesOrder(string orderId)
    {
        //throw new NotImplementedException();

			IEnumerable<XElement> ordersXml;
			IEnumerable<XElement> productsXml;
    	string oCustomer;
			DateTime oDate;
			try
			{
				ordersXml = CallCartApi(string.Format("orders/{0}", orderId), "orders");
				if (ordersXml == null) return;
				ordersXml = ordersXml.Elements("order");
				if (ordersXml == null)
					throw new ArgumentNullException();

				productsXml = CallCartApi(string.Format("orders/{0}/products", orderId), "products");
				if (productsXml == null) return;
				productsXml = productsXml.Elements("product");
				if (productsXml == null)
					throw new ArgumentNullException();

				var order = ordersXml.First();
				oCustomer = Client.GetValue(order, "customer_id");
				var dateVal = Client.GetValue(order, "order_date");
				oDate = dateVal.Length < 1 ? DateTime.Now : DateTime.Parse(dateVal);
			}
			catch (Exception ex) 
			{ 
				//log error
				return;
			}

    	int errors = 0;
			foreach (var p in productsXml)
			{
				try
				{
					var oProduct = Client.GetValue(p, "product_id");
					var q = Client.GetValue(p, "product_qty");
					int oQuantity = Convert.ToInt32(q);

					//upload to 4-Tell
					UsageLog.Instance.LogSingleAction(m_alias, oProduct, oCustomer, oQuantity, oDate);
				}
				catch { errors++; }
			}
    }

    protected override string GetCatalog()
    {
      var stopWatch = new StopWatch(true);
      var products = new List<ProductRecord>();

			//get product count
			var countXml = CallCartApi("products/count", "products");
			var countVal = countXml.Elements("count").First().Value;
			int pRows = Convert.ToInt16(countVal);

			//setup progress display
      var result = string.Format("{0}{1}: ({2} items) ", Environment.NewLine, CatalogFilename, countVal);
      ProgressText += result + "Extracting...";
			string tempDisplay = ProgressText;


			//first get all the images because there can be many images for each product so record pages are not aligned
			countXml = CallCartApi("products/images/count", "images");
			countVal = countXml.Elements("count").First().Value;
			int iRows = Convert.ToInt16(countVal);
			int pages = (int)Math.Ceiling( (decimal)iRows / (decimal)250 ); //can only get 250 products at a time		
			var imagesXml = new List<XElement>(); //this will hold all images for entire catalog
			for (int page = 1; page <= pages; page++)
			{
				string paging = string.Format("?page={0}&limit=250", page);
				var pageXml = CallCartApi("products/images" + paging, "images");
				if (pageXml == null)
					break;
				imagesXml.AddRange(pageXml.Elements("image"));
				ProgressText = string.Format("{1}{0}{2} Images ({3})", Environment.NewLine, tempDisplay, imagesXml.Count, stopWatch.Lap());
			}
    	tempDisplay = ProgressText;
#if DEBUG
				if (imagesXml.Count() != iRows)
				{
					var errMsg = string.Format("Error reading BigCommerce images\nReported count = {0}, actual count = {1}",
																					iRows, imagesXml.Count());
					m_log.WriteEntry(errMsg, EventLogEntryType.Information, m_alias);
				}
#endif

			//Next get all the custom fields so we don't have to make a separate call for each product
			countXml = CallCartApi("products/customfields/count", "customfields");
			countVal = countXml.Elements("count").First().Value;
			iRows = Convert.ToInt16(countVal);
			pages = (int)Math.Ceiling((decimal)iRows / (decimal)250); //can only get 250 products at a time		
			var customFieldXml = new List<XElement>(); //this will hold all images for entire catalog
			for (int page = 1; page <= pages; page++)
			{
				string paging = string.Format("?page={0}&limit=250", page);
				var pageXml = CallCartApi("products/customfields" + paging, "customfields");
				if (pageXml == null)
					break;
				customFieldXml.AddRange(pageXml.Elements("customfield"));
				ProgressText = string.Format("{1}{0}{2} Custom Fields ({3})", Environment.NewLine, tempDisplay, customFieldXml.Count, stopWatch.Lap());
			}
			tempDisplay = ProgressText;

			//Next get all the Categories so we can construct the tree
			countXml = CallCartApi("categories/count", "categories");
			countVal = countXml.Elements("count").First().Value;
			iRows = Convert.ToInt16(countVal);
			pages = (int)Math.Ceiling((decimal)iRows / (decimal)250); //can only get 250 products at a time		
			var categoryXml = new List<XElement>(); //this will hold all images for entire catalog
			for (int page = 1; page <= pages; page++)
			{
				string paging = string.Format("?page={0}&limit=250", page);
				var pageXml = CallCartApi("categories" + paging, "categories");
				if (pageXml == null)
					break;
				categoryXml.AddRange(pageXml.Elements("category"));
				ProgressText = string.Format("{1}{0}{2} Categories ({3})", Environment.NewLine, tempDisplay, categoryXml.Count, stopWatch.Lap());
			}
    	SetCategoryParents(categoryXml);
    	ProgressText += Environment.NewLine;
			tempDisplay = ProgressText;

			//now get each page of products
			pages = (int)Math.Ceiling((decimal)pRows / (decimal)250); //can only get 250 products at a time										
			IEnumerable<XElement> productsXml = null; //this will only hold a single page of products
			int errors = 0; pRows = 0;
			for (int page = 1; page <= pages; page++)
				{
				string paging = string.Format("?page={0}&limit=250", page);
				productsXml = CallCartApi("products" + paging, "products");
				if (productsXml == null)
				{
					errors++; //not necessarily an error
					break;
				}

				var productElements = productsXml.Elements("product");
				foreach (var product in productElements)
				{
					try
					{
						var p = new ProductRecord
															{
																Name = Client.GetValue(product, "name"),
																ProductId = Client.GetValue(product, "id"),
																Att2Id = Client.GetValue(product, "brand_id"),
																Price = Client.GetValue(product, "price"),
																SalePrice = Client.GetValue(product, "sale_price"),
																Filter = string.Empty,
																Rating = Client.GetValue(product, "rating_total"),
																StandardCode = Client.GetValue(product, "upc"),
																Link = Client.GetValue(product, "custom_url"),
																ImageLink = string.Empty
															};

						p.ImageLink = GetProductImage(imagesXml, p.ProductId);
						p.Att1Id = GetProductCategories(product.Element("categories"));
						if (p.StandardCode == null)
							p.StandardCode = string.Empty;

#if DEBUG
						if (p.ProductId.Equals("11348") || p.ProductId.Equals("11012"))
						{
							var test = CallCartApi(string.Format("products/{0}/customfields", p.ProductId), "customfields");
							var s = "break here";
						}
#endif
						var customFields = GetProductCustomFields(customFieldXml, p.ProductId);
						if (customFields.Any())
							product.Add(customFields);

						//check category conditions, exclusions, and filters
						ApplyRules(ref p, product);

						products.Add(p);
					}
					catch { errors++; }
					ProgressText = string.Format("{0}{1} items completed, {2} errors ({3})", tempDisplay, ++pRows, errors, stopWatch.Lap());
				}
			}
			if (products.Count < 1)
				return result + "(no data)";

			var pCount = string.Format("({0} items) ", products.Count);
      ProgressText = string.Format("{0}{1} items completed ({2}){3}Uploading to server...", tempDisplay, pCount, stopWatch.Lap(), Environment.NewLine);
			result += m_boostService.WriteTable(m_alias, CatalogFilename, products);
			stopWatch.Stop();
      return result;
    }

    protected override string GetSalesMonth(DateTime exportDate, string filename)
    {
			//Note: This was originally written to work with both live API connections 
			//      and manually exported OrderHistory.xml, butAPI sections are now 
			//			commented out so it only works with manual exports. 
			//      The API version worked, but had to call the API too many times to get
			//			all the orders (250 at a time) and then call again to get the details
			//			for each order. This exceeded the API bandwidth limit for larger stores. 

      var stopWatch = new StopWatch(true);

			//get daterange for this month and create query
			var beginDate = new DateTime(exportDate.Year, exportDate.Month, 1);
			var endDate = beginDate.AddMonths(1).AddDays(-1);
			//const string dateFormat = "ddd, dd MMM yyyy HH:mm:ss +0000";
			//string query = string.Format("?min_date_created={0}&max_date_created={1}",
			//                                HttpUtility.UrlEncode(beginDate.ToString(dateFormat)),
			//                                HttpUtility.UrlEncode(endDate.ToString(dateFormat)));

			if (SalesHistory == null || !SalesHistory.Any())
				return Environment.NewLine + "No orders in sales history";
			int rows = SalesHistory.Count();

			//get the order count for this month
			//var ordersXml = CallCartApi("orders/count" + query, "orders");
			//var oCount = ordersXml.Elements("count").First().Value;
			var result = string.Format("{0}{1}: ", Environment.NewLine, filename);
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			//int rows = Convert.ToInt16(rows);
			//int pages = (int)Math.Ceiling( (decimal)rows / (decimal)250 ); //can only get 250 products at a time					
			int errors = 0;  rows = 0;
			var orders = new List<SalesRecord>();
			//for (int page = 1; page <= pages; page++)
			//{
			//  string paging = string.Format("&page={0}&limit=250", page);
			//  ordersXml = CallCartApi("orders" + query + paging, "orders");
			//  if (ordersXml == null) break;

				//var orderElements = ordersXml.Elements("order");
				//var orderElements = SalesHistory.Elements("order");
				//if (orderElements != null)
				//{
				//  foreach (var o in orderElements)
				if (SalesHistory != null)
				{
					foreach (var o in SalesHistory)
					{
						IEnumerable<XElement> productsXml = null;
						string oCustomer = "", oDate = "", oId = "";
						try
						{
							var dateVal = Client.GetValue(o, "order_date");
							if (dateVal.Length < 1) continue; //no date

							DateTime d = DateTime.Parse(dateVal);
							if ((d.CompareTo(beginDate) < 0) || (d.CompareTo(endDate) > 0))
								continue;
							oDate = d.ToShortDateString();
							oCustomer = Client.GetValue(o, "customer_id");
							//var dateVal = Client.GetValue(o, "date_created");
							//oId = Client.GetValue(o, "id");
							oId = Client.GetValue(o, "order_id");

							//productsXml = CallCartApi(string.Format("orders/{0}/products", oId), "products").Elements("product");
							productsXml = o.Elements().Where(x => x.Name.LocalName.Equals("product_details", StringComparison.CurrentCultureIgnoreCase)).Elements("item");
							if (productsXml == null)
								throw new ArgumentNullException();
						}
						catch (Exception ex) 
						{ 
							errors++;
							continue;
						}

						foreach (var p in productsXml)
						{
							try
							{
								orders.Add(new SalesRecord {
																ProductId = Client.GetValue(p, "product_id"),
																CustomerId = oCustomer,
																//Quantity = Client.GetValue(p, "quantity"),
																Quantity = Client.GetValue(p, "product_qty"),
																Date = oDate
															});
							}
							catch { errors++; }
							ProgressText = string.Format("{0}Extracting...{1} products, {2} errors ({3})", result, ++rows, errors, stopWatch.Lap());
						}
					}
				}
			//}
			if (orders.Count < 1)
				return result + "(no data)";

			var countDisplay = string.Format("({0} orders) ", orders.Count);
			ProgressText = tempDisplay + countDisplay + "Uploading...";
			result += countDisplay + m_boostService.WriteTable(m_alias, filename, orders);
			stopWatch.Stop();
      return result;
    }

    protected override string GetExclusions()
    {
			//NOTE: GetCatalog must be called before GetExclusions 
			//most of the exclusion logic is processed in ApplyRules on each product
			m_exclusionsEnabled = false;
      if (m_exclusions == null && m_catConditions == null)
          return Environment.NewLine + "No Exclusions";

      var result = string.Format("{0}{1}: ", Environment.NewLine, ExclusionFilename);
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

      var exclusions = new List<ExclusionRecord>();
      if (m_catConditions != null)
				exclusions = m_catConditions.GetExcludedItems().Distinct().Select(x =>
												new ExclusionRecord { Id = x }).ToList();
					
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
			//NOTE: GetCatalog must be called before GetReplacements 
			//most of the exclusion logic is processed in ApplyRules on each product
			if (!m_replacementsEnabled)
				return Environment.NewLine + "Replacements disabled";
			m_replacementsEnabled = false;
      if (m_replacements == null || m_replacements.Count == 0)
        return Environment.NewLine + "No replacements";

      var result = string.Format("{0}{1}: ", Environment.NewLine, ReplacementFilename);
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			//NOTE: catalog replacements are handled in ApplyRules
			if (m_replacements[0].Type.Equals(ReplacementCondition.RepType.Item)) //individual item replacement
      {
        foreach (var rc in m_replacements)
        {
          if (!rc.Type.Equals(ReplacementCondition.RepType.Item))
          {
            m_log.WriteEntry(string.Format("Invalid replacement condition: {0}", rc), EventLogEntryType.Warning, m_alias);
            continue;
          }
					if (!m_repRecords.Any(r => r.OldId.Equals(rc.OldName))) //can only have one replacement for each item
						m_repRecords.Add(new ReplacementRecord { OldId = rc.OldName, NewId = rc.NewName });
        }
      }
			if (m_repRecords.Count < 1)
				return result + "(no data)";

			m_replacementsEnabled = true;
			var countDisplay = string.Format("({0} items) ", m_repRecords.Count);
			ProgressText = tempDisplay + countDisplay + "Uploading...";
			result += countDisplay + m_boostService.WriteTable(m_alias, ReplacementFilename, m_repRecords);
			return result;
    }

    protected override string GetAtt1Names()
    {
      var result = string.Format("{0}{1}: ", Environment.NewLine, Att1Filename);
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			//need to get count and page through as the api limits to 250 max per call
			var countXml = CallCartApi("categories/count", "categories");
			var countVal = countXml.Elements("count").First().Value;
			int iRows = Convert.ToInt16(countVal);
			int pages = (int)Math.Ceiling((decimal)iRows / (decimal)250); 	
			var categoriesXml = new List<XElement>(); 
			for (int page = 1; page <= pages; page++)
			{
				string paging = string.Format("?page={0}&limit=250", page);
				var pageXml = CallCartApi("categories" + paging, "categories");
				if (pageXml == null)
					break;
				categoriesXml.AddRange(pageXml.Elements("category"));
			}
      if (!categoriesXml.Any())
          return result + "(no data)";

      var categories = new List<AttributeRecord>();
			categories.AddRange(categoriesXml.Select(c => new AttributeRecord 
																	{ 
																		Id = Client.GetValue(c, "id"), 
																		Name = Client.GetValue(c, "name").Replace(",", "").Replace("\t", "")
																	}).Distinct());

			var countDisplay = string.Format("({0} categories) ", categories.Count);
			ProgressText = tempDisplay + countDisplay + "Uploading...";
			result += countDisplay + m_boostService.WriteTable(m_alias, Att1Filename, categories);
      return result;
    }

    protected override string GetAtt2Names()
    {
        var result = string.Format("{0}{1}: ", Environment.NewLine, Att2Filename);
				if (!m_secondAttEnabled) 
					return result + "(not used)";

				ProgressText += result;
				string tempDisplay = ProgressText;
				ProgressText += "Exporting...";

				//need to get count and page through as the api limits to 250 max per call
				var countXml = CallCartApi("brands/count", "brands");
				var countVal = countXml.Elements("count").First().Value;
				int iRows = Convert.ToInt16(countVal);
				int pages = (int)Math.Ceiling((decimal)iRows / (decimal)250);
				var brandsXml = new List<XElement>(); 
				for (int page = 1; page <= pages; page++)
				{
					string paging = string.Format("?page={0}&limit=250", page);
					var pageXml = CallCartApi("brands" + paging, "brands");
					if (pageXml == null)
						break;
					brandsXml.AddRange(pageXml.Elements("brand"));
				}
				if (!brandsXml.Any())
					return result + "(no data)";

				var brands = new List<AttributeRecord>();
				brands.AddRange(brandsXml.Select(b => new AttributeRecord 
																	{ 
																		Id = Client.GetValue(b, "id"), 
																		Name = Client.GetValue(b, "name").Replace(",", "").Replace("\t", "")
																	}).Distinct());

				var countDisplay = string.Format("({0} brands) ", brands.Count);
				ProgressText = tempDisplay + countDisplay + "Uploading...";
				result += countDisplay + m_boostService.WriteTable(m_alias, Att2Filename, brands);
        return result;
    }

    #endregion

    private string GetProductCategories(XContainer categories)
    {
			if (categories == null) return string.Empty;

    	var catList = m_catConditions.RemoveIgnored(categories.Elements("value").Select(x => x.Value));
    	catList = catList.Union(m_catConditions.GetAllParents(catList.ToList()));
      return catList.Aggregate((w, j) => string.Format("{0},{1}", w, j));
    }

	  private static string GetProductImage(List<XElement> imagesXml, string productId)
		{
			//pulls the thumbnail image for the product
      if (imagesXml == null)
          return string.Empty;

			string iLink = string.Empty;
			try
			{
				var idMatches = imagesXml.Where(x => Client.GetValue(x, "product_id").Equals(productId));
				iLink = idMatches.Where(x => Client.GetValue(x, "is_thumbnail").Equals("true")).Select(x => Client.GetValue(x, "image_file")).DefaultIfEmpty("").First();

				if (string.IsNullOrEmpty(iLink)) //no thumbnails so grab first image in list
					iLink = Client.GetValue(idMatches.First(), "image_file");
			}
			catch { }
			return iLink == null ? string.Empty: string.Format("/product_images/{0}", iLink);
			//TODO: provide means for clients to set an exclusion rule if image link empty
		}

		private static List<XElement> GetProductCustomFields(List<XElement> customFieldXml, string productId)
		{
			//pulls all custom fields for the product
			var customfields = new List<XElement>();

			if (customFieldXml != null)
			{
				try
				{
					var fields = customFieldXml.Where(x => Client.GetValue(x, "product_id").Equals(productId)).ToList();
					if (fields.Any())
					{
						//need to construct new XML where the custom field name and text form the elements
						foreach (var f in fields)
						{
							var temp = f.Element("name");
							if (temp == null) continue;
							var name = (XName) temp.Value;
							temp = f.Element("text");
							if (temp == null) continue;
							var text = temp.Value;

							customfields.Add(new XElement(name, text));
						}
					}
				}
				catch {}
			}
			return customfields;
		}

		private void SetCategoryParents(IEnumerable<XElement> categories)
		{
			foreach (var cat in categories)
			{
				var temp = cat.Element("id");
				if (temp == null) continue;
				var child = temp.Value;
				temp = cat.Element("parent_category_list");
				if (temp == null) continue;
				var parents = temp.Value.Split(',').Select(p => p.Trim()).ToList();
				m_catConditions.AddParents(child, parents);
			}
		}

	}
}