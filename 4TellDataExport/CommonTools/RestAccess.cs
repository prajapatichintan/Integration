using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq; //XElement
using System.Net; //HttpWebRequest HttpWebResponse WebException
using System.Text; //Encoding
using System.IO; //Stream
using System.ServiceModel; //FaultException
using System.ServiceModel.Web; //WebFaultException
using System.Collections; //DictionaryEntry
using System.Configuration;

namespace _4_Tell
{
	using Utilities;
	/// <summary>
	/// Summary description for RestAccess
	/// </summary>
	public class RestAccess
	{
		public string DebugText = "";
		private string m_serviceUri = "";
		private bool m_saveAndReplicate = true;
		private bool m_uploadRemote = false;

		#region Instance
		private static readonly RestAccess m_instance = new RestAccess();
		private RestAccess()
		{
			m_serviceUri = ConfigurationManager.AppSettings.Get("BoostServerAddress");
			if (!m_serviceUri.StartsWith("http"))
				m_serviceUri = "http://" + m_serviceUri; //add prefix if missing
			if (!m_serviceUri.EndsWith("/"))
				m_serviceUri += "/"; //add final slash
			m_saveAndReplicate = ConfigurationManager.AppSettings.Get("SaveLocal").Equals("true");
			m_uploadRemote = ConfigurationManager.AppSettings.Get("UploadRemote").Equals("true");
		}
		public static RestAccess Instance { get { return m_instance; } }
		#endregion

		//public string WriteTable<T>(string alias, string dataName, List<T> data, bool lastFile = false)
		//{
		//  var sb = new StringBuilder(CartExtractor.CommonHeader + T.Header()); //need static CartExtractor.GetHeader(T)
		//  foreach (var a in data.Distinct())
		//    sb.Append(a.ToString());

		//  return WriteTable(alias, dataName, sb, lastFile);
		//}

		public string WriteTable(string alias, string dataName, List<AttributeRecord> data, bool lastFile = false)
		{
			var sb = new StringBuilder(CartExtractor.CommonHeader + AttributeRecord.Header());
			foreach (var a in data.Distinct())
				sb.Append(a.ToString());

			return WriteTable(alias, dataName, sb, lastFile);
		}

		public string WriteTable(string alias, string dataName, List<ProductRecord> data, bool lastFile = false)
		{
			var sb = new StringBuilder(CartExtractor.CommonHeader + ProductRecord.Header());
			foreach (var a in data.Distinct())
				sb.Append(a.ToString());

			return WriteTable(alias, dataName, sb, lastFile);
		}

		public string WriteTable(string alias, string dataName, List<SalesRecord> data, bool lastFile = false)
		{
			var sb = new StringBuilder(CartExtractor.CommonHeader + SalesRecord.Header());
			foreach (var a in data.Distinct())
				sb.Append(a.ToString());

			return WriteTable(alias, dataName, sb, lastFile);
		}

		public string WriteTable(string alias, string dataName, List<ExclusionRecord> data, bool lastFile = false)
		{
			var sb = new StringBuilder(CartExtractor.CommonHeader + ExclusionRecord.Header());
			foreach (var a in data.Distinct())
				sb.Append(a.ToString());

			return WriteTable(alias, dataName, sb, lastFile);
		}

		public string WriteTable(string alias, string dataName, List<ReplacementRecord> data, bool lastFile = false)
		{
			var sb = new StringBuilder(CartExtractor.CommonHeader + ReplacementRecord.Header());
			foreach (var a in data.Distinct())
				sb.Append(a.ToString());

			return WriteTable(alias, dataName, sb, lastFile);
		}

		public string WriteTable(string alias, string dataName, List<ManualRecommendationRecord> data, bool lastFile = false)
		{
			var sb = new StringBuilder(CartExtractor.CommonHeader + ManualRecommendationRecord.Header());
			foreach (var a in data.Distinct())
				sb.Append(a.ToString());

			return WriteTable(alias, dataName, sb, lastFile);
		}

		public string WriteTable(string alias, string dataName, StringBuilder data, bool lastFile = false)
		{
			string result = "";

			if (m_saveAndReplicate) //save file locally
			{
				try
				{
					string path = IO.DataPath.Instance.ClientDataPath(ref alias) + "upload\\";
					if (!Directory.Exists(path))
						Directory.CreateDirectory(path);
					StreamWriter sw = File.CreateText(path + dataName);
					sw.Write(data.ToString());
					sw.Close();
					Replicator.Instance.ReplicateDataFile(alias, true, dataName);
					result = "File Saved.";
				}
				catch (Exception ex)
				{
					result = ex.Message;
				}
			}

			if (m_uploadRemote)  //upload to 4-tell service
			{
				//pre-pend header line with parameters
				data.Insert(0, alias + "\t" + dataName + "\t" + (lastFile ? "1" : "0") + "\r\n");

				//convert data to a byte array
				byte[] bData = System.Text.Encoding.UTF8.GetBytes(data.ToString());

				//upload to 4-Tell
				result += Get4TellResponseREST("UploadData/stream", "", bData, "POST");
			}
			else if (lastFile) //no upload flag so must launch generator directly
			{
				result += "\n" + Generate4TellTables(alias);
			}

			return result;
		}

