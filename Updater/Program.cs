using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using AutoUpdaterDotNET;
using CommonHelpers;

namespace Updater
{
    internal static class Program
    {
        public const String Title = "Steam Deck Tools";
        public const String RunPrefix = "-run=";
        public const String UpdatedArg = "-updated";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool firstRun = Environment.GetCommandLineArgs().Contains("-first");
            bool userCheck = Environment.GetCommandLineArgs().Contains("-user");
            bool updated = Environment.GetCommandLineArgs().Contains(UpdatedArg);
            bool cmdLine = !firstRun && !userCheck;

            if (updated)
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (!arg.StartsWith(RunPrefix))
                        continue;

                    var processName = arg.Substring(RunPrefix.Length);
                    CommonHelpers.Log.TraceLine("Running {0}", processName);
                    try { Process.Start(processName); } catch { }
                }
                return;
            }

            Instance.RunOnce(null, "Global\\SteamDeckToolsAutoUpdater");

            var persistence = new RegistryPersistenceProvider(@"Software\SteamDeckTools\AutoUpdater");

            if (userCheck || cmdLine)
            {
                persistence.SetRemindLater(null);
                persistence.SetSkippedVersion(new Version());
            }

            AutoUpdater.AppTitle = Title;
            AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
            AutoUpdater.LetUserSelectRemindLater = true;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.HttpUserAgent = String.Format("AutoUpdater/{0}/{1}", Instance.MachineID, Instance.ProductVersion);
            AutoUpdater.PersistenceProvider = persistence;
            AutoUpdater.ReportErrors = userCheck || cmdLine;
            AutoUpdater.UpdateFormSize = new Size(800, 300);
            AutoUpdater.ShowSkipButton = true;
            AutoUpdater.Synchronous = true;
            AutoUpdater.ApplicationExitEvent += Application_Exit;

            AppendArg(UpdatedArg);
            TrackProcess("FanControl");
            TrackProcess("PowerControl");
            TrackProcess("PerformanceOverlay");
            TrackProcess("SteamController");

#if DEBUG
            AutoUpdater.Start("https://steam-deck-tools.ayufan.dev/docs/updates/debug_zip.xml");
#else
            AutoUpdater.Start("https://steam-deck-tools.ayufan.dev/docs/updates/release_zip.xml");
#endif
        }

        private static void TrackProcess(String processFilerName)
        {
            if (FindProcesses(processFilerName).Any())
                AppendArg(RunPrefix + processFilerName);
        }

        private static void Application_Exit()
        {
            ExitProcess("FanControl");
            ExitProcess("PowerControl");
            ExitProcess("PerformanceOverlay");
            ExitProcess("SteamController");
            ExitProcess("Updater");
        }

        private static void AppendArg(string arg)
        {
            var setCommandLineArgs = typeof(Environment).GetMethod(
                "SetCommandLineArgs", BindingFlags.Static | BindingFlags.NonPublic,
                new Type[] { typeof(string[]) });
            if (setCommandLineArgs is null)
                return;

            // append `-run:<process>` to command line args
            setCommandLineArgs.Invoke(null, new object[] {
                Environment.GetCommandLineArgs().Append(arg).ToArray()
            });
        }

        private static bool ExitProcess(String processFilerName)
        {
            bool found = false;

            foreach (var process in FindProcesses(processFilerName))
            {
                if (process.CloseMainWindow())
                {
                    process.WaitForExit((int)TimeSpan.FromSeconds(10)
                        .TotalMilliseconds); //give some time to process message
                }

                if (!process.HasExited)
                {
                    process.Kill(); //TODO show UI message asking user to close program himself instead of silently killing it
                }

                found = true;
            }

            return found;
        }

        private static IEnumerable<Process> FindProcesses(String processFilerName)
        {
            var currentProcess = Process.GetCurrentProcess();
            var currentDir = Path.GetDirectoryName(currentProcess.MainModule?.FileName);

            foreach (var process in Process.GetProcessesByName(processFilerName))
            {
                string? processFileName, processDir;
                try
                {
                    processFileName = process.MainModule?.FileName;
                    if (processFileName is null)
                        continue;

                    processDir = Path.GetDirectoryName(processFileName);
                }
                catch (Win32Exception)
                {
                    // Current process should be same as processes created by other instances of the application so it should be able to access modules of other instances. 
                    // This means this is not the process we are looking for so we can safely skip this.
                    continue;
                }

                //get all instances of assembly except current
                if (process.Id != currentProcess.Id && currentDir == processDir)
                {
                    yield return process;
                }
            }
        }
    }
}
