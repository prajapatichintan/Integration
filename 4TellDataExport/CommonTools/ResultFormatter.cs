using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using _4_Tell.IO;

namespace _4_Tell.Utilities
{
	public enum ResultFormats // Used externally in config files!!!
	{
		SpaceDelimited,	//space separated values
		CommaDelimited,	//comma separated values
		TabDelimited,	//tab separated values
		XML,
		Ruby,
		Array,
		UseConfig		//legacy support, read format from ConfigBoost
	}

	public class ResultFormatter
	{
		//NOTE: startposition is zero-based here
		public string ToFormat(Rec[] recommendationList, ResultFormats format, int startPosition)
		{
			string result;
			switch (format)
			{
				case ResultFormats.SpaceDelimited:
					result = ToSeparatedValueString(recommendationList, startPosition, " ");
					break;
				case ResultFormats.TabDelimited:
					result = ToSeparatedValueString(recommendationList, startPosition, "\t");
					break;
				case ResultFormats.CommaDelimited:
					result = ToSeparatedValueString(recommendationList, startPosition, ", ");
					break;
				case ResultFormats.XML:
					result = ToXmlString(recommendationList, startPosition);
					break;
				case ResultFormats.Ruby:
					result = "[";
					result += ToSeparatedValueString(recommendationList, startPosition, ", ");
					result += "]";
					break;
				default:
					goto case ResultFormats.TabDelimited;
			}
			return result;
		}

		public string ToSeparatedValueString(Rec[] recommendationList, int startPosition, string delimiter)
		{
			string result = "";
			if (recommendationList == null)
			{
				throw new ArgumentNullException("ResultFormatter.recommendationList");
			}
			int numResults = recommendationList.Length;
			for (int i = startPosition; i < numResults; i++)
			{
				if (i > startPosition) result += delimiter;
				result += recommendationList[i].alphaID;
			}
			return result;
		}

		public string ToXmlString(Rec[] recommendationList, int startPosition)
		{
			string result = "";
			if (recommendationList == null)
			{
				throw new ArgumentNullException("ResultFormatter.recommendationList");
			}
			int numResults = recommendationList.Length;

			result = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n";
			result += string.Format("<Recommendations numResults=\"{0}\" startPosition=\"{1}\">\n", 
								numResults - startPosition, startPosition + 1);
			for (int i = startPosition; i < numResults; i++)
			{
				result += string.Format("  <result number=\"{0}\">{1}</result>\n", i-startPosition+1, recommendationList[i].alphaID);
			}
			result += "</Recommendations>\n";
			return result;
		}

	}
}