		public string Generate4TellTables(string alias)
		{
			string queryData = "clientAlias=" + alias + "&reloadTables=true";
			string result = Get4TellResponseREST("GenerateDataTables", queryData, null, "POST");
			return result;
		}

		public string Test4TellServiceConnection()
		{
			string result;
			if (m_uploadRemote)
				result = Get4TellResponseREST("ServiceTest");
			else result = "Remote upload is not enabled.";
			return result;
		}

		public string Get4TellResponseREST(string function, string queryData = null, byte[] bData = null, string method = "GET", string contentType = "text/plain")
		{
			string result = "";

			HttpWebRequest request = null;
			HttpWebResponse response = null;

			try
			{
				string serviceUriFormat = m_serviceUri + function;
				if (queryData != null)
					if (queryData.Length > 0)
						serviceUriFormat += "?" + queryData;
				Uri serviceURI = new Uri(serviceUriFormat);
				request = WebRequest.Create(serviceURI) as HttpWebRequest;
				request.Method = method;
				request.ContentType = contentType;
				request.KeepAlive = false;
				request.ContentLength = 0;

				if (bData != null && bData.Length > 0)
				{
					request.ContentLength = bData.Length;
					using (Stream postStream = request.GetRequestStream())
					{
						postStream.Write(bData, 0, bData.Length);
					}
				}

				// Get response  		
				//using (response = request.GetResponse() as HttpWebResponse)
				//{
				//  StreamReader reader = new StreamReader(response.GetResponseStream());
				//  result = reader.ReadToEnd();
				//}

				int b1;
				MemoryStream memoryStream = new MemoryStream();
				byte[] xmlBytes;
				using (response = request.GetResponse() as HttpWebResponse)
				{
					while ((b1 = response.GetResponseStream().ReadByte()) != -1)
						memoryStream.WriteByte(((byte)b1));

					xmlBytes = memoryStream.ToArray();
					response.Close();
					memoryStream.Close();
				}
				//convert to string
				result = ASCIIEncoding.ASCII.GetString(xmlBytes);

				if (result.Length > 0)
				{
					if (result.StartsWith("<")) //parse xml response
					{
						XElement xml = XElement.Parse(result);
						if ((xml != null) && (!xml.IsEmpty))
							result = xml.Value;
					}
					if (result.StartsWith("\"")) //remove quotes
					{
						result = result.Remove(0, 1);
						if (result.EndsWith("\""))
							result = result.Remove(result.Length - 1);
					}
				}
			}

			#region Faultcatching
			catch (WebException wex)
			{
				result += "WebFaultException = " + wex.Message;
				// Get the response stream  
				HttpWebResponse wexResponse = (HttpWebResponse)wex.Response;
				if (wexResponse != null)
				{
					StreamReader reader = new StreamReader(wexResponse.GetResponseStream());
					result += "\nException Response = " + reader.ReadToEnd();
				}
				result += "\nStatus = " + wex.Status.ToString();
				if (wex.InnerException != null)
					result += "\nInner Exception = " + wex.InnerException.Message;
				if (response != null)
					if (response.ContentLength > 0)
						result += "\n\nResponse = " + response.StatusDescription;
				throw new Exception(result);
			}
			catch (Exception ex)
			{
				result += "Exception = " + ex.Message;
				if (ex.InnerException != null)
					result += "\nInner Exception = " + ex.InnerException.Message;
				result += "\nStackTrace = " + ex.StackTrace.ToString();
				throw new Exception(result);
			}
			//For Debugging...
			//finally
			//{
			//  if (request != null)
			//  {
			//    result += "\nREST Endpoint---";
			//    result += "\nConection: " + request.Connection;
			//    result += "\nAddress: " + request.Address.AbsoluteUri;
			//    result += "\nHeaders: ";
			//    WebHeaderCollection headers = request.Headers;
			//    foreach (string s in headers)
			//      result += "\n  " + s;
			//  }
			//}
			#endregion

			return result;
		}


