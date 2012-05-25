using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;

namespace _4_Tell
{
	using Utilities;
	using IO;

  public class MivaMerchantExtractor : CartExtractor
  {
		private const string m_orderDetailsFileName = "orders.dat";
		private const string m_catalogExportFileName = "products.csv";
		private const string m_categoriesFileName = "categories.csv";
		private readonly string m_dataPath = string.Empty;

    private readonly string _productsFilePath = string.Empty;
    private readonly string _photoBaseUrl = string.Empty;
    private readonly string m_storeLongUrl = string.Empty;
    private readonly string _categoriesFilePath = string.Empty;
    private readonly string _ordersFilePath = string.Empty;

		private IEnumerable<VOrder> m_orderHistory = null;
		private IEnumerable<VOrder> OrderHistory
		{
			get
			{
				if (m_orderHistory == null)
				{
					m_orderHistory = LoadTabDelimitedFile(_ordersFilePath).Select(order => new VOrder
					{
						OrderId = order["ORDER_ID"],
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

    public MivaMerchantExtractor(string alias, XElement settings) : base(alias, settings)
    {
      base.ParseSettings(settings);

			m_dataPath = DataPath.Instance.ClientDataPath(ref m_alias) + "upload\\";
      _ordersFilePath = m_dataPath + m_orderDetailsFileName;
			_productsFilePath = m_dataPath + m_catalogExportFileName;
			_categoriesFilePath = m_dataPath + m_categoriesFileName;
      _photoBaseUrl = Client.GetValue(settings, "photoBaseUrl");
			if (_photoBaseUrl.Length < 1)
				_photoBaseUrl = m_storeLongUrl + "/mm5/";
    }

    protected override string GetCatalog()
    {
        var stopWatch = new StopWatch(true);

        var products = LoadTabDelimitedFile(_productsFilePath).Select(product => new VProduct
                                                                                      {
                                                                                          ProductId = product["PRODUCT_CODE"], 
                                                                                          Name = product["PRODUCT_NAME"], 
                                                                                          Attr1 = product["CATEGORY_CODES"], 
                                                                                          Attr2 = string.Empty,
                                                                                          Price = product["PRODUCT_PRICE"],
                                                                                          SalePrice = product["PRODUCT_PRICE"], 
                                                                                          Rating = string.Empty, 
                                                                                          Filter = string.Empty,
                                                                                          Link = string.Format("{0}/product/{1}.html", m_storeLongUrl, product["PRODUCT_CODE"]),
                                                                                          ImageLink = string.Format("{0}/{1}", _photoBaseUrl, product["PRODUCT_IMAGE"]), 
                                                                                          StandardCode = product["PRODUCT_CODE"]
                                                                                      }).ToList();

				var sb = new StringBuilder(CommonHeader + CatalogHeader);
        foreach (var product in products)
        {
            sb.Append(string.Format("{0}\t", product.ProductId));
            sb.Append(string.Format("{0}\t", product.Name));
            sb.Append(string.Format("{0}\t", product.Attr1));
            sb.Append(string.Format("{0}\t", product.Attr2));
            sb.Append(string.Format("{0}\t", product.Price));
            sb.Append(string.Format("{0}\t", product.SalePrice));
            sb.Append(string.Format("{0}\t", product.Rating));
            sb.Append(string.Format("{0}\t", product.Filter));
            sb.Append(string.Format("{0}\t", product.Link));
            sb.Append(string.Format("{0}\t", product.ImageLink));
            sb.Append(string.Format("{0}\r\n", product.StandardCode));
        }

        stopWatch.Stop();
        return Environment.NewLine + m_boostService.UploadFileTo4Tell(m_alias, "Catalog.txt", sb);
    }

    protected override string GetAtt1Names()
    {
        var stopWatch = new StopWatch(true);

        var categories = LoadTabDelimitedFile(_categoriesFilePath).Select(category => new VCategory
                                                                                          {
                                                                                              Id = category["CATEGORY_CODE"],
                                                                                              Name = category["CATEGORY_NAME"]
                                                                                          });
        var sb = new StringBuilder(CommonHeader + Att1Header);
        foreach (var c in categories.Distinct())
        {
            sb.Append(string.Format("{0}\t", c.Id));
            sb.Append(string.Format("{0}\r\n", c.Name));
        }

        stopWatch.Stop();
        return Environment.NewLine + m_boostService.UploadFileTo4Tell(m_alias, "Attribute1Names.txt", sb);
    }
        
    private static IEnumerable<Dictionary<string, string>> LoadTabDelimitedFile(string path)
    {
        var contents = new List<Dictionary<string, string>>();
        var keys = new List<string>();

        using(var sr = new StreamReader(path))
        {
            string line;
            if(!string.IsNullOrEmpty(line = sr.ReadLine()))
            {
                keys.AddRange(line.Split(new[] {'\t'}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            }

            while(!string.IsNullOrEmpty(line = sr.ReadLine()))
            {
                var content = new Dictionary<string, string>();
                var values = line.Replace("<br>", "").Split(new[] {'\t'});

								string msg;
								if (values.Length != keys.Count())
									msg = "something wrong";

                for (var i = 0; i < values.Length; i++)
                {
                    content.Add(keys[i], values[i].Trim());
                }

                contents.Add(content);
            }
        }

        return contents;
    }

    public override void LogSalesOrder(string orderID)
    {
        throw new NotImplementedException();
    }

    protected override string GetSalesMonth(DateTime exportDate, string filename)
    {
        var stopWatch = new StopWatch(true);

				if ((OrderHistory == null) || (OrderHistory.Count() == 0))
            return string.Format("No Sales for: {0}", exportDate.ToShortDateString());

        var sb = new StringBuilder(CommonHeader + SalesHeader);
				foreach (var order in OrderHistory)
        {
					//check date
					var orderDate = DateTime.Parse(order.Date);
					if (DateTime.Compare(orderDate, new DateTime(exportDate.Year, exportDate.Month, 1)) < 0 ||
							DateTime.Compare(orderDate, new DateTime(exportDate.Year, exportDate.Month, DateTime.DaysInMonth(exportDate.Year, exportDate.Month))) > 0)
						continue;

					sb.Append(string.Format("{0}\t", order.ProductId));
          sb.Append(string.Format("{0}\t", order.CustomerId));
          sb.Append(string.Format("{0}\t", order.Quantity));
          sb.Append(string.Format("{0}\r\n", order.Date));
        }

        stopWatch.Stop();
        return Environment.NewLine + m_boostService.UploadFileTo4Tell(m_alias, string.Format("Sales-{0}.txt", exportDate.ToString("yyyy-MM")), sb);
    }

    protected override string GetExclusions()
    {
        throw new NotImplementedException();
    }

    protected override string GetReplacements()
    {
        throw new NotImplementedException();
    }

    protected override string GetAtt2Names()
    {
        throw new NotImplementedException();
    }

    private class VProduct
    {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public string Attr1 { get; set; }
        public string Attr2 { get; set; }
        public string Price { get; set; }
        public string SalePrice { get; set; }
        public string Filter { get; set; }
        public string Link { get; set; }
        public string ImageLink { get; set; }
        public string Rating { get; set; }
        public string StandardCode { get; set; }
    }

    private class VCategory
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    private class VOrder
    {
        public string OrderId { get; set; }
        public string ProductId { get; set; }
        public string CustomerId { get; set; }
        public string Quantity { get; set; }
        public string Date { get; set; }
    }
  }
}