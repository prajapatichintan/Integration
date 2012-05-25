using System.Text;
using System.Runtime.Serialization;

namespace _4_Tell.Utilities
{
	// product IDs, title, price, link and image link in JSONP array 
	[DataContract(Namespace = "")] //(Namespace="http://www.4-tell.net/Boost2.0/")]
		[KnownType(typeof(RecDisplayItem))]
	public class RecDisplayItem
	{
		[DataMember]
		public string productID { get; set; }
		[DataMember]
		public string title { get; set; }
		[DataMember]
		public string price { get; set; }
		[DataMember]
		public string salePrice { get; set; }
		[DataMember]
		public string rating { get; set; }
		[DataMember]
		public string pageLink { get; set; }
		[DataMember]
		public string imageLink { get; set; }

		public override string ToString() //format input params as an element of a JSON array
		{
			return "{\"productID\":\"" + productID
									+ "\"title\":\"" + title
									+ "\"price\":\"" + price
									+ "\"salePrice\":\"" + salePrice
									+ "\"rating\":\"" + rating
									+ "\"pageLink\":\"" + pageLink
									+ "\"imageLink\":\"" + imageLink + "\"}";
		}

	}
}
