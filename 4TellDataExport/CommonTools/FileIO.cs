using System;
using System.Collections.Generic;
using System.IO;
using _4_Tell.Utilities;

namespace _4_Tell.IO
{
	#region Global Structures
	public enum RecType
	{
		// Higher numbers have higher priority
		//note, when adding to this list, keep in mind that the order matters. 
		//The order of this list specifies the priority for sort replacement
		NoRec, // 0
		TopSellerAtt , // 1
		CrossSellAttToAtt, // 2
		TopSeller, //= 3
		TopSell, // 4
		SimilarNonUpsell, // = 5
		ClickStreamNonUpsell, // = 6
		SimilarAttPartialMatch, //= 7
		SimilarAttMatch, //= 8
		SimilarRelatedItems, //= 9
		ClickStream2ClickStream, // = 10
		ClickStream2Buy, // = 11
		SimilarManual, // = 12
		CrossSellGenomicAtt2, // = 13
		CrossSellGenomicAtt1, // = 14
		CrossSellGenomic, // = 15
		CrossSellAction2Action, // = 16
		CrossSellManual // = 17
	}

	public struct Rec // Recommendation structure
	{
		public string alphaID;
		public float likelihood;
		public int numCommon;
		public byte type;

		public void Reset()
		{
			alphaID = "0";
			likelihood = -1;
			numCommon = 0;
			type = 0;
		}

		public void Copy(Rec newRec)
		{
			this.alphaID = newRec.alphaID;
			this.likelihood = newRec.likelihood;
			this.numCommon = newRec.numCommon;
			this.type = newRec.type;
		}
	}

	public struct RecHeaderAtt // Items uses an array for numActions since does not need numIndexes - which is number of 
	{
		public int numUsers;
		public int numActions;
		public int numItems;
	}

	public struct TopSellRec // Top sellers, etc.
	{
		public string alphaID;
		public int numActions;
		public byte type;

		public void Reset()
		{
			alphaID = "0";
			numActions = -1;
			type = 0;
		}

		public Rec ToRec()
		{
			Rec rec;
			rec.alphaID = alphaID;
			rec.numCommon = 0;
			rec.likelihood = numActions;
			rec.type = type;
			return rec;
		}
	}
	
	public struct WhyRec // Recommendation structure
	{
		public string alphaID;
		public float similarity;
		public int numCommon;
	}

	public struct BundleRec
	{
		public float similarity;
		public string alphaID1;
		public string alphaID2;
		public int numCommon;
	}

	public struct TopSellerRec
	{
		public string alphaID;
		public int numItems;
		public int numActions;
		public byte type;

		public Rec ToRec()
		{
			Rec rec;
			rec.alphaID = alphaID;
			rec.numCommon = 0;
			rec.likelihood = numActions;
			rec.type = type;
			return rec;
		}
	}
 
  public struct ItemDetail
  {
    public string itemID; // redundant, since can be looked up via itemIndex, but not a big memory waste
    public int numAtt1IDs;
    public string[] att1IDs;
    public string att2ID;
    public string name;
    public int price;
		public int numFilter1IDs;
    public string[] filter1IDs;
    public string link;
    public string imageLink;
		public string standardCode;
		public int salePrice;
		public string rating;
		public int numActions;
		public int numClicks;

    public void Reset()
    {
			itemID = "";
			numAtt1IDs = 0;
      att2ID = "";
      name = "";
      price = -100;
      numFilter1IDs = 0;
      link = "";
      imageLink = "";
			standardCode = "";
			salePrice = -100;
			rating = "";
			//numActions = -1;
    }
    public bool FindDuplicate(string att1ID)
    {
        for (int i = 0; i < att1IDs.Length; i++)
        {
            if (att1ID == att1IDs[i])
                return true;
        } // for i to numAtt1IDs
        return false;
    }
  }

	//public struct RecDisplayItem
	//{
	//  public string productID;
	//  public string title;
	//  public string price;
	//  public string pageLink;
	//  public string imageLink;
	//}

	public struct SalesData
	{
		public int numBins;
		public int numCountsWithLTE10Action; // Actions with value of less than or equal to 10
		public int numCountsWithGT10Action; // Actions with value greater than 10
		public int numActionsForMaxCount; // Number of actions for max value (i.e. mode)
		public int minActions; // Input actions
		public int maxActions; // Input actions
		public int minCount; // Output bin
		public int maxCount; // Output bin
		public int totalActions;
		public float aveActions;
		public int numRecs;
		// Below is for if decide to read into Marketing tool to lookup or chart, 
		// but for now for our eys only in Excel
		//public int[] panActionValue; // Smaller size than MaxActions
		//public int[] panCount; // Smaller size than MaxActions
	}
	#endregion Global Structures

	// Class //////////////////
	public class FileIO
	{
		#region Global Properties
		///////////////////////////////////////////////////
		// Global Members and Properties /////////////////////////
		// Basenames //
		// Items
		public const string SimilarItemsBasenameV2 = "SimilarTopSellItems.txt";
		public const string SimilarItemsBasename = "SimilarItems.txt";
		public const string CrossSellItemsBasename = "CrossSellItems.txt";
		public const string CrossSellAttItemsBasename = "CrossSellAttItems.txt";
		public const string PersonalizedItemsBasename = "PersonalizedItems.txt";
		public const string WhyItemsBasename = "WhyItems.txt";
		public const string BundleItemsBasename = "BundleItems.txt";
		public const string SalesDataBasename = "SalesData.txt";
		public const string TopSellerItemsBasename = "TopSellerItems.txt";
		public const string TopBuyerUsersBasename = "TopBuyerUsers.txt";
		// Att1s
		public const string CrossSellAtt1sBasename = "CrossSellAtt1s.txt";
		public const string TopSellAtt1ItemsBasename = "TopSellAtt1Items.txt";
		public const string BundleAtt1sBasename = "BundleAtt1s.txt";
		public const string TopSellerAtt1sBasename = "TopSellerAtt1s.txt";
		// Att2s
		public const string CrossSellAtt2sBasename = "CrossSellAtt2s.txt";
		public const string TopSellAtt2ItemsBasename = "TopSellAtt2Items.txt";
		public const string BundleAtt2sBasename = "BundleAtt2s.txt";
		public const string TopSellerAtt2sBasename = "TopSellerAtt2s.txt";
		// Input
		public const string RecStatsBasename = "RecStats.txt";
		public const string ConfigBasename = "ConfigBoost.txt";
		public const string ConfigDefaultBasename = "ConfigBoostDefault.txt";
		public const string ConfigOverrideBasename = "ConfigBoostOverride.txt";
		public const string ItemDetailsBasename = "ItemDetails.txt";
		public const string Att1NamesBasename = "Attribute1Names.txt";
		public const string Att2NamesBasename = "Attribute2Names.txt";
		public const string Att1NamesBasenameV1 = "CategoryNames.txt";
		public const string Att2NamesBasenameV1 = "BrandNames.txt";
		public const string DoNotRecommendBasename = "Exclusions.txt";
		public const string DoNotRecommendBasenameV2 = "DoNotRecommend.txt";
		public const string DoNotRecommendBasenameV1 = "ProductsSoldOut.txt";
		public const string PromotionsBasename = "Promotions.txt";

		// Misc
		char[] m_tabSeparator = new char[] { '\t' };
		const string m_newestVersion = "3.1";
		public static string Version { get { return m_newestVersion; } }

		// Error Members and Properties//
		int m_error;
		string m_errorText;
		public string ErrorText
		{
			set { m_errorText = value; }
			get { return m_errorText; }
		}

		int m_warning;
		public int Warning
		{
			set { m_warning = value; }
			get { return m_warning; }
		}
		string m_warningText;
		public string WarningText
		{
			set { m_warningText = value; }
			get { return m_warningText; }
		}

		// Config Stuff //
		// Version
		double m_configVersion = 0;
		public double ConfigVersion
		{
			get { return m_configVersion; }
		}

		// Owner
		string m_owner = null;
		public string Owner
		{
			set { m_owner = value; }
			get { return m_owner; }
		}

		// OwnerEmail
		string m_ownerEmail = null;
		public string OwnerEmail
		{
			set { m_ownerEmail = value; }
			get { return m_ownerEmail; }
		}

		// ReportLevel
		ReportLevel m_reportLevel = ReportLevel.Error;
		public ReportLevel ReportLevel
		{
			set { m_reportLevel = value;}
			get { return m_reportLevel;}
		}

		// Currency
		string m_currency = null;
		public string Currency
		{
			set { m_currency = value;}
			get { return m_currency;}
		}

		// Attribute1Name
		string m_attribute1Name = null;
		public string Att1Name
		{
			set { m_attribute1Name = value;}
			get { return m_attribute1Name;}
		}

		// Attribute2Name
		string m_attribute2Name = null;
		public string Att2Name
		{
			set { m_attribute2Name = value;}
			get { return m_attribute2Name;}
		}

		// Resell
		bool m_resell = false;
		public bool Resell
		{
			set { m_resell = value; }
			get { return m_resell; }
		}

		// MinLikelihood
		int m_minLikelihood = 0;
		public int MinLikelihood
		{
			set { m_minLikelihood = value; }
			get { return m_minLikelihood; }
		}

		// MinCommon
		int m_minCommon = 0;
		public int MinCommon
		{
			set { m_minCommon = value; }
			get { return m_minCommon; }
		}

		// ResultFormat
		private ResultFormats m_resultFormat;
		public ResultFormats ResultFormat
		{
			set { m_resultFormat = value; }
			get { return m_resultFormat; }
		}

		// SimilarTopSellerRule
		private string m_similarTopSellerRule = "Att1";
		public string SimilarTopSellerRule
		{
			set { m_similarTopSellerRule = value; }
			get { return m_similarTopSellerRule; }
		}

		// SimilarClickStreamRule
		private string m_similarClickStreamRule = "Att1";
		public string SimilarClickStreamRule
		{
			set { m_similarClickStreamRule = value; }
			get { return m_similarClickStreamRule; }
		}

		// RulesEnabled
		private RulesTypes m_rulesEnabled = RulesTypes.NoRules;
		public RulesTypes RulesEnabled
		{
			set { m_rulesEnabled = value; }
			get { return m_rulesEnabled; }
		}

		// RulesDisabled
		private RulesTypes m_rulesDisabled = RulesTypes.NoRules;
		public RulesTypes RulesDisabled
		{
			set { m_rulesDisabled = value; }
			get { return m_rulesDisabled; }
		}

		// UniversalFilterIDs
		private string m_universalFilterIDs = "";
		public string UniversalFilterIDs
		{
			set { m_universalFilterIDs = value; }
			get { return m_universalFilterIDs; }
		}

		// FilterFilenames
		private string m_filterFilename = "";
		public string FilterFilename
		{
			set { m_filterFilename = value; }
			get { return m_filterFilename; }
		}

		// RulesExist
		private bool m_rulesExist = false;
		public bool RulesExist
		{
			set { m_rulesExist = value; }
			get { return m_rulesExist; }
		}

		// UpsellFactor
		private float m_upsellFactor = 0.2F;
		public float UpsellFactor
		{
			set { m_upsellFactor = value; }
			get { return m_upsellFactor; }
		}

		// CrossSellAtt1IDs
		private string m_crossAtt1IDs = "";
		public string CrossSellAtt1IDs
		{
			set { m_crossAtt1IDs = value; }
			get { return m_crossAtt1IDs; }
		}

		// DoNotRecommendExists
		// defined below

		// ReplacementsExists
		// defined below
		#endregion Globals

		#region Global Members
		public int ReadKeywordFile(string filename, string[] tags, ref string[] tagValues)
		{
			int i;
			StreamReader file;

			try
			{
				file = new StreamReader(m_clientDataPath + filename);
			}
			catch (Exception)
			{
				return 0;
			}

			//Read parameters
			m_lineNum = 0;

			// Read File
			string inputLine;
			do
			{
				m_lineNum++;
				inputLine = file.ReadLine();

				//EOF
				if (inputLine == null)
					break;
				if (inputLine.Length == 0)
					continue;

				// == FIND TAG ==
				string[] tempSplit = inputLine.Split(m_tabSeparator);
				// TagValue
				for (i = 0; i < tags.Length; i++)
				{
					if (tempSplit[0].ToLower().StartsWith(tags[i].ToLower()))
						break;
				}
				// Good Tag
				// Sales data files will not have tag
				if (i < tags.Length)
				{
					if (tempSplit.Length > 1)
						tagValues[i] = tempSplit[1];
					else
						tagValues[i] = "";
				}

			} while (true);

			file.Close();
			return 0;
		}

		//converter for integer values
		public bool ConvertToInt(ref int value, string sVal, string tag, int lineNum)
		{
			int iVal;
			try
			{
				iVal = Convert.ToInt32(sVal);
			}
			catch (Exception)
			{
				string clientMsg = "";
				if (m_clientAlias.Length > 0) clientMsg = "for client " + m_clientAlias;
				m_errorText = String.Format(
					"Invalid Keyword file {0}.\n"
					+ "Line {1}: Expected {2} value as a number but read \"{3}\"\n",
					clientMsg, lineNum, tag, sVal);
				return false;
			}
			value = iVal; // only update on success
			return true;
		}

		//converter for integer values
		public bool ConvertToFloat(ref float value, string sVal, string tag, int lineNum)
		{
			float fVal;
			try
			{
				fVal = (float)Convert.ToDouble(sVal);
			}
			catch (Exception)
			{
				string clientMsg = "";
				if (m_clientAlias.Length > 0) clientMsg = "for client " + m_clientAlias;
				m_errorText = String.Format(
					"Invalid Keyword file {0}.\n"
					+ "Line {1}: Expected {2} value as a number but read \"{3}\"\n",
					clientMsg, lineNum, tag, sVal);
				return false;
			}
			value = fVal; // only update on success
			return true;
		}

		//override converter for boolean values
		public bool ConvertToBool(ref bool value, string sVal, string tag, int lineNum)
		{
			bool bVal;
			if (sVal == null)
				return false;

			if (sVal.CompareTo("1") == 0) bVal = true;
			else if (sVal.CompareTo("0") == 0) bVal = false;
			else if (sVal.ToLower().CompareTo("true") == 0) bVal = true;
			else if (sVal.ToLower().CompareTo("false") == 0) bVal = false;
			else if (sVal.ToLower().CompareTo("yes") == 0) bVal = true;
			else if (sVal.ToLower().CompareTo("no") == 0) bVal = false;
			else
			{
				string clientMsg = "";
				if (m_clientAlias.Length > 0) clientMsg = "for client " + m_clientAlias;
				m_errorText = String.Format(
					"Invalid Keyword file {0}.\n"
					+ "Line {1}: Expected {2} value as a boolean but read \"{3}\"\n",
					clientMsg, lineNum, tag, sVal);
				return false;
			}
			value = bVal;
			return true;
		}

		public bool GetVersion(string version, out double versionNum, string newestVersion, string filename)
		{
			versionNum = -1;
			try
			{
				versionNum = Convert.ToDouble(version);
				double valid = Convert.ToDouble(newestVersion);
				if (versionNum <= valid) return true;
			}
			catch (Exception) { } //ignore exception, it is still an invalid version

			m_errorText = String.Format("Invalid Version {0} in {1}. This program requires a version less than {2}.",
				version, filename, newestVersion);
			return false;
		}
		#endregion // Global Members

		#region Data Path
		// DataPathRoot
		public string DataPathRoot
		{
			get { return DataPath.Instance.Root; }
		}

		// ClientDataPath
		private string m_clientDataPath = "";
		public string ClientDataPath
		{
			get 
			{
				if ((m_clientDataPath.Length < 1) && (m_clientAlias.Length > 0))
					m_clientDataPath = DataPathRoot + m_clientAlias + "\\";
				return m_clientDataPath; 
			}
			set { m_clientDataPath = value; }
		}

		// ClientAlias
		private string m_clientAlias = "";
		public string ClientAlias
		{
			get { return m_clientAlias; }
			set
			{
				string clientAlias = DataPath.CleanFolderName(value);
				if (clientAlias.Length > 0)
					m_clientAlias = clientAlias;
			}
		}

		#endregion Data Path

		#region Config File
		// !!! SEE GENERATOR ReadConfigFileV3 !!!
		// Many rules are not used in WebEngine, so they are not checked. 
		 // In additon, bugs in RulesEnabled & RulesDisabled.

		//////////////////////////////////////////////////////////////////
		// Input ////////////////////////////////////////////////
		// Input Members and Properties **
		StreamReader m_configFile = null;
		string m_configFilename = null;
		int m_maxSalesDataAgeInMonths;
		int m_lineNum = 0;

		// !!! SEE GENERATOR ReadConfigFileV3 !!!
		public enum RulesTypes : byte
		{
			NoRules,
			Filter,
			Upsell,
			CrossAtt1,
			Resell,
			Exclusions,
			Replacements,
			Promotions,
			ManualCrossSell,
			ManualUpsell
		};

		enum eConfigTags : byte
		{
			Version,
			Owner,
			Email,
			ReportLevel,
			Currency,
			Attribute1Name,
			Attribute2Name,
			Resell,
			MinLikelihood,
			MinCommon,
			ResultFormat,
			DoNotRecommendExists, // Depricated 3.2 on 05-10-2012
			ExclusionsExist,  // Depricated 3.2 on 05-10-2012
			ReplacementsExist,  // Depricated 3.2 on 05-10-2012
			MaxSalesDataAgeInMonths,
			SimilarTopSellerRule,
			SimilarClickStreamRule,
			RulesTypes, // Depricated 3.2 on 5-10-2012
			RulesEnabled,
			RulesDisabled,
			FilterFilename,
			UniversalFilterIDs,
			UpsellFactor,
			CrossAtt1IDs
		};
		
		string[] configTags = 
		{ 
			"Version", 
			"Owner", 
			"Email", 
			"ReportLevel",
			"Currency",
			"Attribute1Name",
			"Attribute2Name",
			"Resell", 
			"MinLikelihood", 
			"MinCommon", 
			"ResultFormat",
			"DoNotRecommend",
			"Exclusions",
			"Replacements",
			"MaxSales",
			"SimilarTopSellerRule", 
			"SimilarClickStreamRule",
			"RulesTypes",
			"RulesEnabled", // FilterRule, UpsellRule, CrossAtt1Rule, Promotions, Exclusions, Replacements, Related
			"RulesDisabled", // FilterRule, UpsellRule, CrossAtt1Rule, Promotions, Exclusions, Replacements, Related
			"FilterFilename",
			"UniversalFilterIDs",
			"UpsellFactor",
			"CrossAtt1IDs",
		};

		public string ConfigFilename
		{
			set { m_configFilename = value; }
			get { return m_configFilename; }
		}

