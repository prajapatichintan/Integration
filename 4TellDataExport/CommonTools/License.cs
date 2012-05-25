using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using _4_Tell.IO;

namespace _4_Tell.Utilities
{
	//The License class validates licenses for the entire recommendation service (not each client)
	//It reads in a license file that contains all valid client licenses in the format of client<TAB>Activatino code
	public class License
	{
		#region Internal Globals
		//key generation
		//The version will be encoded into the first character of the key and specifies the rest of the encoding
		//It is tied to the current alpha set, so if future versions change the alpha set, 
		//they will need to preserve this alpha set at least for version encoding/decoding
		//note that capitol letters are placed first in case future keys use all caps (the first 26 versions will use caps)
		private const int m_version = 1;
		private const string m_licGenAssbyName = "LicenseGenerator"; 
		private const string m_alphaSet = "CWMFJORDBANKGLYPHSVEXTQUIZcwmfjordbankglyphsvextquiz0123456789@&-!?'*"; 
		private const string m_cover = "2Predict";
		protected const int m_clientAliasLength = 8;
		private const int m_codeLength = 12; //code is version(1) + tier(1) + toolFlags(2) + mangled clientAlias(8)
		private const int m_keyLength = 24;
		private static readonly MD5 Md5 = MD5.Create();

		//initialization
		private static bool m_initFlag = false;
		private string m_errorMsg = "";
		private string m_dataPathRoot = ""; 
		private const string m_licenseName = "4-TellLicense.txt";
		private string m_licenseFilename = "";
		private List<LicenseInfo> m_clientList;	//invalid clients are disabled, but left in list
		private static bool m_allowGenerator = false;
		public ToolTypes ToolType;
		#endregion

		#region External Parameters
		public enum ToolTypes
		{
			Generator = 0,
			Dashboard,
			WebService,
			EmailService,
			LicenseGenerator = 255
		};
		public struct LicenseInfo
		{
			public string clientAlias;
			public int tier;
			public int maxUsers;
			public byte toolFlags;
			public string key;
			public bool isValid;

			public void Clear()
			{
				clientAlias = "";
				tier = 0;
				maxUsers = 0;
				toolFlags = 0x0;
				key = "";
				isValid = false;
			}
			public void Copy(LicenseInfo li)
			{
				clientAlias = li.clientAlias;
				tier = li.tier;
				maxUsers = li.maxUsers;
				toolFlags = li.toolFlags;
				key = li.key;
				isValid = li.isValid;
			}
		}
		public string ErrorText
		{
			set { m_errorMsg = value; }
			get { return m_errorMsg; }
		}
		public int ClientCount
		{
			get { return (m_clientList == null) ? 0 : m_clientList.Count; }
		}
		public string DataPathRoot
		{
			get { return m_dataPathRoot; }
			set { m_dataPathRoot = value; }
		}
		public string LicenseName
		{
			get { return m_licenseName; }
		}
		public string LicenseFilename
		{
			get { return m_licenseFilename; }
		}
		#endregion

		#region Exposed Member Functions
		//Initialize must at least be called once when the program starts
		//but may be called more than once in the case where the file path is changed
		//for example, when it fails the first time, let the user locate the license file...
		public virtual bool Initialize(ToolTypes tool, string dataPathRoot = "") 
		{
			m_errorMsg = "";
			ToolType = tool;

			//special case: only let the LicenseGen child class identify itself as a generator
			if (tool == ToolTypes.LicenseGenerator)
			{
				Assembly caller = Assembly.GetCallingAssembly();
				string callerName = caller.FullName;
				if (!m_allowGenerator || !caller.FullName.Contains(m_licGenAssbyName))
				{
					m_errorMsg = "Illegal tool type: LicenseGenerator";
					m_initFlag = false;
					return false;
				}
			}

			if (dataPathRoot.Length > 0)
			{
				m_dataPathRoot = dataPathRoot;
				if (!m_dataPathRoot.EndsWith("\\")) m_dataPathRoot += "\\";
			}
			else
			{
				m_dataPathRoot = DataPath.Instance.Root;
			}
			//Validate that an external license file exists and is not expired
			m_licenseFilename = m_dataPathRoot + m_licenseName;
			if (!LoadLicenseFile())
			{
				//error message set in LoadLicenseFile()
				m_initFlag = false;
				return false;
			}

			//Then confirm external extivation
			m_initFlag = ConfirmActivation();

			return m_initFlag;
		}


