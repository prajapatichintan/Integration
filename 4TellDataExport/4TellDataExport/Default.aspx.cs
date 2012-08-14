using System;
using System.Collections; //ArrayList
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Xml.Linq;	//XElement
using System.Text;			//StringBuilder
using System.Net;				//HttpWebRequest/Response
using System.IO;				//StreamReader
using System.Threading; //Thread
using System.Timers;

using _4_Tell;
using _4_Tell.Utilities;
using _4_Tell.IO;

	public partial class _Default : System.Web.UI.Page
	{
		//private static System.Web.UI.Timer Timer2; 
		private static bool m_init = false;
		private static string m_dataPath = "";
		private static Replicator m_rep = null;
		private static BoostLog m_log = null;
		private static ClientList m_clients = null;
		private static Client m_activeClient = null; //not sure this should be static since it should be related only to the current process
		private static string m_orderID;
		private static ArrayList m_aliasList;

		#region WorkerThread
		//NOTE: In order to get progress updates, all functions are run on a worker thread
		//			operations post progress to the static ProgressText and a timer displays the contents on a set interval
		//			the worker thread reports when it is done and only one worker is run at a time
		public static bool WorkerDone = true;
		public static string ProgressText = "";
		private static Thread WorkerThread = null; 

		private class ExportWorker
		{
			private object m_workerLock = new object();

			public ExportWorker()
			{
			}

			public void ExtractAllData()
			{
				try
				{
					lock (m_workerLock)
					{
						if (m_activeClient == null) //nothing to do
						{
							ProgressText = "Error: no active client";
							WorkerDone = true;
							return;
						}
						m_activeClient.ExtractAllData();
						ProgressText = "Extraction complete for " + m_activeClient.Alias;
					}
				}
				catch (Exception ex)
				{
					ProgressText = "Error: " + ex.Message;
					if (ex.InnerException != null)
						ProgressText += "\n" + ex.InnerException.Message;
				}
				finally
				{
					WorkerDone = true;
				}
			}

			public void ExtractUpdate()
			{
				try
				{
					lock (m_workerLock)
					{
						if (m_activeClient == null) //nothing to do
						{
							ProgressText = "Error: no active client";
							WorkerDone = true;
							return;
						}
						m_activeClient.ExtractUpdate();
						ProgressText = "Sales update extracted for " + m_activeClient.Alias;
					}
				}
				catch (Exception ex)
				{
					ProgressText = "Error: " + ex.Message;
					if (ex.InnerException != null)
						ProgressText += "\n" + ex.InnerException.Message;
				}
				finally
				{
					WorkerDone = true;
				}
			}

		}

		#endregion


		protected void Page_Load(object sender, EventArgs e)
		{
			if (!IsPostBack)
			{
				if (!m_init || (m_clients == null)) Initialize();
				if (DropDownListClientAlias.SelectedItem == null) LoadClientList();
			}
		}

		private void Initialize()
		{
			m_dataPath = DataPath.Instance.Root;
			m_rep = Replicator.Instance;
			m_log = BoostLog.Instance;
			m_clients = ClientList.Instance; 

			AbortWorker();
			LoadClientList();

			ProgressTimer.Interval = 500;
			ProgressTimer.Enabled = false;
			m_init = true;
		}

		private void LoadClientList()
		{
			m_aliasList = m_clients.GetAliasList();
			m_aliasList.Sort();
			DropDownListClientAlias.DataSource = m_aliasList;
			DropDownListClientAlias.DataBind();
			if (DropDownListClientAlias.SelectedItem != null)
				TextBox_clientAlias.Text = DropDownListClientAlias.SelectedItem.Text;
		}

		private void AbortWorker()
		{
			if (WorkerThread != null)
				if (!WorkerDone)
					WorkerThread.Abort();
			WorkerDone = true;			
		}

		protected void ProgressTimer_Tick(object sender, EventArgs e)
		{
			if (m_activeClient != null)
			{
				string progress = m_activeClient.GetProgress();
				if (TextBox_result.Text != progress)
				{
					TextBox_result.Text = progress;
					UpdatePanel_Results.Update();
				}
				//if (WorkerDone)
				//{
				//  m_activeClient = null;
				//  ProgressTimer.Enabled = false;
				//}
			}
			else
				ProgressTimer.Enabled = false;
		}


		protected void Button_export_Click(object sender, EventArgs e)
		{
			if (!WorkerDone) return; //only one task at a time

			WorkerDone = false;
			string alias = TextBox_clientAlias.Text;
			string result = "Extracting all data for " + alias + "\n";
			TextBox_result.Text = result;
			UpdatePanel_Results.Update();

			try
			{
				m_activeClient = m_clients.Get(alias);
				if (m_activeClient == null)
					throw new Exception("Could not find the client: " + alias);

				//launch the thread to export and upload data files
				ExportWorker oWorker = new ExportWorker();
				WorkerThread = new Thread(new ThreadStart(oWorker.ExtractAllData));
				WorkerThread.Start();
				ProgressTimer.Enabled = true;
				
			}
			catch (Exception ex)
			{
				result += ex.Message;
				if (ex.InnerException != null)
					result += ex.InnerException.Message;
				ProgressTimer.Enabled = false;
				m_activeClient = null;
				TextBox_result.Text += result;
				UpdatePanel_Results.Update();
				WorkerDone = true;
			}
		}

		protected void Button_update_Click(object sender, EventArgs e)
		{
			if (!WorkerDone) return; //only one task at a time

			WorkerDone = false;
			string alias = TextBox_clientAlias.Text;
			string result = "Extracting sales update for " + alias + "\n";
			TextBox_result.Text = result;
			UpdatePanel_Results.Update();

			try
			{
				m_activeClient = m_clients.Get(alias);
				if (m_activeClient == null)
					throw new Exception("Could not find the client: " + alias);

				//launch the thread to export and upload data files
				ExportWorker oWorker = new ExportWorker();
				WorkerThread = new Thread(new ThreadStart(oWorker.ExtractUpdate));
				WorkerThread.Start();
				ProgressTimer.Enabled = true;
			}
			catch (Exception ex)
			{
				result += ex.Message;
				if (ex.InnerException != null)
					result += ex.InnerException.Message;
				ProgressTimer.Enabled = false;
				m_activeClient = null;
				TextBox_result.Text += result;
				UpdatePanel_Results.Update();
				WorkerDone = true;
			}
		}


		protected void Button_UpdateAllClients_Click(object sender, EventArgs e)
		{
			if (!WorkerDone) return; //only one task at a time

			string result = "Updating all Clients\n";
			foreach (string alias in m_aliasList)
			{
				WorkerDone = false;
				result += "\nExtracting data for " + alias + "\n";
				TextBox_result.Text = result;
				UpdatePanel_Results.Update();
				ProgressText = "";

				try
				{
					m_activeClient = m_clients.Get(alias);
					if (m_activeClient == null)
						throw new Exception("Could not find the client: " + alias);

					//launch the thread to export and upload data files
					ExportWorker oWorker = new ExportWorker();
					WorkerThread = new Thread(new ThreadStart(oWorker.ExtractUpdate));
					WorkerThread.Start();
					ProgressTimer.Enabled = true;

					while (!WorkerDone) Thread.Sleep(1500);
					result += ProgressText;
				}
				catch (Exception ex)
				{
					result += ex.Message;
					if (ex.InnerException != null)
						result += ex.InnerException.Message;
					ProgressTimer.Enabled = false;
					m_activeClient = null;
					TextBox_result.Text += result;
					UpdatePanel_Results.Update();
				}
				finally
				{
					WorkerDone = true;
				}
			}
		}

		protected void DropDownListClientAlias_SelectedIndexChanged(object sender, EventArgs e)
		{
			TextBox_clientAlias.Text = DropDownListClientAlias.SelectedItem.Text;
		}

		protected void Button_ResetClientList_Click(object sender, EventArgs e)
		{
			AbortWorker();
			m_clients.LoadClients();
			LoadClientList();
		}

		protected void Button_CancelThread_Click(object sender, EventArgs e)
		{
			AbortWorker();
		}

	}

//namespace