		public int ReadConfig() 
		{
			bool configFileExist;

			m_warning = 0; m_warningText = "";

			// Input =====================================
			string[] tagValues = null;

			// Determine version //
			// Open file
			configFileExist = true;
			m_configFilename = ConfigBasename;
			try
			{
				m_configFile = new StreamReader(m_clientDataPath + m_configFilename);
			}
			catch (Exception)
			{
				m_configVersion = 3;
				configFileExist = false;
			}

			if (configFileExist)
			{
				// Get version
				// Although file can have other configTags in any order, version must be first!
				string inputLine = m_configFile.ReadLine();
				if (inputLine == null)
				{
					m_errorText = String.Format("The first line of {0} should be Version <tab> <versionNum>.", ConfigBasename);
					m_configFile.Close();
					return 1;
				}
				string[] tempSplit = inputLine.Split(m_tabSeparator);
				if (tempSplit.Length < 2 || !tempSplit[0].Equals("version", StringComparison.CurrentCultureIgnoreCase))
				{
					m_errorText = String.Format("The first line of {0} should be Version <tab> <versionNum>.", ConfigBasename);
					m_configFile.Close();
					return 1;
				}
				if (!GetVersion(tempSplit[1], out m_configVersion, m_newestVersion, ConfigBasename))
				{
					m_configFile.Close();
					return 1;
				}
				// Close file
				m_configFile.Close();
			}

			// Read for Correct Version
			if (m_configVersion < 2)
			{
				m_errorText = "Your config file is version 1, and needs to be updated to version 3.";				
				return 1;
			}

			// Read Config Files //
			tagValues = new string[configTags.Length];
			for (int i = 0; i < configTags.Length; i++)
				tagValues[i] = "";

			// Read ConfigBoostDefault
			m_error = ReadKeywordFile(ConfigDefaultBasename, configTags, ref tagValues);
			if (m_error != 0) 
				return m_error;

			// Read ConfigBoost
			// This is that automated export
			m_error = ReadKeywordFile(ConfigBasename, configTags, ref tagValues);
			if (m_error != 0) 
				return m_error;

			// Read ConfigBoostOverride
			// This is second so OVERWRITES previous configTags
			m_error = ReadKeywordFile(ConfigOverrideBasename, configTags, ref tagValues);
			if (m_error != 0) 
				return m_error;

			// Assign values //
			// version already set = values[0];

			// Owner
			m_owner = tagValues[(int)eConfigTags.Owner];

			// Email
			m_ownerEmail = tagValues[(int)eConfigTags.Email];

			// ReportLevel
			try
			{
				//m_reportLevel = (ReportLevel)Enum.Parse(typeof(ReportLevel), tagValues[pos], true);
				m_reportLevel = BoostLog.GetReportLevel( tagValues[(int)eConfigTags.ReportLevel] );
			}
			catch
			{
				m_reportLevel = ReportLevel.Error;
			}
						
			// Currency
			m_currency = tagValues[(int)eConfigTags.Currency];
			if (m_currency == "")
			{
				m_currency = "$";
			}

			// Attribute Names
			m_attribute1Name = tagValues[(int)eConfigTags.Attribute1Name];
			if (m_attribute1Name == "")// && m_att1Exists) 
			{
				m_attribute1Name = "Category";
			}
			m_attribute2Name = tagValues[(int)eConfigTags.Attribute2Name];
			if (m_attribute2Name == "") // && m_att2Exists) 
			{
				m_attribute2Name = "Brand";
			}

			// Resell
			if (!ConvertToBool(ref m_resell, tagValues[(int)eConfigTags.Resell], configTags[(int)eConfigTags.Resell], (int)eConfigTags.Resell))
			{
				m_resell = true;
			}

			// MinLikelihood 
			if (!ConvertToInt(ref m_minLikelihood, tagValues[(int)eConfigTags.MinLikelihood], configTags[(int)eConfigTags.MinLikelihood], (int)eConfigTags.MinLikelihood))
			{
				m_minLikelihood = 3;
			}

			// MinCommon
			if (!ConvertToInt(ref m_minCommon, tagValues[(int)eConfigTags.MinCommon], configTags[(int)eConfigTags.MinCommon], (int)eConfigTags.MinCommon))
			{
				m_minCommon = 2;
			}

			// ResultFormat
			try
			{
				m_resultFormat = (ResultFormats)Enum.Parse(typeof(ResultFormats), tagValues[(int)eConfigTags.ResultFormat], true);
			}
			catch
			{
				m_resultFormat = ResultFormats.TabDelimited;
			}

			// Depricated on 5-10-2012
			// DoNotRecommendExists
			// ExclusionsExist takes priority, but also check for DoNotRecommendExists
			if (!ConvertToBool(ref m_doNotRecommendExists, tagValues[(int)eConfigTags.ExclusionsExist], configTags[(int)eConfigTags.ExclusionsExist], (int)eConfigTags.ExclusionsExist))
			{
				if (!ConvertToBool(ref m_doNotRecommendExists, tagValues[(int)eConfigTags.DoNotRecommendExists], configTags[(int)eConfigTags.DoNotRecommendExists], (int)eConfigTags.DoNotRecommendExists))
					m_doNotRecommendExists = false;
			}

			// Depricated on 5-10-2012
			// ReplacementExists
			if (!ConvertToBool(ref m_replacementsExists, tagValues[(int)eConfigTags.ReplacementsExist], configTags[(int)eConfigTags.ReplacementsExist], (int)eConfigTags.ReplacementsExist))
			{
				m_replacementsExists = false;
			}

			// MaxSalesDataAgeInMonths
			if (!ConvertToInt(ref m_maxSalesDataAgeInMonths, tagValues[(int)eConfigTags.MaxSalesDataAgeInMonths], configTags[(int)eConfigTags.MaxSalesDataAgeInMonths], (int)eConfigTags.MaxSalesDataAgeInMonths))
			{
				m_maxSalesDataAgeInMonths = 18;
			}

			// SimilarTopSellerRule
			if (tagValues[(int)eConfigTags.SimilarTopSellerRule] != "")
				m_similarTopSellerRule = tagValues[(int)eConfigTags.SimilarTopSellerRule];

			// SimilarClickStreamRule
			if (tagValues[(int)eConfigTags.SimilarClickStreamRule] != "")
				m_similarClickStreamRule = tagValues[(int)eConfigTags.SimilarClickStreamRule];

			// !!! THIS DOES NOT WORK PROPERLY. NOT USED IN WEBENGINE. SEE GENERATOR ReadConfigFileV3 !!!
			 // If ConfigBoost or ConfigBoostOverride add new rules, they should accumulate, whereas they overwrite here
			// RulesEnabled
			try
			{
				// RulesEnabled takes precedence
				if (tagValues[(int)eConfigTags.RulesTypes] != "")
					m_rulesEnabled = (RulesTypes)Enum.Parse(typeof(RulesTypes), tagValues[(int)eConfigTags.RulesTypes], true);
				m_rulesEnabled = (RulesTypes)Enum.Parse(typeof(RulesTypes), tagValues[(int)eConfigTags.RulesEnabled], true);
				if (m_rulesEnabled != RulesTypes.NoRules)
					m_rulesExist = true;
			}
			catch
			{
				m_rulesEnabled = RulesTypes.NoRules;
			}

			// RulesDisabled
			// See ReadConfigFileV3 in Generator

			// UniversalFilterIDs
			m_universalFilterIDs = tagValues[(int)eConfigTags.UniversalFilterIDs];

			// FilterFilename
			m_filterFilename = tagValues[(int)eConfigTags.FilterFilename];

			// UpsellFactor 
			if (!ConvertToFloat(ref m_upsellFactor, tagValues[(int)eConfigTags.UpsellFactor], configTags[(int)eConfigTags.UpsellFactor], (int)eConfigTags.UpsellFactor))
				m_upsellFactor = 0.2F;

			// CrossAtt1IDs
			m_crossAtt1IDs = tagValues[(int)eConfigTags.CrossAtt1IDs];

			return 0;
		}
		#endregion Config

		#region RecStats
		//////////////////////////////////////////////////////////////////
		// RecStats ////////////////////////////////////////////////
		// RecStats Members **
		//int m_lineNumRecStats;
		StreamReader m_recStatsFile = null;
		string m_recStatsFilename = null;

		enum eRecStatsTags : byte
		{
			NumItems,
			NumExclusions,
			NumUsers,
			NumAtt1s,
			NumAtt2s,
			NumRecs,
			NumTopSellerRecs,
			NumBundleRecs,
			NumWhyItems,
			NumSales,
			NumUniqueSales,
			Attribute1Exists,
			Attribute2Exists,
			Filter1Exists,
			SalesExportDate,
			CatalogExportDate,
			DoNotRecommendExportDate,
			ExclusionsExportDate,
			ReplacementsExportDate,
			PromotionsExportDate
		};

		string[] recStatsTags = 
		{ 
			"NumItems",
			"NumExclusions",
			"NumUsers",
			"NumAtt1s",
			"NumAtt2s",
			"NumRecs",
			"NumTopSellerRecs",
			"NumBundleRecs",
			"NumWhyItems",
			"NumSales",
			"NumUniqueSales",
			"Attribute1 Exist",
			"Attribute2 Exist",
			"Filter1 Exist",
			"Sales Export",
			"Catalog Export",
			"DoNoRecommend Export",
			"Exclusions Export",
			"Replacements Export",
			"Promotions Export"
		};

		// Properties
		public string RecStatsFilename
		{
			set { m_recStatsFilename = value; }
			get { return m_recStatsFilename; }
		}

		// Version
		double m_recStatsVersion = 0;
		public double RecStatsVersion
		{
			get { return m_recStatsVersion; }
		}

		// NumItems
		int m_numItems = -1;
		public int NumItems
		{
			set { m_numItems = value; }
			get { return m_numItems; }
		}

		// NumExclusions
		int m_numExclusions = -1;
		public int NumExclusions
		{
			set { m_numExclusions = value; }
			get { return m_numExclusions; }
		}

		// NumUsers
		int m_numUsers = -1;
		public int NumUsers
		{
			set { m_numUsers = value; }
			get { return m_numUsers; }
		}

		// NumAtt1s
		int m_numAtt1s = -1;
		public int NumAtt1s
		{
			set { m_numAtt1s = value; }
			get { return m_numAtt1s; }
		}

		// NumAtt2s
		int m_numAtt2s = -1;
		public int NumAtt2s
		{
			set { m_numAtt2s = value; }
			get { return m_numAtt2s; }
		}

		// MaxItemID // DEPRICATED - remove in next version
		int m_maxItemID = -1;
		// MaxUserID // DEPRICATED - remove in next version
		int m_maxUserID = -1;

		// NumRecs
		int m_numRecs = 20;
		public int NumRecs
		{
			set { m_numRecs = value; }
			get { return m_numRecs; }
		}

		// NumTopSellerRecs
		int m_numTopSellerRecs = 80;
		public int NumTopSellerRecs
		{
			set { m_numTopSellerRecs = value; }
			get { return m_numTopSellerRecs; }
		}

		// NumBundleRecs
		int m_numBundleRecs = 100;
		public int NumBundleRecs
		{
			set { m_numBundleRecs = value; }
			get { return m_numBundleRecs; }
		}

		// NumWhyItems
		int m_numWhyItems = -1;
		public int NumWhyItems
		{
			set { m_numWhyItems = value; }
			get { return m_numWhyItems; }
		}

		// NumSales
		int m_numSales = -1;
		public int NumSales
		{
			get { return m_numSales; }
		}

		// NumUniqueSales
		int m_numUniqueSales = -1;
		public int NumUniqueSales
		{
			get { return m_numUniqueSales; }
		}

		// AttsExists
		bool m_att1Exists = false;
		public bool Att1Exists
		{
			get { return (m_att1Exists); }
		}
		bool m_att2Exists = false;
		public bool Att2Exists
		{
			get { return (m_att2Exists); }
		}
		public bool ProductOnlyExists
		{
			get { return (!m_att1Exists && !m_att2Exists); }
		}
		bool m_filter1Exists = false;
		public bool Filter1Exists
		{
			get { return (m_filter1Exists); }
		}

		// Sales Export Date
		string m_salesExportDate = "";
		public string SalesExportDate
		{
			set { m_salesExportDate = value; }
			get { return m_salesExportDate; }
		}

		// Catalog Export Date
		string m_catalogExportDate = "";
		public string CatalogExportDate
		{
			set { m_catalogExportDate = value; }
			get { return m_catalogExportDate; }
		}

		// DoNotRecommendExists Export Date
		string m_doNotRecommendExportDate = "";
		public string DoNotRecommendExportDate
		{
			set { m_doNotRecommendExportDate = value; }
			get { return m_doNotRecommendExportDate; }
		}

		// ReplacementsExist
		bool m_replacementsExists = false; // set to true if file is read
		public bool ReplacementsExists
		{
			set { m_replacementsExists = value; }
			get { return m_replacementsExists; }
		}

		// Replacements Export Date
		string m_replacementsExportDate = "";
		public string ReplacementsExportDate
		{
			set { m_replacementsExportDate = value; }
			get { return m_replacementsExportDate; }
		}

		// Promotions Export Date
		string m_promotionsExportDate = "";
		public string PromotionsExportDate
		{
			set { m_promotionsExportDate = value; }
			get { return m_promotionsExportDate; }
		}

		// RecStats ////////////
		public int ReadRecStats()
		{
			// Init to set m_numItems
			m_warning = 0;
			m_warningText = "";
			m_error = InitRecStats();
			if (m_error != 0)
				return m_error;
			return 0;
		}

		protected int InitRecStats()
		{
			string temp;
			string[] tempSplit;

			// Open file
			if (m_recStatsFilename == null)
				m_recStatsFilename = RecStatsBasename;

			try
			{
				m_recStatsFile = new StreamReader(m_clientDataPath + m_recStatsFilename);
			}
			catch (Exception)
			{
				m_errorText = String.Format("\nError: Could not open recommendation stats file {0}.\n\n", m_recStatsFilename);
				return 2;
			}

			// Check Version //
			temp = m_recStatsFile.ReadLine();
			if (temp == null)
			{
				m_errorText = String.Format("Reached EOF of recommendation stats {0}.\n", m_recStatsFilename);
				m_recStatsFile.Close();
				return 1;
			} // EOF
			tempSplit = temp.Split(m_tabSeparator);
			m_recStatsVersion = Convert.ToDouble(tempSplit[1]);
			m_recStatsFile.Close();

			// Read RecStats File //
			// Input
			string[] tagValues = null;

			tagValues = new string[recStatsTags.Length];
			for (int i = 0; i < recStatsTags.Length; i++)
				tagValues[i] = "";

			// Read ConfigBoostInternal
			m_error = ReadKeywordFile(RecStatsBasename, recStatsTags, ref tagValues);
			if (m_error != 0) 
				return m_error;

			// Read File //
			bool found;

			// NumItems
			found = ConvertToInt(ref m_numItems, tagValues[(int)eRecStatsTags.NumItems], 
									recStatsTags[(int)eRecStatsTags.NumItems], (int)eRecStatsTags.NumItems);
			if (!found)
			{
				m_errorText +=String.Format("NumItems is not found in {0}.\n", RecStatsBasename);
				return 1;
			}

			// NumExclusions
			found = ConvertToInt(ref m_numExclusions, tagValues[(int)eRecStatsTags.NumExclusions],
									recStatsTags[(int)eRecStatsTags.NumExclusions], (int)eRecStatsTags.NumExclusions);
			if (!found)
			{
				m_errorText += String.Format("NumExclusions is not found in {0}.\n", RecStatsBasename);
			}

			// NumUsers
			found = ConvertToInt(ref m_numUsers, tagValues[(int)eRecStatsTags.NumUsers],
									recStatsTags[(int)eRecStatsTags.NumUsers], (int)eRecStatsTags.NumUsers);
			if (!found)
			{
				m_errorText += String.Format("NumUsers is not found in {0}.\n", RecStatsBasename);
				return 1;
			}

			// NumAtt1s
			found = ConvertToInt(ref m_numAtt1s, tagValues[(int)eRecStatsTags.NumAtt1s],
									recStatsTags[(int)eRecStatsTags.NumAtt1s], (int)eRecStatsTags.NumAtt1s);
			if (!found)
				m_numAtt1s = 0;
				// No error unless Att1sExist
			
			// NumAtt2s
			found = ConvertToInt(ref m_numAtt2s, tagValues[(int)eRecStatsTags.NumAtt2s],
									recStatsTags[(int)eRecStatsTags.NumAtt2s], (int)eRecStatsTags.NumAtt2s);
			if (!found)
				m_numAtt2s = 0;
				// No error unless Att2sExist

			// NumRecs
			found = ConvertToInt(ref m_numRecs, tagValues[(int)eRecStatsTags.NumRecs],
									recStatsTags[(int)eRecStatsTags.NumRecs], (int)eRecStatsTags.NumRecs);
			if ( !found && m_recStatsVersion > 3.05 )
			{
				m_errorText += String.Format("NumRecs is not found in {0}.\n", RecStatsBasename);
				return 1;
			}

			// NumTopSellerRecs
			found = ConvertToInt(ref m_numTopSellerRecs, tagValues[(int)eRecStatsTags.NumTopSellerRecs],
									recStatsTags[(int)eRecStatsTags.NumTopSellerRecs], (int)eRecStatsTags.NumTopSellerRecs);
			if ( !found && m_recStatsVersion > 3.05 )
			{
				m_errorText += String.Format("NumTopSellerRecs is not found in {0}.\n", RecStatsBasename);
				return 1;
			}

			// NumBundleRecs
			found = ConvertToInt(ref m_numBundleRecs, tagValues[(int)eRecStatsTags.NumBundleRecs],
									recStatsTags[(int)eRecStatsTags.NumBundleRecs], (int)eRecStatsTags.NumBundleRecs);
			if ( !found && m_recStatsVersion > 3.05 )
			{
				m_errorText += String.Format("NumBundleRecs is not found in {0}.\n", RecStatsBasename);
				return 1;
			}

			// NumWhyItems
			found = ConvertToInt(ref m_numWhyItems, tagValues[(int)eRecStatsTags.NumWhyItems],
									recStatsTags[(int)eRecStatsTags.NumWhyItems], (int)eRecStatsTags.NumWhyItems);
			if (!found && m_recStatsVersion > 3.05)
			{
				m_warningText += String.Format("NumWhyItems is not found in {0}.\n", RecStatsBasename);
				m_warning = 1;
			}

			// NumSales
			found = ConvertToInt(ref m_numSales, tagValues[(int)eRecStatsTags.NumSales],
						recStatsTags[(int)eRecStatsTags.NumSales], (int)eRecStatsTags.NumSales);
			if (!found)
			{
				m_warningText += String.Format("NumSales is not found in {0}.\n", RecStatsBasename);
				m_warning = 1;
			}

			// NumUniqueSales
			found = ConvertToInt(ref m_numUniqueSales, tagValues[(int)eRecStatsTags.NumUniqueSales],
									recStatsTags[(int)eRecStatsTags.NumUniqueSales], (int)eRecStatsTags.NumUniqueSales);
			if (!found)
			{
				m_warningText += String.Format("NumUniqueSales is not found in {0}.\n", RecStatsBasename);
				m_warning = 1;
			}

			// Attribute1 Exists
			m_att1Exists = false;
			found = ConvertToBool(ref m_att1Exists, tagValues[(int)eRecStatsTags.Attribute1Exists],
									recStatsTags[(int)eRecStatsTags.Attribute1Exists], (int)eRecStatsTags.Attribute1Exists);
			if (!found && m_numAtt1s != 0)
			{
				m_errorText = String.Format("Attribute1Exists is not found and there are {0} attribute1s in {1}.\n", 
						m_numAtt1s, RecStatsBasename);
				return 1;
			}

			// Attribute2 Exists
			m_att2Exists = false;
			found = ConvertToBool(ref m_att2Exists, tagValues[(int)eRecStatsTags.Attribute2Exists],
									recStatsTags[(int)eRecStatsTags.Attribute2Exists], (int)eRecStatsTags.Attribute2Exists);
			if (!found && m_numAtt2s != 0)
			{
				m_errorText = String.Format("Attribute2Exists is not found and there are {0} attribute2s in {1}.\n",
						m_numAtt2s, RecStatsBasename);
				return 1;
			}

			// Filter1 Exists
			found = ConvertToBool(ref m_filter1Exists, tagValues[(int)eRecStatsTags.Filter1Exists],
									recStatsTags[(int)eRecStatsTags.Filter1Exists], (int)eRecStatsTags.Filter1Exists);
			if (!found && m_recStatsVersion > 3.05)
			{
				m_warningText += String.Format("Filter1Exists is not found in {0}.\n",
						m_numAtt1s, RecStatsBasename);
				m_warning = 1;
			}

			// Sales Export Date
			m_salesExportDate = tagValues[(int)eRecStatsTags.SalesExportDate];

			// Catalog Export Date
			m_catalogExportDate = tagValues[(int)eRecStatsTags.CatalogExportDate];

			// DoNotRecommendExists Export Date
			m_doNotRecommendExportDate = tagValues[(int)eRecStatsTags.DoNotRecommendExportDate];
			m_doNotRecommendExportDate = tagValues[(int)eRecStatsTags.ExclusionsExportDate];

			// ReplacementsSale Export Date
			m_replacementsExportDate = tagValues[(int)eRecStatsTags.ReplacementsExportDate];

			// PromotionsSale Export Date
			m_promotionsExportDate = tagValues[(int)eRecStatsTags.PromotionsExportDate];

			m_recStatsFile.Close();
			return 0;
		}
		#endregion RecStats

		#region Item Globals
		// Item Stuff ////////////////////////////////////////////////////////////////////
		// ItemIndex
		CIndex m_itemIndex = new CIndex();
		public CIndex ItemIndex
		{
			set { m_itemIndex = value; }
			get { return m_itemIndex; }
		}
		#endregion ItemGlobals

		#region Similar Items
		//////////////////////////////////////////////////////////////////
		// SimilarItems ////////////////////////////////////////////////
		// SimilarItem Members and Properties **
		StreamReader m_similarItemsFile = null;
		string m_similarItemsFilename = null;
		Rec[,] m_similarItems = null;
		float m_similarItemsVersion;

		public string SimilarItemsFilename
		{
			set { m_similarItemsFilename = value; }
			get { return m_similarItemsFilename; }
		}
		public Rec[,] SimilarItems
		{
			set { m_similarItems = value; }
			get { return m_similarItems; }
		}
		public float SimilarItemsVersion
		{
			set { m_similarItemsVersion = value; }
			get { return m_similarItemsVersion; }
		}

		// Load SimilarItems ///////
		public int ReadSimilarItems()
		{
			m_warning = 0;
			// Version
			DateTime dt, dtV2;
			if (File.Exists(m_clientDataPath + SimilarItemsBasename))
				dt = File.GetLastWriteTime(m_clientDataPath + SimilarItemsBasename);
			else
				dt = new DateTime(0);
			if (File.Exists(m_clientDataPath + SimilarItemsBasenameV2))
				dtV2 = File.GetLastWriteTime(m_clientDataPath + SimilarItemsBasenameV2);
			else
				dtV2 = new DateTime(0);
			if (dt >= dtV2)
				m_similarItemsFilename = SimilarItemsBasename;
			else
				m_similarItemsFilename = SimilarItemsBasenameV2;

			// Read Recommendations Header /////
			// m_nNumItems and m_nNumSimilarItemRecs are read from the header
			m_error = InitSimilarItemsFile();
			if (m_error != 0) 
				return m_error;

			// Allocate Arrays //////
			// SimilarItems **
			m_similarItems = new Rec[m_numItems, m_numRecs];
			if (m_similarItems == null)
			{
				m_errorText = "Could not allocate memory for Similar Items.\n";
				return 1;
			}

			// Item Index **
			if (!m_itemIndex.IsAllocated)
				m_itemIndex.AllocIndex(m_numItems); // (m_maxItemID);

			// Read Similar Items ////
			if (m_similarItemsVersion > 3.05)
				m_error = FinishSimilarItemsFile(); // Reads cross sell items and closes file
			else
				m_error = FinishSimilarItemsFileOLD(); // Reads cross sell items and closes file

			// If any items are missing - should not happen 
			for (int i = 0; i < m_numItems; i++)
			{
				if (m_similarItems[i, 0].alphaID == null)
					for (int j = 0; j < NumRecs; j++)
						m_similarItems[i, j].Reset();
			}

			if (m_error != 0) 
				return 1;

			return 0;
		}

		// InitSimilarItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitSimilarItemsFile()
		{
			string temp;
			string[] tempSplit;

			// == Open Similar Item File ==
			if (m_similarItemsFilename == null)
				m_similarItemsFilename = SimilarItemsBasename;

			try
			{
				m_similarItemsFile = new StreamReader(m_clientDataPath + m_similarItemsFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_similarItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			temp = m_similarItemsFile.ReadLine();
			if (!temp.ToLower().StartsWith("version"))
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_similarItemsFilename);
				m_similarItemsFile.Close();
				return 1;
			}
			tempSplit = temp.Split(m_tabSeparator);
			m_similarItemsVersion = (float)Convert.ToDouble(tempSplit[1]);

			if (m_similarItemsVersion < 3.05)
			{
				// NumItems
				temp = m_similarItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numitems") != 0)
				{
					m_errorText = String.Format("Wrong header for NumItems in {0}.",
							m_similarItemsFilename);
					m_similarItemsFile.Close();
					return 1;
				}
				int numItems = Convert.ToInt32(tempSplit[1]);
				if (m_numItems > -1)
				{
					if (m_numItems != numItems)
					{
						m_errorText = String.Format("{0} items in {1}, but {2} items in other files.\n",
								numItems, m_similarItemsFilename, m_numItems);
						m_similarItemsFile.Close();
						return 1;
					}
				}
				else
					m_numItems = numItems;

				// MaxItemID
				temp = m_similarItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxitemid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxItemID in {0}.",
							m_similarItemsFilename);
					m_similarItemsFile.Close();
					return 1;
				}
				int maxItemID = Convert.ToInt32(tempSplit[1]);
				if (m_maxItemID > -1)
				{
					if (m_maxItemID != maxItemID)
					{
						m_errorText = String.Format("{0} max item IDs in {1}, but {2} item IDs in other files.\n",
								maxItemID, m_similarItemsFilename, m_maxItemID);
						m_similarItemsFile.Close();
						return 1;
					}
				}
				else
					m_maxItemID = maxItemID;