		//ConfirmActivation will be called during Initialize() 
		//--- and should also be called periodically by the service while it is running
		public bool ConfirmActivation()
		{
			//TODO:V2 This will need to tie into a 4-Tell hosted webservice that validates license activation
			return true;
		}

		public LicenseInfo GetLicenseInfo(string clientAlias)
		{
			LicenseInfo Client = new LicenseInfo();
			Client.Clear(); //empty client response will signal error

			int index = GetLicenseIndex(clientAlias);
			if (index < 0) //unable to find client in list;
			{
				//error message compiled in GetLicenseIndex
				return Client;
			}
			// invalid clients are disabled, but left in list, so need to check here...
			if (!m_clientList[index].isValid) //invalid client (bad key or not authorized for this tool)
			{
				//error message compiled in GetLicenseIndex
				return Client;
			}
			
			//valid client
			Client.Copy(m_clientList[index]);
			Client.key = ""; //do not release the key externally
			return Client;
		}

		//GetLicenseIndex takes a client alias and returns the zero-based index of the client in the license file
		//-- it returns -1 if there is an error
		public int GetLicenseIndex( string clientAlias )
		{
			//check initialization
			m_errorMsg = "";
			if (!m_initFlag)
			{
				m_errorMsg = "License is not properly initialized. Request server log for details.";
				return -1;
			}
			//validate input
			if (clientAlias == null)
			{
				m_errorMsg = "Missing Client Alias for license request";
				return -1;
			}
			m_errorMsg = "Invalid License Request: clientAlias = " + clientAlias; //save message before CleanText alters the ID
			if (!CleanText(ref clientAlias, m_clientAliasLength)) return -1;
			m_errorMsg = ""; //clear error

			for (int i = 0; i < m_clientList.Count; i++)
			{
				if (m_clientList[i].clientAlias.Equals(clientAlias)) //found
				{
					// invalid clients are disabled, but left in list, so need to check here...
					if (m_clientList[i].isValid)
						m_errorMsg = clientAlias + ": License valid";

					else  //found but invalid
					{
						if (m_clientList[i].tier == 0) m_errorMsg = clientAlias + ": License invalid";
						else m_errorMsg = clientAlias + ": License not authorized for this product";
					}
					return i; //still return index, even for invalid clients so that AddLicense can properly replace them
				}
			}
			m_errorMsg = clientAlias + ": License not valid or not found";
			return -1;
		}

		//GetClient just returns the name of a client at a given index
		public string GetClientAlias(int index)
		{
			if (index < 0 || index >= m_clientList.Count) //invalid index
			{
				m_errorMsg = "Invalid client index";
				return null;
			}
			return m_clientList[index].clientAlias;
		}

		//Check to see if a given client is valid
		public bool IsClientValid(int index)
		{
			if (index < 0 || index >= m_clientList.Count) return false; //index out of range
			return m_clientList[index].isValid;
		}

		public bool AddLicense(string clientAlias, string key, bool replace)
		{
			//validate input
			if ((clientAlias.Length < 1)||(key.Length != m_keyLength))
			{
				m_errorMsg = string.Format("Invalid client ID or key\nClientAlias = {0}\nKey = {1}", clientAlias, key);
				return false;
			}
			int index = GetLicenseIndex(clientAlias);
			if ((index >= 0) && (!replace))
			{
				m_errorMsg = string.Format("Client not added. License already exists for {0}", clientAlias);
				return false;
			}
			LicenseInfo Client = new LicenseInfo();
			if ( !ValidateClient(ref Client, clientAlias, key) )
			{
				string tempMsg = m_errorMsg;
				m_errorMsg = string.Format("Unable to validate license key for client ID {0}\n{1}", 
					clientAlias, tempMsg);
				return false;
			}

			if (index >= 0) //replace existing
			{
				m_clientList[index].Copy(Client);
			}
			else //append to end
			{
				m_clientList.Add(Client);
			}

			//update the license file
			if (!UpdateLicenseFile())
				return false;

			//create client folders if needed
			string clientFolder = m_dataPathRoot + clientAlias;
			try
			{
				if (!Directory.Exists(clientFolder))
					Directory.CreateDirectory(clientFolder);

				clientFolder += "\\upload";
				if (!Directory.Exists(clientFolder))
					Directory.CreateDirectory(clientFolder);
			}
			catch (Exception ex)
			{
				m_errorMsg = string.Format("Unable to create client folder\nPath = {0}", clientFolder);
				m_errorMsg += "\nException: " + ex.Message;
				if (ex.InnerException != null)
					m_errorMsg += "\nInnerException: " + ex.InnerException.Message;
				return false;
			}

			return true;
		}

