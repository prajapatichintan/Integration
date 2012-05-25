using System;
using System.Net;
//using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.Serialization;
//using System.ServiceModel;
//using System.ServiceModel.Channels; //MessageProperties
//using System.ServiceModel.Web; //WebOperationContext
//using System.Text;
//using System.Diagnostics;
//using System.IO;
//using System.Threading;

namespace _4_Tell.Utilities
{
	public static class Input
	{
		public static void StripQuotes(ref string input)
		{
			if (input == null)
			{
				input = ""; //input was missing this parameter tag so set to empty string
				return;
			}

			string quote = "\"";
			if (input.StartsWith(quote)) input = input.Substring(1);
			if (input.EndsWith(quote)) input = input.Substring(0, input.Length - 1);
		}

		public static bool CheckBool(string input, bool defaultOut = false)
		{
			bool output = defaultOut; //set default
			if (defaultOut)
			{
				if (input.ToLower() == "false" || input == "0") output = false;
			}
			else
				if (input.ToLower() == "true" || input == "1") output = true;
			return output;
		}

		public static int CheckInt(string input, int defaultOut = 0)
		{
			int output = defaultOut;
			try
			{
				if (input.Length > 0)
					output = Convert.ToInt32(input);
			}
			catch { }

			return output;
		}


	}
}