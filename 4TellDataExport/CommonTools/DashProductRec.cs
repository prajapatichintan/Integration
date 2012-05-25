using System.Text;
using System.Runtime.Serialization;

namespace _4_Tell.Utilities
{
	// product IDs, title, price, link and image link in JSONP array 
	[DataContract(Namespace = "")] //(Namespace="http://www.4-tell.net/Boost2.0/")]
	[KnownType(typeof(DashProductRec))]
	public class DashProductRec
	{
		[DataMember]
		public string likelihood { get; set; }
		[DataMember]
		public string name { get; set; }
		[DataMember]
		public string commonSales { get; set; }
		[DataMember]
		public string totalSales { get; set; }
		[DataMember]
		public string id { get; set; }

		public string ToString() //format input params as an element of a JSON array
		{
			return "{\"likelihood\":\"" + likelihood
									+ "\"name\":\"" + name
									+ "\"commonSales\":\"" + commonSales
									+ "\"totalSales\":\"" + totalSales
									+ "\"id\":\"" + id + "\"}";
		}

	}
}