		public bool DeleteLicense(string clientAlias)
		{
			//validate input
			if (clientAlias.Length < 1)
			{
				m_errorMsg = "Client not deleted --no client alias provided";
				return false;
			}
			int index = GetLicenseIndex(clientAlias);
			if (index < 0)
			{
				m_errorMsg = string.Format("Client not deleted --cannot find {0}", clientAlias);
				return false;
			}

			//delete client
			try
			{
				LicenseInfo Client = m_clientList[index];
				m_clientList.Remove(Client);
			}
			catch (Exception ex)
			{
				m_errorMsg = string.Format("Unable to delete client {0}", clientAlias);
				m_errorMsg += "\nException: " + ex.Message;
				if (ex.InnerException != null)
					m_errorMsg += "\nInnerException: " + ex.InnerException.Message;
				return false;
			}

			//update the license file
			if (!UpdateLicenseFile())
				return false;
			return true;
		}

		private bool UpdateLicenseFile()
		{
			//replace the license file
			try
			{
				File.Delete(m_licenseFilename);
			}
			catch (Exception ex)
			{
				m_errorMsg = String.Format("Error: Could not remove license file {0}", m_licenseFilename);
				m_errorMsg += "\nException: " + ex.Message;
				if (ex.InnerException != null)
					m_errorMsg += "\nInnerException: " + ex.InnerException.Message;
				return false;
			}
			StreamWriter licenseFile = null;
			try
			{
				licenseFile = new StreamWriter(m_licenseFilename);
			}
			catch (Exception ex)
			{
				m_errorMsg = String.Format("Error: Could not create license file {0}", m_licenseFilename);
				m_errorMsg += "\nException: " + ex.Message;
				if (ex.InnerException != null)
					m_errorMsg += "\nInnerException: " + ex.InnerException.Message;
				return false;
			}
			try
			{
				for (int i = 0; i < m_clientList.Count; i++)
				{
					licenseFile.WriteLine("{0}\t{1}", m_clientList[i].clientAlias, m_clientList[i].key);
				}
			}
			catch (Exception ex)
			{
				m_errorMsg = string.Format("Could not write to license file\nPath = {0}", m_licenseFilename);
				m_errorMsg += "\nException: " + ex.Message;
				if (ex.InnerException != null)
					m_errorMsg += "\nInnerException: " + ex.InnerException.Message;
				licenseFile.Close();
				return false;
			}
			licenseFile.Close();
			return true;
		}
		#endregion

		#region Main Internals
		private bool LoadLicenseFile()
		{
			List<string> clientAliases = new List<string>();
			List<string> keys = new List<string>();

			//read client-key pairs from the external license file
			StreamReader licenseFile = null;
			try
			{
				licenseFile = new StreamReader(m_licenseFilename);
			}
			catch (Exception)
			{
				m_errorMsg = String.Format("\nCould not open license file {0}.\n\n", m_licenseFilename);
				return false;
			}
			string tempLine;
			string[] tempSplit;
			char delimeter = '\t';
			int count = 0;
			while (true) //read until end of file
			{
				tempLine = licenseFile.ReadLine();
				if (tempLine == null) break;
				if (tempLine.Length == 0) break;

				tempSplit = tempLine.Split(delimeter);
				if (tempSplit.Length != 2)
				{
					m_errorMsg = string.Format(
						"Error: trouble reading license %d in license file %s.\nThis client will be disabled.", 
						count + 1, m_licenseFilename);
					//skip this entry and allow the rest of the licenses to stand
					//TODO: should this be logged?
					continue;
				}
				clientAliases.Add(tempSplit[0]);
				keys.Add(tempSplit[1]);
				count++;
			}
			licenseFile.Close();
			if (count < 1) return false; //must be at least one license to return true;

			// always wipe out the list when reloading
			m_clientList = new List<LicenseInfo>();
			for (int i = 0; i < count; i++)
			{
				//always add the client to the list --validator will clear content if license is invalid
				LicenseInfo client = new LicenseInfo();
				ValidateClient(ref client, clientAliases[i], keys[i]);
				m_clientList.Add(client);
			}

			return true;
		}

