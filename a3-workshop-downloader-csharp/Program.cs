using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
namespace ArmaWorkshopUpdater
{
    class Program
    {
        private static string helpMsg = "Usage: -f [uri] -- BIS Launcher Export file to parse\n\t-l [name] -- Steam Account Name\n\t-install_dir [path] -- Arma 3 Server Install Directory\n";
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
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"[{errlvl.ToString().ToUpper()}] ({DateTime.Now}) {message}");
            Console.ForegroundColor = oldColor;

        }
        static bool SymLink(string path, string target, SymbolicLink type)
        {
            return CreateSymbolicLink(path, target, type);
        }
        static int Main(string[] args)
        {            
            ModListParser modList = null;
            try
            {
                var flags = ProcessCmdLine(args);
                if (flags.ContainsKey("ModList") && flags.ContainsKey("Uname") && flags.ContainsKey("InstallDir"))
                {
                    modList = new ModListParser(flags["ModList"]);
                    modList.ParseModList();
                    DirectoryInfo InstallPath = new DirectoryInfo(flags["InstallDir"]);
                    DirectoryInfo workshop_dir = null;
                    try
                    {
                        workshop_dir = InstallPath.CreateSubdirectory("Workshop");
                    }
                    catch (System.IO.IOException e)
                    {
                        workshop_dir = new DirectoryInfo($"{InstallPath.FullName}\\Workshop");
                        if (!workshop_dir.Exists)
                        {
                            NotifyUser($"Directory cannot be created and does not exist: {e}\n", ErrorLevel.Critical);
                            return 1;
                        }
                    }
                    Process steamcmd = null;
                    NotifyUser($"Caching your credentials in SteamCMD...\n", ErrorLevel.Info);
                    steamcmd = Process.Start("steamcmd.exe", $"+login {flags["Uname"]} +quit");
                    steamcmd.WaitForExit();
                    foreach (var mod in modList.Mods())
                    {
                        string id = mod.Item2.Split('=')[1];
                        NotifyUser($"Downloading {mod.Item1}...", ErrorLevel.Info);
                        ProcessStartInfo steamcmd_info = new ProcessStartInfo() {
                            FileName = "steamcmd.exe",
                            Arguments = $"+force_install_dir {flags["InstallDir"]} +login {flags["Uname"]} +workshop_download_item 107410 {id} +quit"
                        };
                        do
                        {
                            steamcmd = Process.Start(steamcmd_info);
                            steamcmd.WaitForExit();
                            if (steamcmd.ExitCode != 0)
                            {
                                NotifyUser($"\nSomething went wrong with SteamCMD, attempting update process again ({steamcmd.ExitCode})\n", ErrorLevel.Warning);
                                NotifyUser($"Downloading {mod.Item1}...", ErrorLevel.Info);
                            }
                        } while (steamcmd.ExitCode != 0);
                        NotifyUser($"\tSUCCESS\n");
                        NotifyUser("Attempting to symlink workshop folder...\n", ErrorLevel.Info);
                        if (SymLink($"{workshop_dir.FullName}\\@{mod.Item1}", $"{InstallPath.FullName}\\steamapps\\workshop\\content\\107410\\{id}", SymbolicLink.Directory))
                        {
                            NotifyUser($"\tSUCCESS\n");
                        }
                        else
                        {
                            NotifyUser("Symlink creation failed. Are you running as admin or perhaps it already exists\n", ErrorLevel.Warning);
                        }
                    }
                }
                else
                {
                    NotifyUser(helpMsg);
                }
            }
            catch (System.Xml.XPath.XPathException e)
            {
                NotifyUser($"Error loading modlist: {e.Message}\n", ErrorLevel.Error);
            }
            Console.ReadKey();
            return 0;
        }
    }
}
