//#define WEB
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Configuration;
using System.Timers;
using System.Threading;
using System.ServiceModel; //OperationContext
using System.ServiceModel.Channels; //MessageProperties RemoteEndpointMessageProperty
using _4_Tell.IO;


namespace _4_Tell.Utilities
{
	#region Supporting Classes

	public enum ReportLevel : byte
	{
	// order of increasing filter (as we move down the list, lesser items are filtered out)
		All,
		Information,
		Warrning, 
		Error, 
		None
	}

	public class BoostError
	{
		public string Alias = "";
		public string Message = "";
		public EventLogEntryType Type = EventLogEntryType.Information;
		public DateTime Time = DateTime.MinValue; 
	}

	public class ClientPOC
	{
		public string Name = "";
		public string Email = "";
		public ReportLevel Report = ReportLevel.None;
	}
	
	#endregion

	public sealed class BoostLog //BoostLog is a singleton
	{
		#region Internal Parameters
		private static Replicator m_replicator = null;
		private static UsageLog m_usageLog = null;
		private static object m_errWriteLock = new object();

		//event log parameters
		private static EventLog m_eventLog = null;
		private static string m_gmailUsername = "";
		private static string m_gmailPassword = "";
		private static string m_gmailToAddress = "";
		private static ReportLevel m_adminReportLevel = ReportLevel.All;
		private static ReportLevel m_clientReportLevel = ReportLevel.None;
		private static string[] m_logBlockList = null; //list of clients to block from logging
		private string m_errSuffix = "";
		private string m_ServerId = "";
		private static readonly BoostLog m_instance = new BoostLog(); 
		#endregion
		
		#region External Parameters
		public static BoostLog Instance { get { return m_instance; } }
		public string Suffix
		{
			set { m_errSuffix = value; }
			get { return m_errSuffix; }
		}
		public string ServerId
		{
			set { m_ServerId = value; }
			get { return m_ServerId; }
		}
		#endregion

		//main constructor is private because BoostLog is a singleton
		//use BoostLog.Instance instead of new BoostLog()
		private BoostLog() 
		{
			string source = "4-Tell Boost";

			try
			{
				//start logging to system event log
				source = ConfigurationManager.AppSettings.Get("LogSource");
				if (source == null) source = "Unidentified";
				string logMsg = "4-Tell Event Log started for " + source;
				m_eventLog = new EventLog("4-Tell");
				m_eventLog.Source = source;

				//log any replicator startup issues
				m_replicator = Replicator.Instance; 
				if (m_replicator.ErrorText.Length > 0)
					WriteEntry(m_replicator.ErrorText, EventLogEntryType.Warning);
				if (m_replicator.DebugText.Length > 0)
					WriteEntry(m_replicator.DebugText, EventLogEntryType.Information);
				m_replicator.ErrorText = "";
				m_replicator.DebugText = "";

				//Read Gmail settings from web.config
				m_gmailUsername = ConfigurationManager.AppSettings.Get("GmailUsername");
				m_gmailPassword = ConfigurationManager.AppSettings.Get("GmailPassword");
				m_gmailToAddress = ConfigurationManager.AppSettings.Get("GmailToAdress");
				string level = ConfigurationManager.AppSettings.Get("AdminReportLevel");
				m_adminReportLevel = GetReportLevel(level);
				level = ConfigurationManager.AppSettings.Get("CustomerReportLevel");
				m_clientReportLevel = GetReportLevel(level);
				ClientPOC admin = new ClientPOC();
				admin.Name = "Admin";
				admin.Email = m_gmailToAddress;
				admin.Report = m_adminReportLevel;

				//log any usage log issues
				m_usageLog = UsageLog.Instance;
				if (m_usageLog.ErrorText.Length > 0)
					WriteEntry(m_usageLog.ErrorText, EventLogEntryType.Warning);
				m_usageLog.ErrorText = "";
				m_usageLog.AddClient("4Tell", admin); //create 4-tell client to record errors that don't relate to a client

				//Block logging for certain clients
				string blockList = ConfigurationManager.AppSettings.Get("LogBlockList");
				if ((blockList != null) && (blockList.Length > 0))
				{
					logMsg += "\nLogging will be blocked for the following clients:\n";
					m_logBlockList = blockList.Split(',',' '); //convert comma separated list to array
					foreach (string alias in m_logBlockList)
						logMsg += alias + "\n";
				}

				WriteEntry(logMsg, EventLogEntryType.Information);
			}
			catch (Exception ex)
			{
				string errMsg = "Initialization Error for " + source + " Log: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception: " + ex.InnerException.Message;
				if (m_eventLog != null)
					m_eventLog.WriteEntry(errMsg, EventLogEntryType.Error);
			}
		}

		#region Error Logging

