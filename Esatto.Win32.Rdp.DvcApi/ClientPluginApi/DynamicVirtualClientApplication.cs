#define TRACE

using Esatto.Win32.Com;
using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    // Runs on RDS Client
    // Wrapper to minimize Program.cs in plugin applications
    public sealed class DynamicVirtualClientApplication
    {
        public static void Run(string[] args, Dictionary<string, Action<IAsyncDvcChannel>> channelHosts)
            => Run(args, channelHosts,
                wtsPluginClsid: GetEntryAssemblyGuid(),
                wtsPluginProgID: Assembly.GetEntryAssembly().GetName().Name + ".WtsPlugin",
                startCommand: Assembly.GetEntryAssembly().Location);

        private static Guid GetEntryAssemblyGuid()
        {
            var sGuid = Assembly.GetEntryAssembly()
                .GetCustomAttributes(typeof(GuidAttribute), true)
                .Cast<GuidAttribute>().First().Value;

            return Guid.Parse(sGuid);
        }

        public static void Log(string text)
        {
            Trace.WriteLine("DVCAPI> " + text);
            Console.Error.WriteLine(text);
        }

        public static void Run(string[] args, Dictionary<string, Action<IAsyncDvcChannel>> channelHosts, Guid wtsPluginClsid, string wtsPluginProgID, string startCommand)
        {
            Contract.Requires(args != null);
            Contract.Requires(channelHosts.Any());
            Contract.Requires(wtsPluginProgID != null);
            Contract.Requires(startCommand != null);

            // clone array to avoid changes
            channelHosts = new Dictionary<string, Action<IAsyncDvcChannel>>(channelHosts);
            foreach (var chf in channelHosts)
            {
                Contract.Requires(chf.Value != null);
                TSVirtualChannels.NativeMethods.ValidateChannelName(chf.Key);
            }

            if (args.Any(a => !a.Equals("-Embedding", StringComparison.InvariantCultureIgnoreCase)))
            {
                if (args.Length == 1)
                {
                    if (args[0].Equals("/i", StringComparison.CurrentCultureIgnoreCase)
                        || args[0].Equals("/install", StringComparison.CurrentCultureIgnoreCase))
                    {
                        DvcMessageLoop.Register(wtsPluginClsid, wtsPluginProgID, startCommand);

                        return;
                    }
                    else if (args[0].Equals("/u", StringComparison.CurrentCultureIgnoreCase)
                        || args[0].Equals("/uninstall", StringComparison.CurrentCultureIgnoreCase))
                    {
                        DvcMessageLoop.Unregister(wtsPluginClsid);

                        return;
                    }
                }

                PrintUsage(wtsPluginClsid, wtsPluginProgID, startCommand);
                return;
            }
            else
            {
                Application.Run(new DvcMessageLoop(channelHosts, wtsPluginClsid));
            }
        }

        private static void PrintUsage(Guid wtsPluginClsid, string progid, string startCommand)
        {
            Console.Error.WriteLine($@"DVC out-of-process COM Server for mstsc.exe

Usage:
    /install    Register the DVC with mstsc.exe for the native bitness of this binary
    /uninstall  Unregister the DVC with mstsc.exe
    [no args]   Run the out-of-process server

Server metadata:
    CLSID:      {wtsPluginClsid}
    ProgID:     {progid}
    LS32:       {startCommand}
");
        }
    }
}