		//depricated?
		public string GetServiceResponseREST(
													string function,
													string queryData = null,
													byte[] bData = null,
													string sData = null,
													string method = "GET",
													string contentType = "text/plain")
		{
			string result = "";

			HttpWebRequest request = null;
			HttpWebResponse response = null;

			try
			{
				string serviceUriFormat = m_serviceUri + function;
				if (queryData != null)
					if (queryData.Length > 0)
						serviceUriFormat += "?" + queryData;
				Uri serviceURI = new Uri(serviceUriFormat);
				request = WebRequest.Create(serviceURI) as HttpWebRequest;
				request.Method = method;
				request.ContentType = contentType;
				//request.ContentType = "application/x-www-form-urlencoded";
				request.ContentLength = 0;

				//NOTE: can only send one set of data so sData overrides bData
				if (sData != null && sData.Length > 0)
					bData = Encoding.UTF8.GetBytes(sData);

				if (bData != null && bData.Length > 0)
				{
					// Set the content length in the request headers  
					request.ContentLength = bData.Length;

					// Write data  
					using (Stream postStream = request.GetRequestStream())
					{
						postStream.Write(bData, 0, bData.Length);
					}
				}

				// Get response  		
				using (response = request.GetResponse() as HttpWebResponse)
				{
					result = "response.StatusCode = " + response.StatusCode.ToString();
					result += "\nresponse.StatusDescription = " + response.StatusDescription;

					// Get the response stream  
					StreamReader reader = new StreamReader(response.GetResponseStream());

					// get the output  
					result = reader.ReadToEnd();
				}
			}

			#region Faultcatching Tests
			catch (WebFaultException wex)
			{
				result += "WebFaultException = " + wex.Message;
				result += "\nAction = " + wex.Action;
				result += "\nCode = " + wex.Code.ToString();
				if (wex.Data.Count > 0)
				{
					result += "\nData = KEY\tVALUE";
					foreach (DictionaryEntry de in wex.Data)
						result += string.Format("\n\t{0}\t{1}", de.Key, de.Value);
				}
				result += "\nReason = " + wex.Reason.ToString();
				result += "\nStatusCode = " + wex.StatusCode.ToString();
				if (wex.InnerException != null)
					result += "\nInner Exception = " + wex.InnerException.Message;
				if (response != null)
					if (response.ContentLength > 0)
						result += "\n\nResponse = " + response.StatusDescription;
			}
			catch (WebException wex)
			{
				result += "WebFaultException = " + wex.Message;
				// Get the response stream  
				HttpWebResponse wexResponse = (HttpWebResponse)wex.Response;
				if (wexResponse != null)
				{
					StreamReader reader = new StreamReader(wexResponse.GetResponseStream());
					result += "\nException Response = " + reader.ReadToEnd();
				}
				if (wex.Data.Count > 0)
				{
					result += "\nData = KEY\tVALUE";
					foreach (DictionaryEntry de in wex.Data)
						result += string.Format("\n\t{0}\t{1}", de.Key, de.Value);
				}
				result += "\nStatus = " + wex.Status.ToString();
				if (wex.InnerException != null)
					result += "\nInner Exception = " + wex.InnerException.Message;
				if (response != null)
					if (response.ContentLength > 0)
						result += "\n\nResponse = " + response.StatusDescription;
			}
			catch (FaultException fex)
			{
				result += "FaultException = " + fex.Message;
				result += "\nAction = " + fex.Action;
				result += "\nCode = " + fex.Code.ToString();
				if (fex.Data.Count > 0)
				{
					result += "\nData = KEY\tVALUE";
					foreach (DictionaryEntry de in fex.Data)
						result += string.Format("\n\t{0}\t{1}", de.Key, de.Value);
				}
				result += "\nReason = " + fex.Reason.ToString();
				if (fex.InnerException != null)
					result += "\nInner Exception = " + fex.InnerException.Message;
				if (response != null)
					if (response.ContentLength > 0)
						result += "\n\nResponse = " + response.StatusDescription;
			}
			catch (Exception ex)
			{
				result += "Exception = " + ex.Message;
				if (ex.InnerException != null)
					result += "\nInner Exception = " + ex.InnerException.Message;
				result += "\nStackTrace = " + ex.StackTrace.ToString();
			}
			#endregion

#if DEBUG
		if (request != null)
		{
			DebugText = "REST Endpoint---";
			DebugText += "\nConection: " + request.Connection;
			DebugText += "\nAddress: " + request.Address.AbsoluteUri;
			DebugText += "\nHeaders: ";
			WebHeaderCollection headers = request.Headers;
			foreach (string s in headers)
			{
				DebugText += "\n  " + s;
				//DebugText += "\nName: " + ep.Name;
				//DebugText += "\nAddress: " + ep.Address.Uri.ToString();
				//DebugText += "\nBinding Name: " + ep.Binding.Name;
				//DebugText += "\nBinding Namespace: " + ep.Binding.Namespace;
				//DebugText += "\nContract Name: " + ep.Contract.Name;
				//DebugText += "\nContract Namespace: " + ep.Contract.Namespace;
			}
		}
#endif

			return result;
		}

	}//class
}//namespace