//-------------------------------------------------------------------------------
// mctoexception
//
// The use and distribution terms for this software are covered by the
// Common Public License 1.0 (http://opensource.org/licenses/cpl.php)
// which can be found in the file CPL.TXT at the root of this distribution.
// By using this software in any fashion, you are agreeing to be bound by
// the terms of this license. You must not remove this notice, or any other,
// from this software.
//
// Contributor(s):
//	Michael G. Brehm (original author)
//
// This tool takes an input .MC message file and converts the messages into
// throwable Exception classes.  Exception is a custom class that as of this
// writing is in the "vm" project only, but could be extracted out and put
// as an output from this tool as well.
//
// In the .MC file, the only customization is that you can put a special
// ;//ExceptionName= tag in a message declaration to control the name of the
// exception class that is generated.  Otherwise it will default to a really
// ugly name of Facility+SymbolicName+Exception, for example:
//
//  Facility=Test
//	SymbolicName=E_EPICFAIL
//
//	--> struct TestE_EPICFAILException : public Exception {}
//
// USAGE:
//
//	MCTOEXCEPTION [-unicode] [-namedonly] [-include:"include.h"] inputfile.mc outputfile.h
//
//		-unicode		- Build for a project that defines _UNICODE
//		-namedonly		- Only include entries that have custom ;//ExceptionName= tag
//		-include:		- Add an #include into the file (can specify multiple times)
//		inputfile.mc	- Input .MC file to process
//		outputfile.h	- Output C++ header file
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace zuki.build
{
    static class main
	{
		/// <summary>
		/// Converts an insertion string into a C++ data type argument
		/// </summary>
		/// <param name="insertion">Insertion string format</param>
		/// <param name="unicode">Project _UNICODE flag</param>
		/// <returns></returns>
		static string InsertionToType(string insertion, bool unicode)
		{
			switch (insertion)
			{
				case "c": return "wchar_t";
				case "C": return "wchar_t";
				case "d": return "int";
				case "hc": return "wchar_t";
				case "hC": return "wchar_t";
				case "hd": return "short";
				case "hs": return "char const*";
				case "hS": return "char const*";
				case "hu": return "unsigned short";
				case "i": return "int";
				case "lc": return "wchar_t";
				case "lC": return "wchar_t";
				case "ld": return "long";
				case "li": return "long";
				case "ls": return "wchar_t const*";
				case "lS": return "wchar_t const*";
				case "lu": return "unsigned long";
				case "lx": return "unsigned long";
				case "lX": return "unsigned long";
				case "p": return "void const*";
				case "s": return (unicode) ? "wchar_t const*" : "char const*";
				case "S": return (unicode) ? "char const*" : "wchar_t const*";
				case "u": return "unsigned int";
				case "x": return "unsigned int";
				case "X": return "unsigned int";
			}

			// todo: still need to handle all the tokens wsprintf recognizes (-, #, precision, width, etc) -- this will need to 
			// return an array/List<> of the types required to account for that, since precision and width can consume insertions (with *)
			throw new Exception("Unrecognized MC file insertion string format [" + insertion + "]");
		}

		/// <summary>
		/// Main application entry point
		/// </summary>
		/// <param name="cmdlineargs">Array of command line arguments</param>
		[STAThread]
        static void Main(string[] cmdlineargs)
        {
			List<string>	args;							// Command line arguments
			List<string>	switches = new List<string>();  // Command line switches
			bool			unicode = false;				// _UNICODE flag
			bool			namedonly = false;              // Only include named messages
			List<string>	includes = new List<string>();	// List of includes

			args = new List<String>(cmdlineargs);				// Move into a List<>

			// Remove all switches from the argument list for separate processing
			int switchindex = 0;
			while(switchindex < args.Count)
			{
				if (args[switchindex].StartsWith("/") || args[switchindex].StartsWith("-"))
				{
					if(args[switchindex].Length > 1) switches.Add(args[switchindex].Substring(1));
					args.RemoveAt(switchindex);
				}
				else switchindex++;
			}

			// Process command line switches
			foreach(string switcharg in switches)
			{
				string sw = switcharg.ToLower();

				// unicode -- set _UNICODE mode
				if (sw == "unicode") unicode = true;

				// namedonly -- set named-only mode
				else if (sw == "namedonly") namedonly = true;

				// include -- add an include
				else if (sw.StartsWith("include:"))
				{
					if(sw.Length > 8) includes.Add(sw.Substring(8));
				}
			}

			if (args.Count < 2)
			{
				Console.WriteLine("Invalid command line arguments.  Please see documentation.");
				return;
			}

			try
			{
				string outdir = Path.GetDirectoryName(args[1]);
				if (!Directory.Exists(outdir)) Directory.CreateDirectory(outdir);

				if (File.Exists(args[1])) File.SetAttributes(args[1], FileAttributes.Normal);
				using (StreamWriter sw = File.CreateText(args[1]))
				{
					string headername = Path.GetFileNameWithoutExtension(args[1]).ToUpper();

					// HEADER
					//
					sw.WriteLine("#ifndef __AUTOGEN_" + headername + "_H_");
					sw.WriteLine("#define __AUTOGEN_" + headername + "_H_");
					sw.WriteLine();
					sw.WriteLine("#include \"Exception.h\"");
					foreach (string include in includes) sw.WriteLine("#include " + include);
					sw.WriteLine();
					sw.WriteLine("#pragma warning(push, 4)");
					sw.WriteLine();

					// _UNICODE
					//
					// The data types for some exception insertions require knowledge of if the project has _UNICODE
					// defined or not, bad things can happen when passing the wrong string types into FormatMessage
					if (unicode)
					{
						sw.WriteLine("#ifndef _UNICODE");
						sw.WriteLine("#error Auto-generated exception classes require _UNICODE to be defined for this project");
						sw.WriteLine("#endif");
					}
					else
					{
						sw.WriteLine("#ifdef _UNICODE");
						sw.WriteLine("#error Auto-generated exception classes require _UNICODE is not defined for this project");
						sw.WriteLine("#endif");
					}
					sw.WriteLine();

					// EXCEPTION CLASSES
					//
					foreach (Message message in Messages.Load(args[0]))
					{
						// When -namedonly has been specified, don't generate exception classes for anything
						// that didn't have the custom ;//ExceptionName= tag applied to it in the source file
						if ((namedonly) && (String.IsNullOrEmpty(message.ExceptionName))) continue;

						StringBuilder arguments = new StringBuilder();

						// Generate a unique classname if none was specified
						string classname = message.ExceptionName;
						if (String.IsNullOrEmpty(classname)) classname = message.Facility + message.SymbolicName + "Exception";

						sw.WriteLine("// " + classname);
						sw.WriteLine("//");
						foreach (string line in message.MessageText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
							sw.WriteLine("// " + line);

						// struct CLASSNAME : public Exception
						// {
						sw.WriteLine("struct " + classname + " : public Exception");
						sw.WriteLine("{");

						//     explicit CLASSNAME([type] insert1, [type] insert2, [type] insert3) : Exception{insert1, insert2, insert3} {}
						sw.Write("\texplicit " + classname + "(");
						for (int index = 0; index < message.InsertionTypes.Count; index++)
						{
							arguments.Append(InsertionToType(message.InsertionTypes[index], unicode) + " ");
							arguments.Append("insert" + (index + 1).ToString() + ", ");
						}
						sw.Write(arguments.ToString().TrimEnd(new char[] { ',', ' ' }));
						sw.Write(") : Exception{ ");
						arguments.Clear();
						arguments.Append(message.SymbolicName + ", ");
						for (int index = 0; index < message.InsertionTypes.Count; index++) arguments.Append("insert" + (index + 1).ToString() + ", ");
						sw.Write(arguments.ToString().TrimEnd(new char[] { ',', ' ' }));
						sw.WriteLine(" } {}");

						//     virtual ~CLASSNAME()=default;
						// };
						sw.WriteLine("\tvirtual ~" + classname + "()=default;");
						sw.WriteLine("};");
						sw.WriteLine();
					}

					sw.WriteLine("#pragma warning(pop)");
					sw.WriteLine();
					sw.WriteLine("#endif	// __AUTOGEN_" + headername + "_H_");

					sw.Flush();
				}

				Console.WriteLine("mctoexception: " + args[0] + " --> " + args[1]);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception: " + ex.Message);
				return;
			}
		}
	}
}
