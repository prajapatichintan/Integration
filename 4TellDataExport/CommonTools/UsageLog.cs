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
	
	public class Accumulator
	{
		//logged values
		public int[] RollingHours = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //24 hours
		public int[] RollingDays = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //31 days (max)
		public long[] RollingMonths = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //12 months
		public long[] RollingYears = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; //10 years
		private int m_logHour = DateTime.Now.Hour;
		private int m_logDay = DateTime.Now.Day - 1; //first day of month = 0
		private int m_logMonth = DateTime.Now.Month - 1; //January = 0
		private int m_logYear = DateTime.Now.Year % 10; //last digit of year is index

		//commonly requested values
		public int Today { get { return RollingDays[DateTime.Now.Day - 1]; } }
		public long ThisMonth { get { return RollingMonths[DateTime.Now.Month - 1]; } }
		public long ThisYear { get { return RollingYears[DateTime.Now.Year % 10]; } }
		public long LastYear { get { return RollingYears[(DateTime.Now.Year - 1) % 10]; } }
		public long TTM //TTM = Trailing Twelve Months
		{ 
			get 
			{ 
				int total = 0;
				foreach (int month in RollingMonths) total += month;
				return total; 
			} 
		}

		//Resetters
		public void ClearAll()
		{
			ResetHours();
			ResetDays();
			ResetMonths();
			ResetYears();
		}

		public void ResetHours()
		{
			for (int i = 0; i < 24; i++)
				RollingHours[i] = 0;
			m_logHour = DateTime.Now.Hour;
		}

		public void ResetDays()
		{
			for (int i = 0; i < 31; i++)
				RollingDays[i] = 0;
			m_logDay = DateTime.Now.Day - 1; //first day of month = 0
		}

		public void ResetMonths()
		{
			for (int i = 0; i < 12; i++)
				RollingMonths[i] = 0;
			m_logMonth = DateTime.Now.Month - 1; //January = 0
		}

		public void ResetYears()
		{
			for (int i = 0; i < 10; i++)
				RollingYears[i] = 0;
			m_logYear = DateTime.Now.Year % 10; //last digit of year is index
		}

		//Setters
		//IMPORTANT: "Set" functions do not question whether the hour or day or month or year has changed. 
		//		They all assume they should clear the current bin. Only call when that bin is changing.
		//		This logic was moved to the BoostLog level for efficiency (one "if" statement instead of num_clients x 6)
		//						
		public void SetLogHour(int hour)
		{
			RollingHours[hour] = 0;
			m_logHour = hour;
		}

		public void SetLogDay(int day)
		{
			if ((day == 0) && (m_logDay < 30)) //need to clear extra days in previous month
				for (int d = m_logDay + 1; d < 31; d++)
					RollingDays[d] = 0;
			RollingDays[day] = 0;
			m_logDay = day;
		}

		public void SetLogMonth(int month)
		{
			RollingMonths[month] = 0;
			m_logMonth = month;
		}

		public void SetLogYear(int year)
		{
			RollingYears[year] = 0;
			m_logYear = year;
		}

		public void Add(int num = 1)
		{
			RollingHours[m_logHour] += num;
			RollingDays[m_logDay] += num;
			RollingMonths[m_logMonth] += num;
			RollingYears[m_logYear] += num;
		}

		public void NewDay(DateTime now)
		{
			SetLogDay(now.Day - 1);
			SetLogHour(now.Hour);
		}

		public void NewMonth(DateTime now)
		{
			SetLogMonth(now.Month - 1);
			NewDay(now);
		}

		public void NewYear(DateTime now)
		{
			SetLogHour(now.Hour);
			NewMonth(now);  //January = 0
		}

		public string ReportUsage(string delimiter = "\t") //one line of tab-delimited values
		{
			string report = "";
			foreach (int h in RollingHours)
				report += delimiter + h.ToString();
			foreach (int d in RollingDays)
				report += delimiter + d.ToString();
			foreach (long m in RollingMonths)
				report += delimiter + m.ToString();
			foreach (long y in RollingYears)
				report += delimiter + y.ToString();
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
				if (values.Length < 1) //no data
					throw new Exception("Error reading usage data for " + type + ". No data.");
				if (!values[0].Equals(type)) //first parameter is type of this accumulator (Calls, Errors, Warnings)
					throw new Exception("Error reading usage data for " + type + ". Type = " + values[0]);
				if (values.Length != 78) //type, 10 years, 12 months, 31 days, 24 hours == 78
					ReadUsageOld(values, type); //see if we are reading from an old usage file

				int index = 1; //zero was type
				for (int i = 0; i < 10; i++)
					RollingYears[i] = Convert.ToInt64(values[index++]);
				for (int i = 0; i < 12; i++)
					RollingMonths[i] = Convert.ToInt64(values[index++]);
				for (int i = 0; i < 31; i++)
					RollingDays[i] = Convert.ToInt32(values[index++]);
				for (int i = 0; i < 24; i++)
					RollingHours[i] = Convert.ToInt32(values[index++]);
			}
			catch (Exception ex)
			{
				string msg = ex.Message + "\nAccumulator type: " + type;
				throw new Exception(msg, ex.InnerException);
			}
		}

		public void ReadUsageOld(string[] values, string type)
		{
			//streamReader must be pointing at the data line for this Accumulator
			try
			{
				if (values.Length != 41) //type, today, this-year, last-year, TTM, plus 12 months, plus 24 hours
					throw new Exception("Error reading usage data. Data length = " + values.Length.ToString());

				//old individual values need to be mapped into the new bin (day, year)
				DateTime now = DateTime.Now;
				RollingDays[now.Day - 1] = Convert.ToInt32(values[1]); //Today
				RollingYears[now.Year % 10] = Convert.ToInt64(values[2]); //ThisYear
				RollingYears[(now.Year - 1) % 10] = Convert.ToInt64(values[3]); //LastYear
				long unused = Convert.ToInt64(values[4]); //TTM
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
		public ServiceCallLog ServiceCalls;
		public ActionsLog Actions;
		public ClickStreamLog Clicks;

		public ClientLog(string alias, string dataPath)
		{
			Alias = alias;
			ServiceCalls = new ServiceCallLog(alias, dataPath);
			Actions = new ActionsLog(alias, dataPath);
			Clicks = new ClickStreamLog(alias, dataPath);
		}

		public void RestAll()
		{
			ResetHours();
			ResetDays();
			ResetMonths();
			ResetYears();
		}

		public void ResetHours()
		{
			Calls.ResetHours();
			Errors.ResetHours();
			Warnings.ResetHours();
		}

		public void ResetDays()
		{
			Calls.ResetDays();
			Errors.ResetDays();
			Warnings.ResetDays();
		}

		public void ResetMonths()
		{
			Calls.ResetMonths();
			Errors.ResetMonths();
			Warnings.ResetMonths();
		}

		public void ResetYears()
		{
			Calls.ResetYears();
			Errors.ResetYears();
			Warnings.ResetYears();
		}

		public void SetLogHour(int hour)
		{
			Calls.SetLogHour(hour);
			Errors.SetLogHour(hour);
			Warnings.SetLogHour(hour);
		}

		public void SetLogDay(int month)
		{
			Calls.SetLogDay(month);
			Errors.SetLogDay(month);
			Warnings.SetLogDay(month);
		}

		public void SetLogMonth(int month)
		{
			Calls.SetLogMonth(month);
			Errors.SetLogMonth(month);
			Warnings.SetLogMonth(month);
		}

		public void SetLogYear(int month)
		{
			Calls.SetLogYear(month);
			Errors.SetLogYear(month);
			Warnings.SetLogYear(month);
		}

		public void NewDay(DateTime now)
		{
			Calls.NewDay(now);
			Errors.NewDay(now);
			Warnings.NewDay(now);
		}

		public void NewMonth(DateTime now)
		{
			Calls.NewMonth(now);
			Errors.NewMonth(now);
			Warnings.NewMonth(now);
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
							+ "Type\t"
							+ "12am\t1am\t2am\t3am\t4am\t5am\t6am\t7am\t8am\t9am\t10am\t11am\t"
							+ "12pm\t1pm\t2pm\t3pm\t4pm\t5pm\t6pm\t7pm\t8pm\t9pm\t10pm\t11pm\t";
			for (int h = 1; h < 32; h++)
				report += string.Format("Day{0}\t", h);
			report += "Jan\tFeb\tMar\tApr\tMay\tJun\tJul\tAug\tSep\tOct\tNov\tDec\t";
			int thisYr = DateTime.Now.Year;
			string thisYY = thisYr.ToString().Substring(0, 2); //first two digits of year
			string lastYY = (thisYr - 10).ToString().Substring(0, 2);
			thisYr %= 10; //get last two digits
			for (int y = 0; y < thisYr; y++)
				report += string.Format("{0}{1}\t", thisYY, y);
			for (int y = thisYr; y < 10; y++)
				report += string.Format("{0}{1}\t", lastYY, y);
			report += "\n";

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


	public class WebContextProxy
	{
		public string ip = "";
		public string method = "";
		public string parameters = "";
	}

	//Transaction describes a request/response to the service for logging purposes
	public class ServiceCall
	{
		DateTime Date;
		private string m_ip;
		private string m_method;
		private string m_parameters;
		private string m_response;
		private string m_duration;

		public ServiceCall(DateTime date, string ip, string method, string parameters, string response, string duration)
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
	public class ServiceCallLog
	{
		private string m_alias = "";
		private string m_path = "";
		private List<ServiceCall> m_calls = new List<ServiceCall>();
		private static object m_callDataLock = new object();
		private static object m_callFileLock = new object();
		private const string m_callLogFolder = "TransactionLogs\\";
		private const string m_callLogPrefix = "ServiceCallLog-";
		private const string m_delimiter = "\t";
		private const string m_endOfLine = "\r\n";
		private const int m_maxLogSize = 1000000; //1MB

		public ServiceCallLog(string alias, string path)
		{
			m_alias = alias;
			m_path = path + m_callLogFolder;
		}

		public void Add(DateTime date, string ip, string method, string parameters, string response, string duration)
		{
			ServiceCall item = new ServiceCall(date, ip, method, parameters, response, duration);

			lock (m_callDataLock) //must lock so new item is not added while list is being flushed
			{
				m_calls.Add(item);
			}
		}

		//write transactions to file and flush memory (called from a separate thread)
		public void Flush()
		{
			if (m_calls.Count < 1) return; //nothing to do

			string report = "";

			lock (m_callDataLock) //must lock so new items cannot be added during flush
			{
				foreach (ServiceCall item in m_calls)
					report += item.Report(m_delimiter) + m_endOfLine;

				//clear list each time it is reported
				m_calls = new List<ServiceCall>();
			}

			//archive the log
			QueueTrasactionReport(report);

			//replicate to other servers
			Replicator.Instance.ReplicateReport(m_alias, report, "TransactionLog");
		}

		public void QueueTrasactionReport(string report)
		{
			//queue a thread to write the log
			ThreadPool.QueueUserWorkItem(delegate(object s)
			{
				WriteTransactions((string)s);
			}, report);
		}

		//append transaction report to the log
		private void WriteTransactions(string report)
		{
			bool addHeader = true;
			lock (m_callFileLock) //must lock to avoid simultaneous writes
			{
				string logPath = m_path + m_callLogPrefix + m_alias + ".txt";
				//archive the log if it is too big and start a new one
				if (File.Exists(logPath))
				{
					FileInfo fInfo = new FileInfo(logPath);
					if (fInfo.Length > m_maxLogSize)
					{
						string tempPath = m_path + m_callLogPrefix + m_alias;
						string archive;
						int ver = 1;
						while (File.Exists(archive = tempPath + "-" + ver.ToString() + ".txt"))
							ver++;
						File.Move(logPath, archive);
					}
					else
						addHeader = false;
				}
				if (addHeader)
					report = "Version" + m_delimiter + "2" + m_delimiter
								+ DateTime.Now.ToString("MM-dd-yyyy") + m_endOfLine
								+ "Date" + m_delimiter + "IP" + m_delimiter
								+ "Method" + m_delimiter + "Params" + m_delimiter
								+ "Response" + m_delimiter + "Duration" + m_endOfLine
								+ report; //date, ip, method, parameters, response, duration
				using (StreamWriter file = new StreamWriter(logPath, true))
				{
					file.Write(report);
				}
			}
		}

	}

	//ClickRecord is a single click event to be recorded (click-stream data)
	public class ClickRecord
	{
		public string ProductID;
		public string CustomerID;
		public pageType PageType;
		public DateTime Date;

		public enum pageType
		{
			Home,
			PDP,
			Category,
			Search,
			Cart,
			Checkout,
			Admin,
			Other
		}

		public ClickRecord(string productID, string customerID, string page, DateTime date)
		{
			ProductID = productID;
			CustomerID = customerID;
			PageType = ToPageTypeEnum(page);
			Date = date;
		}

		public ClickRecord(string productID, string customerID, pageType type, DateTime date)
		{
			ProductID = productID;
			CustomerID = customerID;
			PageType = type;
			Date = date;
		}

		public string Report(string delimiter)
		{
			string report = ProductID + delimiter + CustomerID + delimiter
										+ ToPageTypeCode(PageType) + delimiter + Date.ToString("MM-dd-yyyy");
			return report;
		}

		public static pageType ToPageTypeEnum(string code)
		{
			pageType type; 
			if (code.Equals("Hm", StringComparison.CurrentCultureIgnoreCase))
				type = pageType.Home;
			else if (code.Equals("Pdp", StringComparison.CurrentCultureIgnoreCase))
				type = pageType.PDP;
			else if (code.Equals("Cat", StringComparison.CurrentCultureIgnoreCase))
				type = pageType.Category;
			else if (code.Equals("Search", StringComparison.CurrentCultureIgnoreCase))
				type = pageType.Search;
			else if (code.Equals("Cart", StringComparison.CurrentCultureIgnoreCase))
				type = pageType.Cart;
			else if (code.Equals("Chkout", StringComparison.CurrentCultureIgnoreCase))
				type = pageType.Checkout;
			else if (code.Equals("Admin", StringComparison.CurrentCultureIgnoreCase))
				type = pageType.Admin;
			else
				type = pageType.Other; 
			return type;
		}

		public static string ToPageTypeCode(pageType type)
		{
			switch (type)
			{
				case pageType.Home:
					return "Hm";
				case pageType.PDP:
					return "Pdp";
				case pageType.Category:
					return "Cat";
				case pageType.Search:
					return "Srch";
				case pageType.Cart:
					return "Cart";
				case pageType.Checkout:
					return "Chkout";
				case pageType.Admin:
					return "Admin";
				default:
					return "Other";
			}
		}
	}

	public class ClickStreamLog
	{
		private string m_alias = "";
		private string m_path = "";
		private List<ClickRecord> m_clickStream = new List<ClickRecord>();
		private static object m_clickDataLock = new object();
		private static object m_clickFileLock = new object();
		private const string m_clickLogPrefix = "AutoClickStreamLog-";
		private const string m_delimiter = "\t";
		private const string m_endOfLine = "\r\n";
		private const int m_maxLogSize = 20000000; //20MB

		public ClickStreamLog(string alias, string path)
		{
			m_alias = alias;
			m_path = path;
		}

		public void Add(string productID, string customerID, string pageCode, DateTime date)
		{
			Add(productID, customerID, ClickRecord.ToPageTypeEnum(pageCode), date);
		}

		public void Add(string productID, string customerID, ClickRecord.pageType pagetype, DateTime date)
		{
			ClickRecord item = new ClickRecord(productID, customerID, pagetype, date);

			lock (m_clickDataLock) //must lock so new item is not added while list is being flushed
			{
				m_clickStream.Add(item);
			}
		}

		//write transactions to file and flush memory (called from a separate thread)
		public void Flush()
		{
			if (m_clickStream.Count < 1) return; //nothing to do

			string report = "";

			lock (m_clickDataLock) //must lock so new items cannot be added during flush
			{
				foreach (ClickRecord item in m_clickStream)
					report += item.Report(m_delimiter) + m_endOfLine;

				//clear list each time it is reported
				m_clickStream = new List<ClickRecord>();
			}

			//archive the log
			QueueClickReport(report);

			//replicate to other servers
			Replicator.Instance.ReplicateReport(m_alias, report, "ClickStreamLog");
		}

		public void QueueClickReport(string report)
		{
			//queue a thread to write the log
			ThreadPool.QueueUserWorkItem(delegate(object s)
			{
				WriteClickLog((string)s);
			}, report);
		}

		//append transaction report to the log
		private void WriteClickLog(string report)
		{
			bool addHeader = true;
			lock (m_clickFileLock) //must lock to avoid simultaneous writes
			{
				//clickstream is dated by the following sunday, so new log each week
				DateTime now = DateTime.Now;
				int sundayOffset = (7 - (int)(now.DayOfWeek)) % 7; //Monday offset = 6, Tuesday = 5, etc.

				string filename = m_clickLogPrefix + now.AddDays(sundayOffset).ToString("yyyy-MM-dd");
				string logPath = m_path + m_alias + "\\upload\\" + filename + ".txt";
				//archive the log if it is too big and start a new one
				if (File.Exists(logPath))
				{
					FileInfo fInfo = new FileInfo(logPath);
					if (fInfo.Length > m_maxLogSize)
					{
						string tempPath = m_path + m_alias + "\\upload\\" + filename;
						string archive;
						int ver = 1;
						while (File.Exists(archive = tempPath + "-v" + ver.ToString() + ".txt"))
							ver++;
						File.Move(logPath, archive);
						string warnMsg = string.Format("ClickStreamLog exceeded size limit for{0}: {1}", m_alias, filename);
						BoostLog.Instance.WriteEntry(warnMsg, EventLogEntryType.Warning, m_alias);
					}
					else
						addHeader = false;
				}
				if (addHeader)
					report = "Version" + m_delimiter + "3" + m_delimiter
								+ now.ToString("MM-dd-yyyy") + m_endOfLine
								+ "Product ID" + m_delimiter + "Customer ID"
								+ m_delimiter + "PageType" + m_delimiter + "Date" + m_endOfLine
								+ report;
				using (StreamWriter file = new StreamWriter(logPath, true))
				{
					file.Write(report);
				}
			}
		}

	}

	//ActionRecord is a single purchase event to be recorded (could also be downloads, plays, etc.)
	public class ActionRecord
	{
		public string ProductID;
		public string CustomerID;
		public int Quantity;
		public DateTime Date;

		public ActionRecord(string productID, string customerID, int quantity, DateTime date)
		{
			ProductID = productID;
			CustomerID = customerID;
			Quantity = quantity;
			Date = date;
		}

		public string Report(string delimiter)
		{
			string report = ProductID + delimiter + CustomerID + delimiter 
										+ Quantity + delimiter + Date.ToString("MM-dd-yyyy");
			return report;
		}
	}

	//ActionsLog contains a list of sales records to be accumulated realtime 
	//and then written to disk asynchronously when a timer fires
	public class ActionsLog
	{
		private string m_alias = "";
		private string m_path = "";
		private List<ActionRecord> m_actions = new List<ActionRecord>();
		private static object m_actionsDataLock = new object();
		private static object m_actionsFileLock = new object();
		private const string m_actionsLogPrefix = "AutoActionsLog-";
		private const string m_delimiter = "\t";
		private const string m_endOfLine = "\r\n";
		private const int m_maxLogSize = 5000000; //5MB

		public ActionsLog(string alias, string path)
		{
			m_alias = alias;
			m_path = path;
		}

		public void Add(string productID, string customerID, int quantity, DateTime date)
		{
			ActionRecord item = new ActionRecord(productID, customerID, quantity, date);

			lock (m_actionsDataLock) //must lock so new item is not added while list is being flushed
			{
				m_actions.Add(item);
			}
		}

		//write transactions to file and flush memory (called from a separate thread)
		public void Flush()
		{
			if (m_actions.Count < 1) return; //nothing to do

			string report = "";

			lock (m_actionsDataLock) //must lock so new items cannot be added during flush
			{
				foreach (ActionRecord item in m_actions)
					report += item.Report(m_delimiter) + m_endOfLine;

				//clear list each time it is reported
				m_actions = new List<ActionRecord>();
			}

			//archive the log
			QueueActionReport(report);

			//replicate to other servers
			Replicator.Instance.ReplicateReport(m_alias, report, "ActionsLog");
		}

		public void QueueActionReport(string report)
		{
			//queue a thread to write the log
			ThreadPool.QueueUserWorkItem(delegate(object s)
			{
				WriteActionLog((string)s);
			}, report);
		}

		//append transaction report to the log
		private void WriteActionLog(string report)
		{
			bool addHeader = true;
			lock (m_actionsFileLock) //must lock to avoid simultaneous writes
			{
				string filename = m_actionsLogPrefix + DateTime.Now.ToString("yyyy-MM");
				string logPath = m_path + m_alias + "\\upload\\" + filename + ".txt";
				//archive the log if it is too big and start a new one
				if (File.Exists(logPath))
				{
					FileInfo fInfo = new FileInfo(logPath);
					if (fInfo.Length > m_maxLogSize)
					{
						string tempPath = m_path + m_alias + "\\upload\\" + filename;
						string archive;
						int ver = 1;
						while (File.Exists(archive = tempPath + "-v" + ver.ToString() + ".txt"))
							ver++;
						File.Move(logPath, archive);
					}
					else
						addHeader = false;
				}
				if (addHeader)
					report = "Version" + m_delimiter + "3" + m_delimiter
								+ DateTime.Now.ToString("MM-dd-yyyy") + m_endOfLine
								+ "Product ID" + m_delimiter + "Customer ID"
								+ m_delimiter + "Quantity" + m_delimiter + "Date" + m_endOfLine
								+ report;
				using (StreamWriter file = new StreamWriter(logPath, true))
				{
					file.Write(report);
				}
			}
		}

	}

	#endregion

	public class UsageLog : IDisposable
	{
		#region Internal Parameters
		private static string m_dataPath;
		private string m_errMsg = "";
		private bool m_readOnly = false;

		//usage log parameters
		private bool disposed = false;
		private static List<ClientLog> m_clients = new List<ClientLog>();
		private static DateTime m_lastWriteDate = DateTime.MinValue;
		private static DateTime m_lastCheckDate = DateTime.MinValue;
		private static System.Timers.Timer m_dateCheckTimer = new System.Timers.Timer();
		private static object m_logWriteLock = new object();
		private static string m_usageLogName = "BoostLog.txt";
		private static string m_boostVersion = "2.0";
		private static long m_totalRecsServed = 0;
		private static readonly UsageLog m_instance = new UsageLog();

		//transaction log parameters
		private static System.Timers.Timer m_transactionWriteTimer = new System.Timers.Timer();
		private static bool m_initCalled = false;
		#endregion
		
		#region External Parameters
		public static UsageLog Instance { get { return m_instance; } }
		public string ErrorText
		{
			get { return m_errMsg; }
			set { m_errMsg = value; }
		}
		public bool InitCalled
		{
			get { return m_initCalled; }
			set { m_initCalled = value; }
		}
		public bool ReadOnly
		{
			get { return m_readOnly; }
			set { m_readOnly = value; }
		}
		#endregion

		//main constructor is private because UsageLog is a singleton
		//use UsageLog.Instance instead of new UsageLog()
		private UsageLog() //main constructor
		{
			m_readOnly = false;
#if USAGE_READONLY
			m_readOnly = true;
#endif
			try
			{
				//usage log settings
				m_dataPath = DataPath.Instance.Root;
				ReadUsage(); //Try to read past usage data from log
				//set a timer to check the date/time each hour and update accumulator indices
				CheckDate(); //See if we need to move to a new day or new year
				m_dateCheckTimer.Interval = 60 * 60 * 1000; // converted to ms
				m_dateCheckTimer.Elapsed += new ElapsedEventHandler(OnDateCheckTimer);
				m_dateCheckTimer.Enabled = true;

				if (!m_readOnly)
				{
					//get value (in minutes) for transaction log write timer
					string logFrequency = ConfigurationManager.AppSettings.Get("LogFrequency");
					int frequency;
					try
					{
						frequency = Convert.ToInt32(logFrequency);
					}
					catch
					{
						m_errMsg = "Warning--Illegal timer frequency: " + logFrequency;
						frequency = 15; //default 
					}

					m_transactionWriteTimer.Interval = frequency * 60 * 1000; //converted to ms
					m_transactionWriteTimer.Elapsed += new ElapsedEventHandler(OnTransactionWriteTimer);
					m_transactionWriteTimer.Enabled = true;
				}
			}
			catch (Exception ex)
			{
				m_errMsg = "Initialization Error for Usage Log: " + ex.Message;
				if (ex.InnerException != null)
					m_errMsg += "\nInner Exception: " + ex.InnerException.Message;
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

		//return true if existing or false if new client had to be added
		public bool GetClient(string alias, out ClientLog client, bool add = true)
		{
			client = null;

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
			if (add)
			{
				client = new ClientLog(alias, m_dataPath);
				m_clients.Add(client);
			}
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
				errMsg = "No events logged for client alias: " + alias;
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

		//write all usage data to a single file (to be run in a separate thread)
		private void WriteUsage(string logName)
		{
			if (m_readOnly) return; //do nothing

			BoostLog log = BoostLog.Instance;
			try
			{
				lock (m_logWriteLock)
				{
					DateTime now = DateTime.Now;
					string report = "Usage Report\tVersion\t" + m_boostVersion + "\t" + now.ToString()
						+ "\nTotal Recommendations Served:\t" + m_totalRecsServed + "\n\n";
					foreach (ClientLog c in m_clients)
						report += c.ReportUsage();

					log.WriteEntry(report, EventLogEntryType.Information);

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
				log.WriteEntry(errMsg, EventLogEntryType.Error);
			}
		}

		public bool ReadUsage()
		{
			string errSuffix = "\n\nUsage file: " + m_dataPath + m_usageLogName;
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
				BoostLog.Instance.WriteEntry(errMsg, EventLogEntryType.Warning);
				return false;
			}
			return true;
		}
		#endregion
	
		#region Transaction Logging

		private void OnTransactionWriteTimer(object source, ElapsedEventArgs e)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				FlushTransactions(); //record current transaction data 
				FlushSales();
			});
		}

		//write all transcation data to separate files (to be run in a separate thread)
		private void FlushTransactions()
		{
			if (m_readOnly) return; //do nothing

			string alias = ""; //for error reporting
			foreach (ClientLog c in m_clients)
			{
				try
				{
						alias = c.Alias;
						c.ServiceCalls.Flush();
				}
				catch (Exception ex)
				{
					string errMsg = "Error flushing transaction log: " + ex.Message;
					if (ex.InnerException != null)
						errMsg += "\nInner Exception: " + ex.InnerException.Message;
					BoostLog.Instance.WriteEntry(errMsg, EventLogEntryType.Error, alias);
				}
			}
		}

#if !USAGE_READONLY
		//log a transaction
		public void LogTransaction(string clientAlias, string[] resultList, string duration)
		{
			string result = "";
			bool first = true;
			if (resultList != null)
				foreach (string r in resultList)
				{
					if (first) first = false;
					else result += ",";
					result += r;
				}
			LogTransaction(clientAlias, result, duration);
		}

		public void LogTransaction(string clientAlias, List<RecDisplayItem> recList, string duration)
		{
			string result = "{\"RecDisplayItem\":[";
			bool first = true;
			if (recList != null)
				foreach (RecDisplayItem item in recList)
				{
					if (first) first = false;
					else result += ",";
					result += item.ToString();
				}
			result += "]}";
			LogTransaction(clientAlias, result, duration);
		}

		public void LogTransaction(string clientAlias, List<DashProductRec> recList, string duration)
		{
			string result = "{\"DashProductRec\":[";
			bool first = true;
			if (recList != null)
				foreach (DashProductRec item in recList)
				{
					if (first) first = false;
					else result += ",";
					result += item.ToString();
				}
			result += "]}";
			LogTransaction(clientAlias, result, duration);
		}

		public void LogTransaction(string clientAlias, List<DashCatalogItem> catalog, string duration)
		{
			string result = "{\"DashCatalogItem\":[";
			bool first = true;
			if (catalog != null)
				foreach (DashCatalogItem item in catalog)
				{
					if (first) first = false;
					else result += ",";
					result += item.ToString();
				}
			result += "]}";
			LogTransaction(clientAlias, result, duration);
		}

		public void LogTransaction(string clientAlias, List<TopCustomer> customers, string duration)
		{
			string result = "{\"TopCustomer\":[";
			bool first = true;
			if (customers != null)
				foreach (TopCustomer item in customers)
				{
					if (first) first = false;
					else result += ",";
					result += item.ToString();
				}
			result += "]}";
			LogTransaction(clientAlias, result, duration);
		}
		
		public void LogTransaction(string clientAlias, string result, string duration, WebContextProxy context = null)
		{
			string ip = "";
			string method = "";
			string parameters = "";
			try
			{
#if WEB
				WebHelper wh = new WebHelper();
				wh.GetContextOfRequest(out ip, out method, out parameters);
#endif
				if (context != null)
				{
					ip = context.ip;
					method = context.method;
					parameters = context.parameters;
				}
				LogTransaction(clientAlias, ip, method, parameters, result, duration);

				if (m_initCalled) //log the call that forced instantiation
				{
					string message = "Call that forced instantiation:"
												+ "\nip = " + ip
												+ "\nmethod = " + method
												+ "\nparameters = " + parameters;
					BoostLog.Instance.WriteEntry(message, EventLogEntryType.Information, clientAlias);
					m_initCalled = false;
				}
			}
			catch (Exception ex)
			{
				BoostLog log = BoostLog.Instance;
				log.Suffix = "ip = " + ip
										+ "\nmethod = " + method
										+ "\nparameters = " + parameters
										+ "\nresult = " + result;
				string errMsg = "Error logging transaction: " + ex.Message;
				if (ex.InnerException != null)
					errMsg += "\nInnerException: " + ex.InnerException.Message;
				log.WriteEntry(errMsg, EventLogEntryType.Warning, clientAlias);
			}
		}

		private bool LogTransaction(string alias, string ip, string method, string parameters, string response, string duration)
		{
			//truncate if too long
			const int logLen = 500;
			parameters = CheckLength(parameters, logLen);
			response = CheckLength(response, logLen);

			ClientLog client;
			bool found = GetClient(alias, out client);
			client.ServiceCalls.Add(DateTime.Now, ip, method, parameters, response, duration);
			return found;
		}
#endif

		private string CheckLength(string text, int maxLen)
		{
			int len = text.Length;
			if (len <= maxLen) return text;
			else return text.Trim().Substring(0, maxLen);
		}

		#endregion

		#region Report Replication

		//end point called by replicator from other servers
		public bool AppendReport(string alias, string report, string type)
		{
			if (m_readOnly) return false; //nothing to do

			ClientLog client;
			bool found = GetClient(alias, out client, false); //don't create if the client doesn't exist
			if (found)
			{
				if (type.Equals("TransactionLog"))
					client.ServiceCalls.QueueTrasactionReport(report);
				else if (type.Equals("ClickStreamLog"))
					client.Clicks.QueueClickReport(report);
				else if (type.Equals("ActionsLog"))
					client.Actions.QueueActionReport(report);
				else
					//TODO: modify methods to allow append of UsageLog from other server 
					return false; //unknown type
			}
			return found;
		}

		#endregion

		#region Sales Logging

		//write all transcation data to separate files (to be run in a separate thread)
		private void FlushSales()
		{
			if (m_readOnly) return; //nothing to do

			string alias = ""; //for error reporting
			foreach (ClientLog c in m_clients)
			{
				try
				{
						alias = c.Alias;
						c.Actions.Flush();
				}
				catch (Exception ex)
				{
					string errMsg = "Error flushing actions log: " + ex.Message;
					if (ex.InnerException != null)
						errMsg += "\nInner Exception: " + ex.InnerException.Message;
					BoostLog.Instance.WriteEntry(errMsg, EventLogEntryType.Error, alias);
				}
			}
		}

		public void LogSingleClick(string alias, string productID, string customerID, string pageCode)
		{
			LogSingleClick(alias, productID, customerID, pageCode, DateTime.Now);
		}

		public void LogSingleClick(string alias, string productID, string customerID, ClickRecord.pageType pageType)
		{
			LogSingleClick(alias, productID, customerID, pageType, DateTime.Now);
		}

		public void LogSingleClick(string alias, string productID, string customerID, string pageCode, DateTime date)
		{
			ClickRecord.pageType pageType = ClickRecord.ToPageTypeEnum(pageCode);
			LogSingleClick(alias, productID, customerID, pageType, date);
		}

		public void LogSingleClick(string alias, string productID, string customerID, ClickRecord.pageType pageType, DateTime date)
		{
			ClientLog client;
			bool found = GetClient(alias, out client, false); //don't create if the client doesn't exist
			if (found)
				client.Clicks.Add(productID, customerID, pageType, date);
		}

		public void LogSingleAction(string alias, string productID, string customerID, int quantity)
		{
			LogSingleAction(alias, productID, customerID, quantity, DateTime.Now);
		}

		public void LogSingleAction(string alias, string productID, string customerID, int quantity, DateTime date)
		{
			ClientLog client;
			bool found = GetClient(alias, out client, false); //don't create if the client doesn't exist
			if (found)
			{
				client.Actions.Add(productID, customerID, quantity, date);
				client.Clicks.Add(productID, customerID, ClickRecord.pageType.Checkout, date); //logging an action means checkout was completed
			}
		}

		#endregion

		}
}
