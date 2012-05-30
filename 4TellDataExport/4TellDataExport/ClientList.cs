using System;
using System.Collections; //ArrayList
using System.Collections.Generic;
using System.Xml.Linq;	//XElement
using System.Linq;
using System.Web;
using System.Threading;

namespace _4_Tell
{
	using Utilities;
	using IO;

	public enum CartType
	{
		Magento,
		ThreeDCart,
		osCommerce,
		WebsitePipeline,
		Volusion,
		BigCommerce,
		MivaMerchant,
		PrestaShop,
		other
	}

	public enum MagentoLevel
	{
		Go,
		Community,
		Pro,
		Enterprise
	}

	public enum ThreeDCartLevel
	{
		Standard, //Access
		Enterprise //SQL Server?
	}

	public class Client
	{
		public bool InProgress = false;
		public string Alias;
		public string PocName;
		public string PocEmail;
		public string ReportLevel;
		public List<string> UploadAddresses;
		public CartType CartType;
		public int CartLevel; //must translate to/from level for specific cart
		public bool IsValid { get; set; }
		public bool HasData { get; set; }
		private CartExtractor m_cart; //cart specific settings are stored in the cart extractor


		public Client(XElement settings, bool createCart = true)
		{
			Alias = GetValue(settings, "alias");
			PocName = GetValue(settings, "pocName");
			PocEmail = GetValue(settings, "pocEmail");
			ReportLevel = GetValue(settings, "reportLevel");

			IEnumerable<XElement> ipList = settings.Elements("approvedUploadIP");
			if (ipList != null)
				UploadAddresses = ipList.Select(ip => ip.Value).ToList<string>();

			CartLevel = 0;
			m_cart = null;
			if (createCart)
			{
				try
				{
					CartType = (CartType)Enum.Parse(typeof(CartType), GetValue(settings, "cartType"), true);
					string level = GetValue(settings, "cartLevel");
					switch (CartType)
					{
						case CartType.Magento:
							MagentoLevel mLevel = (MagentoLevel)Enum.Parse(typeof(MagentoLevel), level, true);
							CartLevel = (int)mLevel;
							if (mLevel == MagentoLevel.Go)
								m_cart = (CartExtractor)new MagentoExtractor(Alias, settings);
							break;
						case CartType.ThreeDCart:
							ThreeDCartLevel tLevel = (ThreeDCartLevel)Enum.Parse(typeof(ThreeDCartLevel), level, true);
							CartLevel = (int)tLevel;
							m_cart = (CartExtractor)new ThreeDCartExtractor(Alias, settings);
							break;
						case CartType.Volusion:
							CartLevel = 0;
							m_cart = (CartExtractor)new VolusionExtractor(Alias, settings);
							break;
						case CartType.MivaMerchant:
							CartLevel = 0;
							m_cart = (CartExtractor)new MivaMerchantExtractor(Alias, settings);
							break;
						case CartType.BigCommerce:
							CartLevel = 0;
							m_cart = (CartExtractor)new BigCommerceExtractor(Alias, settings);
							break;
						default:
							if (level.Length > 0)
								CartLevel = Convert.ToInt32(level); //default is just a numeric level
							break;
					}
				}
				catch (Exception ex)
				{
					CartType = _4_Tell.CartType.other;
					m_cart = null;
					CartLevel = 0;
				}
			}
		}

		public string GetProgress()
		{
			if (m_cart == null) return "Cart does not exist";
			return m_cart.ProgressText;
		}
		

		public void LogSalesOrder(string orderID, bool useThread = false)
		{
			if (m_cart == null)
				throw new Exception("Cannot log sales for client " + Alias);

			if (useThread)
			{
				ThreadPool.QueueUserWorkItem(delegate(object s)
				{
					m_cart.LogSalesOrder((string)s);
				}, orderID);
			}
			else
			{
				InProgress = true;
				m_cart.LogSalesOrder(orderID);
				InProgress = false;
			}
		}

