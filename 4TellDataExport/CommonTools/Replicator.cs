using System;
using System.Net; //Dns and IPAddress
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.IO;
using System.Configuration;
using System.Runtime.InteropServices; // DllImport
using System.Security.Principal; // WindowsImpersonationContext
using System.Security.Permissions; // PermissionSetAttribute
using System.Diagnostics; //EventLogEntryType
using _4_Tell.IO;

namespace _4_Tell.Utilities
{

	public class Replicator //Singleton
	{
		#region External Prototypes
		[DllImport("ADVAPI32.DLL", SetLastError = true)]
		public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
																int dwLogonType, int dwLogonProvider, out IntPtr phToken);
		[DllImport("ADVAPI32.DLL", SetLastError = true)]
		public extern static bool DuplicateToken(IntPtr ExistingTokenHandle, int SECURITY_IMPERSONATION_LEVEL,
																out IntPtr DuplicateTokenHandle);
		[DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
		internal static extern int CloseHandle(IntPtr hObject);

		public enum SecurityImpersonationLevel : int
		{
			/// <summary>
			/// The server process cannot obtain identification information about the client, 
			/// and it cannot impersonate the client. It is defined with no value given, and thus, 
			/// by ANSI C rules, defaults to a value of zero. 
			/// </summary>
			SecurityAnonymous = 0,

			/// <summary>
			/// The server process can obtain information about the client, such as security identifiers and privileges, 
			/// but it cannot impersonate the client. This is useful for servers that export their own objects, 
			/// for example, database products that export tables and views. 
			/// Using the retrieved client-security information, the server can make access-validation decisions without 
			/// being able to use other services that are using the client's security context. 
			/// </summary>
			SecurityIdentification = 1,

			/// <summary>
			/// The server process can impersonate the client's security context on its local system. 
			/// The server cannot impersonate the client on remote systems. 
			/// </summary>
			SecurityImpersonation = 2,

			/// <summary>
			/// The server process can impersonate the client's security context on remote systems. 
			/// NOTE: Windows NT:  This impersonation level is not supported.
			/// </summary>
			SecurityDelegation = 3,
		}
		#endregion

		#region Internal Globals
		private string m_dataPathRoot;
		private bool m_redundant = false;
		private bool m_impersonated = false;
		private string m_errMsg;
		private string m_debugMsg;
		private int m_serverCount = 0;
		private string[] m_serverIP;
		private string m_repUser = "4Tell";
		private string m_repDomain = ".";
		private string m_repPassword = "2Predict";
		private string m_boostVersion = "2.0";
		private bool m_localCopyDone; //temp flag to tell me that local copy is done while IO tobleshoot network copy
		private static readonly Replicator m_instance = new Replicator();
		#endregion

		#region External Parameters
		public static Replicator Instance { get { return m_instance; } }
		public bool Redundant { get { return m_redundant; } }
		public bool Impersonated { get { return m_impersonated; } }
		public string ErrorText 
		{ 
			get { return m_errMsg; }
			set { m_errMsg = value; }
		}
		public string DebugText 
		{ 
			get { return m_debugMsg; }
			set { m_debugMsg = value; }
		}
		#endregion

		//main constructor is private because Replicator is a singleton
		//use Replicator.Instance instead of new Replicator()
		private Replicator()
		{
			m_dataPathRoot = DataPath.Instance.Root;
			m_errMsg = "";
			m_debugMsg = "";
			CheckRedundancy();
			//AttemptImpersonation();
		}

		//Check config settings to see if there are any redundant servers
		private bool CheckRedundancy()
		{
			m_redundant = false;
			List<string> fullIpList, cleanIpList;
			m_errMsg = "";

			try
			{
				m_boostVersion = ConfigurationManager.AppSettings.Get("BoostVersion");
				m_serverCount = Convert.ToInt32(ConfigurationManager.AppSettings.Get("RedundantServerCount"));
				if (m_serverCount == 0)
					return false; //no redundant servers

				fullIpList = new List<string>();
				cleanIpList = new List<string>();
				for (int i = 0; i < m_serverCount; i++)
					fullIpList.Add(ConfigurationManager.AppSettings.Get("ServerAddress" + (i + 1).ToString()));

				//get the local IP address so that we skip copy to this server
				string host = Dns.GetHostName();
				IPAddress[] localIPs = Dns.GetHostAddresses(host);

				foreach (string listIP in fullIpList) //looking for a match between the local host IP list and the config list
				{
					bool matched = false;
					foreach (IPAddress thisIP in localIPs)
					{
						string temp = thisIP.ToString();
						if (temp.Equals(listIP))
						{
							matched = true;
#if DEBUG
							m_debugMsg += "This IP = " + temp + "\n";
#endif
							break;
						}
					}
					if (!matched)  //found an ip that is not in the local IP list
						cleanIpList.Add(listIP);
				}

				//now setup the real server list excluding matches to this server
				m_serverCount = cleanIpList.Count;
				if (m_serverCount > 0)
				{
					m_redundant = true;
					m_serverIP = new string[m_serverCount];
					m_serverIP = cleanIpList.ToArray();
				}

			}
			catch (Exception ex)
			{
				m_errMsg += "Error checking redundancy\nException: " + ex.Message;
				if (ex.InnerException != null)
					m_errMsg += "\nInner Exception: " + ex.InnerException.Message;
			}
			
			return m_redundant;
		}

		//Login as 4Tell to get access to folders on remote server 
		//---------This method does not seem to work. using 4Tell as app pool identity instead-------------
		// (4Tell login must exist on all servers)
		private bool AttemptImpersonation()
		{
			m_impersonated = false;
			m_errMsg = "";
			
			// initialize tokens
			IntPtr pExistingTokenHandle = new IntPtr(0);
			IntPtr pDuplicateTokenHandle = new IntPtr(0);
			pExistingTokenHandle = IntPtr.Zero;
			pDuplicateTokenHandle = IntPtr.Zero;

			// Get username and password from web.config
			string sUser = ConfigurationManager.AppSettings.Get("ReplicationUser");
			if ((sUser != null) && (sUser.Length > 0))
				m_repUser = sUser;
			string sDomain = ConfigurationManager.AppSettings.Get("ReplicationDomain");
			if ((sDomain != null) && (sDomain.Length > 0))
				m_repDomain = sDomain;
			string sPwd = ConfigurationManager.AppSettings.Get("ReplicationPassword");
			if ((sPwd != null) && (sPwd.Length > 0))
				m_repPassword = sPwd;

			try
			{
				#if DEBUG
					// Get identity before impersonation
					m_debugMsg += "\nBefore impersonation: " + WindowsIdentity.GetCurrent().Name + "\n"
							 + "New User = " + m_repUser + "\nNew Domain = " + m_repDomain + "\n";
				#endif

				// create token
				const int LOGON32_PROVIDER_DEFAULT = 0;
				const int LOGON32_LOGON_NEW_CREDENTIALS = 9;
				//const int LOGON32_LOGON_INTERACTIVE = 2;
				//const int SecurityImpersonation = 2;

				// get handle to token
				bool loggedOn = LogonUser(m_repUser, m_repDomain, m_repPassword, LOGON32_LOGON_NEW_CREDENTIALS, 
											LOGON32_PROVIDER_DEFAULT, out pExistingTokenHandle);

				// did impersonation fail?
				if (!loggedOn)
				{
					int nErrorCode = Marshal.GetLastWin32Error();
					m_errMsg += "LogonUser() failed with error code: " + nErrorCode + "\n"
							 + "New User = " + m_repUser + "\nNew Domain = " + m_repDomain;
				}
				else
				{
					bool tokened = DuplicateToken(pExistingTokenHandle,
							(int)SecurityImpersonationLevel.SecurityImpersonation, out pDuplicateTokenHandle);

					// did DuplicateToken fail?
					if (!tokened)
					{
						int nErrorCode = Marshal.GetLastWin32Error();
						// close existing handle
						CloseHandle(pExistingTokenHandle);
						m_errMsg += "DuplicateToken() failed with error code: " + nErrorCode + "\n";
					}
					else
					{
						// create new identity using new primary token
						WindowsIdentity newId = new WindowsIdentity(pDuplicateTokenHandle);
						WindowsImpersonationContext impersonatedUser = newId.Impersonate();
						m_impersonated = true;
					}
				}
				
				// check the identity after impersonation
				#if DEBUG
					m_debugMsg += "After impersonation: " + WindowsIdentity.GetCurrent().Name + "\n";
				#endif
			}
			catch (Exception ex)
			{
				m_errMsg += "\nAttemptImpersonation Exception thrown: " + ex.Message;
				if (ex.InnerException != null)
					m_errMsg += "\n\nInner Exception = " + ex.InnerException.Message;
			}
			finally
			{
				// close handle(s)
				if (pExistingTokenHandle != IntPtr.Zero)
					CloseHandle(pExistingTokenHandle);
				if (pDuplicateTokenHandle != IntPtr.Zero)
					CloseHandle(pDuplicateTokenHandle);
			}
			return m_impersonated;
		}

		public bool CopyFiles(string fromFolder, string toFolder, bool all)
		{
			//RULES:
			//all==false: copy one file (both from and to paths include filename)
			//all==true: copy all files in folder (both paths to folders with no filename)
			//if folders are equal then don't copy to this server
			//always copy files to all the servers in the web.config server list (this server is not in the list)

			string fromPath = m_dataPathRoot + fromFolder;
			m_localCopyDone = false;
			m_errMsg = "";
			string[] files;

			try
			{
				//create the file list (only one file if all==false)
				if (all)
				{
					files = Directory.GetFiles(fromPath, "*.txt");
				}
				else
				{
					files = new string[1];
					files[0] = fromPath;
				}

				//copy files locally only if paths are different 
				if (!fromPath.EndsWith(toFolder))
				{
					string toPath = m_dataPathRoot + toFolder;
					#if DEBUG
					m_errMsg += "Local copy from: " + fromPath + "\n to: " + toPath + "\n"; //(extra verbose for debugging)
					#endif

					//if copying all then clear the toFolder first
					if (all)
					{
						try
						{
							string[] oldFiles = Directory.GetFiles(toPath, "*.txt");
							foreach (string oldFile in oldFiles)
								File.Delete(oldFile);
						}
						catch (Exception ex)
						{
							m_errMsg += "\nError deleting files in: " + toPath;
							m_errMsg += "\nException thrown: " + ex.Message;
							if (ex.InnerException != null)
								m_errMsg += "\n\nInner Exception = " + ex.InnerException.Message;
						}
					}

					foreach (string file in files)
					{
						if (all) //then toPath is just the directory
						{
							string name = Path.GetFileName(file);
							#if DEBUG
							m_errMsg += name + "\n"; //(extra verbose for debugging)
							#endif
							File.Copy(file, toPath + name, true);
						}
						else //toPath includes the filename and there is only one file
							File.Copy(file, toPath, true);
					}
				}
				#if DEBUG
					else
					m_errMsg += "No local copy needed\n"; //(extra verbose for debugging)
				#endif
				m_localCopyDone = true; //local copy completed successfully
			}
			catch (Exception ex)
			{
				m_errMsg += "\nCopyFiles Exception thrown: " + ex.Message;
				if (ex.InnerException != null)
					m_errMsg += "\n\nInner Exception = " + ex.InnerException.Message;
				return false;
			}

			//check for redundant servers -- will need to copy files to each
			bool successAll = false;
			if (m_redundant)
			{
				//if (!m_impersonated)
				//  AttemptImpersonation();
					
				//if (m_impersonated)
				//{
					//copy to redundant servers
					successAll = true;
					string toPath = ""; 
					
					foreach (string ip in m_serverIP)
					{
						//each main boost version data folder must be shared on each server
						toPath = string.Format("\\\\{0}\\4-Tell{1}\\{2}", ip, m_boostVersion, toFolder);
						#if DEBUG
							m_debugMsg += "\nNetwork copy to: " + toPath + "\n"; //(extra verbose for debugging)
						#endif

						//if copying all then clear text files from the toFolder first
						if (all)
						{
							try
							{
								string[] oldFiles = Directory.GetFiles(toPath, "*.txt");
								foreach (string oldFile in oldFiles)
									File.Delete(oldFile);
							}
							catch (Exception ex)
							{
								m_errMsg += "\nError deleting files in: " + toPath;
								m_errMsg += "\nException thrown: " + ex.Message;
								if (ex.InnerException != null)
									m_errMsg += "\n\nInner Exception = " + ex.InnerException.Message;
							}
						}

						try
						{
							foreach (string file in files)
							{
								//check permission
								//FileIOPermissionAccess acc = ;
								//FileIOPermission per = new System.Security.Permissions.FileIOPermission(

								if (all) //then path is just the directory
								{
									string name = Path.GetFileName(file);
									#if DEBUG
										m_debugMsg += name + "\n"; //(extra verbose for debugging)
									#endif
									File.Copy(file, toPath + name, true);
								}
								else //path includes the filename and there is only one file
									File.Copy(file, toPath, true);
							}
						}
						catch (Exception ex)
						{
							successAll = false;
							m_errMsg += "\nNetwork Copy Exception thrown: " + ex.Message;
							if (ex.InnerException != null)
								m_errMsg += "\n\nInner Exception = " + ex.InnerException.Message;
						}
					} //for each ip
				//} //if impersonated
			} //if redundant
			
			#if DEBUG
				m_debugMsg += "\nCopyFiles Complete"; //(extra verbose for debugging)
			#endif

			//currently we are treating network replication errors as internal and only logging them
			//clients are only informed about local copy errors
			//to inform on network copy errors we would return successAll
			return m_localCopyDone; 
		}

		public bool ReloadTables(string clientAlias)
		{
			string function = "rest/ReloadDataTables";
			string query = "clientAlias=" + clientAlias;
			string method = "POST";
			bool result = ReplicateServiceCall(function, query, "true", null, method);
			return result;
		}

		public bool AppendReport(string clientAlias, string report, string logType)
		{
			string function = "sale/UploadData/appendReport";
			string query = "clientAlias=" + clientAlias + "&report=" + report + "&type=" + logType;
			string method = "POST";
			bool result = ReplicateServiceCall(function, query, "", null, method);
			return result;
		}

		//replicate files to other servers through replicator
		public void ReplicateDataFile(string alias, bool inUpload, string filename)
		{
			m_errMsg = "";
			bool success = false;
			try
			{
				string fromFolder = alias + "\\";
				if (inUpload) fromFolder += "upload\\";
				fromFolder += filename;
				success = CopyFiles(fromFolder, fromFolder, false);
				if (!success)
					throw new Exception(m_errMsg);
			}
			catch (Exception ex)
			{
				m_errMsg = string.Format("Error replicating {0} for {1}:\n{2}", filename, alias, ex.Message);
				if (ex.InnerException != null)
					m_errMsg += "\nInner Exception: " + ex.InnerException.Message;
				throw new Exception(m_errMsg);
			}
		}

		//convenient place to replicate files to other servers through replicator
		public void ReplicateAllData(string alias)
		{
			m_errMsg = "";
			bool success = false;
			try
			{
				string fromFolder = alias + "\\";
				success = CopyFiles(fromFolder, fromFolder, true);
				if (!success)
					throw new Exception(ErrorText);
			}
			catch (Exception ex)
			{
				string errMsg = string.Format("Error replicating data files for {1}:\n{2}", alias, ex.Message);
				if (ex.InnerException != null)
					errMsg += "\nInner Exception: " + ex.InnerException.Message;
				throw new Exception(errMsg);
			}
		}

		//starting point for replicating reports to other servers through replicator
		public void ReplicateReport(string alias, string report, string type)
		{
			m_errMsg = "";
			bool success = false;
			try
			{
				success = AppendReport(alias, report, type);
				if (!success)
					throw new Exception(m_errMsg);
			}
			catch (Exception ex)
			{
				m_errMsg = string.Format("Error replicating {0} for {1}:\n{2}", type, alias, ex.Message);
				if (ex.InnerException != null)
					m_errMsg += "\nInner Exception: " + ex.InnerException.Message;
				throw new Exception(m_errMsg);
			}
		}

		protected bool ReplicateServiceCall(string function, string queryData = null, string requiredResult = "", byte[] bData = null, string method = "GET", string contentType = "text/plain")
		{
			m_errMsg = "";

			//only reloads other servers, not the local one
			// local service should be reloaded from direct call to RecFilter
			if (!m_redundant)
				return true; //nothing to do

			bool successEach = false;
			bool successAll = true;
			string result = "";

			foreach (string ip in m_serverIP)
			{
				string functionUri = string.Format("http://{0}/Boost{1}/{2}", ip, m_boostVersion, function);
				try
				{
					result = GetServiceResponseREST(functionUri, queryData, bData, method, contentType);
					if (requiredResult.Length > 0)
						successEach = result.ToLower().Contains(requiredResult);
					else
						successEach = true;
					if (!successEach)
					{
						successAll = false;
						m_errMsg += string.Format("Error calling {0} on server {1}\nResult = {2}\n", function, ip, result);
					}
#if DEBUG
					else
						m_debugMsg += string.Format("Success calling {0} on server {1}\n", function, ip); //(extra verbose for debugging)
#endif
				}
				catch (Exception ex)
				{
					successAll = false;
					m_errMsg += string.Format("Error calling {0} on server {1}\nException: {2}\n", function, ip, ex.Message);
					if (ex.InnerException != null)
						m_errMsg += "Inner Exception: " + ex.InnerException.Message + "\n";
				}
			}
			//if (!successAll)
			//  m_log.WriteEntry(errMsg, EventLogEntryType.Warning);

//#if DEBUG
			//else
			//  m_log.WriteEntry(errMsg, EventLogEntryType.Information);
//#endif

			return successAll;
		}

		protected string GetServiceResponseREST(string functionUri, string queryData = null, byte[] bData = null, string method = "GET", string contentType = "text/plain")
		{
			string result = "";

			HttpWebRequest request = null;
			HttpWebResponse response = null;

			try
			{
				if (queryData != null)
					if (queryData.Length > 0)
						functionUri += "?" + queryData;
				Uri serviceURI = new Uri(functionUri);
				request = WebRequest.Create(serviceURI) as HttpWebRequest;
				request.Method = method;
				request.ContentType = contentType;
				request.ContentLength = 0;

				if (bData != null && bData.Length > 0)
				{
					// Write data  
					request.ContentLength = bData.Length;
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
			catch (WebException wex)
			{
				result += "WebFaultException = " + wex.Message;
				result += "\nStatus = " + wex.Status.ToString();
				if (wex.InnerException != null)
					result += "\n\nInner Exception = " + wex.InnerException.Message;
				// Get the response stream  
				HttpWebResponse wexResponse = (HttpWebResponse)wex.Response;
				if (wexResponse != null)
				{
					StreamReader reader = new StreamReader(wexResponse.GetResponseStream());
					result += "\nException Response = " + reader.ReadToEnd();
				}
				if (response != null)
					if (response.ContentLength > 0)
						result += "\n\nResponse = " + response.StatusDescription;
			}
			catch (Exception ex)
			{
				result += "Exception = " + ex.Message;
				if (ex.InnerException != null)
					result += "\n\nInner Exception = " + ex.InnerException.Message;
				result += "\nStackTrace = " + ex.StackTrace.ToString();
				if (response != null)
					if (response.ContentLength > 0)
						result += "\n\nResponse = " + response.StatusDescription;
			}
			#endregion

			return result;
		}
	} //class
} //namespace
