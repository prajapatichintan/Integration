using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Configuration;
using System.Timers;
using System.Threading;
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

	public class StopWatch
	{
		private long startTime = 0;
		private long lapStart = 0;
		private long lapEnd = 0;
		private long endTime = 0;
		private bool started = false;
		private bool ended = false;

		public StopWatch(bool start = false)
		{
			Reset();
			if (start) Start();
		}

		public void Reset()
		{
			startTime = 0;
			lapStart = 0;
			lapEnd = 0;
			endTime = 0;
			started = false;
			ended = false;
		}

		public void Start()
		{
			startTime = DateTime.Now.Ticks;
			lapStart = startTime;
			lapEnd = startTime;
			started = true;
			ended = false;
		}

		public string Lap()
		{
			if (!started) return "not started";

			lapEnd = DateTime.Now.Ticks;
			//TimeSpan lap = new TimeSpan(lapEnd - lapStart);
			long lap = lapEnd - lapStart;
			lapStart = lapEnd;
			return Format(lap);
		}

		public string Stop()
		{
			if (!started) return "not started";

			lapEnd = DateTime.Now.Ticks;
			//TimeSpan lap = new TimeSpan(lapEnd - lapStart);
			long lap = lapEnd - lapStart;
			endTime = lapEnd;
			ended = true;
			started = false;
			return Format(lap);
		}

		public string TotalTime //gets total for period up to last Lap or Stop command
		{
			get { return Format(lapEnd - startTime); }
		}

		private string Format(long ticks)
		{
			int ms = (int)(ticks / TimeSpan.TicksPerMillisecond);
			int s = ms / 1000;
			int m = s / 60;
			s %= 60;
			ms %= 1000;
			return string.Format("{0:0}:{1:00}.{2:000}", m, s, ms);
		}
	}

	//Accumulator class tallys up usage counts for one type of event 
	//ClientLog has one accumulator for each type of event being tracked (rec call, error, warning, etc)
	//Accumulator knows how to read and write it's own data to/from the log file
	//Will need to be updated when file changes to database
	public class Accumulator
	{
		public int Today = 0;
		public long ThisYear = 0;
		public long LastYear = 0;
		public long TTM = 0; //TTM = Trailing Twelve Months
		public int[] RollingMonths = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //12 months
		public int[] RollingHours = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //24 hours
		private int m_logMonth = DateTime.Now.Month - 1; //January = 0
		private int m_logHour = DateTime.Now.Hour;
		//note SetLogMonth and SetLogHour do not question whether the hour or month has changed
		//this logic was moved to the BoostLog level for efficiency (one "if" statement instead of num_clients x 6)

		public void Reset()
		{
			ResetHours();
			ResetMonths();
			ThisYear = 0;
			LastYear = 0;
		}

		public void ResetHours()
		{
			for (int i = 0; i < 24; i++)
				RollingHours[i] = 0;
			m_logHour = DateTime.Now.Hour;
			Today = 0;
		}

		public void ResetMonths()
		{
			for (int i = 0; i < 12; i++)
				RollingMonths[i] = 0;
			m_logMonth = DateTime.Now.Month - 1; //January = 0
			TTM = 0;
		}

		public void SetLogMonth(int month)
		{
			RollingMonths[month] = 0;
			m_logMonth = month;
			TTM = 0;
			for (int i = 0; i < 12; i++)
				TTM += RollingMonths[i];
		}

		public void SetLogHour(int hour)
		{
			RollingHours[hour] = 0;
			m_logHour = hour;
		}

		public void Add(int num = 1)
		{
			Today += num;
			ThisYear += num;
			TTM += num;
			RollingMonths[m_logMonth] += num;
			RollingHours[m_logHour] += num;
		}

		public void NewDay(DateTime now)
		{
			Today = 0;
			SetLogHour(now.Hour);
		}

		public void NewYear(DateTime now)
		{
			LastYear = ThisYear;
			ThisYear = 0;
			Today = 0;
			SetLogHour(now.Hour);
			SetLogMonth(now.Month - 1);  //January = 0
		}

		public string ReportUsage()
		{
			string report = Today.ToString() 
										+ "\t" + ThisYear.ToString() 
										+ "\t" + LastYear.ToString() 
										+ "\t" + TTM.ToString();
			foreach (int m in RollingMonths)
				report += "\t" + m.ToString();
			foreach (int h in RollingHours)
				report += "\t" + h.ToString();
			report += "\n";
			return report;
		}

		public void ReadUsage(StreamReader file, string type)
		{
			//streamReader must be pointing at the data line for this Accumulator
			try
			{
				string inputLine = file.ReadLine();
				string[] values = inputLine.Split('\t');
				if (values.Length != 41) //type, today, this-year, last-year, TTM, plus 12 months, plus 24 hours
					throw new Exception("Error reading usage data. Data length = " + values.Length.ToString());
				if (!values[0].Equals(type)) //first parameter is type of this accumulator (Calls, Errors, Warnings)
					throw new Exception("Error reading usage data for " + type + ". Type = " + values[0]);
				Today = Convert.ToInt32(values[1]);
				ThisYear = Convert.ToInt32(values[2]);
				LastYear = Convert.ToInt32(values[3]);
				TTM = Convert.ToInt32(values[4]);
				for (int i = 0; i < 12; i++)
					RollingMonths[i] = Convert.ToInt32(values[i + 5]);
				for (int i = 0; i < 24; i++)
					RollingHours[i] = Convert.ToInt32(values[i + 17]);
			}
			catch(Exception ex)
			{
				string msg = ex.Message + "\nAccumulator type: " + type;
				throw new Exception(msg, ex.InnerException);
			}
		}
	}

	//Transaction describes a request/response to the service for logging purposes
	public class Transaction
	{
		DateTime Date;
		private string m_ip;
		private string m_method;
		private string m_parameters;
		private string m_response;
		private string m_duration;

		public Transaction(DateTime date, string ip, string method, string parameters, string response, string duration)
		{
			Date = date;
			m_ip = ip;
			m_method = method;
			m_parameters = parameters;
			m_response = response;
			m_duration = duration;
		}

		public string Report(string delimiter)
		{
			string report = Date.ToString() + delimiter + m_ip + delimiter 
										+ m_method + delimiter + m_parameters + delimiter + m_response + delimiter + m_duration;
			return report;
		}
	}

	//transaction Log contains list of transactions
	//it collects transactions in memory until told to flush to a file
	//it knows how to archive the file if it gets too big
	public class TransactionLog
	{
		private string m_alias = "";
		private string m_path = "";
		private List<Transaction> m_transactions = new List<Transaction>();
		private static object m_transactionDataLock = new object();
		private static object m_transactionFileLock = new object();
		private const string m_transactionLogPrefix = "TransactionLog-";
		private const int m_maxLogSize = 1000000; //1MB

		public TransactionLog(string alias, string path)
		{
			m_alias = alias;
			m_path = path;
		}

		public void Add(DateTime date, string ip, string method, string parameters, string response, string duration)
		{
			Transaction item = new Transaction(date, ip, method, parameters, response, duration);

			lock (m_transactionDataLock) //must lock so new item is not added while list is being flushed
			{
				m_transactions.Add(item);
			}
		}

		//write transactions to file and flush memory (called from a separate thread)
		public void Flush()
		{
			if (m_transactions.Count < 1) return; //nothing to do

			string delimiter = "\t";
			string endOfLine = "\r\n";
			string report = "";

			lock (m_transactionDataLock) //must lock so new items cannot be added during flush
			{
				foreach (Transaction item in m_transactions)
					report += item.Report(delimiter) + endOfLine;

				//clear list each time it is reported
				m_transactions = new List<Transaction>();
			}

			//archive the log
			WriteTransactions(report);
		}

		//append transaction report to the log
		private void WriteTransactions(string report)
		{
			lock (m_transactionFileLock) //must lock to avoid simultaneous writes
			{
				string logPath = m_path + m_transactionLogPrefix + m_alias + ".txt";
				//archive the log if it is too big and start a new one
				if (File.Exists(logPath))
				{
					FileInfo fInfo = new FileInfo(logPath);
					if (fInfo.Length > m_maxLogSize)
					{
						string tempPath = m_path + m_transactionLogPrefix + m_alias;
						string archive;
						int ver = 1;
						while (File.Exists(archive = tempPath + "-" + ver.ToString() + ".txt"))
							ver++;
						File.Move(logPath, archive);
					}
				}
				using (StreamWriter file = new StreamWriter(logPath, true))
				{
					file.Write(report);
				}
			}
		}

	}

	//ClientLog contains usage tracking data for one client
	//It knows how to read and write it's own data to/from the log file
	//Will need to be updated when file changes to database
	public class ClientLog
	{
		public string Alias = "";
		public ClientPOC POC = new ClientPOC();
		public DateTime LastUpload = DateTime.MinValue;
		public DateTime LastGenerator = DateTime.MinValue;
		public BoostError LastError = new BoostError();
		public Accumulator Calls = new Accumulator();
		public Accumulator Errors = new Accumulator();
		public Accumulator Warnings = new Accumulator();
		public TransactionLog Transactions;

		public ClientLog(string alias, string dataPath)
		{
			Alias = alias;
			Transactions = new TransactionLog(alias, dataPath);
		}

		public void RestAll()
		{
			ResetHours();
			ResetMonths();
		}

		public void ResetHours()
		{
			Calls.ResetHours();
			Errors.ResetHours();
			Warnings.ResetHours();
		}

		public void ResetMonths()
		{
			Calls.ResetMonths();
			Errors.ResetMonths();
			Warnings.ResetMonths();
		}

		public void SetLogMonth(int month)
		{
			Calls.SetLogMonth(month);
			Errors.SetLogMonth(month);
			Warnings.SetLogMonth(month);
		}

		public void SetLogHour(int hour)
		{
			Calls.SetLogHour(hour);
			Errors.SetLogHour(hour);
			Warnings.SetLogHour(hour);
		}

		public void NewDay(DateTime now)
		{
			Calls.NewDay(now);
			Errors.NewDay(now);
			Warnings.NewDay(now);
		}

		public void NewYear(DateTime now)
		{
			Calls.NewYear(now);
			Errors.NewYear(now);
			Warnings.NewYear(now);
		}

		public string ReportUsage()
		{
			//create client report header
			string report = Alias + "\n"
							+ "Last Upload:\t" + LastUpload.ToString() + "\n"
							+ "Last Generator:\t" + LastGenerator.ToString() + "\n"
							+ "Type\tToday\tThisYr\tLastYr\tTTM\tJan\tFeb\tMar\tApr\tMay\tJun\tJul\tAug\tSep\tOct\tNov\tDec\t12am\t1am\t2am\t3am\t4am\t5am\t6am\t7am\t8am\t9am\t10am\t11am\t12pm\t1pm\t2pm\t3pm\t4pm\t5pm\t6pm\t7pm\t8pm\t9pm\t10pm\t11pm\n";
			
			//add accumulator reports
			report += "Calls\t" + Calls.ReportUsage();
			report += "Errors\t" + Errors.ReportUsage();
			report += "Warns\t" + Warnings.ReportUsage() 
								+ "\n"; //extra line break between clients
			return report;
		}

		public void ReadUsage(StreamReader file)
		{
			//StreamReader must be pointing at first line for this client (line after the client alias)

			//read last date data was uploaded
			string inputLine = file.ReadLine(); 
			string[] values = inputLine.Split('\t');
			if (values.Length < 2) //Last Upload:\t<date>
				throw new Exception("Error reading last upload date for " + Alias);
			this.LastUpload = DateTime.Parse(values[1]);

			//read last date data was generated successfully
			inputLine = file.ReadLine(); 
			values = inputLine.Split('\t');
			if (values.Length < 2) //Last Ganerator:\t<date>
				throw new Exception("Error reading last generator date for " + Alias);
			this.LastGenerator = DateTime.Parse(values[1]);

			//read accumulators
			inputLine = file.ReadLine(); //throw away the header line
			Calls.ReadUsage(file, "Calls");
			Errors.ReadUsage(file, "Errors");
			Warnings.ReadUsage(file, "Warns");
			inputLine = file.ReadLine(); //throw away the extra line feed
		}
	}
	#endregion

	class BoostLog : IDisposable
	{
		#region Internal Parameters
		//usage log parameters
		private bool disposed = false;
		private static List<ClientLog> m_clients = new List<ClientLog>();
		private static DateTime m_lastWriteDate = DateTime.MinValue;
		private static DateTime m_lastCheckDate = DateTime.MinValue;
		private static System.Timers.Timer m_transactionWriteTimer = new System.Timers.Timer();
		private static System.Timers.Timer m_dateCheckTimer = new System.Timers.Timer();
		private static object m_logWriteLock = new object();
		private static object m_errWriteLock = new object();
		private static string m_usageLogName = "BoostLog.txt";
		private static string m_dataPath;
		private static string m_boostVersion = "2.0";
		private static long m_totalRecsServed = 0;

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
		#endregion
		
		#region External Parameters
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

		public BoostLog(string source) //main constructor
		{
			//TODO: read client log data from file so accumulation can continue

			string logMsg = "4-Tell Event Log started for " + source;
			try
			{
				//start logging to system event log
				m_eventLog = new EventLog("4-Tell");
				m_eventLog.Source = source;

				//setup the data path
				//DataPath path = DataPath.Instance;
				//m_dataPath = path.Root;
				m_dataPath = DataPath.Instance.Root;

				//Read Gmail settings from web.config
				m_boostVersion = ConfigurationManager.AppSettings.Get("BoostVersion");
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
				AddClient("4Tell", admin); //create 4-tell client to record errors that don't relate to a client

				//usage log settings
				ReadUsage(); //Try to read past usage data from log
				CheckDate(); //See if we need to move to a new day or new year
				//set a timer to check the date/time each hour and update accumulator indices
				m_dateCheckTimer.Interval = 60 * 60 * 1000; // converted to ms
				m_dateCheckTimer.Elapsed += new ElapsedEventHandler(OnDateCheckTimer);
				m_dateCheckTimer.Enabled = true;

				//get value (in minutes) for transaction log write timer
				string logFrequency = ConfigurationManager.AppSettings.Get("LogFrequency");
				int frequency;
				try
				{
					frequency = Convert.ToInt32(logFrequency);
				}
				catch
				{
					WriteEntry("Warning--Illegal timer frequency: " + logFrequency, EventLogEntryType.Warning);
					frequency = 15; //default 
				}
				m_transactionWriteTimer.Interval = frequency * 60 * 1000; //converted to ms
				m_transactionWriteTimer.Elapsed += new ElapsedEventHandler(OnTransactionWriteTimer);
				m_transactionWriteTimer.Enabled = true;

				//Block logging for certain clients
				string blockList = ConfigurationManager.AppSettings.Get("LogBlockList");
				if (blockList != null)
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
				string errMsg = "Initialization Error for " + source + ": " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception: " + ex.InnerException.Message;
				if (m_eventLog != null)
					m_eventLog.WriteEntry(errMsg, EventLogEntryType.Error);
			}
		}

		#region Usage Logging

		// Implement IDisposable.
		//TODO: Figure out why this is not being called
		//NOTE: Not critical since we write the usage log every hour
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected virtual void Dispose(bool disposing)
		{
      if(!this.disposed)
      {
				if (disposing)
				{
					WriteUsage(m_usageLogName); //record current usage data before destruction...
					disposed = true;
				}
			}
		}
		// End IDisposable

		private void OnDateCheckTimer(object source, ElapsedEventArgs e)
		{
			CheckDate(); //update accumulators for new time/date 
		}

		private void OnTransactionWriteTimer(object source, ElapsedEventArgs e)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				FlushTransactions(); //record current transaction data 
			});
		}

		//return true if existing or false if new client had to be added
		private bool GetClient(string alias, out ClientLog client)
		{
			//use 4Tell in place of a blank alias
			if ((alias == null) || (alias.Length < 1))
				alias = "4Tell";
			if (m_clients.Count > 0)
			{
				foreach (ClientLog c in m_clients)
					if (c.Alias.Equals(alias)) //found
					{
						client = c; //set reference to this client
						return true;
					}
			}
			//client not found
			client = new ClientLog(alias, m_dataPath);
			m_clients.Add(client);
			return false;
		}

		//add a client to the list of client logs
		public void AddClient(string alias, ClientPOC poc = null, StreamReader file = null)
		{
			ClientLog client;
			GetClient(alias, out client); //if already in list, use ref to existing otherwise add
			if (poc != null)
				client.POC = poc; //set poc
			if (file != null)
				client.ReadUsage(file); //read usage from log file			
		}

		//record time and date of new data upload for a client
		//return true if an existing client and false if it had to be added
		public bool UploadComplete(string alias)
		{
			ClientLog client;
			bool found = GetClient(alias, out client); 
			client.LastUpload = DateTime.Now;
			return found;
		}

		//record time and date of last sucessful Generator for a client
		//return true if an existing client and false if it had to be added
		public bool GeneratorComplete(string alias)
		{
			ClientLog client;
			bool found = GetClient(alias, out client);
			client.LastGenerator = DateTime.Now;
			return found;
		}

		//log a transaction
		public bool LogTransaction(string alias, string ip, string method, string parameters, string response, string duration)
		{
			ClientLog client;
			bool found = GetClient(alias, out client);
			client.Transactions.Add(DateTime.Now, ip, method, parameters, response, duration);
			return found;
		}

		//add a recommendation call to the tally
		//always add client if it is unknown so we can track invalid alias calls too
		//return true if an existing client and false if it had to be added
		public bool AddCall(string alias, int numResults)
		{
			m_totalRecsServed += numResults;

			ClientLog client;
			bool found = GetClient(alias, out client);
			client.Calls.Add();	
			return found; 
		}

		//add an error to the tally
		//return true if an existing client and false if it had to be added
		public bool AddError(string alias)
		{
			ClientLog client;
			bool found = GetClient(alias, out client);
			client.Errors.Add();
			return found;
		}

		//add a warning to the tally
		//return true if an existing client and false if it had to be added
		public bool AddWarning(string alias)
		{
			ClientLog client;
			bool found = GetClient(alias, out client);
			client.Warnings.Add();
			return found;
		}

		//record the latest error that was encountered
		//return true if an existing client and false if it had to be added
		public bool AddLastError(BoostError error)
		{
			ClientLog client;
			bool found = GetClient(error.Alias, out client);
			client.LastError = error;
			return found;
		}

		public bool GetLastError(string alias, ref BoostError error)
		{
			if ((alias == null) || (alias.Length < 1))
				alias = "4Tell";
			foreach (ClientLog c in m_clients)
				if (c.Alias.Equals(alias))
				{
					error = c.LastError;
					return true;
				}
			return false; 
		}

		public string GetLastError(string alias)
		{
			string errMsg = "";
			BoostError e = new BoostError();
			if (GetLastError(alias, ref e))
			{
				if (e.Message.Length < 1) 
					errMsg = "No errors recorded for client alias: " + alias; 
				else
					errMsg = "Type: " + e.Type.ToString()
								+ "\nDate: " + e.Time.ToLongDateString()
								+ "\nTime: " + e.Time.ToLongTimeString()
								+ "\nMessage: " + e.Message;
			}
			else //client alias not found
			{
				errMsg = "Unable to retrieve last error. Client alias not found: " + alias;
			}
			return errMsg; //client alias not found
		}

		//reset log hour on each accumulator
		private void NewHour(int hour)
		{
			foreach (ClientLog c in m_clients)
				c.SetLogHour(hour);
		}


		//reset all accumulators for a new day
		//NOTE: advisable to log with today's date after resetting ---
		private void NewDay(DateTime now)
		{
			foreach (ClientLog c in m_clients)
				c.NewDay(now);
			if (m_lastCheckDate.Month != now.Month)
				foreach (ClientLog c in m_clients)
					c.SetLogMonth(now.Month - 1);
		}

		//reset all accumulators for new year
		//NOTE: advisable to log with today's date after resetting ---
		private void NewYear(DateTime now)
		{
			foreach (ClientLog c in m_clients)
			{
				c.NewYear(now);
			}
		}

		private void CheckDate()
		{
			DateTime now = DateTime.Now;

			//logic:
			// This gets called from the timer (currently every hour, but could change so don't count on it)
			// All comparison logic should be here and not in the client logs or accumulators so it is done once per timer.
			// Since hourly checks are the normal behavior, that should be the fastest ruote
			//   Update hour index, clearing the new hour slot
			//	 Check for day change or year change and update accordingly
			// Allow larger lags up to 24 hours. 
			//   Update all hour indices, clearing all hour slots between old hour and new hour
			//	 Check for day change or year change and update accordingly
			//   NewDay and NewYear functions should reset month index if necessary
			// If larger then that, something is wrong: save a backup and reset all accumulators

			TimeSpan span = new TimeSpan(now.Ticks - m_lastCheckDate.Ticks);
			int hourSpan = (int)(span.TotalHours);
			if (hourSpan == 1) //normal behavior
			{
				if (m_lastCheckDate.Year != now.Year)
					NewYear(now); //year also resets month, day, and hour
				else if (m_lastCheckDate.DayOfYear != now.DayOfYear)
					NewDay(now); //day also resets hour and checks month
				else
					NewHour(now.Hour);
			}
			else if (hourSpan == 0) //less than an hour passed so do nothing
				return;

			//hours greater than 1 and less than 24 requires the most work
			else if (hourSpan < 24)
			{
				int lastHour = m_lastCheckDate.Hour;
				foreach (ClientLog c in m_clients)
				{
					for (int h = lastHour + 1; h < lastHour + span.TotalHours; h++)
						c.SetLogHour(h % 24); //clears each hour slot in the array
				}
				if (m_lastCheckDate.Year != now.Year)
					NewYear(now); //year also resets month, day, and hour
				else if (m_lastCheckDate.DayOfYear != now.DayOfYear)
					NewDay(now); //day checks month
			}
			else //something wrong if more than 24 hours
			{
				// backup current log to a unique name  
				string tempName = "BackupLog" + m_lastCheckDate.ToString("yyyyMMdd");
				string backupLogname = tempName + ".txt";
				int ver = 0;
				while (File.Exists(m_dataPath + backupLogname))
					backupLogname = tempName + "-" + (++ver).ToString() + ".txt";
				WriteUsage(backupLogname);

				//clear all logs
				foreach (ClientLog c in m_clients)
					c.RestAll();
			}

			//queue a thread to write the log
			ThreadPool.QueueUserWorkItem(delegate(object s)
			{
				WriteUsage((string)s);
			}, m_usageLogName);

			m_lastCheckDate = now;
		}

		//write all transcation data to separate files (to be run in a separate thread)
		private void FlushTransactions()
		{
			string alias = ""; //for error reporting
			try
			{
				foreach (ClientLog c in m_clients)
				{
					alias = c.Alias;
					c.Transactions.Flush();
				}
			}
			catch (Exception ex)
			{
				string errMsg = "Error writing transaction log for client: " + alias
											+ "\n" + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception: " + ex.InnerException.Message;
				WriteEntry(errMsg, EventLogEntryType.Error);
			}
		}

		//write all usage data to a single file (to be run in a separate thread)
		private void WriteUsage(string logName)
		{
			try
			{
				lock (m_logWriteLock)
				{
					DateTime now = DateTime.Now;
					string report = "Usage Report\tVersion\t" + m_boostVersion + "\t" + now.ToString()
						+ "\nTotal Recommendations Served:\t" + m_totalRecsServed + "\n\n";
					foreach (ClientLog c in m_clients)
						report += c.ReportUsage();

					WriteEntry(report, EventLogEntryType.Information);

					//create a log file that can be read back in on startup
					using (StreamWriter file = new StreamWriter(m_dataPath + logName))
					{
						file.Write(report);
						m_lastWriteDate = now;
					}
				}
			}
			catch (Exception ex)
			{
				string errMsg = "Error during WriteUsage\nException: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception: " + ex.InnerException.Message;
				WriteEntry(errMsg, EventLogEntryType.Error);
			}
		}

		public bool ReadUsage()
		{
			string errSuffix =  "\n\nUsage file: " + m_dataPath + m_usageLogName;
			try
			{
				lock (m_logWriteLock) //prevent reading and writing at the same time
				{
					using (StreamReader file = new StreamReader(m_dataPath + m_usageLogName))
					{
						//usage file header line
						string inputLine = file.ReadLine();
						string[] values = inputLine.Split('\t');
						if (values.Length < 4) //should be "Usage Report\tVersion\t{version}\t{date}"
							throw new Exception("Invalid usage file header");
						m_lastWriteDate = DateTime.Parse(values[3]);
						m_lastCheckDate = m_lastWriteDate; //set the check date to match data read in

						//total recs served
						inputLine = file.ReadLine();
						values = inputLine.Split('\t');
						if (values.Length > 1) //older logs did not inculde this value
						{
							if (values.Length != 2)
								throw new Exception("Invalid total recs line in usage file");
							m_totalRecsServed = Convert.ToInt64(values[1]);
							inputLine = file.ReadLine(); //discard blank line
						}

						//read each client log
						do
						{
							inputLine = file.ReadLine(); //client header line
							if (inputLine == null) //end of file
								break;
							values = inputLine.Split('\t');
							if (values.Length < 1) //should be client alias
								throw new Exception("Invalid client header");
							string alias = values[0];
							AddClient(alias, null, file); //create client if new or ReadUsage if not
						} while (true);
					}
				}
			}
			catch (Exception ex)
			{
				string errMsg = "Exception: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInner Exception: " + ex.InnerException.Message;
				errMsg += errSuffix;
				WriteEntry(errMsg, EventLogEntryType.Warning);
				return false;
			}
			return true;
		}
		#endregion

		#region Error Logging

		public void WriteEntry(string message, EventLogEntryType type, string clientAlias = "")
		{
			BoostError error = new BoostError();
			error.Time = DateTime.Now;
			error.Message = message; 
			error.Type = type;
			error.Alias = clientAlias;

			//m_errSuffix should be populated with the service call and parameters
			if (m_errSuffix.Length > 0) //add suffix here to avoid threading issues
				error.Message += "\n" + m_errSuffix;
			else if (clientAlias.Length > 0) //if no suffix, then add client alias
				error.Message += "\nClientAlias = " + error.Alias;

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

			bool blockLog = false; 
			ClientPOC poc = new ClientPOC();
			if ((error.Alias != null) && (error.Alias.Length > 0))
			{
				foreach (ClientLog c in m_clients)
					if (c.Alias.Equals(error.Alias))
					{
						poc = c.POC;
						break;
					}

				if (m_logBlockList != null)
				{
					//block this message from the log (if any clients are generating too many errors)
					foreach (string alias in m_logBlockList)
						if (alias.Equals(error.Alias))
						{
							blockLog = true;
							break;
						}
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
						AddError(error.Alias); //add to error tally
						AddLastError(error);	//replace last error
						break;
					case EventLogEntryType.Warning:
						subject = "Warning";
						sendAdmin = ((int)m_adminReportLevel <= (int)(ReportLevel.Warrning));
						sendClient = ((int)(poc.Report) <= (int)(ReportLevel.Warrning));
						AddWarning(error.Alias); //add to warning tally
						AddLastError(error);	//replace last error
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
