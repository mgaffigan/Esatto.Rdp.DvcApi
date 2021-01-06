using Esatto.Win32.Com;
using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Esatto.Win32.Com.ComInterop;

namespace Esatto.Win32.Rdp.DvcApi.Broker
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Any(a => !a.Equals("-Embedding", StringComparison.InvariantCultureIgnoreCase)))
            {
                var startCommand = System.Reflection.Assembly.GetEntryAssembly().Location;
                if (args.Length == 1)
                {
                    if (args[0].Equals("/i", StringComparison.CurrentCultureIgnoreCase)
                        || args[0].Equals("/install", StringComparison.CurrentCultureIgnoreCase))
                    {
                        DvcMessageLoop.Register(Guid.Parse(DvcBrokerConstants.PluginClsid), 
                            DvcBrokerConstants.PluginProgId, startCommand);

                        return;
                    }
                    else if (args[0].Equals("/u", StringComparison.CurrentCultureIgnoreCase)
                        || args[0].Equals("/uninstall", StringComparison.CurrentCultureIgnoreCase))
                    {
                        DvcMessageLoop.Unregister(Guid.Parse(DvcBrokerConstants.PluginClsid));

                        return;
                    }
                }

                PrintUsage(startCommand);
                return;
            }
            else
            {
                var broker = new ChannelBroker();
                var plugin = RawDynamicVirtualClientChannel.Create("ESBR", broker.AcceptConnection);
                Application.Run(new BrokerMessageLoop(plugin, broker));
            }
        }

        private static void PrintUsage(string startCommand)
        {
            Console.Error.WriteLine($@"DVC Channel Broker out-of-process COM Server for mstsc.exe

Usage:
    /install    Register the DVC with mstsc.exe for the native bitness of this binary
    /uninstall  Unregister the DVC with mstsc.exe
    [no args]   Run the out-of-process server

Server metadata:
    CLSID:      {DvcBrokerConstants.PluginClsid}
    ProgID:     {DvcBrokerConstants.PluginProgId}
    LS32:       {startCommand}
");
        }
    }

    class BrokerMessageLoop : DvcMessageLoop
    {
        public BrokerMessageLoop(IDynamicVirtualClientChannelFactory brokerFactory, ChannelBroker targetBroker)
            : base(new[] { brokerFactory }, Guid.Parse(DvcBrokerConstants.PluginClsid), false)
        {
            this.BrokerRegistration = new ClassObjectRegistration(Guid.Parse(DvcBrokerConstants.BrokerClsid),
                CreateClassFactoryFor(() => targetBroker), CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | REGCLS.SUSPENDED);
            CoResumeClassObjects();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.BrokerRegistration.Dispose();
            }
        }

        private readonly ClassObjectRegistration BrokerRegistration;
    }
}