				// NumRecs
				temp = m_similarItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_similarItemsFilename);
					m_similarItemsFile.Close();
					return 1;
				}
				m_numRecs = Convert.ToInt32(tempSplit[1]);
			} // m_similarItemsVersion < 3.05

			// Read subsection header line with ItemID, SimilarItemID, Likelihood etc.
			temp = m_similarItemsFile.ReadLine();

			return 0;
		}

		// FinishSimilarItemsFile ///////////// 
		// Reads Similar Items and closes file!
		protected int FinishSimilarItemsFile()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string itemID;
			int itemIndex;
			bool isNew = false;

			// == Read Similar Items ==
			for (i = 0; i < m_numItems; i++)
			{
				temp = m_similarItemsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation - still works even with no data
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of Similar Items {0}. {1} items in Similar, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_similarItemsFilename, i, m_numItems);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				// Read ItemID
				itemID = tempSplit[0];
					
				// Create Index
				itemIndex = m_itemIndex.Index(itemID, ref isNew, 1);
				if (itemIndex < 0) //skip bad indices (could be caused by blank ID)
					continue;

				for (j = 0; j < m_numRecs; j++)
				{
					m_similarItems[itemIndex, j].alphaID = tempSplit[4 * j + 1];
					m_similarItems[itemIndex, j].likelihood = (float) Convert.ToDouble(tempSplit[4 * j + 2]);
					m_similarItems[itemIndex, j].numCommon = Convert.ToInt32(tempSplit[4 * j + 3]);
					m_similarItems[itemIndex, j].type = (byte) Convert.ToInt32(tempSplit[4 * j + 4]);
				} // for j nNumSimilarItemRecs
			} // for i m_nNumItems

			// == Close File ==
			m_similarItemsFile.Close();

			return 0;
		}
		protected int FinishSimilarItemsFileOLD()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string itemID;
			int itemIndex;
			bool isNew = false;
			byte trainingType;

			// == Read Similar Items ==
			for (i = 0; i < m_numItems; i++)
			{
				temp = m_similarItemsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Similar Items {0}.\n", m_similarItemsFilename);
					m_similarItemsFile.Close();
					return 1;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				// Read ItemID
				itemID = tempSplit[0];

				// Create Index
				itemIndex = m_itemIndex.Index(itemID, ref isNew, 1);
				if (itemIndex < 0) //skip bad indices (could be caused by blank ID)
					continue;

				// Read NumActions
				m_itemDetails[itemIndex].numActions = Convert.ToInt32(tempSplit[1]);

				for (j = 0; j < m_numRecs; j++)
				{
					m_similarItems[itemIndex, j].alphaID = tempSplit[3 * j + 2];
					m_similarItems[itemIndex, j].likelihood = (float)Convert.ToDouble(tempSplit[3 * j + 3]);
					trainingType = Convert.ToByte(tempSplit[3 * j + 4]);
					m_similarItems[itemIndex, j].type = trainingType;
				} // for j nNumSimilarItemRecs
			} // for i m_nNumItems

			// == Close File ==
			m_similarItemsFile.Close();

			return 0;
		}
		#endregion // SimilarItems

		#region CrossSellItems
		//////////////////////////////////////////////////////////////////
		// CrossSellItems ////////////////////////////////////////////////
		// CrossSell Members and Properties **
		StreamReader m_crossSellItemsFile;
		string m_crossSellItemsFilename = null;
		Rec[,] m_crossSellItems = null;
		float m_crossSellItemsVersion;
		int m_numHeadersPerRec = 4;

		public string CrossSellItemsFilename
		{
			set { m_crossSellItemsFilename = value; }
			get { return m_crossSellItemsFilename; }
		}
		public Rec[,] CrossSellItems
		{
			set { m_crossSellItems = value; }
			get { return m_crossSellItems; }
		}
		public float CrossSellItemsVersion
		{
			set { m_crossSellItemsVersion = value; }
			get { return m_crossSellItemsVersion; }
		}

		
		// Load Cross Sell Items ///////
		public int ReadCrossSellItems()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumItems and m_nNumCrossSellItemRecs are read from the header
			m_error = InitCrossSellItemsFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// CrossSellItems **
			m_crossSellItems = new Rec[m_numItems, m_numRecs];
			if (m_crossSellItems == null)
			{
				m_errorText = "Could not allocate memory for Cross Sell Items.\n";
				return 1;
			}

			// Item Index **
			if (!m_itemIndex.IsAllocated)
				m_itemIndex.AllocIndex(m_numItems); // (m_maxItemID);

			// Read Cross Sell Items ////
			m_error = FinishCrossSellItemsFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			// If any items are missing - should not happen 
			for (int i = 0; i < m_numItems; i++)
			{
				if (m_crossSellItems[i, 0].alphaID == null)
					for (int j = 0; j < NumRecs; j++)
						m_crossSellItems[i, j].Reset();
			}

			return 0;
		}

		// InitCrossSellItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitCrossSellItemsFile()
		{
			string temp;
			string[] tempSplit;

			// == Open Cross Sell Item File ==
			if (m_crossSellItemsFilename == null)
				m_crossSellItemsFilename = CrossSellItemsBasename;
			try
			{
				m_crossSellItemsFile = new StreamReader(m_clientDataPath + m_crossSellItemsFilename);
			}
			catch(Exception)
			{
				m_errorText = "Error: Could not open file " + m_crossSellItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			temp = m_crossSellItemsFile.ReadLine();
			if ( !temp.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_crossSellItemsFilename);
				m_crossSellItemsFile.Close();
				return 1;
			}
			tempSplit = temp.Split(m_tabSeparator);
			m_crossSellItemsVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (m_crossSellItemsVersion < 3.05)
			{
				// NumItems
				temp = m_crossSellItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numitems") != 0)
				{
					m_errorText = String.Format("Wrong header for NumItems in {0}.",
							m_crossSellItemsFilename);
					m_crossSellItemsFile.Close();
					return 1;
				}
				int numItems = Convert.ToInt32(tempSplit[1]);
				if (m_numItems > -1)
				{
					if (m_numItems != numItems)
					{
						m_errorText = String.Format("{0} items in {1}, but {2} items in the other files.\n",
								numItems, m_crossSellItemsFilename, m_numItems);
						m_crossSellItemsFile.Close();
						return 1;
					}
				}
				else
					m_numItems = numItems;

				// MaxItemID
				temp = m_crossSellItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxitemid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxItemID in {0}.",
							m_crossSellItemsFilename);
					m_crossSellItemsFile.Close();
					return 1;
				}
				int maxItemID = Convert.ToInt32(tempSplit[1]);
				if (m_maxItemID > -1)
				{
					if (m_maxItemID != maxItemID)
					{
						m_errorText = String.Format("{0} item IDs in {1}, but {2} item IDs in the other files.\n",
								maxItemID, m_crossSellItemsFilename, m_maxItemID);
						m_crossSellItemsFile.Close();
						return 1;
					}
				}
				else
					m_maxItemID = maxItemID;

				// NumCrossSellItemRecs
				temp = m_crossSellItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_crossSellItemsFilename);
					m_crossSellItemsFile.Close();
					return 1;
				}
				m_numRecs = Convert.ToInt32(tempSplit[1]);
			} // if m_crossSellItemsVersion < 3.05

			// Read subsection header line with ItemID, CrossSellItemID, Likelihood etc.
			temp = m_crossSellItemsFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			// Means that file includes Type
			if (tempSplit.Length > 70)
				m_numHeadersPerRec = 4;
			else
				m_numHeadersPerRec = 3;

			return 0;
		}

		// FinishCrossSellItemsFile ///////////// 
		// Reads Cross Sell Items and closes file!
		protected int FinishCrossSellItemsFile()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string itemID;
			int itemIndex;
			bool isNew = true;
			int pos;

			// == Read CrossSellItems ==
			for (i = 0; i < m_numItems; i++)
			{
				// Read Line - includes ItemID and recommendations **
				temp = m_crossSellItemsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation - still works even if no data
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of CrossSell Items {0}. {1} items in CrossSell, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_crossSellItemsFilename, i, m_numItems);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);

				// Read ItemID
				itemID = tempSplit[0];
				pos = 1;
				// Create Index
				itemIndex = m_itemIndex.Index(itemID, ref isNew, 1);
				if (itemIndex < 0) // skip bad indices (could be caused by blank ID)
					continue;

				// Read NumActions
				if (m_crossSellItemsVersion < 3.05)
				{
					pos = 2;
					m_itemDetails[itemIndex].numActions = Convert.ToInt32(tempSplit[1]);
				}

				// Read recs
				 // Factor of 3 since 3 items per rec
				for (j = 0; j < m_numRecs; j++)
				{
					m_crossSellItems[itemIndex, j].alphaID = tempSplit[m_numHeadersPerRec * j + pos];
					m_crossSellItems[itemIndex, j].likelihood = Convert.ToSingle(tempSplit[m_numHeadersPerRec * j + pos + 1]);
					m_crossSellItems[itemIndex, j].numCommon = Convert.ToInt32(tempSplit[m_numHeadersPerRec * j + pos + 2]);
					if (m_numHeadersPerRec == 4) // Means includes type - can be removed later
						m_crossSellItems[itemIndex, j].type = Convert.ToByte(tempSplit[m_numHeadersPerRec * j + pos + 3]);
					else
					{
						if (m_crossSellItems[itemIndex, j].numCommon > 0)
							m_crossSellItems[itemIndex, j].type = (byte)RecType.CrossSellAction2Action;
						else
							m_crossSellItems[itemIndex, j].type = (byte)RecType.CrossSellGenomic;
					} // type not included
				} // for j nNumCrossSellItemRecs
			} // for i m_nNumItems

			// == Close File ==
			m_crossSellItemsFile.Close();

			return 0;
		}
		#endregion

		#region CrossSellAttItems
		//////////////////////////////////////////////////////////////////
		// CrossSellAttItems ////////////////////////////////////////////////
		// CrossSell Members and Properties **
		StreamReader m_crossSellAttItemsFile;
		string m_crossSellAttItemsFilename = null;
		Rec[,] m_crossSellAttItems = null;

		public string CrossSellAttItemsFilename
		{
			set { m_crossSellAttItemsFilename = value; }
			get { return m_crossSellAttItemsFilename; }
		}
		public Rec[,] CrossSellAttItems
		{
			set { m_crossSellAttItems = value; }
			get { return m_crossSellAttItems; }
		}

		// Load Cross Sell Items ///////
		// DEPRICATED 12-2011. 
		 // Genomic CrossSell included in CrossSellItems. CrossSellAttItems does not exist!!!
		public int ReadCrossSellAttItems()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumItems and m_nNumCrossSellItemRecs are read from the header
			m_error = InitCrossSellAttItemsFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// CrossSellAttItems **
			m_crossSellAttItems = new Rec[m_numItems, m_numRecs];
			if (m_crossSellAttItems == null)
			{
				m_errorText = "Could not allocate memory for Cross Sell Attribute Items.\n";
				return 1;
			}

			// Read Cross Sell Items ////
			m_error = FinishCrossSellAttItemsFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitCrossSellAttItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitCrossSellAttItemsFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Cross Sell Item File ==
			if (m_crossSellAttItemsFilename == null)
				m_crossSellAttItemsFilename = CrossSellAttItemsBasename;

			try
			{
				m_crossSellAttItemsFile = new StreamReader(m_clientDataPath + m_crossSellAttItemsFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_crossSellAttItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_crossSellAttItemsFile.ReadLine();
			if ( !version.ToLower().StartsWith("version") &&
					 !version.ToLower().Contains("crosssellatt") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_crossSellAttItemsFilename);
				m_crossSellAttItemsFile.Close();
				return 1;
			}

			// NumItems
			temp = m_crossSellAttItemsFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numitems") != 0)
			{
				m_errorText = String.Format("Wrong header for NumItems in {0}.",
						m_crossSellAttItemsFilename);
				m_crossSellAttItemsFile.Close();
				return 1;
			}
			int numItems = Convert.ToInt32(tempSplit[1]);
			if (m_numItems > -1)
			{
				if (m_numItems != numItems)
				{
					m_errorText = String.Format("Error: Number of items ({0}) in {1} does not match num items ({2}) in the other files.",
							numItems, m_crossSellAttItemsFilename, m_numItems);
					m_crossSellAttItemsFile.Close();
					return 1;
				}
			}
			else
				m_numItems = numItems;

			// MaxItemID
			temp = m_crossSellAttItemsFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("maxitemid") != 0)
			{
				m_errorText = String.Format("Wrong header for MaxItemID in {0}.",
						m_crossSellAttItemsFilename);
				m_crossSellAttItemsFile.Close();
				return 1;
			}
			int maxItemID = Convert.ToInt32(tempSplit[1]);
			if (maxItemID > -1)
			{
				if (m_maxItemID != maxItemID)
				{
					m_errorText = String.Format("Error: Max item ID ({0}) in {1} does not match max item ID ({2}) in the other files.",
							maxItemID, m_crossSellAttItemsFilename, m_maxItemID);
					m_crossSellAttItemsFile.Close();
					return 1;
				}
			}
			else
				m_maxItemID = maxItemID;

			// NumCrossSellItemRecs
			temp = m_crossSellAttItemsFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
			{
				m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
						m_crossSellAttItemsFilename);
				m_crossSellAttItemsFile.Close();
				return 1;
			}
			int numRecs = Convert.ToInt32(tempSplit[1]);
			if (m_numRecs != numRecs)
			{
				m_errorText = String.Format("Error: Number of recs ({0}) in {1} does not match num recs ({2}) in {3}.",
						numRecs, m_crossSellAttItemsFilename, m_numRecs, m_crossSellItemsFilename);
				m_crossSellAttItemsFile.Close();
				return 1;
			}

			// Read subsection header line with ItemID, CrossSellItemID, Likelihood etc.
			temp = m_crossSellAttItemsFile.ReadLine();

			return 0;
		}

		// FinishCrossSellAttItemsFile ///////////// 
		// Reads Cross Sell Items and closes file!
		protected int FinishCrossSellAttItemsFile()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string itemID;

			// == Read CrossSellAttItems ==
			for (i = 0; i < m_numItems; i++)
			{
				// Read Line - includes ItemID and recommendations **
				temp = m_crossSellAttItemsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Cross Sell Categorical Items {0}.\n", m_crossSellAttItemsFilename);
					m_crossSellAttItemsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				// Read ItemID
				itemID = tempSplit[0];

				// Read recs
				// Factor of 3 since 3 items per rec
				for (j = 0; j < m_numRecs; j++)
				{
					m_crossSellAttItems[i, j].alphaID = tempSplit[2 * j + 1];
					m_crossSellAttItems[i, j].likelihood = Convert.ToSingle(tempSplit[2 * j + 2]);
					m_crossSellAttItems[i, j].numCommon = 0; // 0
					m_crossSellAttItems[i, j].type = (byte) RecType.CrossSellGenomic;
				} // for j nNumCrossSellItemRecs
			} // for i m_nNumItems

			// == Close File ==
			m_crossSellAttItemsFile.Close();

			return 0;
		}
		#endregion CrossSellAttItems

		#region ItemDetails
		//////////////////////////////////////////////////////////////////
		// ItemDetails ////////////////////////////////////////////////
		// ItemDetails Members **
		int m_lineNumItemDetails;
		StreamReader m_itemDetailsFile = null;
		string m_itemDetailsFilename = null;
		ItemDetail[] m_itemDetails = null;

		// Properties
		public string ItemDetailsFilename
		{
			set { m_itemDetailsFilename = value; }
			get { return m_itemDetailsFilename; }
		}
		public ItemDetail[] ItemDetails // Item information
		{
			set { m_itemDetails = value; }
			get { return m_itemDetails; }
		}

		// Version
    double m_itemDetailsVersion = 0;
    public double ItemDetailsVersion
    {
        get { return m_itemDetailsVersion; }
    }

		public int GetAtt1IndexFromItem(int itemIndex)
		{
			if ((itemIndex < 0) || (itemIndex > m_itemDetails.Length))
			{
				m_errorText = "GetAtt1IndexFromItem: Invalid item index";
				return -1;
			}
			string att1ID = m_itemDetails[itemIndex].att1IDs[0];
			bool isNew = false;
			int att1Index = m_att1Index.Index(att1ID, ref isNew, 1);
			if (isNew || (att1Index < 0))
			{
				m_errorText = "GetAtt1IndexFromItem: Att1 not found";
				return -1;
			}
			return att1Index;
		}

		public int GetAtt2IndexFromItem(int itemIndex)
		{
			if ((itemIndex < 0) || (itemIndex > m_itemDetails.Length))
			{
				m_errorText = "GetAtt2IndexFromItem: Invalid item index";
				return -1;
			}
			string att2ID = m_itemDetails[itemIndex].att2ID;
			bool isNew = false;
			int att2Index = m_att2Index.Index(att2ID, ref isNew, 1);
			if (isNew || (att2Index < 0))
			{
				m_errorText = "GetAtt2IndexFromItem: Att2 not found";
				return -1;
			}
			return att2Index;
		}

		// ItemDetails ////////////
		public int ReadItemDetails()
		{
			m_warning = 0;
			// Init to set m_numItems
			m_error = InitItemDetails();
			if (m_error != 0)
				return m_error;

			// ItemDetails =====================================
			// ItemActions **
			m_itemDetails = new ItemDetail[m_numItems];
			if (m_itemDetails == null)
			{
				m_errorText = "Could not allocate memory for Item Details.\n";
				return 1;
			}

			// Item Index **
			if (!m_itemIndex.IsAllocated)
				m_itemIndex.AllocIndex(m_numItems); //();
			// Att1 Index **
			if (!m_att1Index.IsAllocated)
				m_att1Index.AllocIndex(m_numAtt1s); ;
			// Att2 Index **
			if (!m_att2Index.IsAllocated)
				m_att2Index.AllocIndex(m_numAtt2s);
 
			// ReadItemDetails //////
			if (m_itemDetailsVersion >= 2)
				m_error = ReadItemDetailsFile();
			else 
				m_error = ReadItemDetailsFileV1();
			if (m_error != 0)
				return m_error;

			// If any items are missing - should not happen 
			for (int i = 0; i < m_numItems; i++)
			{
				if (m_itemDetails[i].itemID == null)
					m_itemDetails[i].Reset();
			}

			return 0;
		}

		protected int InitItemDetails()
		{
			string temp;
			string[] tempSplit;

			// Open file
			if (m_itemDetailsFilename == null)
				m_itemDetailsFilename = ItemDetailsBasename;

			try
			{
				m_itemDetailsFile = new StreamReader(m_clientDataPath + m_itemDetailsFilename);
			}
			catch (Exception)
			{
				m_errorText = String.Format("\nError: Could not open item details file {0}.\n\n", m_itemDetailsFilename);
				return 2;
			}

			// Check Version //
			temp = m_itemDetailsFile.ReadLine();
			if (temp == null)
			{
				m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
				m_itemDetailsFile.Close();
				return 1;
			} // EOF
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower() != "version")
			{
				m_itemDetailsVersion = 1.1;
				m_itemDetailsFile.Close();
				return 0;
			}
			m_itemDetailsVersion = Convert.ToDouble(tempSplit[1]);

			// Read Header //
			if (m_itemDetailsVersion < 3.05)
			{
				// Sales Export Date
				m_lineNumItemDetails = 2;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower() != "sales export date")
				{
					m_errorText = String.Format("Line {0} of {1} should be 'Sales Export Date', but is {2}.",
						m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
					m_itemDetailsFile.Close();
					return 1;
				}
				m_salesExportDate = tempSplit[1];

				// Catalog Export Date
				m_lineNumItemDetails++;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower() != "catalog export date")
				{
					m_errorText = String.Format("Line {0} of {1} should be 'Catalog Export Date', but is {2}.",
						m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
					m_itemDetailsFile.Close();
					return 1;
				}
				m_catalogExportDate = tempSplit[1];

				// DoNotRecommendExists Export Date
				m_lineNumItemDetails++;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower() != "donotrecommend export date")
				{
					m_errorText = String.Format("Line {0} of {1} should be 'DoNotRecommend Export Date', but is {2}.",
						m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
					m_itemDetailsFile.Close();
					return 1;
				}
				m_doNotRecommendExportDate = tempSplit[1];

				// ReplacementsSale Export Date
				m_lineNumItemDetails++;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower() != "replacements export date")
				{
					m_errorText = String.Format("Line {0} of {1} should be 'Replacements Export Date', but is {2}.",
						m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
					m_itemDetailsFile.Close();
					return 1;
				}
				m_replacementsExportDate = tempSplit[1];

				// Attribute1 Exists
				m_lineNumItemDetails++;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower() != "attribute1 exists")
				{
					m_errorText = String.Format("Line {0} of {1} should be 'Attribute1 Exists', but is {2}.",
						m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
					m_itemDetailsFile.Close();
					return 1;
				}
				if (tempSplit[1] == "1")
					m_att1Exists = true;
				else
					m_att1Exists = false;

				// Attribute2 Exists
				m_lineNumItemDetails++;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower() != "attribute2 exists")
				{
					m_errorText = String.Format("Line {0} of {1} should be 'Attribute2 Exists', but is {2}.",
						m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
					m_itemDetailsFile.Close();
					return 1;
				}
				if (tempSplit[1] == "1")
					m_att2Exists = true;
				else
					m_att2Exists = false;

				// Filter1 Exists
				if (m_itemDetailsVersion >= 2.9)
				{
					m_lineNumItemDetails++;
					temp = m_itemDetailsFile.ReadLine();
					if (temp == null)
					{
						m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
						m_itemDetailsFile.Close();
						return 1;
					} // EOF
					tempSplit = temp.Split(m_tabSeparator);
					if (tempSplit[0].ToLower() != "filter1 exists")
					{
						m_errorText = String.Format("Line {0} of {1} should be 'Filter1 Exists', but is {2}.",
							m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
						m_itemDetailsFile.Close();
						return 1;
					}
					if (tempSplit[1] == "1")
						m_filter1Exists = true;
					else
						m_filter1Exists = false;
				}

				// NumItems
				m_lineNumItemDetails++;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower() != "numitems")
				{
					m_errorText = String.Format("Line {0} of {1} should be NumItems, but is {2}.",
						m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
					m_itemDetailsFile.Close();
					return 1;
				}
				int numItems = Convert.ToInt32(tempSplit[1]);
				if (m_numItems > -1)
				{
					if (m_numItems != numItems)
					{
						m_errorText = String.Format("{0} items in {1}, but {2} items in the other files.\n",
								numItems, m_itemDetailsFilename, m_numItems);
						m_itemDetailsFile.Close();
						return 1;
					}
				}
				else
					m_numItems = numItems;

				// NumSales
				m_lineNumItemDetails++;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower() != "numsales")
				{
					m_errorText = String.Format("Line {0} of {1} should be NumSales, but is {2}.",
						m_lineNumItemDetails, m_itemDetailsFilename, tempSplit[0]);
					m_itemDetailsFile.Close();
					return 1;
				}
				m_numSales = Convert.ToInt32(tempSplit[1]);
			} // Version < 3.05

			// HeaderLine
			if (m_itemDetailsVersion >= 2.9)
			{
				m_lineNumItemDetails++;
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
					m_itemDetailsFile.Close();
					return 1;
				} // EOF
			}

			return 0;
		}

		protected int ReadItemDetailsFile()
		{
			int i, j;
			int itemIndex;
			string itemID;
			int numAtt1IDs;
			int numFilter1IDs;
			string temp;
			string[] tempSplit;
			bool isNew = true;
			int numElements;
			int pos;

			// Read Body //
			// Read in items to array
			for (i = 0; i < m_numItems; i++)
			{
				m_lineNumItemDetails++;

				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of ItemDetails {0}. {1} items in ItemDetails, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_itemDetailsFilename, i, m_numItems);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				numElements = tempSplit.Length;
				itemID = tempSplit[0];
				itemIndex = m_itemIndex.Index(itemID, ref isNew, 1);
				// Skip items that have an invalid ID, such as ""
				if (itemIndex < 0)
					continue;

				m_itemDetails[itemIndex].Reset();

				// ItemID
				m_itemDetails[itemIndex].itemID = itemID;
				// NumAtt1IDs
				if (m_att1Exists && numElements <= 1)
				{
					m_error = 1;
					m_errorText = String.Format("There is no number of attribute 1 on line {0} of {1}.\n",
						m_lineNumItemDetails, m_itemDetailsFilename);
					return m_error;
				}
				if (numElements > 1) // may only have productOnly with IDs and no names
					numAtt1IDs = Convert.ToInt32(tempSplit[1]);
				else
					numAtt1IDs = 0;
				m_itemDetails[itemIndex].numAtt1IDs = numAtt1IDs;
				m_itemDetails[itemIndex].att1IDs = new string[numAtt1IDs];
				// Att1IDs
				for (j = 0; j < numAtt1IDs; j++)
				{
					if (m_att1Exists && numElements <= j+2)
					{
						m_error = 1;
						m_errorText = String.Format("There are not enough attribute 1's on line {0} of {1}.\n", 
							m_lineNumItemDetails, m_itemDetailsFilename);
						return m_error;
					}

					if (numElements > j + 2)
					{
						string id = tempSplit[j + 2];
						m_itemDetails[itemIndex].att1IDs[j] = id;
						m_att1Index.Index(id, ref isNew, 0);
					}
				}
				// Att2ID
				pos = numAtt1IDs + 2;
				if (m_att2Exists && numElements <= pos)
				{
					m_error = 1;
					m_errorText = String.Format("There is no attribute 2 on line {0} of {1}.\n",
						m_lineNumItemDetails, m_itemDetailsFilename);
					return m_error;
				}
				if (numElements > pos)
				{
					string id = tempSplit[pos];
					m_itemDetails[itemIndex].att2ID = id;
					m_att2Index.Index(id, ref isNew, 0);
				}
				// Name - required so make entry
				pos++; // = numAtt1IDs + 3;
				if (numElements > pos)
					m_itemDetails[itemIndex].name = tempSplit[pos];
				else
					m_itemDetails[itemIndex].name = itemID;
				// Price
				pos++; // = numAtt1IDs + 4;
				if (numElements > pos)
					if (tempSplit[pos] != "")
						m_itemDetails[itemIndex].price = Convert.ToInt32(Convert.ToDouble(tempSplit[pos])*100.0F + 0.1F);
				if (m_itemDetailsVersion >= 2.9)
				{
					// SalePrice
					pos++; // = numAtt1IDs + 5;
					if (numElements > pos)
						if (tempSplit[pos] != "")
							m_itemDetails[itemIndex].salePrice = Convert.ToInt32(Convert.ToDouble(tempSplit[pos])*100.0F + 0.1F);
					// NumFilter1IDs
					pos++; // numAtt1IDs + 6;
					if (m_filter1Exists && numElements <= pos)
					{
						m_error = 1;
						m_errorText = String.Format("There is no number of filter 1 on line {0} of {1}.\n",
							m_lineNumItemDetails, m_itemDetailsFilename);
						return m_error;
					}
					if (numElements > pos) // may only have productOnly with IDs and no names
						numFilter1IDs = Convert.ToInt32(tempSplit[pos]);
					else
						numFilter1IDs = 0;
					m_itemDetails[itemIndex].numFilter1IDs = numFilter1IDs;
					m_itemDetails[itemIndex].filter1IDs = new string[numFilter1IDs];
					// Filter1IDs
					for (j = 0; j < numFilter1IDs; j++)
					{
						if (m_filter1Exists && numElements <= j + 1 + pos)
						{
							m_error = 1;
							m_errorText = String.Format("There are not enough filter 1's on line {0} of {1}.\n",
								m_lineNumItemDetails, m_itemDetailsFilename);
							return m_error;
						}

						if (numElements > j + 1 + pos)
							m_itemDetails[itemIndex].filter1IDs[j] = tempSplit[j + 1 + pos];
					}
				} // if m_itemDetailsVersion >= 2.9
				else
					numFilter1IDs = 1; // and skip it for v2 and below (+1 is for old FilterID)
				// Link
				 // +2 since v2 had an element for Filter1, but did not use it
				pos += numFilter1IDs + 1; // = numAtt1IDs + numFilter1IDs + 7;
				if (numElements > pos)
					m_itemDetails[itemIndex].link = tempSplit[pos];
				// ImageLink
				pos++; // = numAtt1IDs + numFilter1IDs + 8;
				if (numElements > pos)
					m_itemDetails[itemIndex].imageLink = tempSplit[pos];
				// Rating
				if (m_itemDetailsVersion >= 2.9)
				{
					pos++; // = numAtt1IDs + numFilter1IDs + 9;
					if (numElements > pos)
						if (tempSplit[pos] != "")
							m_itemDetails[itemIndex].rating = tempSplit[pos];
				}
				// StandardCode
				pos++; // = numAtt1IDs + numFilter1IDs + 10;
				if (numElements > pos)
					m_itemDetails[itemIndex].standardCode = tempSplit[pos];

				if (m_itemDetailsVersion > 3.05)
				{
					// NumActions
					pos++; // = numAtt1IDs + numFilter1IDs + 10;
					if (numElements > pos)
					{
						m_itemDetails[itemIndex].numActions = Convert.ToInt32(tempSplit[pos]);
					}
					// NumViews
					pos++; // = numAtt1IDs + numFilter1IDs + 10;
					if (numElements > pos)
						m_itemDetails[itemIndex].numClicks = Convert.ToInt32(tempSplit[pos]);
				} // Version > 3.05 (i.e 3.1 or newer without troubles with rounding errors)

			} // for i 0 to m_numItems

			m_itemDetailsFile.Close();
			return 0;
		}

    protected int ReadItemDetailsFileV1()
    {
		int i, j;
		int itemIndex;
		string itemID;
		int numAtt1IDs;
		string temp;
		string[] tempSplit;

		// Open file
		try
		{
			m_itemDetailsFile = new StreamReader(m_clientDataPath + m_itemDetailsFilename);
		}
		catch (Exception)
		{
			m_errorText = String.Format("\nError: Could not open item details file {0}.\n\n", m_itemDetailsFilename);
			return 2;
		}

		// Read in items to array
		for (i = 0; i < m_numItems; i++)
		{
				temp = m_itemDetailsFile.ReadLine();
				if (temp == null)
				{
				  m_errorText = String.Format("Reached EOF of Item Name {0}.\n", m_itemDetailsFilename);
				  m_itemDetailsFile.Close();
				  return 1;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				itemID = tempSplit[0];
				itemIndex = m_itemIndex.GetIndex(itemID);
				// Skip items that have an invalid ID
				if (itemIndex < 0)
					continue;

				m_itemDetails[itemIndex].att2ID = tempSplit[1];
				numAtt1IDs = Convert.ToInt32(tempSplit[2]);
				m_itemDetails[itemIndex].numAtt1IDs = numAtt1IDs;
				m_itemDetails[itemIndex].att1IDs = new string[numAtt1IDs];
				for (j = 0; j < numAtt1IDs; j++)
					m_itemDetails[itemIndex].att1IDs[j] = tempSplit[j + 3];
				m_itemDetails[itemIndex].name = tempSplit[j + 3];
			} // for i 0 to m_numItems

			m_itemDetailsFile.Close();
			return 0;
		}

		public RecDisplayItem GetDisplayItem(string alphaID)
		{
			RecDisplayItem item = new RecDisplayItem();
			item.productID = null; //indicates failure response
			item.title = "";
			item.price = "";
			item.salePrice = "";
			item.rating = "";
			item.pageLink = "";
			item.imageLink = "";

			int itemIndex = m_itemIndex.GetIndex(alphaID);
			if (itemIndex < 0)
			{
				m_errorText = "Item details not found for " + alphaID;
				return item;
			}
			item.productID = alphaID;
			item.title = m_itemDetails[itemIndex].name;
			float price = (float)(m_itemDetails[itemIndex].price / 100f);
			item.price = m_currency + price.ToString("N2"); //TODO: currency formatting should be left to the client
			float salePrice = (float)(m_itemDetails[itemIndex].salePrice / 100f);
			if (salePrice > 0 && salePrice < price)
				item.salePrice = m_currency + salePrice.ToString("N2"); //TODO: currency formatting should be left to the client
			item.rating = m_itemDetails[itemIndex].rating;
			//TODO: Update all site javascript to allow this value to be "NoEntry"
			if (item.rating.Equals("noentry", StringComparison.CurrentCultureIgnoreCase))
				item.rating = "-1"; 
			item.pageLink = m_itemDetails[itemIndex].link;
			item.imageLink = m_itemDetails[itemIndex].imageLink;

			return item;
		}

		public DashProductRec GetDashProductRec(string alphaID)
		{
			DashProductRec item = new DashProductRec();
			item.id = null; //indicates failure response
			item.name = "";
			item.att1IDs = "";
			item.att2ID = "";
			item.filters = "";
			item.likelihood = "";
			item.commonSales = "";
			item.totalSales = "";
			item.totalViews = "";
			item.source = "";

			int itemIndex = m_itemIndex.GetIndex(alphaID);
			if (itemIndex < 0)
			{
				m_errorText = "Item details not found for " + alphaID;
				return item;
			}
			item.id = alphaID;
			item.name = m_itemDetails[itemIndex].name;
			item.att1IDs = GetItemAttNames(1, itemIndex);
			item.att2ID = GetItemAttNames(2, itemIndex);
			item.filters = GetFilterNames(itemIndex);

			item.totalSales = m_itemDetails[itemIndex].numActions.ToString();
			item.totalViews = m_itemDetails[itemIndex].numClicks.ToString();

			return item;
		}

		private string GetFilterNames(int index)
		{
			if ((index < 0) || (index > m_itemDetails.Length)
					|| (m_itemDetails[index].filter1IDs == null)) 
				return "";

			string filters = "";
			bool first = true;
			foreach (string id in m_itemDetails[index].filter1IDs)
			{
				if (first) first = false;
				else filters += ",";
				filters += id;
			}
			return filters;
		}

		private string GetItemAttNames(int attNumber, int index)
		{
			string attNames = "";
			if (attNumber == 1) //att1
			{
				if (!Att1Exists) return "";

				bool first = true;
				foreach (string id in m_itemDetails[index].att1IDs)
				{
					if (first) first = false;
					else attNames += ",";
					if ((m_att1Index != null) && (m_att1Names != null))
					{
						int att1Index = m_att1Index.GetIndex(id);
						if (att1Index > -1)
							attNames += m_att1Names[att1Index];
						else
							attNames += id;
					}
					else
						attNames += id;
				}
			}
			else //att2
			{
				if (!Att2Exists) return "";

				if ((m_att2Index != null) && (m_att2Names != null))
				{
					int att2Index = m_att2Index.GetIndex(m_itemDetails[index].att2ID);
					if (att2Index > -1)
						attNames = m_att2Names[att2Index];
					else
						attNames = m_itemDetails[index].att2ID;
				}
				else
					attNames = m_itemDetails[index].att2ID;
			}

			return attNames;
		}

		public List<DashCatalogItem> GetDashCatalog(int numResults, int startPosition)
		{
			//note: startPosition is 1-based but array is zero-based
			if (startPosition > 0) startPosition--; //decrement so zero-based
			int maxItem = m_itemDetails.Length;
			if (startPosition >= maxItem) return null; //no records to return
			if (startPosition + numResults > maxItem)
				numResults = maxItem - startPosition;

			List<DashCatalogItem> result = new List<DashCatalogItem>();
			for (int i = startPosition; i < startPosition + numResults; i++)
			{
				DashCatalogItem item = new DashCatalogItem();
				item.name = m_itemDetails[i].name;
				item.id = m_itemDetails[i].itemID;
				item.att1IDs = "";
				item.att1IDs = GetItemAttNames(1, i);
				item.att2ID = GetItemAttNames(2, i);
				item.totalSales = "0";
				//int itemIndex = m_itemIndex.GetIndex(m_itemDetails[i].itemID);
				//if (itemIndex >= 0)
				item.totalSales = m_itemDetails[i].numActions.ToString();
				result.Add(item);
			}
			return result;
		}

		public int GetCatalogSize()
		{
			return m_itemDetails.Length;
		}

		#endregion ItemDetails

		#region ItemNames

		public string[] GetClientAttributeNames()
		{
			string[] names = new string[2];
			names[0] = m_attribute1Name;
			names[1] = m_attribute2Name;
			return names;
		}

		// ItemNames
		string[] m_itemNames = null;
		public string[] ItemNames // Item information
		{
			set { m_itemNames = value; }
			get { return m_itemNames; }
		}
		// ItemNamesWithInfo
		public string[] m_itemNamesWithInfo;
		public string[] ItemNamesWithInfo
		{
			set { m_itemNamesWithInfo = value; }
			get { return m_itemNamesWithInfo; }
		}

		public int CreateItemNames()
		{
			int i;
			string att2Name = "";
			string att1Name = "";
			int att2Index, att1Index;

			// Create name arrays //
			if (m_numItems < 0)
			{
				m_error = 1;
				m_errorText = "There are no items for which to create names. Please contact 4-Tell.";
				return m_error;
			}

			m_itemNames = new string[m_numItems];
			if (m_itemNames == null)
			{
				m_error = 1;
				m_errorText = "There is not enough memory to create name arrays. Please free some memory and try again.";
				return m_error;
			}

			m_itemNamesWithInfo = new string[m_numItems];
			if (m_itemNamesWithInfo == null)
			{
				m_error = 1;
				m_errorText = "There is not enough memory to create name arrays. Please free some memory and try again.";
				return m_error;
			}

			// Create Names //
			for (i = 0; i < m_numItems; i++)
			{
				//itemName = m_itemDetails[i].name;
				//if (itemName.EndsWith(" [OUT]"))
				//{
				//	doNotRecommend = true;
				//	itemName = itemName.Remove(itemName.Length - 6, 6);
				//}

				if (m_itemDetails[i].itemID == "")
					continue;

				if (Att1Exists)
				{
					att1Index = m_att1Index.GetIndex(m_itemDetails[i].att1IDs[0]);
					if (att1Index > -1)
						att1Name = m_att1Names[att1Index];

					//att1Name = "";
					//for (j = 0; j < m_itemDetails[i].numAtt1IDs; j++)
					//{
					//	att1Index = m_att1Index.GetIndex(m_itemDetails[i].att1IDs[j]);
					//	att1Name += m_catNames[att1Index] + ", and ";
					//}
					//att1Name = att1Name.Remove(att1Name.Length - 6, 6);
				}

				if (Att2Exists)
				{
					att2Index = m_att2Index.GetIndex(m_itemDetails[i].att2ID);
					if (att2Index > -1)
						att2Name = m_att2Names[att2Index];
				}

				m_itemNames[i] = m_itemDetails[i].name;

				m_itemNamesWithInfo[i] = m_itemDetails[i].name;
				if (Att1Exists)
					m_itemNamesWithInfo[i] += " in " + att1Name;
				
				if (Att2Exists && Att1Exists)
					m_itemNamesWithInfo[i] += " and in " + att2Name;
				else if (Att2Exists)
					m_itemNamesWithInfo[i] += " in " + att2Name;

				if (m_doNotRecommendExists && m_doNotRecommend[i])
				{
					m_itemNames[i] += " [OUT]";
					m_itemNamesWithInfo[i] += " [OUT]";
				}
			} // for i numItems

			return 0;
		} // CreateItemNames
		#endregion ItemNames

		#region Promotions
		//////////////////////////////////////////////////////////////////
		// Promotions ////////////////////////////////////////////////
		// Promotions Members and Properties **
		StreamReader m_promotionsFile = null;
		StreamWriter m_promotionsFileOut = null;
		string m_promotionsFilename = null;
		public const int NumPromotionsIndexes = 4;
		public enum Promotions
		{
			ProductID,
			Level,
			StartDate,
			EndDate,
		};
		string[] m_promotionsRecord = null;
		List<string[]> m_promotions = null;

		// Read Promotions ////////////////////////////////////////////////
		public int ReadPromotions()
		{
			m_promotions = new List<string[]>();

			m_error = ReadPromotionsFile();
			if (m_error != 0)
				return m_error;

			return 0;
		}

		protected int ReadPromotionsFile()
		{
			int i;
			int itemIndex;
			string temp;
			string[] tempSplit;

			// Open file
			m_promotionsFilename = PromotionsBasename;
			try
			{
				m_promotionsFile = new StreamReader(m_clientDataPath + m_promotionsFilename);
			}
			catch (Exception)
			{
				m_errorText = String.Format("\nError: Could not open promotions name file {0}.\n\n", m_promotionsFilename);
				return 2;
			}

			int numHeaderLines = 2;
			for (i = 0; i < numHeaderLines; i++)
				m_promotionsFile.ReadLine();
			// In the future, may want to check for official version here

			// Read Promotions into List
			int N;
			do
			{
				temp = m_promotionsFile.ReadLine();
				if (temp == null)
					break; // EOF

				// !!! BUG - This requires a fixed order of the data !!!
				 // It should be variable based upon the header when we update the wrapper. 
				 // See Generator SetupHeaderIndex() for an example
				// Keep creating new strings, otherwise list point to same place in memory
				m_promotionsRecord = new string[NumPromotionsIndexes];
				tempSplit = temp.Split(m_tabSeparator);
				N = (tempSplit.Length >= NumPromotionsIndexes ? NumPromotionsIndexes : tempSplit.Length);
				for (i=0; i<N; i++)
					m_promotionsRecord[i] = tempSplit[i];
				for (i=N; i<NumPromotionsIndexes; i++)
					m_promotionsRecord[i] = "";

				itemIndex = m_itemIndex.GetIndex(m_promotionsRecord[0]);
				if (itemIndex < 0)
					continue;

				m_promotions.Add(m_promotionsRecord);
			} while (true);

			m_promotions.TrimExcess();
			m_promotionsFile.Close();
			return 0;
		}

		public int DestroyPromotions()
		{
			m_promotions = new List<string[]>(); // I assume this empties the promotion list
			return 0;
		}

		public int GetPromotionsLength()
		{
			return m_promotions.Count;
		}

		public string[] GetPromotionsRecord(int index)
		{
			return m_promotions[index];
		}

		// Write Promotions ////////////////////////////////////////////////
		public int OpenPromotionsFileOut()
		{
			// Open file
			m_promotionsFilename = PromotionsBasename;
			try
			{
				m_promotionsFileOut = new StreamWriter(m_clientDataPath + m_promotionsFilename);
			}
			catch (Exception)
			{
				m_errorText = String.Format("\nError: Could not open promotions name file {0}.\n\n", m_promotionsFilename);
				return 2;
			}

			// Write header
			DateTime now = DateTime.Now;
			m_promotionsFileOut.WriteLine("Version\t3.1\t{0}", now.ToString("dd-mm-yyyy"));
			m_promotionsFileOut.WriteLine("ProductID\tLevel\tStartDate\tEndDate");

			return 0;
		}

		public int WritePromotionsRecord(string[] record)
		{

			if (m_promotionsFileOut == null)
				OpenPromotionsFileOut();

			// Write Promotions Record
			m_promotionsFileOut.WriteLine("{0}\t{1}\t{2}\t{3}", record[(int) Promotions.ProductID],
					record[(int) Promotions.Level], record[(int) Promotions.StartDate], record[(int) Promotions.EndDate]);

			return 0;
		}

		public int ClosePromotionsFileOut()
		{
			m_promotionsFileOut.Close();
			return 1;
		}
		#endregion

		// User Stuff
		#region User Globals
		// UserIndex
		CIndex m_userIndex = new CIndex();
		public CIndex UserIndex
		{
			set { m_userIndex = value; }
			get { return m_userIndex; }
		}

		// NumUserActions
		int[] m_numUserActions;
		public int[] NumUserActions
		{
			set { m_numUserActions = value; }
			get { return m_numUserActions; }
		}
		#endregion User Globals

		#region PersonalizedItems
		//////////////////////////////////////////////////////////////////
		// PersonalizedItems ////////////////////////////////////////////////
		// Personalized Members and Properties **
		int m_numPersonalizedItemRecs = 0;
		StreamReader m_personalizedItemsFile;
		string m_personalizedItemsFilename = null;
		Rec[,] m_personalizedItems = null;

		public int NumPersonalizedItemRecs
		{
			set { m_numPersonalizedItemRecs = value; }
			get { return m_numPersonalizedItemRecs; }
		}
		public string PersonalizedItemsFilename
		{
			set { m_personalizedItemsFilename = value; }
			get { return m_personalizedItemsFilename; }
		}
		public Rec[,] PersonalizedItems
		{
			set { m_personalizedItems = value; }
			get { return m_personalizedItems; }
		}

		// Load PersonalizedItems ///////
		public int ReadPersonalizedItems()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumUsers and m_nNumPersonalizedItemRecs are read from the header
			m_error = InitPersonalizedItemsFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// PersonalizedItems **
			m_personalizedItems = new Rec[m_numUsers, m_numPersonalizedItemRecs];
			if (m_personalizedItems == null)
			{
				m_errorText = "Could not allocate memory for Up Sell Items.\n";
				m_personalizedItemsFile.Close();
				return 1;
			}
			// Number of actions by each user
			m_numUserActions = new int[m_numUsers];
			if (m_numUserActions == null)
			{
				m_errorText = "Could not allocate memory for Up Sell Actions.\n";
				m_personalizedItemsFile.Close();
				return 1;
			}

			// User Index **
			m_userIndex.AllocIndex(m_numUsers); // (m_maxUserID);

			// Read Cross Sell Items ////
			m_error = FinishPersonalizedItemsFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			// If any items are missing - should not happen 
			for (int i = 0; i < m_numUsers; i++)
			{
				if (m_personalizedItems[i, 0].alphaID == null)
					for (int j = 0; j < NumRecs; j++)
						m_personalizedItems[i, j].Reset();
			}

			return 0;
		}

		// InitPersonalizedItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitPersonalizedItemsFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Cross Sell Item File ==
			if (m_personalizedItemsFilename == null)
				m_personalizedItemsFilename = PersonalizedItemsBasename;

			try
			{
				m_personalizedItemsFile = new StreamReader(m_clientDataPath + m_personalizedItemsFilename);
			}
			catch(Exception)
			{
				m_errorText = "Error: Could not open file " + m_personalizedItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_personalizedItemsFile.ReadLine();
			if ( !version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_personalizedItemsFilename);
				m_personalizedItemsFile.Close();
				return 1;
			}

			m_numPersonalizedItemRecs = m_numRecs;
			tempSplit = version.Split(m_tabSeparator);
			float personalizedVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (personalizedVersion < 3.15)
			{
				// NumUsers
				temp = m_personalizedItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numusers") != 0)
				{
					m_errorText = String.Format("Wrong header for NumUsers in {0}.",
							m_personalizedItemsFilename);
					m_personalizedItemsFile.Close();
					return 1;
				}
				int numUsers = Convert.ToInt32(tempSplit[1]);
				if (m_numUsers > -1)
				{
					if (m_numUsers != numUsers)
					{
						m_warning = 1;
						m_warningText = String.Format("numUsers in SalesData ({0}) is not equal to the number of items ({1}) from the other files.\n" +
						"Please try a different file.\n", Convert.ToInt32(tempSplit[1]), m_numUsers);
						m_salesDataFile.Close();
					}
				}
				else
					m_numUsers = numUsers;

				// MaxUserID
				temp = m_personalizedItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxuserid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxUserID in {0}.",
							m_personalizedItemsFilename);
					m_personalizedItemsFile.Close();
					return 1;
				}
				m_maxUserID = Convert.ToInt32(tempSplit[1]);

				// NumPersonalizedItemRecs
				temp = m_personalizedItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_personalizedItemsFilename);
					m_personalizedItemsFile.Close();
					return 1;
				}
				m_numPersonalizedItemRecs = Convert.ToInt32(tempSplit[1]);
			} // version < 3.15

			// Read subsection header line with ItemID, PersonalizedItemID, Likelihood etc.
			temp = m_personalizedItemsFile.ReadLine();

			return 0;
		}

		// FinishPersonalizedItemsFile ///////////// 
		// Reads Cross Sell Items and closes file!
		protected int FinishPersonalizedItemsFile()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string strUserID;
			int userIndex;
			bool isNew = true;

			// == Read PersonalizedItems ==
			// Read PersonalizedItems **
			for (i = 0; i < m_numUsers; i++)
			{
				// Read Line - includes ItemID and recommendations **
				temp = m_personalizedItemsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_numUsers = i;
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of Personalized Items {0}. {1} items in Personalized, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_personalizedItemsFilename, i, m_numUsers);
					break;
				} // EOF
				
				tempSplit = temp.Split(m_tabSeparator);
				// Read ItemID
				strUserID = tempSplit[0];
				// Read NumActions
				m_numUserActions[i] = Convert.ToInt32(tempSplit[1]);
				// Create Index
				userIndex = m_userIndex.Index(strUserID, ref isNew, 1);
				if (userIndex != i)
				{
					m_warningText = "Error: User index is not in order for PersonalizedItems.\n";
					//return 1;
				}
				// Read recs
				for (j = 0; j < m_numPersonalizedItemRecs; j++)
				{
					m_personalizedItems[i, j].alphaID = tempSplit[3 * j + 2];
					m_personalizedItems[i, j].likelihood = Convert.ToSingle(tempSplit[3 * j + 3]);
					m_personalizedItems[i, j].numCommon = Convert.ToInt32(tempSplit[3 * j + 4]);
					if (m_personalizedItems[i, j].numCommon > 0)
						m_personalizedItems[i, j].type = (byte)RecType.CrossSellAction2Action;
					else
						m_personalizedItems[i, j].type = (byte)RecType.CrossSellGenomic;
				} // for j nNumPersonalizedItemRecs
			} // for i m_nNumUsers

			// == Close File ==
			m_personalizedItemsFile.Close();

			return 0;
		}
		#endregion

		#region User Names
		//////////////////////////////////////////////////////////////////
		// UserNames ////////////////////////////////////////////////
		// UserNames Properties **
		string[] m_userNames = null; // User information
		public string[] UserNames // Item information
		{
			set { m_userNames = value; }
			get { return m_userNames; }
		}

		// Called from RecommendInitCrossSellItems since m_nNumItems set there
		public int ReadUserNames()
		{
			int i;

			// UserNames =====================================
			m_userNames = new string[m_numUsers];
			if (m_userNames == null)
			{
				m_errorText = "Could not allocate memory for User Info.\n";
				return 1;
			}

			// Read User Info //////
			 // When have file with user names
			//m_nError = ReadItemNamesFile();
			//if (m_nError != 0) return 1;
			for (i = 0; i < m_numUsers; i++)
				m_userNames[i] = m_userIndex.GetID(i);

			return 0;
		}
		#endregion
		
		#region WhyItems
		//////////////////////////////////////////////////////////////////
		// WhyItems ////////////////////////////////////////////////
		// WhyItem Members and Properties **
		int m_numWhyItemRecs = 0; // Should be equal to PersonalizedItemRecs
		int m_numWhyItemsPerRec = 0; 
		StreamReader m_whyItemsFile;
		string m_whyItemsFilename = null;
		WhyRec[,,] m_whyItems = null; // Assumes 1 why Item

		public int NumWhyItemRecs
		{
			set { m_numWhyItemRecs = value; }
			get { return m_numWhyItemRecs; }
		}
		public int NumWhyItemsPerRec
		{
			set { m_numWhyItemsPerRec = value; }
			get { return m_numWhyItemsPerRec; }
		}
		public string WhyItemsFilename
		{
			set { m_whyItemsFilename = value; }
			get { return m_whyItemsFilename; }
		}
		public WhyRec[,,] WhyItems // Assumes 1 why Item
		{
			set { m_whyItems = value; }
			get { return m_whyItems; }
		}

		// Load WhyItems ///////
		public int ReadWhyItems()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumUsers and m_nNumWhyItemRecs are read from the header
			m_error = InitWhyItemsFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// WhyItems **
			m_whyItems = new WhyRec[m_numUsers, m_numWhyItemRecs, m_numWhyItemsPerRec];
			if (m_whyItems == null)
			{
				m_errorText = "Could not allocate memory for Cross Sell Items.\n";
				m_whyItemsFile.Close();
				return 1;
			}

			// User Index **
			if (!m_userIndex.IsAllocated)
				m_userIndex.AllocIndex(m_numUsers); // (m_maxUserID);

			// Read Cross Sell Items ////
			m_error = FinishWhyItemsFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitWhyItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitWhyItemsFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Cross Sell Item File ==
			if (m_whyItemsFilename == null)
				m_whyItemsFilename = WhyItemsBasename;

			try
			{
				m_whyItemsFile = new StreamReader(m_clientDataPath + m_whyItemsFilename);
			}
			catch(Exception)
			{
				m_errorText = "Error: Could not open file " + m_whyItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_whyItemsFile.ReadLine();
			if ( !version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_whyItemsFilename);
				m_whyItemsFile.Close();
				return 1;
			}

			m_numWhyItemRecs = m_numRecs;
			m_numWhyItemsPerRec = m_numWhyItems;
			tempSplit = version.Split(m_tabSeparator);
			float whyVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (whyVersion < 3.15)
			{
				// NumUsers
				temp = m_whyItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numusers") != 0)
				{
					m_errorText = String.Format("Wrong header for NumUsers in {0}.",
							m_whyItemsFilename);
					m_whyItemsFile.Close();
					return 1;
				}
				int numUsers = Convert.ToInt32(tempSplit[1]);
				if (m_numUsers > -1)
				{
					if (m_numUsers != numUsers)
					{
						m_warning = 1;
						m_warningText = String.Format("numUsers in SalesData ({0}) is not equal to the number of items ({1}) from the other files.\n" +
						"Please try a different file.\n", Convert.ToInt32(tempSplit[1]), m_numUsers);
						m_salesDataFile.Close();
					}
				}
				else
					m_numUsers = numUsers;

				// MaxUserID
				temp = m_whyItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxuserid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxUserID in {0}.",
							m_whyItemsFilename);
					m_whyItemsFile.Close();
					return 1;
				}
				m_maxUserID = Convert.ToInt32(tempSplit[1]);

				// NumWhyItemRecs
				temp = m_whyItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_whyItemsFilename);
					m_whyItemsFile.Close();
					return 1;
				}
				m_numWhyItemRecs = Convert.ToInt32(tempSplit[1]);

				// WhyItemsPerRec
				temp = m_whyItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("whyitemsperrec") != 0)
				{
					m_errorText = String.Format("Wrong header for WhyItemsPerRec in {0}.",
							m_whyItemsFilename);
					m_whyItemsFile.Close();
					return 1;
				}
				m_numWhyItemsPerRec = Convert.ToInt32(tempSplit[1]);
			} // version < 3.15

			// Read subsection header line with ItemID, WhyItemID, Likelihood etc.
			temp = m_whyItemsFile.ReadLine();

			return 0;
		}

		// FinishWhyItemsFile ///////////// 
		// Reads Cross Sell Items and closes file!
		protected int FinishWhyItemsFile()
		{
			int i, j, k;
			int index;
			string temp;
			string[] tempSplit;
			string strUserID;
			int userIndex;
			bool isNew = true;

			// == Read Cross Sell Items ==
			// Read WhyItems **
			for (i = 0; i < m_numUsers; i++)
			{
				// Read Line - includes ItemID and recommendations **
				temp = m_whyItemsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of Why Items {0}. {1} items in Why Items, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_whyItemsFilename, i, m_numUsers);
					break;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				// Read ItemID
				strUserID = tempSplit[0];
				// Verify Index
				userIndex = m_userIndex.Index(strUserID, ref isNew, 1);

				// Read recs
				for (j = 0; j < m_numWhyItemRecs; j++)
				{
					for (k = 0; k < m_numWhyItemsPerRec; k++)
					{
						index = 3 * m_numWhyItemsPerRec * j + 3 * k;
						m_whyItems[i, j, k].alphaID = tempSplit[index + 1];
						m_whyItems[i, j, k].similarity = Convert.ToSingle(tempSplit[index + 2]);
						m_whyItems[i, j, k].numCommon = Convert.ToInt32(tempSplit[index + 3]);
					} // for j NumWhyItemsPerRec
				} // for j nNumWhyItemRecs
			} // for i m_nNumUsers

			// == Close File ==
			m_whyItemsFile.Close();

			return 0;
		}
		#endregion

		// Other Tab Stuff
		#region BundleItems
		//////////////////////////////////////////////////////////////////
		// BundleItems ////////////////////////////////////////////////
		// BundleItems Members and Properies **
		int m_numBundleItems = 0;
		StreamReader m_bundleItemsFile;
		string m_bundleItemsFilename = null;
		BundleRec[] m_bundleItems = null;

		public int NumBundleItems
		{
			set { m_numBundleItems = value; }
			get { return m_numBundleItems; }
		}
		public string BundleItemsFilename
		{
			set { m_bundleItemsFilename = value; }
			get { return m_bundleItemsFilename; }
		}
		public BundleRec[] BundleItems
		{
			set { m_bundleItems = value; }
			get { return m_bundleItems; }
		}

		// Load Bundle Items ///////
		public int ReadBundleItems()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumItems and m_nNumBundleItemRecs are read from the header
			m_error = InitBundleItemsFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// BundleItems **
			m_bundleItems = new BundleRec[m_numBundleItems];
			if (m_bundleItems == null)
			{
				m_errorText = "Could not allocate memory for Bundle Items.\n";
				m_bundleItemsFile.Close();
				return 1;
			}

			// Read Bundle Items ////
			m_error = FinishBundleItemsFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitBundleItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitBundleItemsFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Bundle Item File ==
			if (m_bundleItemsFilename == null)
				m_bundleItemsFilename = BundleItemsBasename;

			try
			{
				m_bundleItemsFile = new StreamReader(m_clientDataPath + m_bundleItemsFilename);
			}
			catch(Exception)
			{
				m_errorText = "Error: Could not open file " + m_bundleItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_bundleItemsFile.ReadLine();
			if ( !version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_bundleItemsFilename);
				m_bundleItemsFile.Close();
				return 1;
			}

			m_numBundleItems = m_numBundleRecs;
			tempSplit = version.Split(m_tabSeparator);
			float bundleVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (bundleVersion < 3.15)
			{
				// NumBundles
				temp = m_bundleItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numbundles") != 0)
				{
					m_errorText = String.Format("Wrong header for NumBundles in {0}.",
							m_bundleItemsFilename);
					m_bundleItemsFile.Close();
					return 1;
				}
				m_numBundleItems = Convert.ToInt32(tempSplit[1]);
			} // If bundled version is less than 3.15

			// Read subsection header line with ItemID1, ItemID2, Similarity
			temp = m_bundleItemsFile.ReadLine();

			return 0;
		}

		// FinishBundleItemsFile ///////////// 
		// Reads Bundle Items and closes file!
		protected int FinishBundleItemsFile()
		{
			int i;
			string temp;
			string[] tempSplit;

			// == Read Bundle Items ==
			for (i = 0; i < m_numBundleItems; i++)
			{
				temp = m_bundleItemsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_numBundleItems = i;
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of Bundled Items {0}. {1} items in Bundled Itesm, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_bundleItemsFilename, i, m_numBundleItems);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				// Index1
				m_bundleItems[i].alphaID1 = tempSplit[0];
				// Index2
				m_bundleItems[i].alphaID2 = tempSplit[1];
				// Similarity
				m_bundleItems[i].similarity = Convert.ToSingle(tempSplit[2]);
				// NunCommon
				m_bundleItems[i].numCommon = Convert.ToInt32(tempSplit[3]);
			} // for i m_nNumBundles

			// == Close File ==
			m_bundleItemsFile.Close();

			return 0;
		}
		#endregion

		#region SalesData
		//////////////////////////////////////////////////////////////////
		// SalesData ////////////////////////////////////////////////
		// SalesData Members and Properies **
		StreamReader m_salesDataFile;
		string m_salesDataFilename = null;
		SalesData m_salesDataItems = new SalesData();
		SalesData m_salesDataUsers = new SalesData();

		public string SalesDataFilename
		{
			set { m_salesDataFilename = value; }
			get { return m_salesDataFilename; }
		}
		public SalesData SalesDataItems
		{
			set { m_salesDataItems = value; }
			get { return m_salesDataItems; }
		}
		public SalesData SalesDataUsers
		{
			set { m_salesDataUsers = value; }
			get { return m_salesDataUsers; }
		}

		// LoadSalesData Items ///////
		public int ReadSalesData()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumItems and m_nNumsalesDataItemRecs are read from the header
			m_error = InitSalesDataFile();
			if (m_error != 0) return m_error;

			// ReadSalesData Items ////
			m_error = FinishSalesDataFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitsalesDataFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitSalesDataFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == OpenSalesData Item File ==
			if (m_salesDataFilename == null)
				m_salesDataFilename = SalesDataBasename;

			try
			{
				m_salesDataFile = new StreamReader(m_clientDataPath + m_salesDataFilename);
			}
			catch(Exception)
			{
				m_errorText = "Error: Could not open file " + m_salesDataFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_salesDataFile.ReadLine();
			if ( !version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version ({1}) in {0}.",
						m_salesDataFilename, version);
				m_salesDataFile.Close();
				return 1;
			}

			// Item Stats **
			temp = m_salesDataFile.ReadLine();

			// NumCountsWithOneAction
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numcountswithlte10action") != 0)
			{
				m_errorText = String.Format("Wrong header for NumCountsWithLTE10Action in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.numCountsWithLTE10Action = Convert.ToInt32(tempSplit[1]);

			// NumCountsWithGTOneAction
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numcountswithgt10action") != 0)
			{
				m_errorText = String.Format("Wrong header for NumCountsWithGT10Action in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.numCountsWithGT10Action = Convert.ToInt32(tempSplit[1]);

			// NumItems
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numitems") != 0)
			{
				m_errorText = String.Format("Wrong header for NumItems in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			int numItems = Convert.ToInt32(tempSplit[1]);
			if (m_numItems > -1)
			{
				if (m_numItems != numItems)
				{
					m_warning = 1;
					m_warningText = String.Format("NumItems in SalesData ({0}) is not equal to the number of items ({1}) from the other files.\n" +
					"Please try a different file.\n", Convert.ToInt32(tempSplit[1]), m_numItems);
					m_salesDataFile.Close();
					return 1;
				}
			}
			else
				m_numItems = numItems;

			// NumBins
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numbinsused") != 0)
			{
				m_errorText = String.Format("Wrong header for NumBins in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.numBins = Convert.ToInt32(tempSplit[1]);

			// MinItemActions
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("minitemactions") != 0)
			{
				m_errorText = String.Format("Wrong header for MinItemActions in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.minActions = Convert.ToInt32(tempSplit[1]);

			// MaxItemActions
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("maxitemactions") != 0)
			{
				m_errorText = String.Format("Wrong header for MaxItemActions in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.maxActions = Convert.ToInt32(tempSplit[1]);

			// MinCount
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("mincount") != 0)
			{
				m_errorText = String.Format("Wrong header for MinCount in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.minCount = Convert.ToInt32(tempSplit[1]);

			// MaxCount
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("maxcount") != 0)
			{
				m_errorText = String.Format("Wrong header for MaxCount in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.maxCount = Convert.ToInt32(tempSplit[1]);

			// NumActionsForMaxCount
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numactionsformaxcount") != 0)
			{
				m_errorText = String.Format("Wrong header for NumActionsForMaxCount in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.numActionsForMaxCount = Convert.ToInt32(tempSplit[1]);

			// TotalItemActions
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("totalitemactions") != 0)
			{
				m_errorText = String.Format("Wrong header for TotalItemActions in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.totalActions = Convert.ToInt32(tempSplit[1]);

			// AveItemActions
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("aveitemactions") != 0)
			{
				m_errorText = String.Format("Wrong header for AveItemActions in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.aveActions = Convert.ToSingle(tempSplit[1]);

			// NumItemRecs
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numitemrecs") != 0)
			{
				m_errorText = String.Format("Wrong header for NumItemRecs in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataItems.numRecs = Convert.ToInt32(tempSplit[1]);

			// User Details **
			temp = m_salesDataFile.ReadLine();

			// NumCountsWithOneAction
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numcountswithlte10action") != 0)
			{
				m_errorText = String.Format("Wrong header for NumCountsWithLTE10Action in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.numCountsWithLTE10Action = Convert.ToInt32(tempSplit[1]);

			// NumCountsWithGTOneAction
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numcountswithgt10action") != 0)
			{
				m_errorText = String.Format("Wrong header for NumCountsWithGT10Action in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.numCountsWithGT10Action = Convert.ToInt32(tempSplit[1]);

			// NumUsers
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numusers") != 0)
			{
				m_errorText = String.Format("Wrong header for NumUsers in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			int numUsers = Convert.ToInt32(tempSplit[1]);
			if (m_numUsers > -1)
			{
				if (m_numUsers != numUsers)
				{
					m_warning = 1;
					m_warningText = String.Format("numUsers in SalesData ({0}) is not equal to the number of items ({1}) from the other files.\n" +
					"Please try a different file.\n", Convert.ToInt32(tempSplit[1]), m_numUsers);
				}
			}
			else
				m_numUsers = numUsers;

			// NumBins
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numbinsused") != 0)
			{
				m_errorText = String.Format("Wrong header for NumBins in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.numBins = Convert.ToInt32(tempSplit[1]);

			// MinUserActions
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("minuseractions") != 0)
			{
				m_errorText = String.Format("Wrong header for MinUserActions in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.minActions = Convert.ToInt32(tempSplit[1]);

			// MaxUserActions
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("maxuseractions") != 0)
			{
				m_errorText = String.Format("Wrong header for MaxUserActions in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.maxActions = Convert.ToInt32(tempSplit[1]);

			// MinCount
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("mincount") != 0)
			{
				m_errorText = String.Format("Wrong header for MinCount in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.minCount = Convert.ToInt32(tempSplit[1]);

			// MaxCount
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("maxcount") != 0)
			{
				m_errorText = String.Format("Wrong header for MaxCount in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.maxCount = Convert.ToInt32(tempSplit[1]);

			// NumActionsForMaxCount
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numactionsformaxcount") != 0)
			{
				m_errorText = String.Format("Wrong header for NumActionsForMaxCount in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.numActionsForMaxCount = Convert.ToInt32(tempSplit[1]);

			// TotalUserActions
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("totaluseractions") != 0)
			{
				m_errorText = String.Format("Wrong header for TotalUserActions in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.totalActions = Convert.ToInt32(tempSplit[1]);

			// AveUserActions
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("aveuseractions") != 0)
			{
				m_errorText = String.Format("Wrong header for AveUserActions in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.aveActions = Convert.ToSingle(tempSplit[1]);

			// NumUserRecs
			temp = m_salesDataFile.ReadLine();
			tempSplit = temp.Split(m_tabSeparator);
			if (tempSplit[0].ToLower().CompareTo("numuserrecs") != 0)
			{
				m_errorText = String.Format("Wrong header for NumUserRecs in {0}.",
						m_salesDataFilename);
				m_salesDataFile.Close();
				return 1;
			}
			m_salesDataUsers.numRecs = Convert.ToInt32(tempSplit[1]);

			return 0;
		}

		// FinishSalesDataFile ///////////// 
		// Reads SalesData Items and closes file!
		protected int FinishSalesDataFile()
		{
			// == Sales Data ==
			// if add to read bins

			// == Close File ==
			m_salesDataFile.Close();

			return 0;
		}
		#endregion SalesData

		#region TopSellerItems
		//////////////////////////////////////////////////////////////////
		// TopSellerItems ////////////////////////////////////////////////
		// ItemTopSeller Members and Properties **
		// TopSellerItems
		int m_numTopSellerItemsRecs = 0;
		TopSellerRec[] m_topSellerItems = null;
		StreamReader m_topSellerItemsFile = null;
		string m_topSellerItemsFilename = null;

		public TopSellerRec[] TopSellerItems
		{
			set { m_topSellerItems = value; }
			get { return m_topSellerItems; }
		}
		public string TopSellerItemsFilename
		{
			set { m_topSellerItemsFilename = value; }
			get { return m_topSellerItemsFilename; }
		}
		public int NumTopSellerItemsRecs
		{
			set { m_numTopSellerItemsRecs = value; }
			get { return m_numTopSellerItemsRecs; }
		}

		// Load Item Items ///////
		public int ReadTopSellerItems()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			m_error = InitTopSellerItemsFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// TopSellerItems **
			m_topSellerItems = new TopSellerRec[m_numTopSellerItemsRecs];
			if (m_topSellerItems == null)
			{
				m_errorText = "Could not allocate memory for Item Top Sellers.\n";
				m_topSellerItemsFile.Close();
				return 1;
			}

			// Read Item Items ////
			m_error = FinishTopSellerItemsFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitTopSellerItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitTopSellerItemsFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Item Item File ==
			if (m_topSellerItemsFilename == null)
				m_topSellerItemsFilename = TopSellerItemsBasename;

			try
			{
				m_topSellerItemsFile = new StreamReader(m_clientDataPath + m_topSellerItemsFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_topSellerItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_topSellerItemsFile.ReadLine();
			if ( !version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_topSellerItemsFilename);
				m_topSellerItemsFile.Close();
				return 1;
			}

			m_numTopSellerItemsRecs = m_numTopSellerRecs;
			tempSplit = version.Split(m_tabSeparator);
			float topSellerVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (topSellerVersion < 3.15)
			{
				// MaxItemID
				temp = m_topSellerItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxitemid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxItemID in {0}.",
							m_topSellerItemsFilename);
					m_topSellerItemsFile.Close();
					return 1;
				}

				// NumItemTopSellerRecs
				temp = m_topSellerItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_topSellerItemsFilename);
					m_topSellerItemsFile.Close();
					return 1;
				}
				m_numTopSellerItemsRecs = Convert.ToInt32(tempSplit[1]);
			} //If version is less than 3.15

			// Read subsection header line with ItemID, ItemTopSellerID, numActions etc.
			temp = m_topSellerItemsFile.ReadLine();

			return 0;
		}

		// FinishTopSellerItemsFile ///////////// 
		// Reads Item Items and closes file!
		protected int FinishTopSellerItemsFile()
		{
			int i;
			string temp;
			string[] tempSplit;

			// == Read Item Items ==
			for (i = 0; i < m_numTopSellerItemsRecs; i++)
			{
				temp = m_topSellerItemsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_numTopSellerItemsRecs = i;
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of TopSeller Items {0}. {1} items in TopSellers, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_topSellerItemsFilename, i, m_numTopSellerItemsRecs);
					break;
				} // EOF

				// Fill data
				tempSplit = temp.Split(m_tabSeparator);
				m_topSellerItems[i].alphaID = tempSplit[0];
				m_topSellerItems[i].numActions = Convert.ToInt32(tempSplit[1]);
				m_topSellerItems[i].type = (byte) RecType.TopSeller;
			} // for i m_nNumItemTopSellerRecs

			// == Close File ==
			m_topSellerItemsFile.Close();

			return 0;
		}
		#endregion // TopSellerItems

		#region TopBuyerUsers
		//////////////////////////////////////////////////////////////////
		// TopBuyerUsers ////////////////////////////////////////////////
		// UserTopSeller Members and Properties **
		// TopBuyerUsers
		int m_numTopBuyerUsersRecs = 0;
		TopSellerRec[] m_topBuyerUsers = null;
		StreamReader m_topBuyerUsersFile = null;
		string m_topBuyerUsersFilename = null;

		public List<TopCustomer> GetDashTopCustomers()
		{
			List<TopCustomer> result = new List<TopCustomer>();
			foreach (TopSellerRec user in TopBuyerUsers)
			{
				TopCustomer item = new TopCustomer();
				item.id = user.alphaID;
				item.purchases = user.numActions;
				result.Add(item);
			}
			return result;
		}

		public TopSellerRec[] TopBuyerUsers
		{
			set { m_topBuyerUsers = value; }
			get { return m_topBuyerUsers; }
		}
		public string TopBuyerUsersFilename
		{
			set { m_topBuyerUsersFilename = value; }
			get { return m_topBuyerUsersFilename; }
		}
		public int NumTopBuyerUsersRecs
		{
			set { m_numTopBuyerUsersRecs = value; }
			get { return m_numTopBuyerUsersRecs; }
		}

		// Load Item Items ///////
		public int ReadTopBuyerUsers()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			m_error = InitTopBuyerUsersFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// TopBuyerUsers **
			m_topBuyerUsers = new TopSellerRec[m_numTopBuyerUsersRecs];
			if (m_topBuyerUsers == null)
			{
				m_errorText = "Could not allocate memory for Top Buyers.\n";
				m_topBuyerUsersFile.Close();
				return 1;
			}

			// Read Item Items ////
			m_error = FinishTopBuyerUsersFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitTopBuyerUsersFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitTopBuyerUsersFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Item Item File ==
			if (m_topBuyerUsersFilename == null)
				m_topBuyerUsersFilename = TopBuyerUsersBasename;

			try
			{
				m_topBuyerUsersFile = new StreamReader(m_clientDataPath + m_topBuyerUsersFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_topBuyerUsersFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_topBuyerUsersFile.ReadLine();
			if (!version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_topBuyerUsersFilename);
				m_topBuyerUsersFile.Close();
				return 1;
			}

			m_numTopBuyerUsersRecs = m_numTopSellerRecs;
			tempSplit = version.Split(m_tabSeparator);
			float topBuyerVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (topBuyerVersion < 3.15)
			{
				// MaxItemID
				temp = m_topBuyerUsersFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxuserid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxUserID in {0}.",
							m_topBuyerUsersFilename);
					m_topBuyerUsersFile.Close();
					return 1;
				}
				//if (m_maxUserID != Convert.ToInt32(tempSplit[1]))
				//{
				//  m_errorText = String.Format("Wrong maximum user ID in header for {0}.",
				//      m_topBuyerUsersFilename);
				//  m_topBuyerUsersFile.Close();
				//  return 1;
				//}

				// NumUserTopSellerRecs
				temp = m_topBuyerUsersFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_topBuyerUsersFilename);
					m_topBuyerUsersFile.Close();
					return 1;
				}
				m_numTopBuyerUsersRecs = Convert.ToInt32(tempSplit[1]);
			} //if version less than 3.15

			// Read subsection header line with ItemID, UserTopSellerID, numActions etc.
			temp = m_topBuyerUsersFile.ReadLine();

			return 0;
		}

		// FinishTopBuyerUsersFile ///////////// 
		// Reads Item Items and closes file!
		protected int FinishTopBuyerUsersFile()
		{
			int i;
			string temp;
			string[] tempSplit;

			// == Read Item Items ==
			for (i = 0; i < m_numTopBuyerUsersRecs; i++)
			{
				temp = m_topBuyerUsersFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_numTopBuyerUsersRecs = i;
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of TopBuyer Items {0}. {1} items in TopBuyers, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_topBuyerUsersFilename, i, m_numTopBuyerUsersRecs);
					break;
				} // EOF

				// Fill data
				tempSplit = temp.Split(m_tabSeparator);
				m_topBuyerUsers[i].alphaID = tempSplit[0];
				m_topBuyerUsers[i].numActions = Convert.ToInt32(tempSplit[1]);
				m_topBuyerUsers[i].type = (byte)RecType.TopSeller;
			} // for i m_nNumUserTopSellerRecs

			// == Close File ==
			m_topBuyerUsersFile.Close();

			return 0;
		}
		#endregion // TopBuyerUsers
		
		#region DoNotRecommendExists
		//////////////////////////////////////////////////////////////////
		// DoNotRecommendExists ////////////////////////////////////////////////
		// DoNotRecommendExists Members and Properties **
		bool m_doNotRecommendExists = false; // set to true if file is read
		int m_doNotRecommendVersion;
		StreamReader m_doNotRecommendFile = null;
		string m_doNotRecommendFilename = null;
		bool[] m_doNotRecommend = null; // Item information
		int m_doNotRecommendCount = 0;

		public string DoNotRecommendFilename
		{
			set { m_doNotRecommendFilename = value; }
			get { return m_doNotRecommendFilename; }
		}
		public bool DoNotRecommendExists
		{
			//set { m_doNotRecommendExists = value; }
			get { return m_doNotRecommendExists; }
		}
		public bool[] DoNotRecommend
		{
			//set { m_doNotRecommend = value; }
			get { return m_doNotRecommend; }
		}

		public int DoNotRecommendCount
		{
			get { return m_doNotRecommendCount; }
		}

		// Called from RecommendInitCrossSellItems since m_nNumItems set there
		public int ReadDoNotRecommend()
		{
			// DoNotRecommendExists =====================================
			m_doNotRecommend = new bool[m_numItems];
			if (m_doNotRecommend == null)
			{
				m_errorText = "Could not allocate memory for out of stock.\n";
				return 1;
			}
			for (int i = 0; i < m_numItems; i++)
				m_doNotRecommend[i] = false;

			// Read Item Name //////
			m_error = ReadDoNotRecommendFile();
			if (m_error != 0)
				return m_error;

			return 0;
		}

		protected int ReadDoNotRecommendFile()
		{
			int i;
			int itemIndex;
			string strItemID;
			string temp;
			string[] tempSplit;

			// Safety check
			// Must be after read ConfigBoost file
			if (!m_doNotRecommendExists)
				return 0;

			// Open file
			// Determine name - first if set use it, if not try newest version, then v1
			// If name previously set, find just the name
			if (m_doNotRecommendFilename != null)
			{
				int index = m_doNotRecommendFilename.LastIndexOf('\\');
				if (index > -1)
					m_doNotRecommendFilename = m_doNotRecommendFilename.Remove(0, index + 1);
			}
			else
			{
				m_doNotRecommendVersion = 3;
				m_doNotRecommendFilename = DoNotRecommendBasename;
				if (!File.Exists(m_clientDataPath + m_doNotRecommendFilename))
				{
					m_doNotRecommendVersion = 2;
					m_doNotRecommendFilename = DoNotRecommendBasenameV2;
					if (!File.Exists(m_clientDataPath + m_doNotRecommendFilename))
					{
						m_doNotRecommendVersion = 1;
						m_doNotRecommendFilename = DoNotRecommendBasenameV1;
					}
				} 
			}

			// Could use File.Exist above, but this is safest
			try
			{
				m_doNotRecommendFile = new StreamReader(m_clientDataPath + m_doNotRecommendFilename);
			}
			catch (Exception)
			{
				m_errorText = String.Format("\nError: Could not open item name file {0}.\n\n", m_doNotRecommendFilename);
				if (m_doNotRecommendFilename == DoNotRecommendBasenameV1 || m_doNotRecommendFilename == DoNotRecommendBasenameV2)
					m_doNotRecommendFilename = DoNotRecommendBasename; // Set back to the original name for message
				return 2;
			}

			int numHeaderLines = 0;
			if (m_doNotRecommendVersion >= 2)
				numHeaderLines = 2;
			for (i = 0; i < numHeaderLines; i++)
				m_doNotRecommendFile.ReadLine();
			// In the future, may want to check for official version here

			// Read in items to array
			m_doNotRecommendCount = 0;
			do
			{
				temp = m_doNotRecommendFile.ReadLine();

				if (temp == null)
					break; // EOF

				tempSplit = temp.Split(m_tabSeparator);
				// Remove leading and trailing spaces
				strItemID = tempSplit[0].Trim();

				// Item Index - Created in FinishCrossSellItemsFile
				itemIndex = m_itemIndex.GetIndex(strItemID);
				if (itemIndex > -1)
				{
					m_doNotRecommendCount++;
					m_doNotRecommend[itemIndex] = true;
				}
			} while (true);

			m_doNotRecommendFile.Close();
			return 0;
		}
		#endregion DoNotRecommendExists

		// Att1 Stuff
		#region Att1 Globals
		// MaxAtt1ID - Depricated in v3.2, remove next release
		int m_maxAtt1ID = 0;
		public int MaxAtt1ID
		{
			set { m_maxAtt1ID = value; }
			get { return m_maxAtt1ID; }
		}

		// Att1Index
		CIndex m_att1Index = new CIndex();
		public CIndex Att1Index
		{
			set { m_att1Index = value; }
			get { return m_att1Index; }
		}

		// Att1Headers
		RecHeaderAtt[] m_att1Headers = null; // NumIndexes and NumActions
		public RecHeaderAtt[] Att1Headers
		{
			set { m_att1Headers = value; }
			get { return m_att1Headers; }
		}
		#endregion Att1Globals

		#region Att1Names
		//////////////////////////////////////////////////////////////////
		// Att1Names ////////////////////////////////////////////////
		// Att1Names Members and Properties **
		int m_att1NamesVersion;
		StreamReader m_att1NamesFile = null;
		string m_att1NamesFilename = null;
		string[] m_att1Names = null; // Att1 information

		public string Att1NamesFilename
		{
			set { m_att1NamesFilename = value; }
			get { return m_att1NamesFilename; }
		}
		public string[] Att1Names // Att1 information
		{
			set { m_att1Names = value; }
			get { return m_att1Names; }
		}

		// must first call ReadCrossSellAtt1s since m_nNumAtt1s set there
		public int ReadAtt1Names()
		{
			m_warning = 0;
			// Att1Names =====================================
			m_att1Names = new string[m_numAtt1s];
			if (m_att1Names == null)
			{
				m_errorText = "Could not allocate memory for Att1 Info.\n";
				return 1;
			}
			// Read Att1 Info //////
			m_error = ReadAtt1NamesFile();
			if (m_error != 0)
				return m_error;

			return 0;
		}

		protected int ReadAtt1NamesFile()
		{
			int i;
			int att1Index;
			string strAtt1ID;
			string temp;
			string[] tempSplit;

			// Open file
			// Determine name - first if set use it, if not try newest version, then v1
			// If name previously set (which is not currently used)
			if (m_att1NamesFilename != null)
			{
				int index = m_att1NamesFilename.LastIndexOf('\\');
				if (index > -1)
					m_att1NamesFilename = m_att1NamesFilename.Remove(0, index + 1);
			}
			else
			{
				m_att1NamesVersion = 2;
				m_att1NamesFilename = Att1NamesBasename;
				if (!File.Exists(m_clientDataPath + m_att1NamesFilename))
				{
					m_att1NamesVersion = 1;
					m_att1NamesFilename = Att1NamesBasenameV1;
				}
			}
			if (!File.Exists(m_clientDataPath + m_att1NamesFilename))
			{
				for (int j = 0; j < m_numAtt1s; j++)
					m_att1Names[j] = m_att1Index.GetID(j);
				return 2;
			}

			// Still safest
			try
			{
				m_att1NamesFile = new StreamReader(m_clientDataPath + m_att1NamesFilename);
			}
			catch (Exception)
			{
				m_errorText = String.Format("\nError: Could not open attribute 1 name file {0}.\n\n", m_att1NamesFilename);
				return 2;
			}

			int numHeaderLines = 0;
			if (m_att1NamesVersion >= 2)
				numHeaderLines = 2;
			for (i = 0; i < numHeaderLines; i++)
				m_att1NamesFile.ReadLine();
			// In the future, may want to check for official version here

			// Read Att1s into array
			for (i = 0; i < m_numAtt1s; i++)
			{
				temp = m_att1NamesFile.ReadLine();
				if (temp == null)
				{
					//EOF reached early - means some are missing
					//find missing names and fill using IDs
					for (int j = 0; j < m_numAtt1s; j++)
					{
						if (m_att1Names[j] == null) //found one
							m_att1Names[j] = m_att1Index.GetID(j);
					}
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				strAtt1ID = tempSplit[0].Trim();

				// Skip if Att1ID was not in Product Details
					// Works if wrong number of headers
				// Att1Index was created in FinishCrossSellAttsFile
				att1Index = m_att1Index.GetIndex(strAtt1ID);
				if (att1Index < 0)
				{
					i--;
					continue;
				}

				// Get Name
				 // If no second entry, create name from ID
				 // Must be before reading tempSplit[1]
				if (tempSplit.Length < 2)
				{		
					m_att1Names[att1Index] = strAtt1ID;
					continue;
				}
				// If second entry is blank, create name from ID
				if (tempSplit[1] != "")
					m_att1Names[att1Index] = tempSplit[1].Trim();
				else 
					m_att1Names[att1Index] = strAtt1ID;
			}

			m_att1NamesFile.Close();
			return 0;
		}
		#endregion

		#region CrossSellAtt1s
		//////////////////////////////////////////////////////////////////
		// CrossSellAtt1s ////////////////////////////////////////////////
		// CrossSell Members and Properties **
		int m_numCrossSellAtt1Recs = 0;
		StreamReader m_crossSellAtt1File;
		string m_crossSellAtt1Filename = null;
		Rec[,] m_crossSellAtt1s = null;

		public int NumCrossSellAtt1Recs
		{
			set { m_numCrossSellAtt1Recs = value; }
			get { return m_numCrossSellAtt1Recs; }
		}
		public string CrossSellAtt1Filename
		{
			set { m_crossSellAtt1Filename = value; }
			get { return m_crossSellAtt1Filename; }
		}
		public Rec[,] CrossSellAtt1s
		{
			set { m_crossSellAtt1s = value; }
			get { return m_crossSellAtt1s; }
		}

		// Load CrossSellAtt1s ///////
		public int ReadCrossSellAtt1s()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumAtt1s and m_nNumCrossSellAtt1Recs are read from the header
			m_error = InitCrossSellAtt1File();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// CrossSellAtt1s **
			m_crossSellAtt1s = new Rec[m_numAtt1s, m_numCrossSellAtt1Recs];
			if (m_crossSellAtt1s == null)
			{
				m_errorText = "Could not allocate memory for Cross Sell Attribute1's.\n";
				m_crossSellAtt1File.Close();
				return 1;
			}
			// Below is way more memory efficient, since only one per cat
			// If part of Rec, have one with each entry - really belongs with ItemDetails
			m_att1Headers = new RecHeaderAtt[m_numAtt1s];
			if (m_att1Headers == null)
			{
				m_errorText = "Could not allocate memory for Cross Sell Actions.\n";
				m_crossSellAtt1File.Close();
				return 1;
			}

			// Att1 Index **
			if (!m_att1Index.IsAllocated)
				m_att1Index.AllocIndex(m_numAtt1s); // (m_maxAtt1ID);

			// Read CrossSellAtt1s ////
			m_error = FinishCrossSellAtt1File(); // Reads cross sell att1 and closes file
			if (m_error != 0) return 1;

			// If any items are missing - should not happen 
			for (int i = 0; i < m_numAtt1s; i++)
			{
				if (m_crossSellAtt1s[i, 0].alphaID == null)
					for (int j = 0; j < NumRecs; j++)
						m_crossSellAtt1s[i, j].Reset();
			}

			return 0;
		}

		// InitCrossSellAtt1File 
		// Opens file and sets number of Att1s and recs so arrays can be allocated
		protected int InitCrossSellAtt1File()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Cross Sell Att1 File ==
			if (m_crossSellAtt1Filename == null)
				m_crossSellAtt1Filename = CrossSellAtt1sBasename;

			try
			{
				m_crossSellAtt1File = new StreamReader(m_clientDataPath + m_crossSellAtt1Filename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_crossSellAtt1Filename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_crossSellAtt1File.ReadLine();
			if (!version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_crossSellAtt1Filename);
				m_crossSellAtt1File.Close();
				return 1;
			}

			m_numCrossSellAtt1Recs = m_numRecs;

			tempSplit = version.Split(m_tabSeparator);
			float crossSellVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (crossSellVersion < 3.15)
			{
				// NumAtt1s
				temp = m_crossSellAtt1File.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numatts") != 0)
				{
					m_errorText = String.Format("Wrong header for NumAtts in {0}.",
							m_crossSellAtt1Filename);
					m_crossSellAtt1File.Close();
					return 1;
				}
				m_numAtt1s = Convert.ToInt32(tempSplit[1]);

				// MaxAtt1ID
				temp = m_crossSellAtt1File.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxID in {0}.",
							m_crossSellAtt1Filename);
					m_crossSellAtt1File.Close();
					return 1;
				}
				m_maxAtt1ID = Convert.ToInt32(tempSplit[1]);

				// NumCrossSellAtt1Recs
				temp = m_crossSellAtt1File.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_crossSellAtt1Filename);
					m_crossSellAtt1File.Close();
					return 1;
				}
				m_numCrossSellAtt1Recs = Convert.ToInt32(tempSplit[1]);
			} // if version < 3.15

			// Read subsection header line with Att1ID, CrossSellAtt1ID, Likelihood etc.
			temp = m_crossSellAtt1File.ReadLine();

			return 0;
		}

		// FinishCrossSellAtt1File ///////////// 
		// Reads CrossSellAtt1s and closes file!
		protected int FinishCrossSellAtt1File()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string att1ID;
			bool isNew = true;
			int index;

			// == Read CrossSellAtt1s ==
			for (i = 0; i < m_numAtt1s; i++)
			{
				// Read Line - includes Att1ID and recommendations **
				temp = m_crossSellAtt1File.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of CrossSellAtt1 Items {0}. {1} items in CrossSellAtt1, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_crossSellAtt1Filename, i, m_numAtt1s);
					break;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				// Read Att1ID
				att1ID = tempSplit[0];
				// Create Index
				index = m_att1Index.Index(att1ID, ref isNew, 1);
				// Read NumIndexes
				m_att1Headers[index].numUsers = Convert.ToInt32(tempSplit[1]);
				// Read NumActions
				m_att1Headers[index].numActions = Convert.ToInt32(tempSplit[2]);
				// Read recs
				// Factor of 3 since 3 att1 per rec
				for (j = 0; j < m_numCrossSellAtt1Recs; j++)
				{
					m_crossSellAtt1s[index, j].alphaID = tempSplit[3 * j + 3];
					m_crossSellAtt1s[index, j].likelihood = Convert.ToSingle(tempSplit[3 * j + 4]);
					m_crossSellAtt1s[index, j].numCommon = Convert.ToInt32(tempSplit[3 * j + 5]);
					m_crossSellAtt1s[index, j].type = (byte)RecType.CrossSellAttToAtt;
				} // for j nNumCrossSellAtt1Recs
			} // for i m_nNumAtt1

			// == Close File ==
			m_crossSellAtt1File.Close();

			return 0;
		}
		#endregion

		#region TopSellAtt1Items
		//////////////////////////////////////////////////////////////////
		// TopSellAtt1Items ////////////////////////////////////////////////
		// TopSellAtt1Item Members and Properties **
		int m_numTopSellAtt1ItemRecs = 0;
		StreamReader m_topSellAtt1ItemsFile = null;
		string m_topSellAtt1ItemsFilename = null;
		TopSellRec[,] m_topSellAtt1Items = null;

		public int NumTopSellAtt1ItemRecs
		{
			set { m_numTopSellAtt1ItemRecs = value; }
			get { return m_numTopSellAtt1ItemRecs; }
		}
		public string TopSellAtt1ItemsFilename
		{
			set { m_topSellAtt1ItemsFilename = value; }
			get { return m_topSellAtt1ItemsFilename; }
		}
		public TopSellRec[,] TopSellAtt1Items
		{
			set { m_topSellAtt1Items = value; }
			get { return m_topSellAtt1Items; }
		}

		// Load Att1 Items ///////
		public int ReadTopSellAtt1Items()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumItems and m_nNumTopSellAtt1ItemRecs are read from the header
			m_error = InitTopSellAtt1ItemsFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// TopSellAtt1Items **
			m_topSellAtt1Items = new TopSellRec[m_numAtt1s, m_numTopSellAtt1ItemRecs];
			if (m_topSellAtt1Items == null)
			{
				m_errorText = "Could not allocate memory for Top Sell Att1 Items.\n";
				m_topSellAtt1ItemsFile.Close();
				return 1;
			}

			// Att1Index **
			if (!m_att1Index.IsAllocated)
				m_att1Index.AllocIndex(m_numAtt1s); // (m_maxAtt1ID);

			// Read Att1 Items ////
			m_error = FinishTopSellAtt1ItemsFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			// If any items are missing - should not happen 
			for (int i = 0; i < m_numAtt1s; i++)
			{
				if (m_topSellAtt1Items[i, 0].alphaID == null)
					for (int j = 0; j < NumRecs; j++) // Sames as m_numTopSellAtt1ItemRecs
						m_topSellAtt1Items[i, j].Reset();
			}

			return 0;
		}

		// InitTopSellAtt1ItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitTopSellAtt1ItemsFile()
		{
			int numAtt1s;
			int maxAtt1ID;
			string version;
			string temp;
			string[] tempSplit;

			// == Open Att1 Item File ==
			if (m_topSellAtt1ItemsFilename == null)
				m_topSellAtt1ItemsFilename = TopSellAtt1ItemsBasename;

			try
			{
				m_topSellAtt1ItemsFile = new StreamReader(m_clientDataPath + m_topSellAtt1ItemsFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_topSellAtt1ItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_topSellAtt1ItemsFile.ReadLine();
			if (!version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_topSellAtt1ItemsFilename);
				m_topSellAtt1ItemsFile.Close();
				return 1;
			}

			m_numTopSellAtt1ItemRecs = m_numRecs;

			tempSplit = version.Split(m_tabSeparator);
			float topSellVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (topSellVersion < 3.15)
			{
				// NumAtt1s
				temp = m_topSellAtt1ItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numatts") != 0)
				{
					m_errorText = String.Format("Wrong header for NumAtts in {0}.",
							m_topSellAtt1ItemsFilename);
					m_topSellAtt1ItemsFile.Close();
					return 1;
				}
				numAtt1s = Convert.ToInt32(tempSplit[1]);
				if (m_numAtt1s != numAtt1s)
				{
					m_errorText = String.Format("{0} categories in {1}, but {2} categories in {3}.\n",
							numAtt1s, m_topSellAtt1ItemsFilename, m_numAtt1s, m_crossSellAtt1Filename);
					m_topSellAtt1ItemsFile.Close();
					return 1;
				}

				// MaxAtt1ID
				temp = m_topSellAtt1ItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxID in {0}.",
							m_topSellAtt1ItemsFilename);
					m_topSellAtt1ItemsFile.Close();
					return 1;
				}
				maxAtt1ID = Convert.ToInt32(tempSplit[1]);
				if (m_maxAtt1ID != maxAtt1ID)
				{
					m_errorText = String.Format("{0} category IDs in {1}, but {2} category IDs in {3}.\n",
							maxAtt1ID, m_topSellAtt1ItemsFilename, m_maxAtt1ID, m_crossSellAtt1Filename);
					m_topSellAtt1ItemsFile.Close();
					return 1;
				}

				// NumTopSellAtt1ItemRecs
				temp = m_topSellAtt1ItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_topSellAtt1ItemsFilename);
					m_topSellAtt1ItemsFile.Close();
					return 1;
				}
				m_numTopSellAtt1ItemRecs = Convert.ToInt32(tempSplit[1]);
			} // if version < 3.15

			// Read subsection header line with ItemID, TopSellAtt1ItemID, Likelihood etc.
			temp = m_topSellAtt1ItemsFile.ReadLine();

			return 0;
		}

		// FinishTopSellAtt1ItemsFile ///////////// 
		// Reads Att1 Items and closes file!
		protected int FinishTopSellAtt1ItemsFile()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string strAtt1ID;
			int att1Index;

			// == Read Att1 Items ==
			for (i = 0; i < m_numAtt1s; i++)
			{
				temp = m_topSellAtt1ItemsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of TopSellAtt1 Items {0}. {1} items in TopSellAtt1, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_topSellAtt1ItemsFilename, i, m_numAtt1s);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				strAtt1ID = tempSplit[0];
				att1Index = m_att1Index.GetIndex(strAtt1ID);
				if (att1Index < 0) //TODO: seems like this should be an error condition?
					continue;

				// Use att1Index to place in Header and Data arrays
				// so match m_att1Name array
				// Different than TopSellerAtt1s, which includes att1ID since in order of numActions
				m_att1Headers[att1Index].numItems = Convert.ToInt32(tempSplit[1]);
				m_att1Headers[att1Index].numActions = Convert.ToInt32(tempSplit[2]);

				for (j = 0; j < m_numTopSellAtt1ItemRecs; j++)
				{
					m_topSellAtt1Items[att1Index, j].alphaID = tempSplit[2 * j + 3];
					m_topSellAtt1Items[att1Index, j].numActions = Convert.ToInt32(tempSplit[2 * j + 4]);
					m_topSellAtt1Items[att1Index, j].type = (byte)RecType.TopSell;
				} // for j nNumTopSellAtt1ItemRecs
			} // for i m_nNumAtt1s

			// == Close File ==
			m_topSellAtt1ItemsFile.Close();

			return 0;
		}
		#endregion // TopSellAtt1Items

		#region BundleAtt1s
		//////////////////////////////////////////////////////////////////
		// BundleAtt1s ////////////////////////////////////////////////
		// BundleAtt1s Members and Properies **
		int m_numBundleAtt1s = 0;
		StreamReader m_bundleAtt1File;
		string m_bundleAtt1Filename = null;
		BundleRec[] m_bundleAtt1s = null;

		public int NumBundleAtt1s
		{
			set { m_numBundleAtt1s = value; }
			get { return m_numBundleAtt1s; }
		}
		public string BundleAtt1Filename
		{
			set { m_bundleAtt1Filename = value; }
			get { return m_bundleAtt1Filename; }
		}
		public BundleRec[] BundleAtt1s
		{
			set { m_bundleAtt1s = value; }
			get { return m_bundleAtt1s; }
		}

		// Load Bundle Att1 ///////
		public int ReadBundleAtt1s()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumAtt1s and m_nNumBundleAtt1Recs are read from the header
			m_error = InitBundleAtt1File();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// BundleAtt1s **
			m_bundleAtt1s = new BundleRec[m_numBundleAtt1s];
			if (m_bundleAtt1s == null)
			{
				m_errorText = "Could not allocate memory for Bundle Att1s.\n";
				m_bundleAtt1File.Close();
				return 1;
			}

			// Read Bundle Att1 ////
			m_error = FinishBundleAtt1File(); // Reads cross sell att1 and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitBundleAtt1File 
		// Opens file and sets number of att1 and recs so arrays can be allocated
		protected int InitBundleAtt1File()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Bundle Att1 File ==
			if (m_bundleAtt1Filename == null)
				m_bundleAtt1Filename = BundleAtt1sBasename;

			try
			{
				m_bundleAtt1File = new StreamReader(m_clientDataPath + m_bundleAtt1Filename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_bundleAtt1Filename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_bundleAtt1File.ReadLine();
			if (!version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_bundleAtt1Filename);
				m_bundleAtt1File.Close();
				return 1;
			}

			m_numBundleAtt1s = m_numBundleRecs;
			tempSplit = version.Split(m_tabSeparator);
			float bundleVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (bundleVersion < 3.15)
			{
				// NumBundles
				temp = m_bundleAtt1File.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numbundles") != 0)
				{
					m_errorText = String.Format("Wrong header for NumBundles in {0}.",
							m_bundleAtt1Filename);
					m_bundleAtt1File.Close();
					return 1;
				}
				m_numBundleAtt1s = Convert.ToInt32(tempSplit[1]);
			} // if Bundle version less than 3.15

			// Read subsection header line with Att1ID1, Att1ID2, Similarity
			temp = m_bundleAtt1File.ReadLine();

			return 0;
		}

		// FinishBundleAtt1File ///////////// 
		// Reads Bundle Att1 and closes file!
		protected int FinishBundleAtt1File()
		{
			int i;
			string temp;
			string[] tempSplit;

			// == Read Bundle Att1 ==
			for (i = 0; i < m_numBundleAtt1s; i++)
			{
				temp = m_bundleAtt1File.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_numBundleAtt1s = i;
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of BundleAtt1 Items {0}. {1} items in BundleAtt1, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_bundleAtt1Filename, i, m_numBundleAtt1s);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				// Index1
				m_bundleAtt1s[i].alphaID1 = tempSplit[0];
				// Index2
				m_bundleAtt1s[i].alphaID2 = tempSplit[1];
				// Similarity
				m_bundleAtt1s[i].similarity = Convert.ToSingle(tempSplit[2]);
				// NunCommon
				m_bundleAtt1s[i].numCommon = Convert.ToInt32(tempSplit[3]);
			} // for i m_nNumBundles

			// == Close File ==
			m_bundleAtt1File.Close();

			return 0;
		}
		#endregion

		#region TopSellerAtt1
		//////////////////////////////////////////////////////////////////
		// TopSellerAtt1 ////////////////////////////////////////////////
		// TopSellerAtt1 Members and Properties **
		int m_numTopSellerAtt1Recs = 0;
		StreamReader m_topSellerAtt1File = null;
		string m_topSellerAtt1Filename = null;
		TopSellerRec[] m_topSellerAtt1s = null;

		public string TopSellerAtt1Filename
		{
			set { m_topSellerAtt1Filename = value; }
			get { return m_topSellerAtt1Filename; }
		}
		public TopSellerRec[] TopSellerAtt1s
		{
			set { m_topSellerAtt1s = value; }
			get { return m_topSellerAtt1s; }
		}
		public int NumTopSellerAtt1Recs
		{
			set { m_numTopSellerAtt1Recs = value; }
			get { return m_numTopSellerAtt1Recs; }
		}

		// Load Att1 Items ///////
		public int ReadTopSellerAtt1s()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumItems and m_nNumTopSellerAtt1Recs are read from the header
			m_error = InitTopSellerAtt1File();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// TopSellerAtt1s **
			m_topSellerAtt1s = new TopSellerRec[m_numTopSellerAtt1Recs];
			if (m_topSellerAtt1s == null)
			{
				m_errorText = "Could not allocate memory for Att1 Top Sellers.\n";
				m_topSellerAtt1File.Close();
				return 1;
			}

			// Att1Index **
			if (!m_att1Index.IsAllocated)
				m_att1Index.AllocIndex(m_numAtt1s); // (m_maxAtt1ID);

			// Read Att1Items ////
			m_error = FinishTopSellerAtt1File(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitTopSellerAtt1File 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitTopSellerAtt1File()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Att1 Item File ==
			if (m_topSellerAtt1Filename == null)
				m_topSellerAtt1Filename = TopSellerAtt1sBasename;

			try
			{
				m_topSellerAtt1File = new StreamReader(m_clientDataPath + m_topSellerAtt1Filename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_topSellerAtt1Filename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_topSellerAtt1File.ReadLine();
			if (!version.ToLower().StartsWith("version")  )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_topSellerAtt1Filename);
				m_topSellerAtt1File.Close();
				return 1;
			}

			m_numTopSellerAtt1Recs = m_numTopSellerRecs;

			tempSplit = version.Split(m_tabSeparator);
			float topSellerVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (topSellerVersion < 3.15)
			{
				// MaxAtt1ID
				temp = m_topSellerAtt1File.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxID in {0}.",
							m_topSellerAtt1Filename);
					m_topSellerAtt1File.Close();
					return 1;
				}
				if (m_maxAtt1ID != Convert.ToInt32(tempSplit[1]))
				{
					m_errorText = String.Format("Wrong maximum category ID in header for {0}.",
							m_topSellerAtt1Filename);
					m_topSellerAtt1File.Close();
					return 1;
				}

				// NumTopSellerAtt1Recs
				temp = m_topSellerAtt1File.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_topSellerAtt1Filename);
					m_topSellerAtt1File.Close();
					return 1;
				}
				m_numTopSellerAtt1Recs = Convert.ToInt32(tempSplit[1]);
			} // if version < 3.2

			// Read subsection header line with ItemID, TopSellerAtt1ID, Likelihood etc.
			temp = m_topSellerAtt1File.ReadLine();

			return 0;
		}

		// FinishTopSellerAtt1File ///////////// 
		// Reads Att1 Items and closes file!
		protected int FinishTopSellerAtt1File()
		{
			int i;
			string temp;
			string[] tempSplit;

			// == Read Att1 Items ==
			for (i = 0; i < m_numTopSellerAtt1Recs; i++)
			{
				temp = m_topSellerAtt1File.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_numTopSellerAtt1Recs = i;
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of TopSellerAtt1 Items {0}. {1} items in TopSellerAtt1, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_topSellerAtt1Filename, i, m_numTopSellerAtt1Recs);
					break;
				} // EOF

				// Fill data
				tempSplit = temp.Split(m_tabSeparator);
				m_topSellerAtt1s[i].alphaID = tempSplit[0];
				m_topSellerAtt1s[i].numItems = Convert.ToInt32(tempSplit[1]);
				m_topSellerAtt1s[i].numActions = Convert.ToInt32(tempSplit[2]);
				m_topSellerAtt1s[i].type = (byte)RecType.TopSellerAtt;
			} // for i m_nNumTopSellerAtt1Recs

			// == Close File ==
			m_topSellerAtt1File.Close();

			return 0;
		}
		#endregion // TopSellerAtt1s

		// Att2 Stuff
		#region Att2 Globals
		// MaxAtt2ID - Depricated in v3.2, remove later
		int m_maxAtt2ID = -1;
		public int MaxAtt2ID
		{
			set { m_maxAtt2ID = value; }
			get { return m_maxAtt2ID; }
		}

		// Att2Index
		CIndex m_att2Index = new CIndex();
		public CIndex Att2Index
		{
			set { m_att2Index = value; }
			get { return m_att2Index; }
		}

		// Att2Headers
		RecHeaderAtt[] m_att2Headers = null; // NumIndexes and NumActions
		public RecHeaderAtt[] Att2Headers
		{
			set { m_att2Headers = value; }
			get { return m_att2Headers; }
		}
		#endregion Att2 Globals

		#region Att2Names
		//////////////////////////////////////////////////////////////////
		// Att2Names ////////////////////////////////////////////////
		// Att2Names Members and Properties **
		int m_att2NamesVersion;
		StreamReader m_att2NamesFile = null;
		string m_att2NamesFilename = null;
		string[] m_att2Names = null; // Att2 information

		public string Att2NamesFilename
		{
			set { m_att2NamesFilename = value; }
			get { return m_att2NamesFilename; }
		}
		public string[] Att2Names // Att2 information
		{
			set { m_att2Names = value; }
			get { return m_att2Names; }
		}

		// Called from RecommendInitCrossSellAtt2s since m_nNumAtt2s set there
		public int ReadAtt2Names()
		{
			// Att2Names =====================================
			m_att2Names = new string[m_numAtt2s];
			if (m_att2Names == null)
			{
				m_errorText = "Could not allocate memory for Att2 Info.\n";
				return 1;
			}
			// Read Att2 Info //////
			m_error = ReadAtt2NamesFile();
			if (m_error != 0)
				return m_error;

			return 0;
		}

		protected int ReadAtt2NamesFile()
		{
			int i;
			int att2Index;
			string strAtt2ID;
			string temp;
			string[] tempSplit;

			// Open file
			// Determine name - first if set use it, if not try newest version, then v1
			// If name previously set (which is not currently used)
			if (m_att2NamesFilename != null)
			{
				int index = m_att2NamesFilename.LastIndexOf('\\');
				if (index > -1)
					m_att2NamesFilename = m_att2NamesFilename.Remove(0, index + 1);
			}
			else
			{
				m_att2NamesVersion = 2;
				m_att2NamesFilename = Att2NamesBasename;
				if (!File.Exists(m_clientDataPath + m_att2NamesFilename))
				{
					m_att2NamesVersion = 1;
					m_att2NamesFilename = Att2NamesBasenameV1;
				}
			}
			if (!File.Exists(m_clientDataPath + m_att2NamesFilename))
			{
				for (int j = 0; j < m_numAtt2s; j++)
					m_att2Names[j] = m_att2Index.GetID(j);
				return 2;
			}

			// Still safest			
			try
			{
				m_att2NamesFile = new StreamReader(m_clientDataPath + m_att2NamesFilename);
			}
			catch (Exception)
			{
				m_errorText = String.Format("\nError: Could not open attribute 2 names file {0}.\n\n", m_att2NamesFilename);
				return 2;
			}

			// Read Header
			int numHeaderLines = 0;
			if (m_att2NamesVersion >= 2)
				numHeaderLines = 2;
			for (i = 0; i < numHeaderLines; i++)
				m_att2NamesFile.ReadLine();

			// Read in att2s to array
			for (i = 0; i < m_numAtt2s; i++)
			{
				temp = m_att2NamesFile.ReadLine();
				if (temp == null)
				{
					//EOF reached early - means some are missing
					//find missing names and fill using IDs
					for (int j = 0; j < m_numAtt2s; j++)
					{
						if (m_att2Names[j] == null) //found one
							m_att2Names[j] = m_att2Index.GetID(j);
					}
					break;
				} // EOF
				tempSplit = temp.Split(m_tabSeparator);
				strAtt2ID = tempSplit[0].Trim();

				// Skip if Att2ID was not in Product Details
					// Works if wrong number of headers
				// Att2Index was created in FinishCrossSellAttsFile
				att2Index = m_att2Index.GetIndex(strAtt2ID);
				if (att2Index < 0)
				{
					i--;
					continue;
				}

				// Get Name
				// If no second entry, create name from ID
				// Must be before reading tempSplit[1]
				if (tempSplit.Length < 2)
				{
					m_att2Names[att2Index] = strAtt2ID;
					continue;
				}
				// If second entry is blank, create name from ID
				if (tempSplit[1] != "")
					m_att2Names[att2Index] = tempSplit[1].Trim();
				else
					m_att2Names[att2Index] = strAtt2ID;
			}

			m_att2NamesFile.Close();
			return 0;
		}
		#endregion

		#region CrossSellAtt2s
		//////////////////////////////////////////////////////////////////
		// CrossSellAtt2s ////////////////////////////////////////////////
		// CrossSell Members and Properties **
		int m_numCrossSellAtt2Recs = 0;
		StreamReader m_crossSellAtt2sFile;
		string m_crossSellAtt2sFilename = null;
		Rec[,] m_crossSellAtt2s = null;

		public int NumCrossSellAtt2Recs
		{
			set { m_numCrossSellAtt2Recs = value; }
			get { return m_numCrossSellAtt2Recs; }
		}
		public string CrossSellAtt2sFilename
		{
			set { m_crossSellAtt2sFilename = value; }
			get { return m_crossSellAtt2sFilename; }
		}
		public Rec[,] CrossSellAtt2s
		{
			set { m_crossSellAtt2s = value; }
			get { return m_crossSellAtt2s; }
		}

		// Load Cross Sell Att2s ///////
		public int ReadCrossSellAtt2s()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumAtt2s and m_nNumCrossSellAtt2Recs are read from the header
			m_error = InitCrossSellAtt2sFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// CrossSellAtt2s **
			m_crossSellAtt2s = new Rec[m_numAtt2s, m_numCrossSellAtt2Recs];
			if (m_crossSellAtt2s == null)
			{
				m_errorText = "Could not allocate memory for Cross Sell Att2s.\n";
				m_crossSellAtt2sFile.Close();
				return 1;
			}
			// Below is way more memory efficient, since only one per att2
			// If part of Rec, have one with each entry - really belongs with ItemDetails
			m_att2Headers = new RecHeaderAtt[m_numAtt2s];
			if (m_att2Headers == null)
			{
				m_errorText = "Could not allocate memory for Cross Sell Att2 Headers.\n";
				m_crossSellAtt2sFile.Close();
				return 1;
			}

			// Att2 Index **
			if (!m_att2Index.IsAllocated)
				m_att2Index.AllocIndex(m_numAtt2s); // (m_maxAtt2ID);

			// Read Cross Sell Att2s ////
			m_error = FinishCrossSellAtt2sFile(); // Reads cross sell att2s and closes file
			if (m_error != 0) return 1;

			// If any items are missing - should not happen 
			for (int i = 0; i < m_numAtt2s; i++)
			{
				if (m_crossSellAtt2s[i, 0].alphaID == null)
					for (int j = 0; j < NumRecs; j++)
						m_crossSellAtt2s[i, j].Reset();
			}

			return 0;
		}

		// InitCrossSellAtt2sFile 
		// Opens file and sets number of Att2s and recs so arrays can be allocated
		protected int InitCrossSellAtt2sFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Cross Sell Att2 File ==
			if (m_crossSellAtt2sFilename == null)
				m_crossSellAtt2sFilename = CrossSellAtt2sBasename;

			try
			{
				m_crossSellAtt2sFile = new StreamReader(m_clientDataPath + m_crossSellAtt2sFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_crossSellAtt2sFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_crossSellAtt2sFile.ReadLine();
			if (!version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_crossSellAtt2sFilename);
				m_crossSellAtt2sFile.Close();
				return 1;
			}

			m_numCrossSellAtt2Recs = m_numRecs;

			tempSplit = version.Split(m_tabSeparator);
			float crossSellVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (crossSellVersion < 3.15)
			{
				// NumAtt2s
				temp = m_crossSellAtt2sFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numatts") != 0)
				{
					m_errorText = String.Format("Wrong header for NumAtts in {0}.",
							m_crossSellAtt2sFilename);
					m_crossSellAtt2sFile.Close();
					return 1;
				}
				m_numAtt2s = Convert.ToInt32(tempSplit[1]);

				// MaxAtt2ID
				temp = m_crossSellAtt2sFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxID in {0}.",
							m_crossSellAtt2sFilename);
					m_crossSellAtt2sFile.Close();
					return 1;
				}
				m_maxAtt2ID = Convert.ToInt32(tempSplit[1]);

				// NumCrossSellAtt2Recs
				temp = m_crossSellAtt2sFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_crossSellAtt2sFilename);
					m_crossSellAtt2sFile.Close();
					return 1;
				}
				m_numCrossSellAtt2Recs = Convert.ToInt32(tempSplit[1]);
			} // if version < 3.15

			// Read subsection header line with Att2ID, CrossSellAtt2ID, Likelihood etc.
			temp = m_crossSellAtt2sFile.ReadLine();

			return 0;
		}

		// FinishCrossSellAtt2sFile ///////////// 
		// Reads Cross Sell Att2s and closes file!
		protected int FinishCrossSellAtt2sFile()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string att2ID;
			bool isNew = true;
			int index;

			// == Read CrossSellAtt2s ==
			for (i = 0; i < m_numAtt2s; i++)
			{
				// Read Line - includes Att2ID and recommendations **
				temp = m_crossSellAtt2sFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of CrossSellAtt2s {0}. {1} items in CrossSellAtt2s, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_crossSellAtt2sFilename, i, m_numAtt2s);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				// Read Att2ID
				att2ID = tempSplit[0];
				// Create Index
				index = m_att2Index.Index(att2ID, ref isNew, 1);
				// Read NumIndexes
				m_att2Headers[index].numUsers = Convert.ToInt32(tempSplit[1]);
				// Read NumActions
				m_att2Headers[index].numActions = Convert.ToInt32(tempSplit[2]);
				// Read recs
				// Factor of 3 since 3 att2s per rec
				for (j = 0; j < m_numCrossSellAtt2Recs; j++)
				{
					m_crossSellAtt2s[index, j].alphaID = tempSplit[3 * j + 3];
					m_crossSellAtt2s[index, j].likelihood = Convert.ToSingle(tempSplit[3 * j + 4]);
					m_crossSellAtt2s[index, j].numCommon = Convert.ToInt32(tempSplit[3 * j + 5]);
					m_crossSellAtt2s[index, j].type = (byte)RecType.CrossSellAttToAtt;
				} // for j nNumCrossSellAtt2Recs
			} // for i m_nNumAtt2s

			// == Close File ==
			m_crossSellAtt2sFile.Close();

			return 0;
		}
		#endregion

		#region TopSellAtt2Items
		//////////////////////////////////////////////////////////////////
		// TopSellAtt2Items ////////////////////////////////////////////////
		// TopSellAtt2Item Members and Properties **
		int m_numTopSellAtt2ItemRecs = 0;
		StreamReader m_topSellAtt2ItemsFile = null;
		string m_topSellAtt2ItemsFilename = null;
		TopSellRec[,] m_topSellAtt2Items = null;

		public int NumTopSellAtt2ItemRecs
		{
			set { m_numTopSellAtt2ItemRecs = value; }
			get { return m_numTopSellAtt2ItemRecs; }
		}
		public string TopSellAtt2ItemsFilename
		{
			set { m_topSellAtt2ItemsFilename = value; }
			get { return m_topSellAtt2ItemsFilename; }
		}
		public TopSellRec[,] TopSellAtt2Items
		{
			set { m_topSellAtt2Items = value; }
			get { return m_topSellAtt2Items; }
		}

		// Load Att2 Items ///////
		public int ReadTopSellAtt2Items()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumItems and m_nNumTopSellAtt2ItemRecs are read from the header
			m_error = InitTopSellAtt2ItemsFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// TopSellAtt2Items **
			m_topSellAtt2Items = new TopSellRec[m_numAtt2s, m_numTopSellAtt2ItemRecs];
			if (m_topSellAtt2Items == null)
			{
				m_errorText = "Could not allocate memory for Att2 Items.\n";
				m_topSellAtt2ItemsFile.Close();
				return 1;
			}

			// Att2 Index **
			if (!m_att2Index.IsAllocated)
				m_att2Index.AllocIndex(m_numAtt2s); // (m_maxAtt2ID);

			// Read Att2 Items ////
			m_error = FinishTopSellAtt2ItemsFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			// If any items are missing - should not happen 
			for (int i = 0; i < m_numAtt2s; i++)
			{
				if (m_topSellAtt2Items[i, 0].alphaID == null)
					for (int j = 0; j < NumRecs; j++) // Sames as m_numTopSellAtt2ItemRecs
						m_topSellAtt2Items[i, j].Reset();
			}

			return 0;
		}

		// InitTopSellAtt2ItemsFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitTopSellAtt2ItemsFile()
		{
			int numAtt2s;
			int maxAtt2ID;
			string version;
			string temp;
			string[] tempSplit;

			// == Open Att2 Item File ==
			if (m_topSellAtt2ItemsFilename == null)
				m_topSellAtt2ItemsFilename = TopSellAtt2ItemsBasename;

			try
			{
				m_topSellAtt2ItemsFile = new StreamReader(m_clientDataPath + m_topSellAtt2ItemsFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_topSellAtt2ItemsFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_topSellAtt2ItemsFile.ReadLine();
			if (!version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_topSellAtt2ItemsFilename);
				m_topSellAtt2ItemsFile.Close();
				return 1;
			}

			m_numTopSellAtt2ItemRecs = m_numRecs;

			tempSplit = version.Split(m_tabSeparator);
			float topSellVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (topSellVersion < 3.15)
			{
				// NumAtt2s
				temp = m_topSellAtt2ItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numatts") != 0)
				{
					m_errorText = String.Format("Wrong header for NumAtts in {0}.",
							m_topSellAtt2ItemsFilename);
					m_topSellAtt2ItemsFile.Close();
					return 1;
				}
				numAtt2s = Convert.ToInt32(tempSplit[1]);
				if (m_numAtt2s != numAtt2s)
				{
					m_errorText = String.Format("{0} att2s in {1}, but {2} att2s in {3}.\n",
							numAtt2s, m_topSellAtt2ItemsFilename, m_numAtt2s, m_crossSellAtt2sFilename);
					m_topSellAtt2ItemsFile.Close();
					return 1;
				}

				// MaxAtt2ID
				temp = m_topSellAtt2ItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxID in {0}.",
							m_topSellAtt2ItemsFilename);
					m_topSellAtt2ItemsFile.Close();
					return 1;
				}
				maxAtt2ID = Convert.ToInt32(tempSplit[1]);
				if (m_maxAtt2ID != maxAtt2ID)
				{
					m_errorText = String.Format("{0} att2 IDs in {1}, but {2} att2 IDs in {3}.\n",
							maxAtt2ID, m_topSellAtt2ItemsFilename, m_maxAtt2ID, m_crossSellAtt2sFilename);
					m_topSellAtt2ItemsFile.Close();
					return 1;
				}

				m_numTopSellAtt2ItemRecs = m_numRecs;
				// NumTopSellAtt2ItemRecs
				temp = m_topSellAtt2ItemsFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_topSellAtt2ItemsFilename);
					m_topSellAtt2ItemsFile.Close();
					return 1;
				}
				m_numTopSellAtt2ItemRecs = Convert.ToInt32(tempSplit[1]);
			} // if version < 3.15

			// Read subsection header line with ItemID, TopSellAtt2ItemID, Likelihood etc.
			temp = m_topSellAtt2ItemsFile.ReadLine();

			return 0;
		}

		// FinishTopSellAtt2ItemsFile ///////////// 
		// Reads Att2 Items and closes file!
		protected int FinishTopSellAtt2ItemsFile()
		{
			int i, j;
			string temp;
			string[] tempSplit;
			string strAtt2ID;
			int att2Index;

			// == Read Att2 Items ==
			for (i = 0; i < m_numAtt2s; i++)
			{
				temp = m_topSellAtt2ItemsFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of TopSellAtt2 Items {0}. {1} items in TopSellAtt2, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_topSellAtt2ItemsFilename, i, m_numAtt2s);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				strAtt2ID = tempSplit[0];
				att2Index = m_att2Index.GetIndex(strAtt2ID);
				if (att2Index < 0) //TODO: seems like this should be an error condition?
					continue;

				// Use att2Index to place in Header and Data arrays
				// so match m_att2Name array
				// Different than TopSellerAtt2s, which includes att2ID since in order of numActions
				m_att2Headers[att2Index].numItems = Convert.ToInt32(tempSplit[1]);
				m_att2Headers[att2Index].numActions = Convert.ToInt32(tempSplit[2]);

				for (j = 0; j < m_numTopSellAtt2ItemRecs; j++)
				{
					m_topSellAtt2Items[att2Index, j].alphaID = tempSplit[2 * j + 3];
					m_topSellAtt2Items[att2Index, j].numActions = Convert.ToInt32(tempSplit[2 * j + 4]);
					m_topSellAtt2Items[att2Index, j].type = (byte)RecType.TopSell;
				} // for j nNumTopSellAtt2ItemRecs
			} // for i m_nNumAtt2s

			// == Close File ==
			m_topSellAtt2ItemsFile.Close();

			return 0;
		}
		#endregion // TopSellAtt2Items

		#region BundleAtt2s
		//////////////////////////////////////////////////////////////////
		// BundleAtt2s ////////////////////////////////////////////////
		// BundleAtt2s Members and Properies **
		int m_numBundleAtt2s = 0;
		StreamReader m_bundleAtt2sFile;
		string m_bundleAtt2sFilename = null;
		BundleRec[] m_bundleAtt2s = null;

		public int NumBundleAtt2s
		{
			set { m_numBundleAtt2s = value; }
			get { return m_numBundleAtt2s; }
		}
		public string BundleAtt2sFilename
		{
			set { m_bundleAtt2sFilename = value; }
			get { return m_bundleAtt2sFilename; }
		}
		public BundleRec[] BundleAtt2s
		{
			set { m_bundleAtt2s = value; }
			get { return m_bundleAtt2s; }
		}

		// Load Bundle Att2s ///////
		public int ReadBundleAtt2s()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumAtt2s and m_nNumBundleAtt2Recs are read from the header
			m_error = InitBundleAtt2sFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// BundleAtt2s **
			m_bundleAtt2s = new BundleRec[m_numBundleAtt2s];
			if (m_bundleAtt2s == null)
			{
				m_errorText = "Could not allocate memory for Bundle Att2s.\n";
				m_bundleAtt2sFile.Close();
				return 1;
			}

			// Read Bundle Att2s ////
			m_error = FinishBundleAtt2sFile(); // Reads cross sell att2s and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitBundleAtt2sFile 
		// Opens file and sets number of Att2s and recs so arrays can be allocated
		protected int InitBundleAtt2sFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Bundle Att2 File ==
			if (m_bundleAtt2sFilename == null)
				m_bundleAtt2sFilename = BundleAtt2sBasename;

			try
			{
				m_bundleAtt2sFile = new StreamReader(m_clientDataPath + m_bundleAtt2sFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_bundleAtt2sFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_bundleAtt2sFile.ReadLine();
			if (!version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_bundleAtt2sFilename);
				m_bundleAtt2sFile.Close();
				return 1;
			}

			m_numBundleAtt2s = m_numBundleRecs;
			tempSplit = version.Split(m_tabSeparator);
			float bundleVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (bundleVersion < 3.15)
			{
				// NumBundles
				temp = m_bundleAtt2sFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numbundles") != 0)
				{
					m_errorText = String.Format("Wrong header for NumBundles in {0}.",
							m_bundleAtt2sFilename);
					m_bundleAtt2sFile.Close();
					return 1;
				}
				m_numBundleAtt2s = Convert.ToInt32(tempSplit[1]);
			} // If bundle version is less than 3.15

			// Read subsection header line with Att2ID1, Att2ID2, Similarity
			temp = m_bundleAtt2sFile.ReadLine();

			return 0;
		}

		// FinishBundleAtt2sFile ///////////// 
		// Reads Bundle Att2s and closes file!
		protected int FinishBundleAtt2sFile()
		{
			int i;
			string temp;
			string[] tempSplit;

			// == Read Bundle Att2s ==
			for (i = 0; i < m_numBundleAtt2s; i++)
			{
				temp = m_bundleAtt2sFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_numBundleAtt2s = i;
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of BundleAtt2s {0}. {1} items in BundleAtt2s, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_bundleAtt2sFilename, i, m_numBundleAtt2s);
					break;
				} // EOF

				tempSplit = temp.Split(m_tabSeparator);
				// Index1
				m_bundleAtt2s[i].alphaID1 = tempSplit[0];
				// Index2
				m_bundleAtt2s[i].alphaID2 = tempSplit[1];
				// Similarity
				m_bundleAtt2s[i].similarity = Convert.ToSingle(tempSplit[2]);
				// NunCommon
				m_bundleAtt2s[i].numCommon = Convert.ToInt32(tempSplit[3]);
			} // for i m_nNumBundles

			// == Close File ==
			m_bundleAtt2sFile.Close();

			return 0;
		}
		#endregion

		#region TopSellerAtt2s
		//////////////////////////////////////////////////////////////////
		// TopSellerAtt2s ////////////////////////////////////////////////
		// TopSellerAtt2s Members and Properties **
		int m_numTopSellerAtt2Recs = 0;
		StreamReader m_topSellerAtt2sFile = null;
		string m_topSellerAtt2sFilename = null;
		TopSellerRec[] m_topSellerAtt2s = null;

		public string TopSellerAtt2sFilename
		{
			set { m_topSellerAtt2sFilename = value; }
			get { return m_topSellerAtt2sFilename; }
		}
		public TopSellerRec[] TopSellerAtt2s
		{
			set { m_topSellerAtt2s = value; }
			get { return m_topSellerAtt2s; }
		}
		public int NumTopSellerAtt2Recs
		{
			set { m_numTopSellerAtt2Recs = value; }
			get { return m_numTopSellerAtt2Recs; }
		}

		// Load Att2 Items ///////
		public int ReadTopSellerAtt2s()
		{
			m_warning = 0;
			// Read Recommendations Header /////
			// m_nNumItems and m_nNumTopSellerAtt2Recs are read from the header
			m_error = InitTopSellerAtt2sFile();
			if (m_error != 0) return m_error;

			// Allocate Arrays //////
			// TopSellerAtt2s **
			m_topSellerAtt2s = new TopSellerRec[m_numTopSellerAtt2Recs];
			if (m_topSellerAtt2s == null)
			{
				m_errorText = "Could not allocate memory for Att2 Top Sellers.\n";
				m_topSellerAtt2sFile.Close();
				return 1;
			}

			// Att2 Index **
			if (!m_att2Index.IsAllocated)
				m_att2Index.AllocIndex(m_numAtt2s); // (m_maxAtt2ID);

			// Read Att2 Items ////
			m_error = FinishTopSellerAtt2sFile(); // Reads cross sell items and closes file
			if (m_error != 0) return 1;

			return 0;
		}

		// InitTopSellerAtt2sFile 
		// Opens file and sets number of Items and recs so arrays can be allocated
		protected int InitTopSellerAtt2sFile()
		{
			string version;
			string temp;
			string[] tempSplit;

			// == Open Att2 Item File ==
			if (m_topSellerAtt2sFilename == null)
				m_topSellerAtt2sFilename = TopSellerAtt2sBasename;

			try
			{
				m_topSellerAtt2sFile = new StreamReader(m_clientDataPath + m_topSellerAtt2sFilename);
			}
			catch (Exception)
			{
				m_errorText = "Error: Could not open file " + m_topSellerAtt2sFilename + ".";
				return 2;
			}

			// == Read Header ==
			// Version
			version = m_topSellerAtt2sFile.ReadLine();
			if (!version.ToLower().StartsWith("version") )
			{
				m_errorText = String.Format("Wrong version in {0}.",
						m_topSellerAtt2sFilename);
				m_topSellerAtt2sFile.Close();
				return 1;
			}

			m_numTopSellerAtt2Recs = m_numTopSellerRecs;

			tempSplit = version.Split(m_tabSeparator);
			float topSellerVersion = (float) Convert.ToDouble(tempSplit[1]);

			if (topSellerVersion < 3.15)
			{
				// MaxAtt2ID
				temp = m_topSellerAtt2sFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("maxid") != 0)
				{
					m_errorText = String.Format("Wrong header for MaxID in {0}.",
							m_topSellerAtt2sFilename);
					m_topSellerAtt2sFile.Close();
					return 1;
				}
				if (m_maxAtt2ID != Convert.ToInt32(tempSplit[1]))
				{
					m_errorText = String.Format("Wrong maximum att2 ID in header for {0}.",
							m_topSellerAtt2sFilename);
					m_topSellerAtt2sFile.Close();
					return 1;
				}

				// NumTopSellerAtt2Recs
				temp = m_topSellerAtt2sFile.ReadLine();
				tempSplit = temp.Split(m_tabSeparator);
				if (tempSplit[0].ToLower().CompareTo("numrecommendations") != 0)
				{
					m_errorText = String.Format("Wrong header for NumRecommendations in {0}.",
							m_topSellerAtt2sFilename);
					m_topSellerAtt2sFile.Close();
					return 1;
				}
				m_numTopSellerAtt2Recs = Convert.ToInt32(tempSplit[1]);
			} // if version < 3.15

			// Read subsection header line with ItemID, TopSellerAtt2ID, Likelihood etc.
			temp = m_topSellerAtt2sFile.ReadLine();

			return 0;
		}

		// FinishTopSellerAtt2sFile ///////////// 
		// Reads Att2 Items and closes file!
		protected int FinishTopSellerAtt2sFile()
		{
			int i;
			string temp;
			string[] tempSplit;

			// == Read Att2 Items ==
			for (i = 0; i < m_numTopSellerAtt2Recs; i++)
			{
				temp = m_topSellerAtt2sFile.ReadLine();
				if (temp == null)
				{
					// Graceful Degregation
					m_numTopSellerAtt2Recs = i;
					m_warning = 1;
					m_warningText = String.Format("Reached EOF of TopSellerAtt2s {0}. {1} items in TopSellerAtt2s, and {2} in RecStats. " +
					"Program will continue with fewer items.\n", m_topSellerAtt2sFilename, i, m_numTopSellerAtt2Recs);
					break;
				} // EOF

				// Fill data
				tempSplit = temp.Split(m_tabSeparator);
				m_topSellerAtt2s[i].alphaID = tempSplit[0];
				m_topSellerAtt2s[i].numItems = Convert.ToInt32(tempSplit[1]);
				m_topSellerAtt2s[i].numActions = Convert.ToInt32(tempSplit[2]);
				m_topSellerAtt2s[i].type = (byte)RecType.TopSellerAtt;
			} // for i m_nNumTopSellerAtt2Recs

			// == Close File ==
			m_topSellerAtt2sFile.Close();

			return 0;
		}
		#endregion // TopSellerAtt2s

	} // class FileIO
} // namespace IO