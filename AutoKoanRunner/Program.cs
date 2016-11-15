using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using AutoKoanRunner.Core;

namespace AutoKoanRunner
{
	class Program
	{
		private static readonly string koansRunner = @"KoanRunner\bin\debug\koanrunner.exe";
		private static DateTime _LastChange;
		private static Analysis _Prior = new Analysis();

		static void Main(string[] args)
		{
			if (!Array.TrueForAll(KoanSource.Sources, source => Directory.Exists(source.SourceFolder)))
			{
				Console.WriteLine("The Koans are not where we expected them to be.");
				Console.WriteLine("They are in '{0}' instead.", string.Join("' and '", KoanSource.Sources.Select(source => source.SourceFolder)));
				return;
			}
			var watchers = Array.ConvertAll(
				KoanSource.Sources,
				source => new FileSystemWatcher(source.SourceFolder, "*" + source.Extension));
			try
			{
                //initially build entire sln
			    BuildProject(null);

                Array.ForEach(watchers, w =>
				{
                    w.Changed += StartRunner;
                    w.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
					w.EnableRaisingEvents = true;
				});


				//Auto run the first time
				Array.ForEach(KoanSource.Sources, s =>
				{
					StartRunner(null, new FileSystemEventArgs(WatcherChangeTypes.Changed, s.SourceFolder, s.Extension));
					ResetLastRunData();
				});

				Console.WriteLine("When you save a Koan, the Master will again ponder your work.");
				Console.WriteLine("Press a key to exit...");
				Console.WriteLine();
				Console.ReadKey();
			}
			finally
			{
				Array.ForEach(watchers, w =>
				{
					w.Changed -= StartRunner;
					w.Dispose();
				});
			}
		}
		private static void ResetLastRunData()
		{
			_LastChange = DateTime.MinValue;
			_Prior = new Analysis();
		}
		private static void StartRunner(object sender, FileSystemEventArgs e)
		{
			if (e != null)
			{
				DateTime timestamp = File.GetLastWriteTime(e.FullPath);
				if (_LastChange.ToString() == timestamp.ToString())// Use string version to eliminate second save by VS a fraction of a second later
					return;
				_LastChange = timestamp;
			}
			KoanSource source = Array.Find(KoanSource.Sources, s => e.FullPath.EndsWith(s.Extension));
			if (sender != null) BuildProject(source); // when runner first starts, build has already run
			RunKoans(source);
		}
		private static bool BuildProject(KoanSource koans)
		{
		    var projectName = koans?.ProjectName;
		    Console.WriteLine($"The master is pondering your {projectName ?? "path to enlightenment"}...");

			using (Process build = new Process())
			{
				build.StartInfo.FileName = "devenv";
			    build.StartInfo.Arguments = String.Format(@"DotNetKoans.sln /build Debug {0}",
                    projectName != null ? "/project " + projectName : null);
                build.StartInfo.CreateNoWindow = true;

                build.Start();
				build.WaitForExit();
				if(build.ExitCode != 0 && koans != null)
				{
				    Console.WriteLine($"There was a build error ({build.ExitCode}).  Please check your code and try again.");
				}
			}
			return false;
		}
		private static void RunKoans(KoanSource koans)
		{
			if (File.Exists(koans.AssemblyPath))
			{
				Console.WriteLine("Checking Koans...");
				using (Process launch = new Process())
				{
					launch.StartInfo.FileName = koansRunner;
					launch.StartInfo.Arguments = koans.AssemblyPath;
					launch.StartInfo.RedirectStandardOutput = true;
					launch.StartInfo.UseShellExecute = false;
					launch.Start();
					string output = launch.StandardOutput.ReadToEnd();
					launch.WaitForExit();
					EchoResult(output, koans.ProjectName);
				}
			}
			File.Delete(koans.AssemblyPath);
		}
		private static void EchoResult(string output, string projectName)
		{
			string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
			Master master = new Master(projectName);
			_Prior = master.Analyze(lines, _Prior);
			PrintLastActions(_Prior);
			PrintMastersComments(_Prior);
			PrintAnswersYouSeek(lines, _Prior);
			PrintFinalWords(_Prior);
		}
		private static void PrintLastActions(Analysis analysis)
		{
			if (string.IsNullOrEmpty(analysis.LastPassedKoan) == false)
			{
				PrintTestLineJustTest(analysis.LastPassedKoan, ConsoleColor.Green, Master.kExpanded);
			}
			if (string.IsNullOrEmpty(analysis.FailedKoan) == false)
			{
				PrintTestLineJustTest(analysis.FailedKoan, ConsoleColor.Red, Master.kDamaged);
			}
		}
		private static void PrintMastersComments(Analysis analysis)
		{
			Console.WriteLine();
			Console.WriteLine("The Master says:");
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("\t{0}", Master.StateOfEnlightenment(analysis));
			string encouragement = Master.Encouragement(analysis);
			if (string.IsNullOrEmpty(encouragement) == false)
				Console.WriteLine("\t{0}", encouragement);
			Console.ForegroundColor = ConsoleColor.White;
		}
		private static void PrintAnswersYouSeek(string[] lines, Analysis analysis)
		{
			if (string.IsNullOrEmpty(analysis.FailedKoan)== false)
			{
				Console.WriteLine();
				Console.WriteLine("The answers you seek...");
				Console.ForegroundColor = ConsoleColor.Red;
				Array.ForEach(Master.WhereToSeek(lines), l => Console.WriteLine("\t{0}", l));
				Console.ForegroundColor = ConsoleColor.White;

				Console.WriteLine();
				Console.WriteLine("Please meditate on the following code:");
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("\t{0}", Master.WhatToMeditateOn(lines));
				Console.ForegroundColor = ConsoleColor.White;
			}
		}
		private static void PrintFinalWords(Analysis analysis)
		{
			Console.WriteLine();
			Console.WriteLine("sleep is the best meditation");
			Console.WriteLine("your path thus far [{0}] {1}/{2}", analysis.ProgressBar, analysis.CompletedKoans, analysis.TotalKoans);
        }
		private static void PrintTestLineJustTest(string koan, ConsoleColor accent, string action)
		{
			Console.ForegroundColor = accent;
			Console.Write(koan);
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine(" {0}", action);
		}
	}
}