		public void WriteEntry(string message, EventLogEntryType type, string clientAlias = "")
		{
			if (clientAlias == null) clientAlias = "";
			BoostError error = new BoostError();
			error.Time = DateTime.Now;
			error.Message = message; 
			error.Type = type;
			error.Alias = clientAlias;

			//m_errSuffix should be populated with the service call and parameters
			if (m_errSuffix.Length > 0) //add suffix here to avoid threading issues
				error.Message += "\n" + m_errSuffix;
			//if client alias sent in then add it to the message
			if ((clientAlias.Length > 0) && !m_errSuffix.Contains(clientAlias))
				error.Message += "\nClientAlias = " + clientAlias;

			ThreadPool.QueueUserWorkItem(delegate(object e)
			{
				WriteEntry((BoostError)e);
			}, error);
		}
		
		//private Error Log function to be run in a separate thread
		private void WriteEntry(BoostError error)
		{
			if (error.Message.Length < 1)
				return; //nothing to do

			//truncate if too long
			const int logLen = 10000;
			error.Message = CheckLength(error.Message, logLen);

			ClientPOC poc = null;
			bool blockLog = false; 
			ClientLog client = null; 
			if (m_usageLog.GetClient(error.Alias, out client, false))
				poc = client.POC;
			else
				poc = new ClientPOC();

			if ((m_logBlockList != null) && (error.Alias != null) && (error.Alias.Length > 0))
			{
				//block this message from the log (if any clients are generating too many errors)
				foreach (string alias in m_logBlockList)
					if (alias.Equals(error.Alias))
					{
						blockLog = true;
						break;
					}
			}

			lock (m_errWriteLock)
			{
				//log all messages (unless blocked above)
				if (!blockLog && (m_eventLog != null))
					m_eventLog.WriteEntry(error.Message, error.Type);

				//email certain messages	
				//NOTE: no longer using m_clientReportLevel ---using ClientPOC from ConfigBoost
				string subject = "";
				bool sendAdmin = true;
				bool sendClient = false;
				switch (error.Type)
				{
					case EventLogEntryType.Error:
						subject = "Error";
						sendAdmin = ((int)m_adminReportLevel <= (int)(ReportLevel.Error));
						sendClient = ((int)(poc.Report) <= (int)(ReportLevel.Error));
						m_usageLog.AddError(error.Alias); //add to error tally
						m_usageLog.SetLastError(error);	//replace last error
						break;
					case EventLogEntryType.Warning:
						subject = "Warning";
						sendAdmin = ((int)m_adminReportLevel <= (int)(ReportLevel.Warrning));
						sendClient = ((int)(poc.Report) <= (int)(ReportLevel.Warrning));
						m_usageLog.AddWarning(error.Alias); //add to warning tally
						m_usageLog.SetLastError(error);	//replace last error
						break;
					case EventLogEntryType.Information:
						subject = "Status Update";
						sendAdmin = ((int)m_adminReportLevel <= (int)(ReportLevel.Information));
						sendClient = ((int)(poc.Report) <= (int)(ReportLevel.Information));
						break;
					default:
						subject = "Unknown EventType";
						sendAdmin = true;
						sendClient = false;
						break;
				}

				if (sendClient && (poc.Email.Length > 0))
				{
					string preMessage = "This is an auto-generated email from the 4-Tell Boost service."
														+ "If you would rather not receive these email notices, please adjust "
														+ "your configuration settings or contact us at support@4-tell.com\n\n";
					try
					{
						Gmail.GmailMessage.SendFromGmail(m_gmailUsername, m_gmailPassword, poc.Email,
																							subject, preMessage + error.Message, m_ServerId, true);
					}
					catch (Exception ex)
					{
						string errMsg = "Error sending email to " + poc.Name + " <" + poc.Email + ">\n"
							+ ex.Message + "\n\nOriginal message to send:\n" + preMessage + error.Message;
						if (!blockLog && (m_eventLog != null))
							m_eventLog.WriteEntry(errMsg, EventLogEntryType.Error);
					}

					//always send admin messages that are sent to clients
					error.Message += "\n\nThis message was emailed to client: " + poc.Name + " <" + poc.Email + ">";
					sendAdmin = true;
				}

				if (sendAdmin && (m_gmailToAddress.Length > 0))
				{
					try
					{
						Gmail.GmailMessage.SendFromGmail(m_gmailUsername, m_gmailPassword, m_gmailToAddress,
																							subject, error.Message, m_ServerId, true);
					}
					catch (Exception ex)
					{
						string errMsg = "Error sending email to " + m_gmailToAddress + "\n"
							+ ex.Message + "\n\nOriginal message to send:\n" + error.Message;
						if (!blockLog && (m_eventLog != null))
							m_eventLog.WriteEntry(errMsg, EventLogEntryType.Error);
					}
				}
			} //end errWritesLock
		}

		private string CheckLength(string text, int maxLen)
		{
			if (text.Length <= maxLen) return text;
			else return text.Trim().Substring(0, maxLen);
		}
		
		static public ReportLevel GetReportLevel(string level)
		{
			level = level.ToLower();
			if ( level.Equals("all") ) return ReportLevel.All;
			if ( level.Equals("information") ) return ReportLevel.Information;
			if ( level.Equals("warning") ) return ReportLevel.Warrning;
			if ( level.Equals("error") ) return ReportLevel.Error;
			if ( level.Equals("info") ) return ReportLevel.Information;
			else return ReportLevel.None;
		}
		#endregion
	
	}
}