		private bool ValidateClient(ref LicenseInfo Client, string clientAlias, string key)
		{
			if (!CleanText(ref clientAlias, m_clientAliasLength)) return false;

			if (!ExtractClientInfo(ref Client, key)) return false;//fill client info from the provided key

			//recalculate the key using the provided clientAlias
			string calcKey = CalcLicenseKey(clientAlias, Client.tier, Client.toolFlags);

			//compare client IDs and keys
			//string tempID = clientAlias.Substring(0, Client.clientAlias.Length); //Not needed because of clean...
			if ((!calcKey.Equals(key)) || (!clientAlias.Equals(Client.clientAlias)))
			{
				m_errorMsg = "Invalid key or client ID.";
				//invalid clients are disabled, but left in list, so keep client ID...
				Client.Clear();
				Client.clientAlias = clientAlias;
				return false;
			}
			//Check to see if this client can use this tool
			if (!IsToolAllowed(Client, ToolType))
			{
				m_errorMsg = "License is not authorized for this product.";
				Client.isValid = false;
				//key is valid in general, just not for this tool, so leave toolFlags etc alone
				return false;
			}

			Client.isValid = true;
			return true;
		}

		private bool IsToolAllowed(LicenseInfo Client, ToolTypes tool)
		{
			if (tool == ToolTypes.LicenseGenerator) return true;

			byte thisTool = (byte)(Math.Pow(2, (int)tool));
			return (thisTool & Client.toolFlags) > 0;
		}
		#endregion

		#region Protected Internals
		//NOTE: These are exposed in the LicenseGen subclass

		protected void AllowGenerator(bool value)
		{
			m_allowGenerator = value;
		}

		protected virtual bool ExtractClientInfo(ref LicenseInfo Client, string key)
		{
			string code = DeInterleave(key);
		  if (code == null) 
			{
				m_errorMsg = "Invalid key length.";
				Client.Clear();
				return false;
			}
			if (code.Length != m_codeLength)
			{
				//this should never be reached, but was added while debugging
				m_errorMsg = "Invalid code length.";
				Client.Clear();
				return false;
			}
			int version = AlphaDecode(key.Substring(0, 1)); //first character of the key is always the version
			if (version != m_version)
			{
				m_errorMsg = "Invalid license key.";
				Client.Clear();
				return false;
			}

			//character 0 was the version, 1 is tier, 2 & 3 are toolFlags
			Client.tier = AlphaDecode(code.Substring(1, 1));
			Client.toolFlags = (byte)AlphaDecode(code.Substring(2, 2));
			Client.clientAlias = UnMangleClientAliasAlpha(code.Substring(4));

			SetTierMaximums(ref Client);
			Client.key = key;

			return true;
		}

		protected virtual string CalcLicenseKey(string clientAlias, int tier, int toolFlags)
		{
			//if (!CleanText(ref clientAlias, m_clientAliasLength)) return ""; //shouldn't need this since the function is internal

			string prefix = AlphaEncode(m_version, 1) + AlphaEncode(tier, 1) + AlphaEncode(toolFlags, 2);
			string key1 = prefix + MangleClientAliasAlpha(clientAlias);
			string key2 = prefix + MangleClientAliasPrintable(clientAlias);

			string hash1 = GetMd5Sum(key1);
			string hash2 = GetMd5Sum(key2);
			string key3 = Interleave(key1, hash2);
			//using hash 2 just because it is there and this gives key generation further obscurity

			string padSymbol = m_alphaSet.Substring(m_alphaSet.Length - 1); //last character of alpha set
			if (key3.Length > m_keyLength) key3 = key3.Substring(0, m_keyLength);
			else while (key3.Length < m_keyLength) key3 += padSymbol; 
			//padding should not be needed - included for robustness to future changes

			return key3;
		}

