﻿using Microsoft.Win32;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace Olympus {
    public class CmdRegWin32Get : Cmd<string, object> {
        public override bool LogRun => false;
        public override object Run(string key) {
            int indexOfSlash = key.LastIndexOf('\\');
            if (indexOfSlash == -1)
                return null;

            try {
                return RegWin32Helper.OpenOrCreateKey(key.Substring(0, indexOfSlash))?.GetValue(key.Substring(indexOfSlash + 1));
            } catch (Exception e) {
                Console.Error.WriteLine($"Cannot get registry value: {key}");
                Console.Error.WriteLine(e);
                return null;
            }
        }
    }
}