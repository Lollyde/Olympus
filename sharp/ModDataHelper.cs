using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using YYProject.XXHash;
using System.Linq;

namespace Olympus
{

    public class ModDataHelper
    {
        private static ModDataHelper instance;
        private static HashAlgorithm Hasher;
        private static Dictionary<string, List<ModInfo>> modlist;
        private static Dictionary<string, List<Dependency>> deplist;

        public static ModDataHelper Instance { get { if (instance == null) instance = new ModDataHelper(); return instance; } }

        private ModDataHelper()
        {
            Hasher = XXHash64.Create();
            modlist = new Dictionary<string, List<ModInfo>>();
        }

        public List<ModInfo> GetModList(string root)
        {
            // TODO up to date check??
            if(!modlist.ContainsKey(root))
            {
                modlist[root] = new List<ModInfo>();
                initializeModList(root);
            }
            if (NeedsRefresh(root))
            {
                RefreshModList(root);
            }
            return modlist[root];
        }

        public List<Dependency> GetMissingDeps(string root)
        {
            if (!deplist.ContainsKey(root) || NeedsRefresh(root))
            {
                deplist[root] = new List<Dependency>();
                RefreshDependencies(root);
            }
            throw new NotImplementedException();
        }

        private void RefreshDependencies(string root)
        {
            // loop over all mods
            // if it has no dependency that isnt everest add it to "deps_statisfied"
            // if it has any dependency that isnt everest, check if all dependencies are either in "deps_statisfied" or "to_check"
            // once all mods have been iterated over once, iterate over "to_check" and check if every dependency exists in either "to_check" or "deps_statisfied"
            // if all dependencies are statisfied, move to "deps_statisfied"
            // after one iteration, if there are any mods left in "to_check", there are missing dependencies
            List<ModInfo> deps_statisfied = new List<ModInfo>(), to_check = new List<ModInfo>();
            foreach (ModInfo mod in modlist[root])
            {
                bool statisfied = true;
                foreach (Dependency dep in mod.Deps)
                {
                    statisfied = dep.Name.Equals("Everest") || has_mod(dep.Name, to_check) || has_mod(dep.Name, deps_statisfied);
                    if (!statisfied)
                    {
                        to_check.Add(mod);
                        goto SKIP;
                    }
                }
                deps_statisfied.Add(mod);
            SKIP:;
            }

            foreach (ModInfo mod in to_check)
            {
                foreach (Dependency dep in mod.Deps)
                {
                    bool statisfied = dep.Name.Equals("Everest") || has_mod(dep.Name, deps_statisfied) || has_mod(dep.Name, to_check);
                    if (!statisfied)
                    {
                        if (!deplist[root].Contains(dep))
                        {
                            deplist[root].Add(dep);
                        }
                    }
                }
            }
        }

        private bool has_mod(string name, List<ModInfo> list)
        {
            foreach (ModInfo mod in list)
            {
                if (mod.Name.Equals(name)) return true;
            }
            return false;
        }

        private void RefreshModList(string root)
        {
            throw new NotImplementedException();
        }

        private bool NeedsRefresh(string root)
        {
            return false;
        }

        private void initializeModList(string root)
        {
            List<string> blacklist;
            string blacklistPath = Path.Combine(root, "blacklist.txt");
            if (File.Exists(blacklistPath))
                blacklist = File.ReadAllLines(blacklistPath).Select(l => (l.StartsWith("#") ? "" : l).Trim()).ToList();
            else
                blacklist = new List<string>();

            string[] files = Directory.GetFiles(root);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string name = Path.GetFileName(file);
                if (!file.EndsWith(".zip"))
                    continue;

                ModInfo info = new ModInfo()
                {
                    Path = file,
                    IsZIP = true,
                    IsBlacklisted = blacklist.Contains(name)
                };

                using (FileStream zipStream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    // info.Hash = BitConverter.ToString(Hasher.ComputeHash(zipStream)).Replace("-", "");
                    zipStream.Seek(0, SeekOrigin.Begin);

                    using (ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    using (Stream stream = (zip.GetEntry("everest.yaml") ?? zip.GetEntry("everest.yml"))?.Open())
                    using (StreamReader reader = stream == null ? null : new StreamReader(stream))
                        info.Parse(reader);
                }

                modlist[root].Add(info);
            }

            files = Directory.GetDirectories(root);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string name = Path.GetFileName(file);
                if (name == "Cache")
                    continue;

                ModInfo info = new ModInfo()
                {
                    Path = file,
                    IsZIP = false,
                    IsBlacklisted = blacklist.Contains(name)
                };

                try
                {
                    string yamlPath = Path.Combine(file, "everest.yaml");
                    if (!File.Exists(yamlPath))
                        yamlPath = Path.Combine(file, "everest.yml");

                    if (File.Exists(yamlPath))
                    {
                        using (FileStream stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (StreamReader reader = new StreamReader(stream))
                            info.Parse(reader);

                        if (!string.IsNullOrEmpty(info.DLL))
                        {
                            string dllPath = Path.Combine(file, info.DLL);
                            if (File.Exists(dllPath))
                            {
                                using (FileStream stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                                    info.Hash = BitConverter.ToString(Hasher.ComputeHash(stream)).Replace("-", "");
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }

                modlist[root].Add(info);

            }
        }
    }

    public class ModInfo {
        public string Path;
        public string Hash;
        public bool IsZIP;
        public bool IsBlacklisted;
        public string Filename;

        public string Name;
        public string Version;
        public string DLL;
        public bool IsValid;
        public List<Dependency> Deps;

        public void Parse(TextReader reader) {
            if (reader != null && YamlHelper.Deserializer.Deserialize(reader) is List<object> yamlRoot &&
                yamlRoot.Count != 0 && yamlRoot[0] is Dictionary<object, object> yamlEntry) {

                IsValid = yamlEntry.TryGetValue("Name", out object yamlName) &&
                !string.IsNullOrEmpty(Name = yamlName as string) &&
                yamlEntry.TryGetValue("Version", out object yamlVersion) &&
                !string.IsNullOrEmpty(Version = yamlVersion as string);

                if (yamlEntry.TryGetValue("DLL", out object yamlDLL))
                    DLL = yamlDLL as string;
                yamlEntry.TryGetValue("Dependencies", out object depsobj);
                var list = depsobj as List<object>;
                Deps = new List<Dependency>();
                foreach (var item in list)
                {
                    var item1 = item as Dictionary<object, object>;
                    if (item1.TryGetValue("Name", out object depName) && item1.TryGetValue("Version", out object depVersion))
                    {
                        var tmp = new Dependency();
                        tmp.Name = depName as string;
                        tmp.Version = depVersion as string;
                        Deps.Add(tmp);
                    }
                }
            }
        }
    }

    public class Dependency
    {
        public string Name, Version;
    }
}
