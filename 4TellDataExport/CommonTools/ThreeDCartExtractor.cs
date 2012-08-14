using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq; //XElement
using System.Diagnostics; //EventlogEntryType


namespace _4_Tell
{
	using Utilities;
	using ws_3dCartApi;

	/// <summary>
	/// Summary description for ThreeDCartExtractor
	/// </summary>
	public class ThreeDCartExtractor : CartExtractor
	{
		#region Internal Parameters
		private readonly ThreeDCartLevel m_cartLevel;
		private readonly string m_imageField;
		private static readonly object m_3dApiLock = new object();
		#endregion


		public ThreeDCartExtractor(string alias, XElement settings)	: base(alias, settings, true)
		{
			base.ParseSettings(settings);

			//Add any 3dCart specific construction here
			try
			{
				m_cartLevel = (ThreeDCartLevel)Enum.Parse(m_cartLevel.GetType(), Client.GetValue(settings, "cartLevel"));
			}
			catch { m_cartLevel = ThreeDCartLevel.Standard; }
			m_imageField = Client.GetValue(settings, "imageField");
			if (m_imageField.Length < 1) m_imageField = "thumbnail";
		}

		public override void LogSalesOrder(string orderId)
		{
			var stopWatch = new StopWatch(true);
			string result = "AutoSalesLog: ";
			ProgressText = result + "Exporting single sale...";

			try
			{
				//Query 3dCart for the data
				string sqlQuery = "SELECT oi.catalogid, oi.numitems, o.odate, o.ocustomerid, o.oemail, o.orderid\n"
									+ "FROM orders AS o\n"
									+ "INNER JOIN oitems AS oi\n"
									+ "ON oi.orderid = o.orderid\n"
									+ "WHERE o.orderid = " + orderId
									+ "AND o.order_status <> 7"; //order status of 7 is an incomplete record
				IEnumerable<XElement> queryResults = Get3dCartQuery(sqlQuery);
				if (queryResults == null)
				{
					ProgressText = result + "(no data)";
					return;
				}

				ProgressText = result + "Uploading...";			
				foreach (XElement x in queryResults)
				{
					string productID = Client.GetValue(x, "catalogid");
					string customerID = Client.GetValue(x, "ocustomerid");
					string emailHash = Client.GetValue(x, "oemail");
					if (emailHash.Length > 0) //create hash of email
						emailHash = GetHash(emailHash);
					if (customerID.Equals("0")) //no ocustomerid
					{
						if (emailHash.Length > 0)
							customerID = emailHash;
						else //no oemail
							customerID = "OID" + Client.GetValue(x, "orderid");
					}
					string q = Client.GetValue(x, "numitems");
					int quantity = Convert.ToInt32(q);
					string d = Client.GetValue(x, "odate");
					DateTime date = DateTime.Parse(d);

					//upload to 4-Tell
					UsageLog.Instance.LogSingleAction(m_alias, productID, customerID, quantity, date);
				}
				result += "Complete";
			}
			catch (Exception ex)
			{
				string errMsg = ex.Message;
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
			ProgressText += result + "Rows to export...";
			var pdWatch = new StopWatch(true);

			//Query 3dCart for the data
			//Note: Can't query for it all at once (times out on server)

			//See how many rows there are
			int targetRows = 0;
			string sqlQuery = "SELECT COUNT(*) FROM products";
			IEnumerable<XElement> queryResults = Get3dCartQuery(sqlQuery);
			if (queryResults == null)
			{
				result +=  "(no data)";
				ProgressText = result;
				return result;
			}

			var child = queryResults.First<XElement>();
			var rowsXml = child.Element("Expr1000") ?? child.Element("field0");
			if (rowsXml != null)
				targetRows = Convert.ToInt32(rowsXml.Value);

			queryResults = null;
			ProgressText += targetRows.ToString();
			ProgressText += " (" + pdWatch.Lap() + ")";
			string tempDisplay1 = ProgressText + "\nProduct/Category pairs...";
			pdWatch.Start(); //restart to time cat export

			//Get all product/category pairs
			ProgressText += "\nRetrieving Product/Category pairs...";
			string tempDisplay2 = ProgressText;

			//need to step through category list in chunks since there may be a very large number
			IEnumerable<XElement> catResults = null;
			int catRows = 0;
			string catID = "0";
			while (true)
			{
				string categorySql = "SELECT TOP 5000 pc.catalogid, pc.categoryid\n"
									+ "FROM product_category AS pc\n"
									+ "WHERE pc.catalogid > " + catID + "\n"
									+ "ORDER BY pc.catalogid";
				queryResults = Get3dCartQuery(categorySql);
				if (queryResults == null)
					break; //no more data

				int tempCount = queryResults.Count();
				if (tempCount > 0)
				{
					catResults = catResults == null ? queryResults : catResults.Concat(queryResults);

					//get the last cat ID from this query so next query can start there
					XElement x = queryResults.ElementAt(tempCount - 1);
					catID = Client.GetValue(x, "catalogid");

					//update count
					catRows = catResults.Count();
					ProgressText = tempDisplay2 + catRows.ToString();
					pdWatch.Lap();
					ProgressText += " (" + pdWatch.TotalTime + ")";
				}
				if (tempCount < 5000) 
					break; //all rows found
			}

			ProgressText = tempDisplay1 + catRows.ToString();
			ProgressText += " (" + pdWatch.TotalTime + ")";
			tempDisplay1 = ProgressText + "\nRows Exported...";
			ProgressText += "\nExporting...";
			pdWatch.Start(); //restart to time row export
			var products = new List<ProductRecord>();

			int exportedRows = 0;
			int categorizedRows = 0;
			int lastId = 0;
			int errors = 0;
			bool hasResults = false;
			char[] illegalChars = { '\r', '\n'}; //list any illegal characters for the product name here

			//compile the list of fields that need to be extracted for each item in the catalog
			var fields = new List<string>() 
										{ "catalogid", 
											"name", 
											"price", 
											"saleprice", 
											"review_average", 
											"onsale", 
											"eproduct_serial", 
											m_imageField 
										};
			if (m_secondAttEnabled) 
				fields.Add(m_secondAttField);
			List<string> extraFields = GetRuleFields();
			if (extraFields.Count > 0)
				fields = fields.Union(extraFields).Distinct().ToList();

			//Loop through catalog grabbing 5000 items at a time from 3dCart
			string sqlQueryStart = "SELECT TOP 5000 p." + fields.Aggregate((w, j) => string.Format("{0}, p.{1}", w, j)); 
			sqlQueryStart += "\nFROM products AS p\n";
			while (true)
			{
				sqlQuery = sqlQueryStart
									+ "WHERE p.catalogid > " + lastId.ToString() + "\n"
									+ "ORDER BY p.catalogid";
				queryResults = Get3dCartQuery(sqlQuery);
				if (queryResults == null)
					break; //no more data

				exportedRows += queryResults.Count();
				pdWatch.Lap();
				ProgressText = tempDisplay1 + exportedRows.ToString();
				tempDisplay2 = ProgressText;
				ProgressText += " (" + pdWatch.TotalTime + ")";
				string productID = "";
				foreach (XElement product in queryResults)
				{
					categorizedRows++;
					productID = Client.GetValue(product, "catalogid");
					string name = RemoveIllegalChars(Client.GetValue(product, "name"), illegalChars);
					try
					{
						var p = new ProductRecord
															{
																Name = name,
																ProductId = productID,
																Link = "product.asp?itemid=" + productID,
																ImageLink = string.Empty,
																Att1Id = string.Empty,
																Att2Id = string.Empty,
																Price = Client.GetValue(product, "price"),
																SalePrice = string.Empty,
																Filter = string.Empty,
																Rating = Client.GetValue(product, "review_average"),
																StandardCode = Client.GetValue(product, "eproduct_serial"),
															};

						//create image link
						string thumb = Client.GetValue(product, m_imageField);
						if (thumb.Length > 0)
						{
							int doublehash = thumb.IndexOf("//");
							if (doublehash >= 0) //thumb contains full url
								p.ImageLink = thumb.Substring(doublehash); //drop http protocol if present
							else //thumb contains asset location
							{
								p.ImageLink = "thumbnail.asp?file=";
								if (!thumb.StartsWith("/")) p.ImageLink += "/";
								p.ImageLink += thumb;
							}
						}
						else //no thumbnail found so try using product id
							p.ImageLink = "thumbnail.asp?file=/assets/images/" + p.ProductId + ".jpg";

						//need to query product_category table for each product to get it's list of categories
						//Note: these are all retrieved in advance in an XElement so we don't call 3dCart 30K times for this
						if (catResults != null)
						{
							List<string> catList = catResults.Where(x => Client.GetValue(x, "catalogid").Equals(productID)).Select(y => Client.GetValue(y, "categoryid")).ToList();
							p.Att1Id += catList.Aggregate((w, j) => string.Format("{0},{1}", w, j));
						}

						if (m_secondAttEnabled) 
							p.Att2Id = Client.GetValue(product, m_secondAttField);

						bool onsale = Client.GetValue(product, "onsale").Equals("1");
						if (onsale)
							p.SalePrice = Client.GetValue(product, "saleprice");
					
						//call base class to check category conditions, exclusions, and filters
						ApplyRules(ref p, product);

						products.Add(p);
					}
					catch { errors++; }
					ProgressText = string.Format("{0} {1} completed, {2} errors ({3})", tempDisplay2, categorizedRows, errors, pdWatch.Lap());
				}
				
				lastId = Convert.ToInt32(productID);
				hasResults = true;
				if (categorizedRows == targetRows)
					break; //read all products
			}
			if (products.Count < 1)
				return result + "(no data)";

			var pCount = string.Format("({0} items) ", products.Count);
			ProgressText = string.Format("{0}{1}completed ({2}){3}Uploading to server...", tempDisplay2, pCount, pdWatch.Lap(), Environment.NewLine);
			result += pCount + m_boostService.WriteTable(m_alias, CatalogFilename, products);
			return result;
		}

		protected override string GetSalesMonth(DateTime exportDate, string filename)
		{
      var stopWatch = new StopWatch(true);
			var result = string.Format("{0}{1}: ", Environment.NewLine, filename);
			string tempDisplay = ProgressText;
			ProgressText += result + "Exporting...";

			//Query 3dCart for the data
			DateTime startDate = new DateTime(exportDate.Year, exportDate.Month, 1);
			exportDate = exportDate.AddMonths(1);
			DateTime endDate = new DateTime(exportDate.Year, exportDate.Month, 1);
			string datequery = "";
			switch (m_cartLevel)
			{
				case ThreeDCartLevel.Standard: //Access
					datequery = "WHERE o.odate >= #" + startDate.ToString("MM/dd/yyyy")
										+ "# AND o.odate < #" + endDate.ToString("MM/dd/yyyy") + "#\n";
					break;
				case ThreeDCartLevel.Enterprise: //MS SQL
					datequery = "WHERE o.odate BETWEEN '" + startDate.ToString("yyyy-MM-dd")
										+ "' AND '" + endDate.ToString("yyyy-MM-dd") + "'\n";
					break;
			}
			string sqlQuery = "SELECT oi.catalogid, oi.numitems, o.odate, o.ocustomerid, o.oemail, o.orderid\n"
								+ "FROM orders AS o\n"
								+ "INNER JOIN oitems AS oi\n"
								+ "ON oi.orderid = o.orderid\n"
								+ datequery
								+ "AND o.order_status <> 7"; //order status of 7 is an incomplete record
			IEnumerable<XElement> queryResults = Get3dCartQuery(sqlQuery);
			if (queryResults == null)
				return result + "(no data)";

			int oCount = queryResults.Descendants().Count();
			result += string.Format("({0} orders) ", oCount);
			ProgressText = tempDisplay + result + "Parsing...";
			tempDisplay = ProgressText;

			var orders = new List<SalesRecord>();
			int errors = 0, rows = 0;
			foreach (XElement x in queryResults)
			{
				try
				{
					string customerID = Client.GetValue(x, "ocustomerid");
					if (customerID.Equals("0")) //no ocustomerid
					{
						string emailHash = Client.GetValue(x, "oemail"); 
						if (emailHash.Length > 0) //create hash of email
							emailHash = GetHash(emailHash);
						if (emailHash.Length > 0)
							customerID = emailHash;
						else //no oemail
							customerID = "OID" + Client.GetValue(x, "orderid");
					}
					orders.Add(new SalesRecord {
													ProductId = Client.GetValue(x, "catalogid"),
													CustomerId = customerID,
													Quantity = Client.GetValue(x, "numitems"),
													Date = Client.GetValue(x, "odate")
												});
				}
				catch { errors++; }
				ProgressText = string.Format("{0}{1} rows, {2} errors ({3})", tempDisplay, ++rows, errors, stopWatch.Lap());
			}
			queryResults = null; //no longer needed

			//upload to 4-Tell
			ProgressText = string.Format("{0}Completed.{1}Uploading to server...", tempDisplay, Environment.NewLine);
			result += m_boostService.WriteTable(m_alias, filename, orders);
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

			//this section is now handled in GetCatalog by using ApplyRules
			//if (m_exclusions != null)
			//{
			//  //Query 3dCart for the data
			//  string sqlQuery = "SELECT catalogid\nFROM products\nWHERE ";
			//  bool first = true;
			//  foreach (Condition c in m_exclusions)
			//  {
			//    if (first) first = false;
			//    else sqlQuery += "\nOR ";
			//    sqlQuery += c.GetSqlEquation();
			//  }
			//  IEnumerable<XElement> queryResults = Get3dCartQuery(sqlQuery);
			//  if (queryResults != null)
			//  {
			//    foreach (XElement x in queryResults)
			//    {
			//      string id = Client.GetValue(x, "catalogid");
			//      if (id.Length > 0)
			//        exclusions.Add(new ExclusionRecord { Id = id });
			//    }
			//  }
			//  queryResults = null; //no longer needed
			//}
			if (exclusions.Count < 1)
				return result + "(no data)";

			m_exclusionsEnabled = true;
			var eCount = string.Format("({0} items) ", exclusions.Count);
			ProgressText = tempDisplay + eCount + "Uploading...";
			result += eCount + m_boostService.WriteTable(m_alias, ExclusionFilename, exclusions);
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
			if (m_replacements[0].Type != ReplacementCondition.RepType.Catalog) //individual item replacement
			{
				foreach (ReplacementCondition rc in m_replacements)
				{
					if (rc.Type != ReplacementCondition.RepType.Item)
					{
						m_log.WriteEntry("Invalid replacement condition: " + rc.ToString(), EventLogEntryType.Warning, m_alias);
						continue;  //ignore any invalid entries 
					}
					replacements.Add(new ReplacementRecord { OldId = rc.OldName, NewId = rc.NewName });
				}
			}
			else //full catalog replacement -- need to retrieve from 3dCart
			{
				//Query 3dCart for the data
				string oldfield = m_replacements[0].OldName;
				string newfield = m_replacements[0].NewName;
				string sqlQuery = "SELECT "
										+ oldfield + ", "
										+ newfield + " FROM products";
				IEnumerable<XElement> queryResults = Get3dCartQuery(sqlQuery);
				if (queryResults == null)
					return result + "Error reading products.";
				
				replacements.AddRange(queryResults.Select(p => new ReplacementRecord
				                                               	{
				                                               		OldId = Client.GetValue(p, oldfield),
				                                               		NewId = Client.GetValue(p, newfield)
				                                               	}));
			}
			if (replacements.Count < 1)
				return result + "(no data)";

			m_replacementsEnabled = true;
			ProgressText = tempDisplay + "Uploading...";
			result += m_boostService.WriteTable(m_alias, ReplacementFilename, replacements);
			return result;
		}

		protected override string GetAtt1Names()
		{
			var result = string.Format("{0}{1}: ", Environment.NewLine, Att1Filename);
			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			//Query 3dCart for the data
			const string sqlQuery = "SELECT c.id, c.category_name\n"
			                        + "FROM category AS c";
			IEnumerable<XElement> queryResults = Get3dCartQuery(sqlQuery);
			if (queryResults == null)
				return result + "(no data)"; //no data matches rules

			var categories = new List<AttributeRecord>();
			categories.AddRange(queryResults.Select(c => new AttributeRecord
														{
															Id = Client.GetValue(c, "id"),
															Name = Client.GetValue(c, "category_name").Replace(",", "")
														}).Distinct());

			queryResults = null; //no longer needed

			ProgressText = tempDisplay + "Uploading...";
			result += m_boostService.WriteTable(m_alias, Att1Filename, categories);
			return result;
		}

		protected override string GetAtt2Names()
		{
			string result = "\n" + Att2Filename + ": ";
			if (!m_secondAttEnabled) return result + "(not used)";

			ProgressText += result;
			string tempDisplay = ProgressText;
			ProgressText += "Exporting...";

			//Query 3dCart for the data
			const string sqlQuery = "SELECT m.id, m.manufacturer\n"
			                        + "FROM manufacturer AS m";
			IEnumerable<XElement> queryResults = Get3dCartQuery(sqlQuery);
			if (queryResults == null)
				return result + "(no data)"; //no data matches rules

			var brands = new List<AttributeRecord>();
			brands.AddRange(queryResults.Select(b => new AttributeRecord
													{
														Id = Client.GetValue(b, "id"),
														Name = Client.GetValue(b, "manufacturer").Replace(",", "")
													}).Distinct());

			ProgressText = tempDisplay + "Uploading...";
			result += m_boostService.WriteTable(m_alias, Att2Filename, brands);
			return result;
		}

		public void QueryTest(string query)
		{
			string result = "";

			try
			{
				IEnumerable<XElement> queryResults = Get3dCartQuery(query);
				if (queryResults == null)
				{
					result = "no results";
					return;
				}
				result = queryResults.Aggregate(result, (current, x) => current + x.ToString());
			}
			catch (Exception ex)
			{
				string errMsg = ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception: " + ex.InnerException.Message;

				result += errMsg;
			}
			finally
			{
				ProgressText = result;
				//WorkerDone = true;
			}
		}

		private IEnumerable<XElement> Get3dCartQuery(string query)
		{
			XElement xmlResult;
			lock (m_3dApiLock) //make sure only one thread at a time is accessing this API 
			{
				var soapClient = new cartAPIAdvancedSoapClient();
				xmlResult = soapClient.runQuery(m_apiUrl, m_apiKey, query, "");
			}
			if (xmlResult == null)
				throw new Exception("Error: during 3dCart data query --no response");
			if ((xmlResult.Value.EndsWith("returned no records)")) //Access version
				|| (xmlResult.Value.EndsWith("returned no records"))) //SQL Version
			{
				return null; //no data
			}

			var error = xmlResult.Element("Error"); //if (xmlResult.Name.LocalName.Equals("Error"))
			if (error != null && !error.IsEmpty)
			{
				string errMsg = "Error during 3dCart data query: ";
				var id = error.Element("Id");
				if (id != null)
					errMsg += "Id = " + id.Value;
				errMsg += " Description = ";
				var description = error.Element("Description");
				if (description != null)
					errMsg += description.Value;
				else 
					errMsg += error.Value;
				throw new Exception(errMsg);
			}
			
			var tempResults = xmlResult.Elements("runQueryRecord");
			if (tempResults == null || !tempResults.Any())
				return null; //no data

			return tempResults;
		}

	}
}