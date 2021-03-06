﻿using Microsoft.Win32;
using NeXt.Vdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace NeXt.Daud.Model
{
    public class Locator
    {
        private static readonly Lazy<Locator> instance = new Lazy<Locator>(() => new Locator(), false);
        public static Locator Instance => instance.Value;

        private Locator()
        {
            //the steam install folder
            steam_folder = new Lazy<string>(delegate
            {
                var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                key = key.OpenSubKey(@"SOFTWARE\Valve\Steam");
                var folder = key.GetValue("InstallPath") as string;
                return folder;
            },isThreadSafe: true);

            //a list of file paths of steam app folders
            steamapps_list = new Lazy<IReadOnlyList<string>>(delegate
            {
                var list = new List<string>();
                var defaultApps = Path.Combine(SteamFolder, "SteamApps");
                list.Add(defaultApps);

                var desr = VdfDeserializer.FromFile(Path.Combine(defaultApps, "libraryfolders.vdf"));
                var tbl = desr.Deserialize() as VdfTable;

                int i = 1;
                while (tbl.ContainsName(i.ToString()))
                {
                    var p = (tbl[i.ToString()] as VdfString).Content;
                    list.Add(Path.Combine(p, "SteamApps"));
                    i++;
                }

                return list.AsReadOnly();
            },isThreadSafe: true);

            
            users = new Lazy<IReadOnlyList<SteamUser>>(()  => new List<SteamUser>(GetUserList()).AsReadOnly(), isThreadSafe: true);
        }


        private IEnumerable<SteamUser> GetUserList()
        {
            var cfgdir = Path.Combine(SteamFolder, "config");
            var ulist = VdfDeserializer.FromFile(Path.Combine(cfgdir, "loginusers.vdf")).Deserialize() as VdfTable;

            foreach (var item in ulist)
            {
                var uid = item.Name;
                var uname = ((VdfString)((VdfTable)item)["PersonaName"]).Content;
                
                yield return new SteamUser(uname, ulong.Parse(uid));
            }
        }

        private readonly Lazy<string> steam_folder;
        private readonly Lazy<IReadOnlyList<string>> steamapps_list;        
        private readonly Dictionary<string, string> gamePaths = new Dictionary<string, string>();

        private string GetInstallPath(string gameid)
        {
            if (gamePaths.TryGetValue(gameid, out var v)) return v;

            foreach (var apps in steamapps_list.Value)
            {
                if (File.Exists(Path.Combine(apps, $"appmanifest_{gameid}.acf")))
                {
                    var desr = VdfDeserializer.FromFile(Path.Combine(apps, $"appmanifest_{gameid}.acf"));
                    var tbl = desr.Deserialize() as VdfTable;
                    var dirName = (tbl["installdir"] as VdfString).Content;
                    var p = Path.Combine(apps, "common", dirName);
                    gamePaths.Add(gameid, p);
                    return p;
                }
            }


            return null;
        }

        public string SteamFolder => steam_folder.Value;
        public IEnumerable<string> SteamAppsFolders => steamapps_list.Value;
        public string this[string gameid] => GetInstallPath(gameid);

        private Lazy<IReadOnlyList<SteamUser>> users;
        public IReadOnlyList<SteamUser> Users => users.Value;
    }
}
