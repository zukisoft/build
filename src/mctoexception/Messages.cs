﻿//-----------------------------------------------------------------------------
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
using System.IO;
using System.Text;

namespace zuki.build
{
	//-------------------------------------------------------------------------
	// Class Messages
	//
	// Collection of Message objects, loaded from an input MC text file
	//-------------------------------------------------------------------------

	class Messages : ReadOnlyCollection<Message>
	{
		// Instance Constructor (private)
		//
		private Messages(List<Message> messages) : base(messages.AsReadOnly())
		{
		}

		//---------------------------------------------------------------------
		// Member Functions
		//---------------------------------------------------------------------

		//---------------------------------------------------------------------
		// Load (static)
		//
		// Creates a new MessageFile instance by parsing an input .MC text file
		//
		// Arguments:
		//
		//	path		- Path to the MC text file to proces
		public static Messages Load(string path)
		{
			// List<> to pass to the MessageFile constructor
			List<Message> messages = new List<Message>();

			// Attempt to open the provided path as a normal text file
			using (StreamReader sr = File.OpenText(path))
			{
				string lastFacility = String.Empty;			// Last seen facility name

				string nextline = sr.ReadLine();
				while (nextline != null)
				{
					if (nextline.TrimStart().StartsWith("MessageId=", StringComparison.OrdinalIgnoreCase))
					{
						string facility = lastFacility;
						string symbolicname = String.Empty;
						string exceptionname = String.Empty;
						StringBuilder messagetext = new StringBuilder();

						// Inside a message declaration, process it.  The MessageId= line itself is ignored,
						// so start with the next line of text
						nextline = sr.ReadLine();
						while (nextline != null)
						{
							nextline = nextline.TrimStart();

							// ;//EXCEPTIONNAME=
							//
							// Custom tag to use to force a specific exception class name
							if (nextline.StartsWith(";//ExceptionName=", StringComparison.OrdinalIgnoreCase))
							{
								if (nextline.Length > 17) exceptionname = nextline.Substring(17);
								nextline = sr.ReadLine();
							}

							// SEVERITY=
							//
							// Ignored
							if (nextline.StartsWith("Severity=", StringComparison.OrdinalIgnoreCase))
							{
								nextline = sr.ReadLine();
							}

							// FACILITY=
							//
							// Set the facility for the current message and store as the last seen facility
							else if (nextline.StartsWith("Facility=", StringComparison.OrdinalIgnoreCase))
							{
								if (nextline.Length > 9)
									facility = lastFacility = nextline.Substring(9).TrimEnd();
								
								nextline = sr.ReadLine();
							}

							// SYMBOLICNAME=
							//
							// Set the symbolic name for the current message
							else if (nextline.StartsWith("SymbolicName=", StringComparison.OrdinalIgnoreCase))
							{
								if (nextline.Length > 13)
									symbolicname = nextline.Substring(13).TrimEnd();
								
								nextline = sr.ReadLine();
							}

							// OUTPUTBASE=
							//
							// Ignored
							else if (nextline.StartsWith("OutputBase=", StringComparison.OrdinalIgnoreCase))
							{
								nextline = sr.ReadLine();
							}

							// LANGUAGE=
							//
							// Ignored
							else if (nextline.StartsWith("Language=", StringComparison.OrdinalIgnoreCase))
							{
								nextline = sr.ReadLine();
							}

							// MESSAGE TEXT
							//
							// Read until single . line of text encountered
							else
							{
								messagetext.AppendLine(nextline);
								nextline = sr.ReadLine();
								while ((nextline != null) && (!nextline.TrimStart().StartsWith(".")))
								{
									messagetext.AppendLine(nextline);
									nextline = sr.ReadLine();
								}

								// If the required message information was collected, add it to the List<>
								if (!String.IsNullOrEmpty(facility) && !String.IsNullOrEmpty(symbolicname) && (messagetext.Length > 0))
									messages.Add(new Message(facility, symbolicname, exceptionname, messagetext.ToString()));

								break;
							}
						}
					}

					nextline = sr.ReadLine();
				}
			}

			// Pass the List<> instance to the MessageFile constructor
			return new Messages(messages);
		}
	}
}