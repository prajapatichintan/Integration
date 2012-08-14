using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq; //XElement
using System.Text;			//StringBuilder
using System.Threading; //Thread
using System.Security.Cryptography;

namespace _4_Tell
{
	using Utilities;

	#region DataRecord Classes

	public class AttributeRecord
	{
		public string Id { get; set; }
		public string Name { get; set; }

		public string ToString(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("{2}{0}{3}{1}", delimiter, newLine, Id, Name);
		}

		static public string Header(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("ID{0}Name{1}", delimiter, newLine);
		}
	}

	public class ProductRecord
	{
		public string ProductId { get; set; }
		public string Name { get; set; }
		public string Att1Id { get; set; }
		public string Att2Id { get; set; }
		public string Price { get; set; }
		public string SalePrice { get; set; }
		public string Filter { get; set; }
		public string Link { get; set; }
		public string ImageLink { get; set; }
		public string Rating { get; set; }
		public string StandardCode { get; set; }

        public string CategoryIDs { get; set; }
        public string BrandID { get; set; }
        public string ActiveFlag { get; set; }
        public string StockLevel { get; set; }

		public string ToString(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}{0}{10}{0}{11}{0}{12}{1}", 
				delimiter, newLine, ProductId, Name, Att1Id, Att2Id, Price, SalePrice, Filter, Link, ImageLink, Rating, StandardCode);
		}

		static public string Header(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("ProductId{0}Name{0}Att1Id{0}Att2Id{0}Price{0}SalePrice{0}Filter{0}Link{0}ImageLink{0}Rating{0}StandardCode{1}", 
				delimiter, newLine);
		}
	}

	public class SalesRecord
	{
		public string ProductId { get; set; }
		public string CustomerId { get; set; }
		public string Quantity { get; set; }
		public string Date { get; set; }

		public string ToString(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("{2}{0}{3}{0}{4}{0}{5}{1}",
				delimiter, newLine, ProductId, CustomerId, Quantity, Date);
		}

		static public string Header(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("ProductId{0}CustomerId{0}Quantity{0}Date{1}",
				delimiter, newLine);
		}
	}

	public class ExclusionRecord
	{
		public string Id { get; set; }

		public string ToString(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("{2}{1}", delimiter, newLine, Id);
		}

		static public string Header(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("Id{1}", delimiter, newLine);
		}
	}

	public class ReplacementRecord
	{
		public string OldId { get; set; }
		public string NewId { get; set; }

		public string ToString(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("{2}{0}{3}{1}", delimiter, newLine, OldId, NewId);
		}

		static public string Header(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("OldId{0}NewId{1}", delimiter, newLine);
		}
	}

	public class ManualRecommendationRecord
	{
		public string PrimaryId { get; set; }
		public string RecommendedId { get; set; }
		public float Likelihood { get; set; }

		public string ToString(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("{2}{0}{3}{0}{4}{1}", delimiter, newLine, PrimaryId, RecommendedId, Likelihood);
		}

		static public string Header(string delimiter = "\t", string newLine = "\r\n")
		{
			return string.Format("Product Id{0}Rec Id{0}Likelihood{1}", delimiter, newLine);
		}
	}

	#endregion
	/// <summary>
	/// Abstract base class for shopping cart data extractor classes
	/// </summary>
	public abstract class CartExtractor
	{
		#region Support Classes

        public class CategoryRecord
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

		protected class Condition
		{
			private enum Comparitor
			{
				Eq, //	=		(string or numerical)
				Ne, //	!=	(string or numerical)
				Lt, //	<		(numerical only)
				Gt, //	>		(numerical only)
				Le, //	<=	(numerical only)
				Ge, //	>=	(numerical only)
				Contains, //	contains (string only)
				StartsWith,  //	startsWith (string only)
				EndsWith,  //	endsWith (string only)
				Copy // no comparison required just copy the value as a result
			}

			public string Name;
			public string FieldName;
			public string Comparison;
			public string Value;

			public Condition(string name, string fieldName, string comparison, string value)
			{
				Name = name;
				FieldName = fieldName;
				Comparison = comparison;
				Value = value;
			}

			public string GetSqlEquation()
			{
				string equation = FieldName;
				var c = (Comparitor)Enum.Parse(typeof(Comparitor), Comparison, true);
				switch (c)
				{
					case Comparitor.Eq:
						equation += " = ";
						break;
					case Comparitor.Ne:
						equation += " <> ";
						break;
					case Comparitor.Lt:
						equation += " < ";
						break;
					case Comparitor.Gt:
						equation += " > ";
						break;
					case Comparitor.Le:
						equation += " <= ";
						break;
					case Comparitor.Ge:
						equation += " >= ";
						break;
					case Comparitor.Contains: //should not call GetSqlEquation for these types
					case Comparitor.StartsWith: 
					case Comparitor.EndsWith:
					case Comparitor.Copy:
					default:
						throw new Exception("Illegal comparison operator (" + Comparison + ")");
				}
				equation += Value;
				return equation;
			}

			public bool Compare(string actual)
			{
				if (string.IsNullOrEmpty(actual)) return false;

				double a, b = 0;
				bool numerical = IsNumerical(actual, out a) && IsNumerical(Value, out b);

				var c = (Comparitor)Enum.Parse(typeof(Comparitor), Comparison, true);
				switch (c)
				{
					case Comparitor.Eq:
						if (numerical) return (a == b);
						return Value != null && actual.Equals(Value);
					case Comparitor.Ne:
						if (numerical) return (a != b);
						return Value != null && !actual.Equals(Value);
					case Comparitor.Lt:
						if (numerical) return (a < b);
						throw new Exception("Illegal string comparison (" + Comparison + ")");
					case Comparitor.Gt:
						if (numerical) return (a > b);
						throw new Exception("Illegal string comparison (" + Comparison + ")");
					case Comparitor.Le:
						if (numerical) return (a <= b);
						throw new Exception("Illegal string comparison (" + Comparison + ")");
					case Comparitor.Ge:
						if (numerical) return (a >= b);
						throw new Exception("Illegal string comparison (" + Comparison + ")");
					case Comparitor.Contains:
						return Value != null && actual.Contains(Value);
					case Comparitor.StartsWith:
						return Value != null && actual.StartsWith(Value);
					case Comparitor.EndsWith:
						return Value != null && actual.EndsWith(Value);
					case Comparitor.Copy:
						Name = actual;
						return true; //always true, just copy the result
					default:
						throw new Exception("Illegal comparison operator (" + Comparison + ")");
				}
			}

			public List<string> Evaluate(IEnumerable<XElement> items, string returnField)
			{
				//var results = new List<string>();
				//IEnumerable<XElement> matchingRecords =
				//          from row in items
				//          where Compare(Client.GetValue(row, FieldName))
				//          select row;
				//foreach (XElement record in matchingRecords)
				//  results.Add(Client.GetValue(record, returnField));
				var results = items.Where(x => Compare(Client.GetValue(x, FieldName))).Select(x => Client.GetValue(x, returnField)).ToList();
				return results;
			}

			public List<string> Evaluate(IEnumerable<Dictionary<string, string>> items, string returnField)
			{
				IEnumerable<Dictionary<string, string>> matchingRecords =
									from row in items
									where Compare(row[FieldName])
									select row;

				return matchingRecords.Select(record => record[returnField]).ToList();
			}

			public bool IsNumerical(string input, out double dval)
			{
				dval = 0;
				try
				{
					dval = Convert.ToDouble(input);
					return true;
				}
				catch
				{
					return false;
				}
			}
		}

		protected class ReplacementCondition
		{
			public enum RepType
			{
				Catalog,
				Item,
				Invalid
			}

			public string Name;
			public RepType Type;
			public string OldName;
			public string NewName;

			public ReplacementCondition(string name, string type, string oldName, string newName)
			{
				Name = name;
				OldName = oldName;
				NewName = newName;
				try
				{
					Type = (RepType)Enum.Parse(typeof(RepType),type, true);
				}
				catch
				{
					Type = RepType.Invalid;
				}
			}

			public override string ToString()
			{
				return "Name = " + Name + "\nType = " + Type.ToString() + "\nOldName = " + OldName + "\nNewName = " + NewName;
			}
		}

		protected class CategoryConditions
		{
			public struct FilterCatDef
			{
				public string CatId;
				public string GroupId;
				public FilterCatDef(string catid, string groupid) { CatId = catid; GroupId = groupid; }
			}
			private readonly List<string> m_ignoreCats;
			private readonly List<FilterCatDef> m_filterCats;
			private readonly List<string> m_universalCats;
			private readonly List<string> m_excludeCats;
			private readonly List<string> m_excludeItems;
			private readonly Dictionary<string, IEnumerable<string>> m_parentList;
			public bool FiltersExist { get { return m_filterCats.Count > 0; } }

			public CategoryConditions()
			{
				m_ignoreCats = new List<string>();
				m_filterCats = new List<FilterCatDef>();
				m_universalCats = new List<string>();
				m_excludeCats = new List<string>();
				m_excludeItems = new List<string>();
				m_parentList = new Dictionary<string, IEnumerable<string>>();
			}

			public void AddCat(string type, string value, string groupId = null)
			{
				if (string.IsNullOrEmpty(type) || (string.IsNullOrEmpty(value)))
					return;

				if (type.Equals("ignore", StringComparison.CurrentCultureIgnoreCase))
					m_ignoreCats.Add(value);
				else if (type.Equals("exclude", StringComparison.CurrentCultureIgnoreCase))
					m_excludeCats.Add(value);
				else if (type.Equals("filter", StringComparison.CurrentCultureIgnoreCase))
					m_filterCats.Add(new FilterCatDef(value, string.IsNullOrEmpty(groupId) ? value : groupId));
				else if (type.Equals("universal", StringComparison.CurrentCultureIgnoreCase))
					m_universalCats.Add(value);
			}

			public bool Ignored(string value)
			{
				return (m_ignoreCats.Contains(value));
			}

			public bool Excluded(string value)
			{
				return (m_excludeCats.Contains(value));
			}

			public bool Universal(string value)
			{
				return (m_universalCats.Contains(value));
			}

			public List<string> Filtered(string value) //returns a list of all filter groups defined for the given att1ID
			{
				var groups = new List<string>();
				try
				{
					groups = m_filterCats.Where(x => x.CatId.Equals(value)).Select(x => x.GroupId).ToList();
				}
				catch { }
				return groups;
			}

			public bool AnyExcluded(string values) //comma separated list of categories
			{
				if ((m_excludeCats.Count < 1) || (string.IsNullOrEmpty(values)))
					return false;

				var catList = values.Split(',').Select(p => p.Trim()).ToList();
				return catList.Any(Excluded);
			}

			public bool AnyUniversal(string values) //comma separated list of categories
			{
				if (string.IsNullOrEmpty(values))
					return false;

				var catList = values.Split(',').Select(p => p.Trim()).ToList();
				return catList.Any(Universal);
			}

			public string AnyFiltered(string values) //comma separated list of categories
			{
				if (string.IsNullOrEmpty(values))
					return "";

				var catList = values.Split(',').Select(p => p.Trim()).ToList();
				var matches = new List<string>();
				foreach (string cat in catList)
					matches.AddRange(Filtered(cat));
				return matches.Count < 1 ? "" : matches.Aggregate((w, j) => string.Format("{0},{1}", w, j));
			}

			public string RemoveIgnored(string values) //comma separated list of categories
			{
				if ((m_ignoreCats.Count < 1) || (string.IsNullOrEmpty(values)))
					return values;

				var catList = values.Split(',').Select(p => p.Trim()).ToList();

				return catList.Where(cat => !Ignored(cat)).Aggregate((w, j) => string.Format("{0},{1}", w, j));

				//string newValues = "";
				//bool first = true;
				//foreach (string cat in catList.Where(cat => !Ignored(cat)))
				//{
				//  if (first) first = false;
				//  else newValues += ",";
				//  newValues += cat;
				//}
				//return newValues;
			}

			public IEnumerable<string> RemoveIgnored(IEnumerable<string> catList) //list of categories
			{
				return catList.Where(cat => !Ignored(cat)).ToList();
			}

			public void AddExcludedItem(string id)
			{
				if (!m_excludeItems.Contains(id))
					m_excludeItems.Add(id);
			}

			public List<string> GetExcludedItems()
			{
				return m_excludeItems;
			}

			//add one parent/child relationship to the parent list
			public void AddParent(string child, string parent)
			{
				AddParents(child, new List<string> {parent});
			}

			//add a many-parent/child relationship to the parent list
			public void AddParents(string child, IEnumerable<string> newParents)
			{
				//make sure there are no duplicates and child is not one of the new parents
				newParents = newParents.Distinct().Where(p => !p.Equals("0") && !child.Equals(p)); 
				if (!newParents.Any()) return;

				IEnumerable<string> parents;
				if (m_parentList.TryGetValue(child, out parents)) //existing child
				{
					parents = parents.Union(newParents);  //no duplicates added
					m_parentList[child] = parents;
				}
				else //new child
					m_parentList.Add(child, newParents);
			}

			//find all distinct parents and ancestors for a list of categories
			public IEnumerable<string> GetAllParents(IEnumerable<string> catList)
			{
				return catList.Select(GetAllParents).Aggregate(catList, (current, ancestors) => current.Union(ancestors).ToList());
			}

			//find all distinct parents and ancestors for a single category
			public IEnumerable<string> GetAllParents(string child)
			{
				if (!m_parentList.Keys.Contains(child)) return new List<string>(); //no parents

				var closeParents = RemoveIgnored(m_parentList[child]);
				return closeParents.Select(GetAllParents).Aggregate(closeParents, (current, ancestors) => current.Union(ancestors).ToList());
			}
		}

		protected class ManualRecommendations
		{
			public struct ManualRecCondition
			{
				public string Fieldname;
				public bool Include; //false = exclude
				public ManualRecCondition(string fieldname, bool include = true) { Fieldname = fieldname; Include = include; }
			}
			public List<ManualRecCondition> Conditions { get; private set; }
			public List<ManualRecommendationRecord> Records { get; private set; } 
			public bool Enabled { get; private set; }
			private object m_createLock;

			public ManualRecommendations()
			{
				Enabled = false;
				m_createLock = new object();
			}


			public void AddCondition(string fieldname, bool include = true)
			{
				if (!Enabled)
				{
					lock (m_createLock)
					{
						if (!Enabled) //make sure it wasn't created while waiting for the lock
						{
							Enabled = true;
							Conditions = new List<ManualRecCondition>();
							Records = new List<ManualRecommendationRecord>();
						}
					}
				}
				Conditions.Add(new ManualRecCondition(fieldname, include));
			}

			public void AddRecords(string primaryId, XElement product)
			{
				if (!Enabled || !Conditions.Any()) return;

				foreach (var c in Conditions)
				{
					var value = Client.GetValue(product, c.Fieldname);
					if (string.IsNullOrEmpty(value)) continue;

					//NOTE: This assumes one product field with a comma separated list for includes
					//			and a second for excludes for each product
					//			includes will be ranked in order of how they are listed
					var recs = value.Split(',').Select(p => p.Trim()).ToList();
					float likelihood = 0; //zero likelihood means to exclude this item
					if (c.Include) likelihood = 0.99F; //Otherwise items will be ranked extremely high and in order of listing
					foreach (var r in recs)
					{
						Records.Add(new ManualRecommendationRecord
						            	{
						            		PrimaryId = primaryId, 
														RecommendedId = r, 
														Likelihood = likelihood
						            	});
						if (likelihood > 0) likelihood -= 0.01F;
					}
				}
			}
		}

		public class UploadTimer
		{
			public enum UploadRate
			{
				Daily,
				Weekly,
				Monthly
			}

			public bool Enabled = false;
			public bool SalesUpdate = false;
			public bool Catalog = false;
			public UploadRate Rate = UploadRate.Daily;
			public int HourOfDay = 0;
			public DayOfWeek DayOfWeek = DayOfWeek.Monday;
			public int WeekOfMonth = 0;
			public int DayOfMonth = 0;

			public UploadTimer(XElement settings)
			{
				if (settings == null)
				{ 
					//use defaults set above
					return;
				}
				ParseTimerSettings(settings);
			}

			private void ParseTimerSettings(XElement settings)
			{
				string enabled = Client.GetAttribute(settings, "enabled");
				Enabled = enabled.Equals("true", StringComparison.CurrentCultureIgnoreCase);

				string salesUpdate = Client.GetAttribute(settings, "salesUpdate");
				SalesUpdate = salesUpdate.Equals("true", StringComparison.CurrentCultureIgnoreCase);

				string catalog = Client.GetAttribute(settings, "catalog");
				Catalog = catalog.Equals("true", StringComparison.CurrentCultureIgnoreCase);

				string rate = Client.GetAttribute(settings, "rate");
				try
				{
					Rate = (UploadRate)Enum.Parse(typeof(UploadRate), rate, true);
				}
				catch
				{
					Rate = UploadRate.Daily;
				}

				string hourOfDay = Client.GetAttribute(settings, "hourOfDay");
				try
				{
					HourOfDay = Convert.ToInt16(hourOfDay);
				}
				catch
				{
					HourOfDay = 0;
				}

				string dayOfWeek = Client.GetAttribute(settings, "dayOfWeek");
				try
				{
					DayOfWeek = (DayOfWeek)Enum.Parse(typeof(DayOfWeek), dayOfWeek, true);
				}
				catch
				{
					DayOfWeek =  DayOfWeek.Monday;
				}

				string weekOfMonth = Client.GetAttribute(settings, "weekOfMonth");
				try 
				{
					WeekOfMonth = Convert.ToInt16(weekOfMonth);
				}
				catch
				{
					WeekOfMonth = 1;
				}

				string dayOfMonth = Client.GetAttribute(settings, "dayOfMonth");
				try
				{
					DayOfMonth = Convert.ToInt16(dayOfMonth);
				}
				catch
				{
					DayOfMonth = 0;
				}
			}

			public bool IsItTime(DateTime now)
			{
				if (!Enabled) return false;
				if (now.Hour != HourOfDay) return false;
				if (Rate == UploadRate.Daily) return true;
				if (DayOfMonth > 0)
				{
					if (now.Day == DayOfMonth) return true;
					return false;
				}
				if (now.DayOfWeek != DayOfWeek) return false;
				if (Rate == UploadRate.Weekly) return true;
				//monthly
				switch (WeekOfMonth)
				{
					case 1:
						if (now.Day < 8) return true;
						break;
					case 2:
						if ((now.Day > 7) && (now.Day < 15)) return true;
						break;
					case 3:
						if ((now.Day > 14) && (now.Day < 22)) return true;
						break;
					case 4:
						if ((now.Day > 21) && (now.Day < 29)) return true;
						break;
					case 5:
						if (now.Day > 28) return true;
						break;
					default:
						return false;
				}
				return false;
			}
		}

		#endregion

		#region Default Values
		private const int defaultMonths = 18;
		private const int defaultMinLikelihood = 3;
		private const int defaultMinCommon = 2;
		#endregion

		#region Internal Params
		protected const string m_dataVersion = "3"; //data file format version

		protected const string CatalogFilename = "Catalog.txt";
		protected const string Att1Filename = "Attribute1Names.txt";
		protected const string Att2Filename = "Attribute2Names.txt";
		protected const string ExclusionFilename = "Exclusions.txt";
		protected const string ReplacementFilename = "Replacements.txt";
		protected const string ManualCrossSellFilename = "CrossSell_x.txt";
		protected const string ManualUpSellFilename = "UpSell_x.txt";
		protected const string SalesFilenameFormat = "Sales-{0}.txt";

		protected RestAccess m_boostService;
		protected static readonly MD5 m_hash = MD5.Create();

		protected BoostLog m_log = BoostLog.Instance;

		protected string m_cartName;
		protected string m_alias;
		protected string m_pocName;
		protected string m_pocEmail;
		protected string m_reportLevel;
		protected string m_storeShortUrl;
		protected string m_apiUrl;
		protected string m_apiUserName;
		protected string m_apiKey;
		protected int m_monthsToExport;
		protected bool m_resell;
		protected int m_minLikelihood;
		protected int m_minCommon;
		protected bool m_secondAttEnabled;
		protected string m_secondAttField;
		protected List<Condition> m_exclusions;
		protected List<Condition> m_filters;
		protected CategoryConditions m_catConditions;
		protected List<ReplacementCondition> m_replacements;
		protected List<ReplacementRecord> m_repRecords;
		protected ManualRecommendations m_manualCrossSell;
		protected ManualRecommendations m_manualUpSell;
		protected bool m_exclusionsEnabled;
		protected bool m_replacementsEnabled;
		protected bool m_filtersEnabled;
		protected string m_rulesEnabled;
		protected string m_universalFilterName;
		protected List<UploadTimer> m_uploadTimers;
        protected List<ReplacementRecord> m_repReplacements;

		private bool m_extractSalesUpdate;
		#endregion

		static public string CommonHeader //first header line in export file (set date dynamically each time it is used)
		{ get { return string.Format("Version\t{0}\t{1}\r\n", m_dataVersion, DateTime.Now.ToShortDateString()); } }
		public string ProgressText = "";

		private CartExtractor()
		{
			throw new ArgumentNullException("CartExtractor cannot use a default constructor");
		}

		#region Base Methods

		protected CartExtractor(string alias, XElement settings, bool extractSalesUpdate = false)
		{
			m_alias = alias;
			m_extractSalesUpdate = extractSalesUpdate;
			m_boostService = RestAccess.Instance;
			m_catConditions = new CategoryConditions();
			m_repRecords = new List<ReplacementRecord>();
			m_manualCrossSell = new ManualRecommendations();
			m_manualUpSell = new ManualRecommendations();
		}

		protected virtual void ParseSettings(XElement settings)
		{
			m_cartName = Client.GetValue(settings, "cartType");
			m_pocName = Client.GetValue(settings, "pocName");
			m_pocEmail = Client.GetValue(settings, "pocEmail");
			m_reportLevel = Client.GetValue(settings, "reportLevel");
			m_storeShortUrl = Client.GetValue(settings, "storeUrl");
			if (m_storeShortUrl.Length > 0)
			{
				if (m_storeShortUrl.StartsWith("http"))
					m_storeShortUrl = m_storeShortUrl.Remove(0, m_storeShortUrl.IndexOf("//") + 2); //remove prefix
				if (m_storeShortUrl.EndsWith("/"))
					m_storeShortUrl = m_storeShortUrl.Remove(m_storeShortUrl.Length - 1); //remove final slash
			}
			m_apiUrl = Client.GetValue(settings, "apiUrl");
 			if (m_apiUrl.Length < 1)
				m_apiUrl = m_storeShortUrl;
			m_apiUserName = Client.GetValue(settings, "apiUserName");
			m_apiKey = Client.GetValue(settings, "apiKey");
			if (!Client.GetValue(out m_monthsToExport, settings, "monthsToExport"))
				m_monthsToExport = defaultMonths;
			string resell = (Client.GetValue(settings, "resell"));
			m_resell = (resell.Equals("1") || resell.Equals("true")); //defaults is false
			if (!Client.GetValue(out m_minLikelihood, settings, "minLikelihood"))
				m_minLikelihood = defaultMinLikelihood;
			if (!Client.GetValue(out m_minCommon, settings, "minCommon"))
				m_minCommon = defaultMinCommon;
			XElement sa = settings.Element("secondAttribute");
			if (sa == null)
			{
				m_secondAttEnabled = true; //default to true
				m_secondAttField = "manufacturer"; 
			}
			else
			{
				m_secondAttEnabled = Client.GetAttribute(sa, "enabled").Equals("true");
				m_secondAttField = Client.GetAttribute(sa, "fieldName");
			}
			m_exclusions = null;
			m_exclusionsEnabled = false;
			XElement exConditions = settings.Element("exclusionConditions");
			if (exConditions != null)
			{
				m_exclusions = new List<Condition>();
				m_exclusionsEnabled = true;
				foreach (XElement ec in exConditions.Elements("condition"))
				{
					string name = Client.GetAttribute(ec, "name");
					string fieldName = Client.GetAttribute(ec, "fieldName");
					string comparison = Client.GetAttribute(ec, "comparison");
					string value = Client.GetAttribute(ec, "value");
					m_exclusions.Add(new Condition(name, fieldName, comparison, value));
				}
			}

			m_filters = null;
			m_filtersEnabled = false;
			m_rulesEnabled = Client.GetValue(settings, "rulesEnabled");
			if (string.IsNullOrEmpty(m_rulesEnabled))
				m_rulesEnabled = Client.GetValue(settings, "rulesType"); //older version
			XElement filterConditions = settings.Element("filterConditions");
			if (filterConditions != null)
			{
				m_filters = new List<Condition>();
				foreach (XElement f in filterConditions.Elements("condition"))
				{
					string name = Client.GetAttribute(f, "name");
					string fieldName = Client.GetAttribute(f, "fieldName");
					string comparison = Client.GetAttribute(f, "comparison");
					string value = Client.GetAttribute(f, "value");
					m_filters.Add(new Condition(name, fieldName, comparison, value));
				}
				if (m_filters.Count > 0)
				{
					m_filtersEnabled = true;
					if (!m_rulesEnabled.Contains("Filter")) {
						if (m_rulesEnabled.Length > 0) m_rulesEnabled += ","; //can have more than one rule enabled
						m_rulesEnabled += "Filter";
					}
				}
			}
			m_universalFilterName = Client.GetValue(settings, "universalFilterName");
			if (m_universalFilterName.Length < 1)
				m_universalFilterName = "Universal";

			m_replacements = null;
			m_replacementsEnabled = false;
			XElement repConditions = settings.Element("replacementConditions");
			if (repConditions != null)
			{
				m_replacements = new List<ReplacementCondition>();
				m_replacementsEnabled = true;
				foreach (XElement rep in repConditions.Elements("condition"))
				{
					string name = Client.GetAttribute(rep, "name");
					string type = Client.GetAttribute(rep, "type");
					string oldName = Client.GetAttribute(rep, "oldName");
					string newName = Client.GetAttribute(rep, "newName");
					m_replacements.Add(new ReplacementCondition(name, type, oldName, newName));
				}
			}

			XElement catConditions = settings.Element("categoryConditions");
			if (catConditions != null)
			{
				foreach (XElement cat in catConditions.Elements("condition"))
				{
					string groupId = Client.GetAttribute(cat, "groupId");
					string type = Client.GetAttribute(cat, "type");
					string value = Client.GetAttribute(cat, "value");
					m_catConditions.AddCat(type, value, groupId);
				}
				if (m_catConditions.FiltersExist)
					m_filtersEnabled = true;
			}

			XElement manualCrossSellConditions = settings.Element("manualCrossSellConditions");
			if (manualCrossSellConditions != null)
			{
				foreach (XElement cat in manualCrossSellConditions.Elements("condition"))
				{
					string fieldName = Client.GetAttribute(cat, "fieldName");
					string type = Client.GetAttribute(cat, "type");
					m_manualCrossSell.AddCondition(fieldName, type.Equals("include", StringComparison.CurrentCultureIgnoreCase));
				}
			}

			XElement manualUpSellConditions = settings.Element("manualUpSellConditions");
			if (manualUpSellConditions != null)
			{
				foreach (XElement cat in manualUpSellConditions.Elements("condition"))
				{
					string fieldName = Client.GetAttribute(cat, "fieldName");
					string type = Client.GetAttribute(cat, "type");
					m_manualUpSell.AddCondition(fieldName, type.Equals("include", StringComparison.CurrentCultureIgnoreCase));
				}
			}

			IEnumerable<XElement> upTimers = settings.Elements("uploadTimer");
			if (upTimers.Any())
			{
				m_uploadTimers = new List<UploadTimer>();
				foreach (XElement ut in upTimers)
				{
					bool enabled = Client.GetAttribute(ut, "enabled").Equals("true", StringComparison.CurrentCultureIgnoreCase);
					if (enabled)
						m_uploadTimers.Add(new UploadTimer(ut));
				}
			}
		}

		public void QueueExports(DateTime now)
		{
			if (m_uploadTimers == null) return;

			foreach (var ut in m_uploadTimers.Where(ut => ut.IsItTime(now)))
				ThreadPool.QueueUserWorkItem(s => GetData((UploadTimer) s), ut);
		}

		public void GetData(UploadTimer ut)
		{
			//the upload timare used to specify which components to update, but now this is controlled by the type of cart
			GetData();
		}

		public void GetData(bool fullExport = false, bool getCatalog = true)
		{
			//choices are update only or full export
			//all normal exports include catalog and related files (exclusions, attNames, etc)
			//full export also includes all sales files (could be pulling from xml depending on the cart)
			//update will include the current sales month only if m_extractSalesUpdate is true for this cart

			//TODO: move progress reporting to a supporting class and standardize messaging from cart classes
			string result = ProgressText = string.Format("Extracting data for {0} from {1}{2}", m_alias, m_cartName, Environment.NewLine);
			m_log.WriteEntry(result, System.Diagnostics.EventLogEntryType.Information, m_alias);

			var stopWatch = new StopWatch(true);

			try
			{
#if DEBUG
				//Make sure service is awake and prevent timeouts later 
				ProgressText += "Testing Boost Service Connection...";
				result += "Boost Service Test: " + m_boostService.Test4TellServiceConnection();
				result += " (" + stopWatch.Lap() + ")";
				ProgressText = result;
#endif

				//------------------Sales.txt------------------
				if (fullExport || m_extractSalesUpdate)
				{
					if (fullExport)
					{
						result += GetAllSales();
						result += "\nSales Export Complete (total: " + stopWatch.Lap() + ")";
					}
					else
						result += GetSalesUpdate();
					ProgressText = result;
				}

				if (getCatalog)
				{
				//------------------Catalog.txt------------------
					result += GetCatalog();
					result += " (" + stopWatch.Lap() + ")";
					ProgressText = result;

				//------------------Exclusions.txt------------------
					result += GetExclusions();
					result += " (" + stopWatch.Lap() + ")";
					ProgressText = result;

				//------------------Replacements.txt------------------
					result += GetReplacements();
					result += " (" + stopWatch.Lap() + ")";
					ProgressText = result;

				//------------------Attribute1Items.txt------------------
					result += GetAtt1Names();
					result += " (" + stopWatch.Lap() + ")";
					ProgressText = result;

				//------------------Attribute2Items.txt------------------
					result += GetAtt2Names();
					result += " (" + stopWatch.Lap() + ")";
					ProgressText = result;
				}

				//------------------ManualCrossSell.txt------------------
				result += GetManualCrossSellRecs();
				result += " (" + stopWatch.Lap() + ")";
				ProgressText = result;

				//------------------ManualUpSell.txt------------------
				result += GetManualUpSellRecs();
				result += " (" + stopWatch.Lap() + ")";
				ProgressText = result;

				//------------------ConfigBoost.txt------------------
				result += GetConfig();
				result += " (" + stopWatch.Lap() + ")";
				ProgressText = result;

			GenerateTables:
				//Generating here so we can track status and time spent (instead of setting lastFile true above)
				ProgressText += "\nGenerating Tables...";
				result += "\n" + m_boostService.Generate4TellTables(m_alias);
				result += " (" + stopWatch.Lap() + ")";
			End:
				ProgressText = result;

			}
			catch (ThreadAbortException ex)
			{
				result = ProgressText + "\nData Extraction Aborted.";
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
				m_log.WriteEntry(result, System.Diagnostics.EventLogEntryType.Information, m_alias);
			}
		}

		public void GetAllData()
		{
			GetData(true);
		}

		public void GetDataUpdate()
		{
			GetData();
		}

		protected string GetSalesUpdate()
		{
			string result = "";
			var stopWatch = new StopWatch(true);

			//------------------Sales_YYYY-MM.txt------------------
			//first update sales file list
			DateTime exportDate = DateTime.Now;
			result += GetSalesMonth(exportDate, string.Format(SalesFilenameFormat, exportDate.ToString("yyyy-MM"))); //only current month
			result += " (" + stopWatch.Lap() + ")";
			ProgressText = result;

			return result;
		}

		protected string GetAllSales()
		{
			string result = "";
			var exportDate = DateTime.Now;
			var stopWatch = new StopWatch(true);
			string tempDisplay = ProgressText;

			for (int month = 0; month < m_monthsToExport; month++)
			{
				//create filename and add to the upload list for ConfigBoost
				result += GetSalesMonth(exportDate, string.Format(SalesFilenameFormat, exportDate.ToString("yyyy-MM")));
				result += " (" + stopWatch.Lap() + ")";
				ProgressText = tempDisplay + result;

				//move back one month for next loop
				exportDate = exportDate.AddMonths(-1);
			}
			return result;
		}

		protected string GetManualCrossSellRecs()
		{
			var result = string.Format("{0}{1}: ", Environment.NewLine, ManualCrossSellFilename);
			if (!m_manualCrossSell.Enabled)
				return result + "(not used)";

			int count = m_manualCrossSell.Records.Count;
			if (count < 1)
				return result + "(no data)";

			var countDisplay = string.Format("({0} items) ", count);
			ProgressText += result;
			ProgressText += countDisplay + "Uploading...";

			result += countDisplay + m_boostService.WriteTable(m_alias, ManualCrossSellFilename, m_manualCrossSell.Records);
			return result;
		}

		protected string GetManualUpSellRecs()
		{
			var result = string.Format("{0}{1}: ", Environment.NewLine, ManualUpSellFilename);
			if (!m_manualUpSell.Enabled)
				return result + "(not used)";

			int count = m_manualUpSell.Records.Count;
			if (count < 1)
				return result + "(no data)";

			var countDisplay = string.Format("({0} items) ", count);
			ProgressText += result;
			ProgressText += countDisplay + "Uploading...";

			result += countDisplay + m_boostService.WriteTable(m_alias, ManualUpSellFilename, m_manualUpSell.Records);
			return result;
		}

		protected string GetConfig()
		{
			string result = "";
			const string filename = "ConfigBoost.txt";
			result += "\n" + filename + ": ";
			ProgressText += result + "Uploading...";

			//Fixed configuration settings may be changed below as long as tabs and formatting are preserved
			//Note: If this is desired it would be better to add more fields to the UI form
			var data = new StringBuilder();
			data.Append("Version\t3\r\n");
			if (m_pocName.Length > 0)
				data.Append("Owner\t" + m_pocName + "\r\n");
			if (m_pocEmail.Length > 0)
				data.Append("Email\t" + m_pocEmail + "\r\n");
			if (m_reportLevel.Length > 0)
				data.Append("ReportLevel\t" + m_reportLevel + "\r\n");
			data.Append("Attribute1Name\tCategory\r\n");
			if (m_secondAttEnabled)
				data.Append("Attribute2Name\t" + m_secondAttField + "\r\n");
			data.Append("Resell\t" + (m_resell? "1" : "0") + "\r\n");
			if (m_minLikelihood > 0)
				data.Append("MinLikelihood\t" + m_minLikelihood.ToString() + "\r\n");
			if (m_minCommon > 0)
				data.Append("MinCommon\t" + m_minCommon.ToString() + "\r\n");
			data.Append("ExclusionsExists\t" + (m_exclusionsEnabled ? "1" : "0") + "\r\n");
			data.Append("ReplacementsExists\t" + (m_replacementsEnabled ? "1" : "0") + "\r\n");
			if (m_monthsToExport > 0)
				data.Append("MaxSalesDataAgeInMonths\t" + m_monthsToExport.ToString() + "\r\n");
			if (m_rulesEnabled.Length > 0)
				data.Append("RulesEnabled\t" + m_rulesEnabled + "\r\n");
			if (m_filtersEnabled)
				data.Append("UniversalFilterNames\t" + m_universalFilterName + "\r\n");
			result += m_boostService.WriteTable(m_alias, filename, data);
			return result;
		}
		#endregion

		#region Abstract Methods
		public abstract void LogSalesOrder(string orderId);
		//for the following, final result should be in the format of "Filename: success/failure status (time spent)"
		protected abstract string GetCatalog();
		protected abstract string GetSalesMonth(DateTime exportDate, string filename);
		protected abstract string GetExclusions();
		protected abstract string GetReplacements();
		protected abstract string GetAtt1Names();
		protected abstract string GetAtt2Names();
		#endregion

		#region Utilities
		//Get a list of all fields needed by any of the catalog extraction rules
		protected List<string> GetRuleFields()
		{
			List<string> fields = new List<string>();
			if ((m_filters != null) && (m_filters.Count() > 0))
				foreach (Condition c in m_filters)
					if (!fields.Contains(c.FieldName))
						fields.Add(c.FieldName);

			if ((m_exclusions != null) && (m_exclusions.Count() > 0))
				foreach (Condition c in m_exclusions)
					if (!fields.Contains(c.FieldName))
						fields.Add(c.FieldName);

			return fields;
		}

		protected void ApplyRules(ref ProductRecord p, XElement product)
		{
			//first check for excluded categories
			if (m_catConditions.AnyExcluded(p.Att1Id))
				m_catConditions.AddExcludedItem(p.ProductId);
			else //then check all other exclusion rules
			{
				if (m_exclusions != null)
				{
					if (m_exclusions.Any(c => c.Compare(Client.GetValue(product, c.FieldName))))
						m_catConditions.AddExcludedItem(p.ProductId); //add to cat excluded items so it will be prepolulated for GetExclusions
				}
			}

			//remove ignored categories
			p.Att1Id = m_catConditions.RemoveIgnored(p.Att1Id);

			//apply filters
			if (m_filtersEnabled)
			{
				if (m_catConditions.AnyUniversal(p.Att1Id))
					p.Filter = m_universalFilterName; 

				string filters = m_catConditions.AnyFiltered(p.Att1Id);
				if (filters.Length > 0)
				{
					if (p.Filter.Length > 0) p.Filter += ",";
					p.Filter += filters;
				}
				if (m_filters != null)
				{
					//bool firstItem = p.Filter.Length < 1;
					//foreach (Condition c in m_filters.Where(c => c.Compare(Client.GetValue(product, c.FieldName))))
					//{
					//  if (firstItem) firstItem = false;
					//  else p.Filter += ",";
					//  p.Filter += c.Name;
					//}
					p.Filter += m_filters.Where(
																			c => c.Compare(Client.GetValue(product, c.FieldName))
																			).Select(	
																								c => c.Name
																								).Aggregate((w, j) => string.Format("{0},{1}", w, j));
				}
				if (p.Filter.Length < 1) //if no matches then assume universal
					p.Filter = m_universalFilterName;
			}

			//check for full catalog replacement 
			if (m_replacements != null && m_replacements[0].Type.Equals(ReplacementCondition.RepType.Catalog))
			{
				var oldId = Client.GetValue(product, m_replacements[0].OldName);
				var newId = Client.GetValue(product, m_replacements[0].NewName);
				if (!m_repRecords.Any(r => r.OldId.Equals(oldId))) //can only have one replacement for each item
					m_repRecords.Add(new ReplacementRecord { OldId = oldId, NewId = newId });
			}

			//check for manual recs
			if (m_manualCrossSell.Enabled)
				m_manualCrossSell.AddRecords(p.ProductId, product);
			if (m_manualUpSell.Enabled)
				m_manualUpSell.AddRecords(p.ProductId, product);
		}
	
		public string RemoveIllegalChars(string source, char[] illigalChars)
		{
			int index = -1;
			while ((index = source.IndexOfAny(illigalChars)) >= 0)
			{
				source = source.Remove(index, 1);
			}
			return source;
		}

		//standard Hash saved as a hex string
		public string GetHash(string source)
		{
			byte[] input = Encoding.UTF8.GetBytes(source);
			byte[] result = m_hash.ComputeHash(input);
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < result.Length; i++)
			{
				sb.Append(result[i].ToString("X2"));
			}
			return sb.ToString();
		}

		public string RemoveHtmlFormatting(string source)
		{
			int index = 0;
			while (true)
			{
				int begin = source.IndexOf('<', index);
				if (begin < 0) break;
				int end = source.IndexOf('>', begin);
				if (end < 0) break;
				//found some formatting
				source = source.Remove(begin, end - begin + 1);
				index = begin;
			}
			return source;
		}

		#endregion

	}//class
} //namespace