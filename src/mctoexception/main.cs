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
		/// Shows command line usage information
		/// </summary>
		static void ShowUsage()
		{
			Console.WriteLine(Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName).ToUpper() + " [-unicode] [-include:\"include.h\"] inputfile.mc outputfile.h");
			Console.WriteLine();
			Console.WriteLine("  -unicode      - Generate for a project that will define _UNICODE");
			Console.WriteLine("  -include:     - Add an #include directive for the specified file(s)");
			Console.WriteLine("  inputfile.mc  - Input message compiler .MC text file");
			Console.WriteLine("  outputfile.h  - Output C++ header file");
			Console.WriteLine();
		}

		/// <summary>
		/// Main application entry point
		/// </summary>
		/// <param name="cmdlineargs">Array of command line arguments</param>
		[STAThread]
		static void Main(string[] cmdlineargs)
		{
			List<string> args;                              // Command line arguments
			List<string> switches = new List<string>();     // Command line switches
			bool unicode = false;							// _UNICODE flag
			List<string> includes = new List<string>();     // List of includes

			args = new List<String>(cmdlineargs);           // Move into a List<>

			// Remove all switches from the argument list for separate processing
			int switchindex = 0;
			while (switchindex < args.Count)
			{
				if (args[switchindex].StartsWith("/") || args[switchindex].StartsWith("-"))
				{
					if (args[switchindex].Length > 1) switches.Add(args[switchindex].Substring(1));
					args.RemoveAt(switchindex);
				}
				else switchindex++;
			}

			// Process command line switches
			foreach (string switcharg in switches)
			{
				string sw = switcharg.ToLower();

				// unicode -- set _UNICODE mode
				if (sw == "unicode") unicode = true;

				// include -- add an include
				else if (sw.StartsWith("include:"))
				{
					if (sw.Length > 8) includes.Add(sw.Substring(8));
				}
			}

			// Insufficient number of arguments
			if (args.Count < 2) { ShowUsage(); return; }

			try
			{
				Console.WriteLine(Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName).ToUpper());
				Console.WriteLine();

				Console.WriteLine("Input File  : " + args[0]);

				// Create a MessageExceptions runtime text template for the output
				Messages messages = Messages.Load(args[0], unicode);
                MessageExceptions output = new MessageExceptions(Path.GetFileNameWithoutExtension(args[1]), messages, includes, unicode);

				// Get the output directory name and ensure that it exists before continuing
				string outdir = Path.GetDirectoryName(args[1]);
				if (!Directory.Exists(outdir)) Directory.CreateDirectory(outdir);

				// Force the output file to Normal attributes if it exists, and delete it
				if (File.Exists(args[1])) { File.SetAttributes(args[1], FileAttributes.Normal); File.Delete(args[1]); }

				Console.WriteLine("Output File : " + args[1]);
				Console.WriteLine();

				// Transform the T4 text template into the specified output file
				using (StreamWriter sw = File.CreateText(args[1]))
				{
					sw.Write(output.TransformText());
					sw.Flush();
				}

				Console.WriteLine("Successfully generated " + messages.Count.ToString() + " custom exceptions");
			}

			catch (Exception ex)
			{
				Console.WriteLine();
				Console.WriteLine("ERROR: " + ex.Message);
			}
		}
	}
}
