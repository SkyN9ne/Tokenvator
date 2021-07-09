﻿using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tokenvator.Resources
{
    public sealed class CommandLineParsing
    {
        private readonly Dictionary<string, object> arguments;

        public int ProcessID { get; private set; }
        public string Privilege { get; private set; }
        public string CommandAndArgs { get; private set; }
        public string Command { get; private set; }
        public string Arguments { get; private set; }
        public bool Remote { get; private set; } = false;

        public static List<string> privileges = new List<string> { "SeAssignPrimaryTokenPrivilege",
            "SeAuditPrivilege", "SeBackupPrivilege", "SeChangeNotifyPrivilege", "SeCreateGlobalPrivilege",
            "SeCreatePagefilePrivilege", "SeCreatePermanentPrivilege", "SeCreateSymbolicLinkPrivilege",
            "SeCreateTokenPrivilege", "SeDebugPrivilege", "SeEnableDelegationPrivilege",
            "SeImpersonatePrivilege", "SeIncreaseBasePriorityPrivilege", "SeIncreaseQuotaPrivilege",
            "SeIncreaseWorkingSetPrivilege", "SeLoadDriverPrivilege", "SeLockMemoryPrivilege",
            "SeMachineAccountPrivilege", "SeManageVolumePrivilege", "SeProfileSingleProcessPrivilege",
            "SeRelabelPrivilege", "SeRemoteShutdownPrivilege", "SeRestorePrivilege", "SeSecurityPrivilege",
            "SeShutdownPrivilege", "SeSyncAgentPrivilege", "SeSystemEnvironmentPrivilege",
            "SeSystemProfilePrivilege", "SeSystemtimePrivilege", "SeTakeOwnershipPrivilege",
            "SeTcbPrivilege", "SeTimeZonePrivilege", "SeTrustedCredManAccessPrivilege",
            "SeUndockPrivilege", "SeUnsolicitedInputPrivilege" };

        public CommandLineParsing()
        {
            arguments = new Dictionary<string, object>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        public void Parse(string input)
        {
            input = Regex.Replace(input, "(\".*?)(/)(.*?\")", "$1\0$3");
            
            //Not working properly needs above regex - not sure why
            
            /*
            var textfieldParser = new TextFieldParser(new System.IO.StringReader(input))
            {
                TextFieldType = FieldType.Delimited,
                Delimiters = new string[] { @"/", "-" },
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true,
            };
            string[] argumentAndData = textfieldParser.ReadFields();
            */

            string[] argumentAndData = input.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string a in argumentAndData)
            {
                if (string.IsNullOrEmpty(a))
                {
                    continue;
                }

                string b = a.Replace('\0', '/');
                b = Regex.Replace(b, "(\".*?)(:)(.*?\")", "$1\0$3").Replace("\"", "");
                string[] argData = b.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (string.IsNullOrWhiteSpace(argData.FirstOrDefault()))
                {
                    continue;
                }

                string c = string.Join(" ", argData.Skip(1).Take(argData.Count() - 1).ToArray()).Replace('\0', ':');
                //Console.WriteLine("[*] a {0}", argData.FirstOrDefault().ToLower());
                //Console.WriteLine("[*] c {0}", c);
                arguments.Add(argData.FirstOrDefault().ToLower(), c.Trim());
            }

            if (arguments.ContainsKey("process"))
            {
                if (arguments.TryGetValue("process", out object process))
                {
                    if (_ParseProcessID((string)process, out int pid))
                    {
                        ProcessID = pid;
                        Remote = true;
                    }
                }
            }

            if (arguments.ContainsKey("privilege"))
            {
                if (arguments.TryGetValue("privilege", out object privilege))
                {
                    if (_ParsePrivileges((string)privilege, out string priv))
                    {
                        Privilege = priv;
                    }
                }
            }

            if (arguments.ContainsKey("command"))
            {
                Console.WriteLine("Parsing Command");
                if (arguments.TryGetValue("command", out object command))
                {
                    _ParseCommand((string)command, out string c, out string a);
                    Command = c; Arguments = a; CommandAndArgs = (string)command;
                    Console.WriteLine("[*] Command: " + c);
                    Console.WriteLine("[*] Arguments: " + a);
                    Console.WriteLine("[*] If the above doesn't look correct you may need quotes");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        public bool GetData(string input, out object output)
        {
            return arguments.TryGetValue(input.ToLower(), out output);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="command"></param>
        /// <param name="arguments"></param>
        private static void _ParseCommand(string input, out string command, out string arguments)
        {
            string[] cmdAndArgs = input.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            command = cmdAndArgs.FirstOrDefault();
            if (!Misc.FindFullPath(command, out string fullpath))
            {
                Console.WriteLine("[-] Unable to parse full path");
            }
            arguments = string.Join(" ", cmdAndArgs.Skip(1).Take(cmdAndArgs.Count() - 1).ToArray());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        private static bool _ParseProcessID(string input, out int output)
        {
            if (int.TryParse(input, out output))
            {
                try
                {
                    Process p = Process.GetProcessById(output);
                    Console.WriteLine("[+] {0} {1}", output, p.ProcessName);
                    return true;
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        Console.WriteLine("[-] Unable to find process with Process ID");
                        return false;
                    }
                    else
                    {
                        Console.WriteLine(ex);
                        return false;
                    }
                }
            }

            Process[] process = Process.GetProcessesByName(input.Replace(".exe", ""));
            if (0 < process.Length)
            {
                if (1 == process.Length)
                {
                    output = process.First().Id;
                    Console.WriteLine("[+] {0} {1}", output, input);
                    return true;
                }
                else
                {
                    Console.WriteLine("[-] Ambiguious Process Name");
                    Console.WriteLine("[*] Matched Process IDs:");
                    foreach (Process p in process)
                    {
                        Console.WriteLine("   {0} {1}", p.ProcessName, p.Id);
                    }
                    return false;
                }
            }

            Console.WriteLine("[-] Unable to Parse Process ID with Data {0}", input);
            return false;
        }      

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        private static bool _ParsePrivileges(string input, out string output)
        {
            //privileges.Any(s => s.Equals(input, StringComparison.OrdinalIgnoreCase))
            int index = privileges.FindIndex(x => x.Equals(input.Trim(), StringComparison.OrdinalIgnoreCase));
            if (-1 != index)
            {
                output = privileges[index];
                return true;
            }
            else
            {
                Console.WriteLine("[-] Unable to validate privilege name {0}", input);
                output = string.Empty;
                return false;
            }
        }
    }
}
