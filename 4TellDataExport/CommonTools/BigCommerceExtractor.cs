using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Xml.Linq;
using _4_Tell.Utilities;
using _4_Tell.IO;


namespace _4_Tell
{
  public class BigCommerceExtractor : CartExtractor
  {
		private string m_webServicesBaseUrl;
		private readonly string m_dataPath = string.Empty;
		private readonly string m_orderDetailsFileName = "OrderHistory.xml";
		private readonly Dictionary<string, IEnumerable<XElement>> _xml = new Dictionary<string, IEnumerable<XElement>>();

    public BigCommerceExtractor(string alias, XElement settings) : base(alias, settings)
    {
			base.ParseSettings(settings);

			m_dataPath = DataPath.Instance.ClientDataPath(ref m_alias) + "upload\\";
			m_webServicesBaseUrl = string.Format("https://{0}/api/v2/", m_storeShortUrl);
    }

		public IEnumerable<XElement> CallCartApi(string service, string tagName = null)
    {
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
						//var salesDoc = XDocument.Load(m_dataPath + m_orderDetailsFileName);
						var salesDoc = XDocument.Load(m_dataPath + m_orderDetailsFileName);
						if ((salesDoc != null) && (salesDoc.Root != null))
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

    public override void LogSalesOrder(string orderID)
    {
        throw new NotImplementedException();
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
				List<XElement> imagesXml = new List<XElement>(); //this will hold all images for entire catalog
				for (int page = 1; page <= pages; page++)
				{
					string paging = string.Format("?page={0}&limit=250", page);
					var pageXml = CallCartApi("products/images" + paging, "images");
					if (pageXml == null)
						break;
					imagesXml.AddRange(pageXml.Elements("image"));
				}
#if DEBUG
				if (imagesXml.Count() != iRows)
				{
					var errMsg = string.Format("Error reading BigCommerce images\nReported count = {0}, actual count = {1}",
																					iRows, imagesXml.Count());
					m_log.WriteEntry(errMsg, EventLogEntryType.Information, m_alias);
				}
#endif

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
																	Link = Client.GetValue(product, "custom_url")
																};

							//string iLink = GetProductImage((IEnumerable<XElement>)imagesXml, p.ProductId);
							string iLink = GetProductImage(imagesXml, p.ProductId);
							if (iLink.Length < 1)
							{
								errors++;
								iLink = p.ProductId + ".jpg"; //placeholder
							}
							p.ImageLink = string.Format("/product_images/{0}", iLink);
							p.Att1Id = GetProductCategories(product.Element("categories"));
							if (p.StandardCode == null)
								p.StandardCode = string.Empty;

							//check category conditions, exclusions, and filters
							ApplyRules(ref p, product);

							products.Add(p);
						}
						catch { errors++; }
						ProgressText = string.Format("{0}{1} completed, {2} errors ({3})", tempDisplay, ++pRows, errors, stopWatch.Lap());
					}
				}
				if (products.Count < 1)
					return result + "(no data)";

				var pCount = string.Format("({0} items) ", products.Count);
        ProgressText = string.Format("{0}{1}completed ({2}){3}Uploading to server...", tempDisplay, pCount, stopWatch.Lap(), Environment.NewLine);
				result += m_boostService.WriteTable(m_alias, CatalogFilename, products);
				stopWatch.Stop();
        return result;
    }

    protected override string GetSalesMonth(DateTime exportDate, string filename)
    {
      var stopWatch = new StopWatch(true);

			//get daterange for this month and create query
			DateTime beginDate = new DateTime(exportDate.Year, exportDate.Month, 1);
			DateTime endDate = beginDate.AddMonths(1).AddDays(-1);
			//const string dateFormat = "ddd, dd MMM yyyy HH:mm:ss +0000";
			//string query = string.Format("?min_date_created={0}&max_date_created={1}",
			//                                HttpUtility.UrlEncode(beginDate.ToString(dateFormat)),
			//                                HttpUtility.UrlEncode(endDate.ToString(dateFormat)));

			//Changed to read from manually exported xml file to avoid bandwidth limit on API
			if ((SalesHistory == null) || (SalesHistory.Count() == 0))
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
			//NOTE: GetCatalog must be called before GetExclusions in case there are any categories excluded
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
					
			//moved to GetCatalog because of paging issue
			//if (m_exclusions != null)
			//{
			//    var productsXml = CallCartApi("products");
			//    if (productsXml == null)
			//        return result + "Error reading products.";

			//    foreach (var c in m_exclusions)
			//    {
			//        var subList = c.Evaluate(productsXml.Elements("product"), "id").Select(x =>
			//                            new ExclusionRecord { Id = x }).ToList();
			//        if(subList.Count > 0)
			//        {
			//            exclusions = exclusions.Union(subList).ToList();
			//            m_exclusionsEnabled = true;
			//        }
			//    }
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
				if (m_replacements[0].Type.Equals(ReplacementCondition.repType.item)) //individual item replacement
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
				else //full catalog replacement -- need to retrieve from Cart
				{
            var productsXml = CallCartApi("products");
            if (productsXml == null)
							return result + "Error reading products.";
						else
	            replacements.AddRange(productsXml.Elements("product").Select(p => new ReplacementRecord 
									{ 
										OldId = Client.GetValue(p, m_replacements[0].OldName), 
										NewId = Client.GetValue(p, m_replacements[0].NewName) 
									}));
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
        var result = string.Format("{0}{1}: ", Environment.NewLine, Att1Filename);
				ProgressText += result;
				string tempDisplay = ProgressText;
				ProgressText += "Exporting...";

        var categoriesXml = CallCartApi("categories");
        if (categoriesXml == null)
            return result + "(no data)";

        var categories = new List<AttributeRecord>();
        categories.AddRange(categoriesXml.Elements("category").Select(c => new AttributeRecord 
																							{ 
																								Id = Client.GetValue(c, "id"), 
																								Name = Client.GetValue(c, "name").Replace(",", "") 
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

        var brandsXml = CallCartApi("brands");
        if (brandsXml == null)
					return result + "(no data)";

        var brands = new List<AttributeRecord>();
        brands.AddRange(brandsXml.Elements("brand").Select(b => new AttributeRecord 
																		{ 
																			Id = Client.GetValue(b, "id"), 
																			Name = Client.GetValue(b, "name").Replace(",", "") 
																		}).Distinct());

				var countDisplay = string.Format("({0} brands) ", brands.Count);
				ProgressText = tempDisplay + countDisplay + "Uploading...";
				result += countDisplay + m_boostService.WriteTable(m_alias, Att2Filename, brands);
        return result;
    }

    #endregion

    private static string GetProductCategories(XContainer categories)
    {
        return categories == null ? string.Empty : categories.Elements("value").Select(x => x.Value).Aggregate((w, j) => string.Format("{0},{1}", w, j));
    }

		//private static string GetProductImage(IEnumerable<XElement> imagesXml, string productId)
	  private static string GetProductImage(List<XElement> imagesXml, string productId)
		{
      //pulls the thumbnail image for the product
      if (imagesXml == null)
          return string.Empty;

			string iLink = string.Empty;
			try
			{
				//var idMatches = imagesXml.Elements("image").Select(i => i.Element("product_id")).Where(pid => pid != null).Where(pid => pid.Value.Equals(productId));
				var idMatches = imagesXml.Where(x => Client.GetValue(x, "product_id").Equals(productId));
				//var idMatches = new List<XElement>();
				//foreach (XElement x in imagesXml)
				//  if (Client.GetValue(x, "product_id").Equals(productId))
				//    idMatches.Add(x);
				if (idMatches == null) 
					return string.Empty;

				iLink = idMatches.Where(x => Client.GetValue(x, "is_thumbnail").Equals("true")).Select(x => Client.GetValue(x, "image_file")).DefaultIfEmpty("").First();

				if (string.IsNullOrEmpty(iLink)) //no thumbnails so grab first image in list
					iLink = Client.GetValue(idMatches.First(), "image_file");

			}
			catch { }
			return iLink;
    }
  }
}