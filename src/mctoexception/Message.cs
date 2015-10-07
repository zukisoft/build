//-----------------------------------------------------------------------------
// Copyright (c) 2015 Michael G. Brehm
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace zuki.build
{
	//-------------------------------------------------------------------------
	// Class Message
	//
	// Holds and processes individual message information extracted from the file
	//-------------------------------------------------------------------------

	class Message
	{
		// Instance Constructor
		//
		public Message(string facility, string symbolicname, string exceptionname, string messagetext)
		{
			m_facility = facility;
			m_symbolicname = symbolicname;
			m_message = messagetext;
			m_exceptionname = exceptionname;

			// Collection of insertion data types
			List<string> insertions = new List<string>();
			int lastindex = 0;

			// Parse the insertions of the message text using a regular expression
			foreach (Match match in Regex.Matches(m_message, @"%(?<index>[1-9]?[0-9])(!(?<formatspecifier>\w*)!)?"))
			{
				// Extract the index and format specifier, if there is no format it defaults to "s"
				int index = Int32.Parse(match.Groups["index"].Value);
				string formatspecifier = match.Groups["formatspecifier"].Value;
				if (String.IsNullOrEmpty(formatspecifier)) formatspecifier = "s";

				// In the event there is a missing index, add a String type in there as a placeholder
				while (index > (lastindex + 1))
				{
					insertions.Add("s");
					lastindex++;
				}

				// Determine the .NET data type for the insertion and store it
				insertions.Add(formatspecifier);
				lastindex = index;
			}

			// Store the insertion types as a read-only collection
			m_insertions = insertions.AsReadOnly();
        }

		//---------------------------------------------------------------------
		// Member Functions
		//---------------------------------------------------------------------

		// FormatToType (private, static)
		//
		// Converts a wsprintf format specifier into a .NET data type
		private static System.Type FormatToType(string format)
		{
			switch (format)
			{
				default: return typeof(String);
			}
		}

		//---------------------------------------------------------------------
		// Properties
		//---------------------------------------------------------------------

		// ExceptionName
		//
		// Gets the exception class name
		public string ExceptionName
		{
			get { return m_exceptionname; }
		}

		// Facility
		//
		// Gets the collected facility name
		public string Facility
		{
			get { return m_facility; }
		}

		// InsertionTypes
		//
		// Gets the collection of insertion string data types
		public ReadOnlyCollection<string> InsertionTypes
		{
			get { return m_insertions; }
		}

		// MessageText
		//
		// Gets the collected message text
		public string MessageText
		{
			get { return m_message; }
		}

		// SymbolicName
		//
		// Gets the collected symbolic name
		public string SymbolicName
		{
			get { return m_symbolicname; }
		}

		//---------------------------------------------------------------------
		// Member Variables
		//---------------------------------------------------------------------

		private string						m_facility;			// Facility name
		private string						m_symbolicname;     // Symbolic name
		private string						m_message;          // Message text
		private string						m_exceptionname;	// Exception name
		private ReadOnlyCollection<string>	m_insertions;		// Insertion types
	}
}