		//clean up the text to remove characters not in the alpha set
		protected virtual bool CleanText(ref string text, int maxLength)
		{
			int index;
			for (int i = 0; i < text.Length; i++)
			{
				index = m_alphaSet.IndexOf(text.Substring(i, 1));
				if ((index < 0)||(index > m_alphaSet.Length - 2)) //last char of alphaSet is only for padding
					text = text.Remove(i--, 1);  //illegal character - remove it
			}
			if (text.Length > maxLength) text = text.Substring(0, maxLength);

			return (text.Length > 0);
		}
		#endregion

		#region Internal Utilities

		private void SetTierMaximums(ref LicenseInfo Client)
		{
			switch (Client.tier)
			{
				case 0: //invalid
					Client.maxUsers = 0;
					break;
				case 1: //bronze
					Client.maxUsers = 5000;
					break;
				case 2: //silver
					Client.maxUsers = 20000;
					break;
				case 3: //gold
					Client.maxUsers = 100000;
					break;
				case 4: //platinum
					Client.maxUsers = 300000;
					break;
				case 5: //titanium
					Client.maxUsers = int.MaxValue;
					break;
				default:
					goto case 0;
			}
		}

		//alternate code and hash to create key
		private string Interleave(string code, string hash)
		{
			//validate parameter string lenghts
			string padSymbol = m_alphaSet.Substring(m_alphaSet.Length - 1); //last character of alpha set
			if (code.Length > m_codeLength) code = code.Substring(0, m_codeLength);
			else while (code.Length < m_codeLength) code += padSymbol; //code can be padded (but shouldn't need it)
			if (hash.Length + m_codeLength < m_keyLength) return null; //hash too short

			//interleave code and hash until code runs out
			StringBuilder sb = new StringBuilder();
			for (int i = 0; sb.Length < m_keyLength; i++)
			{
				if (code.Length > i) 
				{
					sb.Append(code.Substring(i, 1));
				}
				sb.Append(hash.Substring(i, 1));
			}
			return sb.ToString();
		}

		//extract the client code out and discard the hash
		private string DeInterleave(string key)
		{
			//validate key length
			if (key.Length != m_keyLength) return null;

			StringBuilder sb = new StringBuilder();
			for (int i = 0; sb.Length < m_codeLength; i += 2)
			{
				sb.Append(key.Substring(i, 1));
			}
			return sb.ToString();
		}

		//combine the client ID with the cover and convert to printable UTF8 characters
		private string MangleClientAliasAlpha(string clientAlias)
		{
			//validate and adjust to 8chars (crop or zero-pad as necessary)
			if (clientAlias == null) return null;
			//if (!CleanText(ref clientAlias, m_clientAliasLength)) return null; //shouldn't need this since the function is internal
			string padSymbol = m_alphaSet.Substring(m_alphaSet.Length - 1); //last character of alpha set
			while (clientAlias.Length < m_clientAliasLength) clientAlias += padSymbol; 

			//get byte arrays
			byte[] input = AlphaEncode(clientAlias);
			byte[] cover = AlphaEncode(m_cover);
			byte[] result = new byte[m_clientAliasLength];

			//combine arrays (note that printable chacters are between 32 and 126 but 32 is a space) 
			int modulator = m_alphaSet.Length;
			for (int i = 0; i < m_clientAliasLength; i++)
			{
				result[i] = (byte)(((int)cover[i] + (int)input[i]) % modulator);
			}
			return AlphaDecode(result);
		}

