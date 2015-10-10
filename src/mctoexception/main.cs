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
// In the .MC file, the only customization is that you put a special
// ;//ExceptionName= tag in a message declaration to enable exception class
// generation and specify the name of the class.  An optional comma-delimited
// list of argument names can follow the class name:
//
// ;//ExceptionName=MyException,argument1,argument2,argumentN
//
// USAGE:
//
//	MCTOEXCEPTION [-unicode] [-include:"include.h"] inputfile.mc outputfile.h
//
//		-unicode		- Build for a project that defines _UNICODE
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
		/// Main application entry point
		/// </summary>
		/// <param name="cmdlineargs">Array of command line arguments</param>
		[STAThread]
        static void Main(string[] cmdlineargs)
        {
			List<string>	args;								// Command line arguments
			List<string>	switches = new List<string>();		// Command line switches
			bool			unicode = false;					// _UNICODE flag
			List<string>	includes = new List<string>();		// List of includes

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
					foreach (Message message in Messages.Load(args[0], unicode))
					{
						StringBuilder arguments = new StringBuilder();

						sw.WriteLine("// " + message.ClassName);
						sw.WriteLine("//");
						foreach (string line in message.MessageText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
							sw.WriteLine("// " + line);

						// struct CLASSNAME : public Exception
						// {
						sw.WriteLine("struct " + message.ClassName + " : public Exception");
						sw.WriteLine("{");

						//     explicit CLASSNAME([type] insert1, [type] insert2, [type] insert3) : Exception{insert1, insert2, insert3} {}
						sw.Write("\texplicit " + message.ClassName + "(");
						for (int index = 0; index < message.Arguments.Count; index++)
							arguments.Append(message.Arguments[index].Key + " " + message.Arguments[index].Value + ", ");

						sw.Write(arguments.ToString().TrimEnd(new char[] { ',', ' ' }));
						sw.Write(") : Exception{ ");
						arguments.Clear();
						arguments.Append(message.SymbolicName + ", ");
						for (int index = 0; index < message.Arguments.Count; index++) arguments.Append(message.Arguments[index].Value + ", ");
						sw.Write(arguments.ToString().TrimEnd(new char[] { ',', ' ' }));
						sw.WriteLine(" } {}");

						//     virtual ~CLASSNAME()=default;
						// };
						sw.WriteLine("\tvirtual ~" + message.ClassName + "()=default;");
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
