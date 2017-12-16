using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ArmaWorkshopUpdater
{
    class Program
    {
        private static string helpMsg = "Usage: -f [uri] -- BIS Launcher Export file to parse\n\t-l [name] -- Steam Account Name\n\t-install_dir [path] -- Arma 3 Server Install Directory";
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
            Console.WriteLine(message);
        }
        static void Main(string[] args)
        {            
            ModListParser modList = null;
            try
            {
                var flags = ProcessCmdLine(args);
                if (flags.ContainsKey("ModList") && flags.ContainsKey("Uname") && flags.ContainsKey("InstallDir"))
                {
                    modList = new ModListParser(flags["ModList"]);
                    DirectoryInfo InstallPath = new DirectoryInfo(flags["InstallDir"]);
                    DirectoryInfo workshop_dir = null;
                    try
                    {
                        workshop_dir = InstallPath.CreateSubdirectory("Workshop");
                    }
                    catch (System.IO.IOException e)
                    {
                        workshop_dir = new DirectoryInfo($"{InstallPath.FullName}\\Workshop");
                    }
                    Process steamcmd = null;
                    NotifyUser($"Caching your credentials in SteamCMD...");
                    steamcmd = Process.Start("steamcmd.exe", $"+login {flags["Uname"]} +quit");
                    steamcmd.WaitForExit();
                    foreach (var mod in modList.Mods())
                    {
                        string id = mod.Item2.Split('=')[1];
                        NotifyUser($"Downloading {mod.Item1}...");
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
                                NotifyUser($"Something went wrong with SteamCMD, attempting update process again ({steamcmd.ExitCode})");
                            }
                        } while (steamcmd.ExitCode != 0);
                        NotifyUser($"Attempting to symlink workshop folder...");                      

                    }
                }
                else
                {
                    NotifyUser(helpMsg);
                }
            }
            catch (System.Xml.XPath.XPathException e)
            {
                NotifyUser($"Error loading modlist: {e.Message}");
            }
            Console.ReadKey();
        }
    }
}