		private string UnMangleClientAliasAlpha(string mangle)
		{
			//get byte arrays
			byte[] input = AlphaEncode(mangle);
			byte[] cover = AlphaEncode(m_cover);
			byte[] result = new byte[m_clientAliasLength];

			int modulator = m_alphaSet.Length;
			for (int i = 0; i < m_clientAliasLength; i++)
			{
				result[i] = (byte)(((int)input[i] - (int)cover[i] + modulator) % modulator);
			}
			string clientAlias = AlphaDecode(result);

			//remove zero padding
			string padSymbol = m_alphaSet.Substring(m_alphaSet.Length - 1); //last character of alpha set
			int end = clientAlias.IndexOf(padSymbol);
			if (end > 0) clientAlias = clientAlias.Substring(0, end);

			return clientAlias;
		}

		//combine the client ID with the cover and convert to printable UTF8 characters
		private string MangleClientAliasPrintable(string clientAlias)
		{
			//validate and adjust to 8chars (crop or zero-pad as necessary)
			if (clientAlias == null) return null;
			//if (!CleanText(ref clientAlias, m_clientAliasLength)) return null; //shouldn't need this since the function is internal
			string padSymbol = m_alphaSet.Substring(m_alphaSet.Length - 1); //last character of alpha set
			while (clientAlias.Length < m_clientAliasLength) clientAlias += padSymbol; 

			//get byte arrays
			byte[] input = Encoding.UTF8.GetBytes(clientAlias);
			byte[] cover = Encoding.UTF8.GetBytes(m_cover);
			byte[] result = new byte[m_clientAliasLength];

			//combine arrays (note that printable chacters are between 32 and 126 but 32 is a space) 
			for (int i = 0; i < m_clientAliasLength; i++)
			{
				result[i] = (byte)(((int)cover[i] - 33 + (int)input[i] - 33) % 94 + 33);
			}
			return Encoding.UTF8.GetString(result);
		}

		private string UnMangleClientAliasPrintable(string mangle)
		{
			//get byte arrays
			byte[] input = Encoding.UTF8.GetBytes(mangle);
			byte[] cover = Encoding.UTF8.GetBytes(m_cover);
			byte[] result = new byte[m_clientAliasLength];

			for (int i = 0; i < m_clientAliasLength; i++)
			{
				result[i] = (byte)(((int)input[i] - (int)cover[i] + 94) % 94 + 33);
			}
			string clientAlias = Encoding.UTF8.GetString(result);

			//remove zero padding
			string padSymbol = m_alphaSet.Substring(m_alphaSet.Length - 1); //last character of alpha set
			int end = clientAlias.IndexOf(padSymbol);
			if (end > 0) clientAlias = clientAlias.Substring(0, end);

			return clientAlias;
		}

		//Combine clientAlias with cover and convert to hex
		//Hex result is twice as long as clientAlias
		private string MangleClientAliasHex(string clientAlias)
		{
			//adjust to proper length (8 chars)
			if (clientAlias == null) return null;
			//if (!CleanText(ref clientAlias, m_clientAliasLength)) return null; //shouldn't need this since the function is internal
			string padSymbol = m_alphaSet.Substring(m_alphaSet.Length - 1); //last character of alpha set
			while (clientAlias.Length < m_clientAliasLength) clientAlias += padSymbol; 

			//get byte arrays
			byte[] input = Encoding.UTF8.GetBytes(clientAlias);
			byte[] cover = Encoding.UTF8.GetBytes(m_cover);
			byte[] result = new byte[m_clientAliasLength];

			//combine arrays and convert to hex 
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < m_clientAliasLength; i++)
			{
				result[i] = (byte)(((int)cover[i] + (int)input[i]) % 256);
				sb.Append(result[i].ToString("X2"));
			}
			return sb.ToString(); 
		}

		//reverse the HexMangle operation
		private string UnMangleClientAliasHex(string code)
		{
			if ((code.Length % 2) != 0) return null; //hex code must have even number of bytes

			//get byte arrays
			//byte[] input = Encoding.UTF8.GetBytes(code);
			byte[] input = new byte[code.Length / 2];
			byte[] cover = Encoding.UTF8.GetBytes(m_cover);
			byte[] result = new byte[m_clientAliasLength];

			for (int i = 0, j = 0; (j < code.Length - 1)&&(i < m_clientAliasLength); i++, j += 2)
			{
				input[i] = (byte)Convert.ToInt32(code.Substring(j, 2), 16);
				result[i] = (byte)(((int)input[i] - (int)cover[i] + 256) % 256);
			}
			string clientAlias = Encoding.UTF8.GetString(result);

			//remove zero padding
			string padSymbol = m_alphaSet.Substring(m_alphaSet.Length - 1); //last character of alpha set
			int end = clientAlias.IndexOf(padSymbol);
			if (end > 0) clientAlias = clientAlias.Substring(0, end);

			return clientAlias;
		}

