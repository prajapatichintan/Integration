using System.Text;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace _4_Tell.Utilities
{
	[DataContract(Namespace = "")] //(Namespace="http://www.4-tell.net/Boost2.0/")]
	[KnownType(typeof(DashProductRec))]
	public class DashProductRec
	{
		[DataMember]
		public string name { get; set; }
		[DataMember]
		public string id { get; set; }
		[DataMember]
		public string ranking { get; set; }
		[DataMember]
		public string likelihood { get; set; }
		[DataMember]
		public string att1IDs { get; set; }
		[DataMember]
		public string att2ID { get; set; }
		[DataMember]
		public string filters { get; set; }
		[DataMember]
		public string commonSales { get; set; }
		[DataMember]
		public string totalSales { get; set; }
		[DataMember]
		public string totalViews { get; set; }
		[DataMember]
		public string source { get; set; }

		public override string ToString() //format input params as an element of a JSON array
		{
			return "{\"name\":\"" + name
							+ "\",\"id\":\"" + id
							+ "\",\"ranking\":\"" + ranking
							+ "\",\"likelihood\":\"" + likelihood
							+ "\",\"att1IDs\":\"" + att1IDs
							+ "\",\"att2ID\":\"" + att2ID
							+ "\",\"filters\":\"" + filters
							+ "\",\"commonSales\":\"" + commonSales
							+ "\",\"totalSales\":\"" + totalSales
							+ "\",\"totalViews\":\"" + totalViews
							+ "\",\"source\":\"" + source 
							+ "\"}";
		}

		public string[] ToArray()
		{
			return new string[] {name, id, ranking, likelihood, att1IDs, att2ID, filters, commonSales, totalSales, totalViews, source};
		}
	}

	[DataContract(Namespace = "")] //(Namespace="http://www.4-tell.net/Boost2.0/")]
	[KnownType(typeof(DashCatalogItem))]
	public class DashCatalogItem
	{
		[DataMember]
		public string name { get; set; } //item name
		[DataMember]
		public string id { get; set; } // item ID
		[DataMember]
		public string att1IDs { get; set; }
		[DataMember]
		public string att2ID { get; set; }
		[DataMember]
		public string filters { get; set; }
		[DataMember]
		public string totalSales { get; set; }
		[DataMember]
		public string totalViews { get; set; }

		public override string ToString() //format input params as an element of a JSON array
		{
			return "{\"name\":\"" + name
							+ "\",\"id\":\"" + id
							+ "\",\"att1IDs\":\"" + att1IDs
							+ "\",\"att2ID\":\"" + att2ID
							+ "\",\"filters\":\"" + filters
							+ "\",\"totalSales\":\"" + totalSales
							+ "\",\"totalViews\":\"" + totalViews 
							+ "\"}";
		}
		public string[] ToArray()
		{
			return new string[] { name, id, att1IDs, att2ID, filters, totalSales, totalViews };
		}
		public string[] ToArray(string columnOrder, string multiAtt1Image)
		{
			if ((columnOrder == null) || (columnOrder.Length < 1)) 
				return ToArray(); //use default order (faster)

			List<string> result = new List<string>();
			string[] columns = columnOrder.Split(new char[] { ',' });
			foreach (string col in columns)
			{
				string c = col.Trim();
				if (c.Equals("name", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(name);
				else if (c.Equals("id", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(id);
				else if (c.Equals("att1IDs", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(att1IDs);
				else if (c.Equals("att2ID", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(att2ID);
				else if (c.Equals("filters", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(filters);
				else if (c.Equals("totalSales", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(totalSales);
				else if (c.Equals("totalViews", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(totalViews);
				else if (c.Equals("att1FirstID", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(GetFirstItem(att1IDs, multiAtt1Image));
				else if (c.Equals("att1Details", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(DropFirstItem(att1IDs));
				else if (c.Equals("firstFilter", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(GetFirstItem(filters, multiAtt1Image));
				else if (c.Equals("filterDetails", System.StringComparison.CurrentCultureIgnoreCase)) result.Add(DropFirstItem(filters));
			}
			return result.ToArray();
		}
		private string GetFirstItem(string csvList, string image, char delimeter = ',')
		{
			if (csvList == null) return "";

			string[] ids = csvList.Split(delimeter);
			if (ids.Length < 1) return "";

			string result = ids[0];
			if ((ids.Length > 1) && (image != null) && (image.Length > 0))
				result += " " + image;
			return result;
		}
		private string DropFirstItem(string csvList, char delimeter = ',')
		{
			if (csvList == null) return "";

			//drop first item from list
			int i = csvList.IndexOf(delimeter);
			if ((i > 0) && (i + 1 < csvList.Length))
				return csvList.Substring(i + 1);
			else return "";
		}
	}

	[DataContract(Namespace = "")] //(Namespace="http://www.4-tell.net/Boost2.0/")]
	[KnownType(typeof(TopCustomer))]
	public class TopCustomer
	{
		[DataMember]
		public string id { get; set; } // item ID
		[DataMember]
		public int purchases { get; set; } //total 

		public override string ToString() //format input params as an element of a JSON array
		{
			return "{\"id\":\"" + id
							+ "\",\"purchases\":\"" + purchases 
							+ "\"}";
		}
	}

    // Dashboard | GET

	[DataContract(Namespace = "")] //(Namespace="http://www.4-tell.net/Boost2.0/")]
	[KnownType(typeof(DashAdmins))]
	public class DashAdmins
	{
		[DataMember]
		public string UserId { get; set; } 
		[DataMember]
		public int UserName { get; set; }
		[DataMember]
		public int UserEmail { get; set; }
		[DataMember]
		public int UserPassword { get; set; }  

		public override string ToString() //format input params as an element of a JSON array
		{
			return "{\"UserId\":\"" + UserId
							+ "\",\"UserName\":\"" + UserName
							+ "\",\"UserEmail\":\"" + UserEmail
							+ "\",\"UserPassword\":\"" + UserPassword
							+ "\"}";
		}
	}

	[DataContract(Namespace = "")] //(Namespace="http://www.4-tell.net/Boost2.0/")]
	[KnownType(typeof(DashSites))]
	public class DashSites
	{
		[DataMember]
		public string SiteId { get; set; }
		[DataMember]
		public int SiteURL { get; set; }
		[DataMember]
		public int ClientAlias { get; set; }
		[DataMember]
		public int RoleId { get; set; }
		[DataMember]
		public int RoleName { get; set; }

		public override string ToString() //format input params as an element of a JSON array
		{
			return "{\"SiteId\":\"" + SiteId
							+ "\",\"SiteURL\":\"" + SiteURL
							+ "\",\"ClientAlias\":\"" + ClientAlias
							+ "\",\"RoleId\":\"" + RoleId
							+ "\",\"RoleName\":\"" + RoleName
							+ "\"}";
		}
	}

}