		public void ExtractData(bool salesUpdate = true, bool allSales = false, bool catalog = false, bool useThread = false)
		{
			if (m_cart == null)
				throw new Exception("Cannot extract data for client " + Alias);

			if (useThread)
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					m_cart.GetData(salesUpdate, allSales, catalog);
				});
			}
			else
			{
				InProgress = true;
				m_cart.GetData(salesUpdate, allSales, catalog);
				InProgress = false;
			}
		}

		public void ExtractUpdate(bool useThread = false)
		{
			if (m_cart == null)
				throw new Exception("Cannot extract data for client " + Alias);

			if (useThread)
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					m_cart.GetDataUpdate();
				});
			}
			else
			{
				InProgress = true;
				m_cart.GetDataUpdate();
				InProgress = false;
			}
		}

		public void ExtractAllData(bool useThread = false)
		{
			if (m_cart == null)
				throw new Exception("Cannot extract data for client " + Alias);

			if (useThread)
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					m_cart.GetAllData();
				});
			}
			else
			{
				InProgress = true;
				m_cart.GetAllData();
				InProgress = false;
			}
		}

		public void ExtractSalesUpdate(bool useThread = false)
		{
			if (m_cart == null)
				throw new Exception("Cannot extract data for client " + Alias);

			if (useThread)
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					m_cart.GetData(true, false, false); //just sales update
				});
			}
			else
			{
				InProgress = true;
				m_cart.GetData(true, false, false);
				InProgress = false;
			}
		}

		public void ExtractCatalog(bool useThread = false)
		{
			if (m_cart == null)
				throw new Exception("Cannot extract data for client " + Alias);

			if (useThread)
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					m_cart.GetData(false, false, true); //just catalog (and exclusions/replacements)
				});
			}
			else
			{
				InProgress = true;
				m_cart.GetData(false, false, true);
				InProgress = false;
			}	
		}

		public void QueueExports(DateTime now)
		{
			if (m_cart == null) return;
			m_cart.QueueExports(now);
		}

		public static bool GetValue(out int value, XElement container, string elementName)
		{
			value = -1; //value only valid if return is true
			try
			{
				string s = GetValue(container, elementName);
				value = Convert.ToInt16(s);
				return true;
			}
			catch
			{
				return false; 
			}
		}

		public static string GetValue(XElement container, string elementName)
		{
			//return container.Descendants().Where(x => x.Name.LocalName.Equals(elementName, StringComparison.CurrentCultureIgnoreCase)).Select(x => x.Value).DefaultIfEmpty("").Single();
			try
			{
				var e = container.Descendants().Where(x => x.Name.LocalName.Equals(elementName, StringComparison.CurrentCultureIgnoreCase)).DefaultIfEmpty(null).Single();
				//var e = container.Element(elementName); //doesn't work if there is a case mismatch --problem because API returns titlecase tags and manual export is lowercase
				if (e != null)
					return e.Value;
			}
			catch { }
			return ""; 
		}

		public static string GetAttribute(XElement element, string attributeName)
		{
			//return element.Attributes().Where(x => x.Name.LocalName.Equals(attributeName, StringComparison.CurrentCultureIgnoreCase)).Select(x => x.Value).DefaultIfEmpty("").Single();
			try
			{
				foreach (XAttribute xa in element.Attributes())
					if (xa.Name.LocalName.Equals(attributeName, StringComparison.CurrentCultureIgnoreCase))
						return xa.Value;
			}
			catch { }
			return "";
		}
	}


	public class ClientList
	{
		private const string m_settingsFileName = "ClientSettings.xml";
		private List<Client> m_clients;
		//private License m_license;
		private static readonly ClientList m_instance = new ClientList();
		public static ClientList Instance { get { return m_instance; } }
		
		private ClientList() //singleton
		{
			LoadClients();
		}

		public string LoadClients()
		{
			m_clients = new List<Client>();

			string dataPath = DataPath.Instance.Root;
			XElement settings = null;
			try{
				settings = XElement.Load(dataPath + m_settingsFileName);
			} catch { }
			if (settings == null)
				return "Error reading Client Settigns"; //nothing to do

			var clientSettings = settings.Elements("client");
			if ((clientSettings == null) || (clientSettings.Count() < 1))
				return "No clients found in Client Settings"; //nothing to do

			string results = "";
			foreach (XElement x in clientSettings)
			{
				try
				{
					//make sure settings are enabled and contain an alias name
					string test = Client.GetAttribute(x, "enabled");
					bool enabled = (test.Equals("true", StringComparison.CurrentCultureIgnoreCase) || test.Equals("0"));
					string alias = Client.GetValue(x, "alias");
					if (enabled && alias.Length > 0)
					{
						Client c = new Client(x);
						m_clients.Add(c);
						results += string.Format("Client {0} created with cart type {1}{2}", alias, c.CartType, Environment.NewLine);
					}
				}
				catch { }
			}
			if (m_clients.Count < 1)
				results += "No enabled clients found in Client Settings";
			#if DEBUG
				BoostLog.Instance.WriteEntry(results, System.Diagnostics.EventLogEntryType.Information);
			#endif
			return results;
		}

		public int Count
		{ get { return (m_clients == null) ? 0 : m_clients.Count; } }

		public void Add(Client client)
		{
			m_clients.Add(client);
		}

		public Client Get(string alias)
		{
            var result = from n in m_clients
                            where n.Alias.Equals(alias)
                            select n;
                         
            return (Client) result.First();

		}

		public Client Get(int index)
		{
			if (index >= m_clients.Count) return null;

			return m_clients.ElementAt(index);
		}

		public ArrayList GetAliasList()
		{
			ArrayList aliasList = new ArrayList();
           
			foreach (Client c in m_clients)
				aliasList.Add(c.Alias);
			return aliasList;
		}

		public ArrayList GetAliasList(CartType cart)
		{
			ArrayList aliasList = new ArrayList();
			foreach (Client c in m_clients)
			{
				if (c.CartType == cart)
					aliasList.Add(c.Alias);
			}
			return aliasList;
		}

	}


}