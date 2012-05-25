using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using System.Configuration;
using _4_Tell.Utilities;

namespace _4_Tell.IO
{
	public sealed class DataPath //Singleton
	{
		#region Instance

		private static readonly DataPath m_instance = new DataPath();
		private DataPath()
		{
			m_dataSubfolder = "";
			m_dataPathRoot = "";
			//check for custom data path
			try
			{
				m_dataSubfolder = ConfigurationManager.AppSettings.Get("DataPathSubfolder");
				m_dataPathRoot = ConfigurationManager.AppSettings.Get("FullDataPath");
				if ((m_dataSubfolder != null) && (m_dataSubfolder.Length > 0))
				{
					if (!m_dataSubfolder.StartsWith("\\")) m_dataSubfolder = "\\" + m_dataSubfolder;
					if (!m_dataSubfolder.EndsWith("\\")) m_dataSubfolder += "\\";
				}
			}
			catch { }
			if ((m_dataPathRoot != null) && (m_dataPathRoot.Length > 0)) //FullDataPath overrides everything
			{
				if (!m_dataPathRoot.EndsWith("\\")) m_dataPathRoot += "\\";
			}
			else
			{
				if ((m_dataSubfolder == null) || (m_dataSubfolder.Length < 1)) //none supplied so use default
				{
					m_dataSubfolder = "\\4-Tell2.0\\";
				}
				m_dataPathRoot = Environment.GetFolderPath(
										Environment.SpecialFolder.CommonApplicationData)
										+ m_dataSubfolder;
			}
		}
		public static DataPath Instance { get { return m_instance; } }
		#endregion
	
		// DataPathRoot
		private string m_dataSubfolder = "";
		private string m_dataPathRoot = "";
		public string Root { get { return m_dataPathRoot; } }

		//clean invalid chars from client alias
		static public string CleanFolderName(string name)
		{
			int index;
			char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
			while ((index = name.IndexOfAny(invalidChars)) != -1)
				name = name.Remove(index, 1);
			return name;
		}

		// Set ClientDataPath
		public string ClientDataPath(ref string clientAlias)
		{
			clientAlias = CleanFolderName(clientAlias);
			if (clientAlias.Length > 0)
			{
				string clientDataPath = m_dataPathRoot + clientAlias + "\\";
				return clientDataPath;
			}
			return null;
		}
	}
}