		//standard MD5 Hash saved as a hex string
		public string GetMd5Sum(string inputString)
		{
			byte[] input = Encoding.UTF8.GetBytes(inputString);
			byte[] result = Md5.ComputeHash(input);
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < result.Length; i++)
			{
				sb.Append(result[i].ToString("X2"));
			}
			return sb.ToString();
		}

		//This version of AlphaEncode is used to produce hex-like values using the entire alpha set
		//Output will be a string representation of the input value using the characters
		//in the alpha set and using the length of the alpha set as the base (i.e. like hex, but base 68)
		private string AlphaEncode(int value, int length)
		{
			if (value < 0) return ""; //only positive values allowed

			string output = "";
			int baseFactor = m_alphaSet.Length; //this makes the encoding adaptable to future alpha set changes
			int digit = 0;
			while ((value = Math.DivRem(value, baseFactor, out digit)) > 0) //divide value by base to get next "digit"
			{
				output = m_alphaSet.Substring(digit, 1) + output; //string is built starting with lowest order byte so insert
			}
			output = m_alphaSet.Substring(digit, 1) + output; 

			if (output.Length > length) return ""; //value is too large for the length desired
			string zeroCode = m_alphaSet.Substring(0, 1);
			while (output.Length < length) output = zeroCode + output; //pad left with coded zeros
			return output;
		}

		//This version of AlphaDecode reverses the encoder directly above
		private int AlphaDecode(string codedValue)
		{
			int length = codedValue.Length;
			if (length < 1) return -1; //no string input

			int output = 0;
			int digit = 0;
			int baseFactor = m_alphaSet.Length; //this makes the encoding adaptable to future alpha set changes
			for (int i = 0; i < length; i++)
			{
				digit = m_alphaSet.IndexOf(codedValue.Substring(i, 1));
				if (digit < 0) return -1; //illegal character in input string

				output = output * baseFactor + digit; //generate the value one character at a time
			}

			return output;
		}

		//This version of AlphaEncode is used to create a byte array of values representing a given ID
		//The byte array is then mangled with a byte array derived from a cover string before decoding back to a string
		private byte[] AlphaEncode(string textID)
		{
			m_errorMsg = "";
			int index;
			int length = textID.Length;
			byte[] tempOut = new byte[length];
			for (int i = 0, j = 0; i < textID.Length; i++)
			{
				index = m_alphaSet.IndexOf(textID.Substring(i, 1));
				if (index < 0)
				{
					//Invalid character
					if (m_errorMsg.Length > 0) m_errorMsg += "\n"; //add return char before adding messages
					m_errorMsg += string.Format("Invalid character {0} skipped while encoding {1}", textID.Substring(i, 1), textID);
					length--; //adjust length of final output
					continue;
				}
				tempOut[j++] = (byte)index;
			}
			byte[] output = new byte[length];
			for (int i = 0; i < length; i++)
			{
				output[i] = tempOut[i];
			}
			return output;
		}

		//This version of AlphaDecode reverses the encoder directly above
		private string AlphaDecode(byte[] value)
		{
			m_errorMsg = "";
			StringBuilder sb = new StringBuilder();
			int maxAlphaIndex = m_alphaSet.Length;
			for (int i = 0; i < value.Length; i++)
			{
				if (value[i] >= maxAlphaIndex)
				{
					//Invalid character
					if (m_errorMsg.Length > 0) m_errorMsg += "\n"; //add return char before adding messages
					m_errorMsg += string.Format("Invalid value {0} skipped while decoding", value[i]);
					continue;
				}
				sb.Append(m_alphaSet.Substring(value[i], 1));
			}
			return sb.ToString();
		}

		#endregion
	}
}
