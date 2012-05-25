/////////////////////////////////////////////////////////////////////
// Analyze
//
// ROLE: Hash based Index Array
//
// USAGE: Call Index(), Insert() and GetIndex()
// Get..Index.. calls are always the fastest since use m_lookup!!!
//
// by Ken Levy 
// Originated: 1/9/2010
// Edits:
//  . date - comments
//
//////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace _4_Tell.IO
{
	#region Global Namespace Structures
	public class IndexData
	{
		public string alphaID;
		public int index;
		public int numActions; // How many times in this ID used
		public int numIndexes; // How many different indexes use this ID,
		// If product ID, then it's how many different customers acted on it
		// If customer ID, then how many different products did they act upon
		public IndexData next;

		public IndexData()
		{
			Reset();
		}

		public void CopyDataElement(string inAlphaID, int inIndex, int count)
		{
			alphaID = inAlphaID;
			index = inIndex;
			if (count != 0)
			{
				numActions = count;
				numIndexes = 1;
			}
			next = null;
		}

		// Update
		public void Update(int count)
		{
			numActions += count;
			numIndexes++;
		}

		public void Reset()
		{
			alphaID = ""; ;
			index = -1;
			numActions = 0;
			numIndexes = 0;
			next = null;
		}
	}

	public struct LookupData
	{
		public string alphaID;
		public int numActions; // How many times in this ID used
		public int numIndexes; // How many different indexes use this ID

		public void CopyDataElement(string inAlphaID, int inNumActions, int inNumIndexes)
		{
			alphaID = inAlphaID;
			numActions = inNumActions;
			numIndexes = inNumIndexes;
		}
	}
	#endregion Global Structures

	// Class
	public class CIndex
	{
		#region Global Class Variables
		// Variables
		const uint NumHashElements = 50000;
		const uint MaxHashElements = 500000;
		uint m_numHashElements;
		IndexData [] m_index;
		LookupData [] m_lookup;
		int m_indexPos; // Current index position = number of IDs in index, since 1 greater than last entry
		int m_nextID; // Index to look for next item or user
		IndexData m_nextIndexData;
		int m_nextIndex; // for GetNextIndex
		int m_maxActions; // Maximum actions for all indexes
		int m_maxIndexes; // Maximum indexes for all indexes
		string m_errorText;
		int m_numCollisions;

		protected bool isAllocated;
		public bool IsAllocated
		{
			get { return isAllocated; }
		}
		#endregion

		#region Construction
		public CIndex()	
		{
			// Initialize Variable
			isAllocated = false;
			m_numHashElements = NumHashElements; // default
			m_indexPos = 0;
			m_maxActions = 0;
			m_maxIndexes = 0;
			m_numCollisions = 0;
			m_lookup = null;
			m_index = null;
			ResetNextID();
			ResetNextIndex();
		}
		#endregion

		#region Private Hash Tools
		// Private Hash Tools ///////////////////
		void ResetIndex()
		{
			for (int i=0; i<m_numHashElements; i++)
				m_index[i].Reset();
		}

		uint hash(ref string alphaIDOrig)
		{
			string alphaID;
			uint hash;
			int len;
			int i;

			// Remove leading and trailing spaces
 			 // Already removed in ItemDetails and GetRec calls, but could be in DoNotRecommend
			// DO NOT remove internal spaces since makes the ID unintelligible by the client
			alphaIDOrig = alphaIDOrig.Trim('"'); // Passed as reference, trims spaces to be saved in index for GetID(), but does not affect main program
			alphaIDOrig = alphaIDOrig.Trim(); // Passed as reference, trims spaces to be saved in index for GetID(), but does not affect main program
			
			alphaID = alphaIDOrig.ToLower(); // NOT saved in index for GetID()
			len = alphaID.Length;

			// If alphaID is numeric, use number as hash
			//bool success;
			//for (i = 0; i < len; i++)
			//{
			//  if (!Char.IsDigit(alphaID, i))
			//    break;
			//}
			//if (i == len)
			//{
			//  success = uint.TryParse(alphaID, out hash);
			//  if (success)
			//    return hash % m_numHashElements;
			//}

			// If alphaID is "" return 0
			if (alphaID == "")
				return 0;

			// Otherwise create hash
			hash = alphaID[0];
			for (i = 0; i < len; i++)
				hash = (uint)( (hash << (1 + i % 4)) + (alphaID[i] * (1 + (i << 3)) % 80) );
			//hash = (uint)( (hash << (1 + i % 3)) + (alphaID[i] * (1 + i % 10)) );
			hash = hash % m_numHashElements;
			return hash;
		}
		#endregion

		#region Allocation
		// AllocIndex ////////////
		// Override to show that maxID is optional
		public int AllocIndex()
		{
			return AllocIndex(0);
		}

		public int AllocIndex(int numObjects)
		{

			if (numObjects > MaxHashElements)
				m_numHashElements = MaxHashElements * 10;
			else if (numObjects > 0)
				m_numHashElements = (uint) numObjects * 10;
			else
				m_numHashElements = NumHashElements;

			m_index = new IndexData[m_numHashElements];
			for (int i = 0; i < m_numHashElements; i++)
				m_index[i] = new IndexData(); // Implicitly resets variables

			// Initialize Variable
			m_numCollisions = 0;
			m_indexPos = 0;
			m_maxActions = 0;
			m_maxIndexes = 0;
			m_lookup = null;
			ResetNextID();
			ResetNextIndex();
			ResetIndex();

			// isAllocated
			isAllocated = true;

			return 0;
		}

		// Create Reverse Lookup
		public int CreateLookup()
		{
			int i;
			int index = 0;
			string ID = "";
			int numActions = 0;
			int numIndexes = 0;

			// Delete if exists, so can call numerous times
			ResetNextID();

			// Allocate memory
			m_lookup = new LookupData[m_indexPos];
			if (m_lookup == null)
				return 1;

			// Fill
			for (i=0; i<m_indexPos; i++)
			{
				GetNextID(ref index, ref ID, ref numActions, ref numIndexes);
				m_lookup[index].CopyDataElement(ID, numActions, numIndexes);
			}
			if (GetNextID(ref index, ref ID))
			{
				m_errorText = "\nERROR: Wrong number of indexes.\n\n";
				return 1;
			}
			ResetNextID();

			return 0;
		}
		#endregion

		#region Process Index
		// Index /////////////////
		public int Index(string alphaID, ref bool isNew, int count)
		{
			uint ID;
			IndexData pIndex;

			if (alphaID.Length < 1) //error condition: need to check response for < 0
				return -1;
				
			ID = hash(ref alphaID);

			// Check to see if exist, if so return index
				// if not, add and return index
			pIndex = m_index[ID];
			isNew = true;
			do 
			{
				if ( pIndex.index >= 0 && CompareNoCase(pIndex.alphaID, alphaID) )
				{
					if (count != 0) pIndex.Update(count);
					isNew = false;
					break;
				}
				else
					pIndex = pIndex.next;
			} while (pIndex != null);
			
			if (isNew)
				pIndex = Insert(alphaID, ID, count);
			
			if (pIndex.numActions > m_maxActions)
				m_maxActions = pIndex.numActions;
			
			if (pIndex.numIndexes > m_maxIndexes)
				m_maxIndexes = pIndex.numIndexes;

			return pIndex.index;
		} 

		bool CompareNoCase(string alphaIDFromIndex, string alphaID)
		{
			return ( alphaID.ToLower().Equals( alphaIDFromIndex.ToLower() ) );
		}

		// Insert //////////////
		public int Insert(string alphaID, int count)
		{
			IndexData pIndex;
			uint ID;

			ID = hash(ref alphaID);
			pIndex = Insert(alphaID, ID, count);

			if (pIndex.numActions > m_maxActions)
			m_maxActions = pIndex.numActions;
			
			if (pIndex.numIndexes > m_maxIndexes)
				m_maxIndexes = pIndex.numIndexes;

			return pIndex.index;
		}

		IndexData Insert(string alphaID, uint ID, int count)
		{
			IndexData pIndex;

			pIndex = m_index[ID];
			do 
			{
				if (pIndex.index < 0)
					break;
				else if (pIndex.index >= 0 && pIndex.next == null)
				{
					pIndex.next = new IndexData();
					pIndex = pIndex.next;
					m_numCollisions++;
				}
				else // (pIndex.index >=0 && pIndex.next != null)
					pIndex = pIndex.next;
			} while(true);

			pIndex.CopyDataElement(alphaID, m_indexPos, count);
			m_indexPos++; // increment after assign

			return pIndex;
		}

		// Get Index or ID //////////////////////
		public int GetIndex(string alphaID)
		{
			IndexData pIndex = null;
			GetIndex(alphaID, ref pIndex);

			if (pIndex != null)
				return pIndex.index;
			else
				return -1;
		}

		public void GetIndex(string alphaID, ref IndexData pIndex)
		{
			uint ID;

			ID = hash(ref alphaID);
			pIndex = m_index[ID];
			do 
			{
				if ( pIndex.index >= 0 && CompareNoCase(pIndex.alphaID, alphaID) ) 
					return;
				else
					pIndex = pIndex.next;
			} while (pIndex != null);
			
			pIndex = null;
			return;
		}

		public string GetID(int index) // returns index if know that is in index, faster than Index
		{
			// if reverse lookup is not created, create it
			if (m_lookup == null)
				CreateLookup();

			return m_lookup[index].alphaID;
		}

		public int GetIndexPos() // Return current position = number IDs in index
		{
			return m_indexPos;
		}
		#endregion

		#region GetNext
		// GetNext ///////////////////
		public bool GetNextID(ref int index, ref string alphaID)
		{
			int numActions = 0;
			int numIndexes = 0; // Just place holders, as use GetNextID to fill m_lookup
			return GetNextID(ref index, ref alphaID, ref numActions, ref numIndexes); 
		}

		public bool GetNextID(ref int index, ref string alphaID, ref int numActions, ref int numIndexes)
		{
			bool bIsAnother = false; // Changed to true if  exists

			// If nextIndex is still pointing in the link list, use it
			if (m_nextIndexData != null)
			{
				index = m_nextIndexData.index;
				alphaID = m_nextIndexData.alphaID;
				numActions = m_nextIndexData.numActions;
				numIndexes = m_nextIndexData.numIndexes;
				m_nextIndexData = m_nextIndexData.next;
				return true;
			}

			// When nothing linked, go to next element with something
			while (++m_nextID < m_numHashElements) 
			{
				if ( m_index[m_nextID].index < 0) continue;
				index = m_index[m_nextID].index;
				alphaID = m_index[m_nextID].alphaID;
				numActions = m_index[m_nextID].numActions;
				numIndexes = m_index[m_nextID].numIndexes;
				m_nextIndexData = m_index[m_nextID].next; // if null, will go to nextID, if not, will use above
				bIsAnother = true;
				break;
			}

			return bIsAnother;
		}

		public void ResetNextID()
		{
			m_nextID=-1; // since incremented first
			m_nextIndexData = null;
		}

		public bool GetNextIndex(ref int index, ref string alphaID)
		{
			// if reverse lookup is not created, create it
			if (m_lookup == null)
				CreateLookup();

			bool bIsAnother = false; // Changed to true if  exists

			while (++m_nextIndex < m_indexPos) 
			{
				index = m_nextIndex;
				alphaID = m_lookup[m_nextIndex].alphaID;
				bIsAnother = true;
				break;
			}

			return bIsAnother;	
		}

		public void ResetNextIndex()
		{
			m_nextIndex = -1;
		}
		#endregion

		#region Get and Set Stuff
		// Get and Set Stuff //////////////////
		public int GetIDActions(string alphaID)
		{
			IndexData pIndex = null;
			GetIndex(alphaID, ref pIndex);
			if (pIndex != null)
				return pIndex.numActions; 
			else 
				return -1;
		}

		public int GetMaxActions() // Return max actions for one index, used to create temp arrays to hold data
		{
			return m_maxActions;
		}

		public int GetIDIndexes(string alphaID)
		{
			IndexData pIndex = null;
			GetIndex(alphaID, ref pIndex);
			if (pIndex != null)
				return pIndex.numIndexes;
			else
				return -1;
		}

		public int GetMaxIndexes() // Return max actions for one index, used to create temp arrays to hold data
		{
			return m_maxIndexes;
		}

		public int GetIndexActions(int index)
		{
			// if reverse lookup is not created, create it
			if (m_lookup == null)
				CreateLookup();
			return m_lookup[index].numActions;
		}

		public void SetIndexIndexes(int index, int numIndexes)
		{
			IndexData pIndex = null;

			// Set in reverse lookup\
			 // Know that exists since would have to call for GetID below
			if (m_lookup == null)
				CreateLookup();
			m_lookup[index].numIndexes = numIndexes;
			
			// Set in original lookup
			GetIndex(GetID(index), ref pIndex);
			if (pIndex != null)
				pIndex.numIndexes = numIndexes; 
		}

		public int GetIndexIndexes(int index)
		{
			// if reverse lookup is not created, create it
			if (m_lookup == null)
				CreateLookup();
			return m_lookup[index].numIndexes;
		}

		// TotalCount /////////////////////////////////////
		public int GetTotalActions()
		{
			int index = 0;
			int totalActions = 0;
			string ID = "";

			if (m_lookup == null)
				CreateLookup();

			totalActions = 0;
			ResetNextIndex();
			while (GetNextIndex(ref index, ref ID))
			{
				totalActions += GetIndexActions(index);
			} 
			ResetNextIndex();

			return totalActions;
		}

		// Clear /////////////////////////////////////
		public int Clear()
		{
			// Variables
			m_numCollisions = 0;
			m_indexPos = 0;
			m_maxActions = 0;
			m_maxIndexes = 0;

			// Initialize Variable
			ResetNextID();
			ResetNextIndex();
			ResetIndex();

			return 0;
		}
		
		public string GetError()
		{
			return m_errorText;
		}

		public int GetCollisions()
		{
			return m_numCollisions;
		}
		#endregion

	} // CIndex Class
} // namespace