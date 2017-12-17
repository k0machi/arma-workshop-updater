using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
namespace ArmaWorkshopUpdater
{
    class Program
    {
        /**
         * Help Message
         */
        private static string HelpMsg = "Usage: -f [uri] -- BIS Launcher Export file to parse\n\t-l [name] -- Steam Account Name\n\t-install_dir [path] -- Arma 3 Server Install Directory\n";
        /**
         * Import Win32 CreateSymbolicLink
         */
        [DllImport("kernel32.dll")]
        static extern bool CreateSymbolicLink(string lpLinkFilename, string lpTargetFilename, SymbolicLink dwFlags);
        enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }
        enum ErrorLevel
        {
            Critical = 50,
            Error = 40,
            Warning,
            Info,
            Debug,
            Trace
        }
        private static Dictionary<ErrorLevel, ConsoleColor> ErrLevelColors = new Dictionary<ErrorLevel, ConsoleColor>()
        {
            { ErrorLevel.Critical, ConsoleColor.Red },
            { ErrorLevel.Error, ConsoleColor.DarkRed },
            { ErrorLevel.Warning, ConsoleColor.Yellow },
            { ErrorLevel.Info, ConsoleColor.Cyan },
            { ErrorLevel.Debug, ConsoleColor.White },
            { ErrorLevel.Trace, ConsoleColor.Gray }
        };
        static Dictionary<string, string> ProcessCmdLine(string[] args)
        {
            var result = new Dictionary<string, string>();
            for (var arg = 0; arg < args.Length; arg++)
            {
                switch (args[arg])
                {
                    case "-f":
                        {
                            result["ModList"] = args[arg + 1];
                            break;
                        }
                    case "-l":
                        {
                            result["Uname"] = args[arg + 1];
                            break;
                        }
                    case "-steam_web_api_key":
                        {
                            result["SteamKey"] = args[arg + 1];
                            break;
                        }
                    case "-install_dir":
                        {
                            result["InstallDir"] = args[arg + 1];
                            break;
                        }
                    case "-steam_cmd_dir":
                        {
                            result["SteamCMD"] = args[arg + 1];
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }
            return result;
        }
        static void NotifyUser(string message)
        {
            Console.Write(message);
        }
        static void NotifyUser(string message, ErrorLevel errlvl)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ErrLevelColors[errlvl];
            Console.Write($"[{errlvl.ToString().ToUpper()}] ({DateTime.Now}) {message}");
            Console.ForegroundColor = oldColor;

        }
        static bool SymLink(string path, string target, SymbolicLink type)
        {
            return CreateSymbolicLink(path, target, type);
        }
        static private int DownloadMod(Tuple<string, string> mod, string uname, DirectoryInfo installdir, DirectoryInfo workshopdir)
        {
            Process steamcmd = null;
            string id = mod.Item2.Split('=')[1];
            NotifyUser($"Downloading {mod.Item1}...\n", ErrorLevel.Info);
            ProcessStartInfo steamcmd_info = new ProcessStartInfo()
            {
                FileName = "steamcmd.exe",
                Arguments = $"+force_install_dir {installdir.FullName} +login {uname} +workshop_download_item 107410 {id} +quit"
            };
            do
            {
                steamcmd = Process.Start(steamcmd_info);
                steamcmd.WaitForExit();
                if (steamcmd.ExitCode != 0)
                {
                    NotifyUser($"Something went wrong with SteamCMD, attempting update process again ({steamcmd.ExitCode})\n", ErrorLevel.Warning);
                    NotifyUser($"Downloading {mod.Item1}...\n", ErrorLevel.Info);
                }
            } while (steamcmd.ExitCode != 0);
            NotifyUser($"Downloaded {mod.Item1} ({id})\n",ErrorLevel.Info);
            NotifyUser("Attempting to symlink workshop folder...\n", ErrorLevel.Info);
            if (SymLink($"{workshopdir.FullName}\\@{mod.Item1}", $"{installdir.FullName}\\steamapps\\workshop\\content\\107410\\{id}", SymbolicLink.Directory))
            {
                NotifyUser($"Symlink creation successful\n", ErrorLevel.Info);
            }
            else
            {
                NotifyUser("Symlink creation failed. Are you running as admin or perhaps it already exists\n", ErrorLevel.Warning);
            }
            return steamcmd.ExitCode;
        }
        static private Tuple<DirectoryInfo, DirectoryInfo> InitializeInstallDir(string path)
        {

            DirectoryInfo install_path = new DirectoryInfo(path);
            DirectoryInfo workshop_dir = null;
            try
            {
                workshop_dir = install_path.CreateSubdirectory("Workshop");
            }
            catch (System.IO.IOException e)
            {
                workshop_dir = new DirectoryInfo($"{install_path.FullName}\\Workshop");
                if (!workshop_dir.Exists)
                {
                    NotifyUser($"Directory cannot be created and does not exist: {e}\n", ErrorLevel.Critical);
                    throw new System.IO.IOException();
                }
            }
            return new Tuple<DirectoryInfo, DirectoryInfo>(install_path, workshop_dir);
        }
        static private int CacheCredentials(string name)
        {
            Process steamcmd = Process.Start("steamcmd.exe", $"+login {name} +quit");
            steamcmd.WaitForExit();
            return steamcmd.ExitCode;
        }
        static int Main(string[] args)
        {            
            ModListParser modList = null;
            try
            {
                var options = ProcessCmdLine(args);
                if (options.ContainsKey("ModList") && options.ContainsKey("Uname") && options.ContainsKey("InstallDir"))
                {
                    modList = new ModListParser(options["ModList"]);
                    modList.ParseModList();
                    (DirectoryInfo InstallPath, DirectoryInfo workshop_dir) = InitializeInstallDir(options["InstallDir"]);
                    NotifyUser($"Caching your credentials in SteamCMD...\n", ErrorLevel.Info);
                    CacheCredentials(options["Uname"]);
                    foreach (var mod in modList.Mods())
                    {
                        DownloadMod(mod, options["Uname"], InstallPath, workshop_dir);
                    }
                }
                else
                {
                    NotifyUser(HelpMsg);
                }
            }
            catch (System.Xml.XPath.XPathException e)
            {
                NotifyUser($"Error loading modlist: {e.Message}\n", ErrorLevel.Error);
            }
            catch (Exception e)
            {
                NotifyUser($"Unhandled exception: {e}", ErrorLevel.Critical);
            }
            Console.ReadKey();
            return 0;
        }
    }
}
