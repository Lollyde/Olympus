﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YYProject.XXHash;
using System.Diagnostics;

namespace Olympus
{
    public unsafe partial class CmdModList : Cmd<string, IEnumerator> {

        public static HashAlgorithm Hasher = XXHash64.Create();
        private static List<ModInfo> modlist;


        public override IEnumerator Run(string root) {
            root = Path.Combine(root, "Mods");
            if (!Directory.Exists(root))
                yield break;
            modlist = new List<ModInfo>();
            ModDataHelper.Instance.GetModList(root);
            foreach (ModInfo item in ModDataHelper.Instance.GetModList(root))
            {
                yield return item;
            }
        }

        private List<string> checkDeps()
        {
            // loop over all mods
            // if it has no dependency that isnt everest add it to "deps_statisfied"
            // if it has any dependency that isnt everest, check if all dependencies are either in "deps_statisfied" or "to_check"
            // once all mods have been iterated over once, iterate over "to_check" and check if every dependency exists in either "to_check" or "deps_statisfied"
            // if all dependencies are statisfied, move to "deps_statisfied"
            // after one iteration, if there are any mods left in "to_check", there are missing dependencies
            List<ModInfo> deps_statisfied = new List<ModInfo>(), to_check = new List<ModInfo>();
            List<string> missing_deps = new List<string>();
            foreach (ModInfo mod in modlist)
            {
                bool statisfied = true;
                foreach (Dependency dep in mod.Deps)
                {
                    statisfied = dep.Name.Equals("Everest") || has_mod(dep.Name, to_check) || has_mod(dep.Name, deps_statisfied);
                    if (!statisfied)
                    {
                        //Console.Error.WriteLine($"[DepCheck] first round: {mod.Name} is missing dependency: {dep.Name}, skipping all other dependencies");
                        to_check.Add(mod);
                        goto SKIP;
                    }
                }
                //Console.Error.WriteLine($"[DepCheck] first round: {mod.Name} has all dependencies statisfied!");
                deps_statisfied.Add(mod);
                SKIP:;
            }

            //Console.Error.WriteLine($"[DepCheck] out of {modlist.Count} mods, {to_check.Count} mods had at least one dependency missing after the first round");

            foreach (ModInfo mod in to_check)
            {
                //bool allmet = true;
                foreach (Dependency dep in mod.Deps)
                {
                    bool statisfied = dep.Name.Equals("Everest") || has_mod(dep.Name, deps_statisfied) || has_mod(dep.Name, to_check);
                    if (!statisfied)
                    {
                        if (!missing_deps.Contains(dep.Name))
                        {
                            //Console.Error.WriteLine($"[DepCheck] second round: {mod.Name} is missing dependency: {dep.Name}, adding to list of missing dependencies");
                            missing_deps.Add(dep.Name);
                        }
                        else
                        {
                            //Console.Error.WriteLine($"[DepCheck] second round: {mod.Name} is missing dependency: {dep.Name}, however it is already listed as missing.");
                        }
                        
                        //allmet = false;
                    }
                }
                //if (allmet) Console.Error.WriteLine($"[DepCheck] second round: {mod.Name} had previously unstatisfied dependencies, now has all dependencies met!");
            }
            //Console.Error.WriteLine($"[DepCheck] end: {missing_deps.Count} total missing dependencies detected");
            return missing_deps;
        }

        private bool has_mod(string name, List<ModInfo> list)
        {
            foreach (ModInfo mod in list)
            {
                if (mod.Name.Equals(name)) return true;
            }
            return false;
        }

        private void loadMods(string root)
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

                modlist.Add(info);
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

                modlist.Add(info);
               
            }
        }

    }
}
