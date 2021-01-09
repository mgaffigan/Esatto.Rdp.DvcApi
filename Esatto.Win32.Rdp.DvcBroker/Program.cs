using Esatto.Win32.Com;
using Esatto.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Esatto.Win32.Com.ComInterop;
using static Esatto.Rdp.DvcApi.Broker.BrokerConstants;
using Esatto.Rdp.DvcApi;

namespace Esatto.Rdp.DvcBroker
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
                        PluginMessageLoop.Register(Guid.Parse(PluginClsid), PluginProgId, startCommand);

                        return;
                    }
                    else if (args[0].Equals("/u", StringComparison.CurrentCultureIgnoreCase)
                        || args[0].Equals("/uninstall", StringComparison.CurrentCultureIgnoreCase))
                    {
                        PluginMessageLoop.Unregister(Guid.Parse(PluginClsid));

                        return;
                    }
                }

                PrintUsage(startCommand);
                return;
            }
            else
            {
                var broker = new ChannelBroker();
                Application.Run(new BrokerMessageLoop(broker.AcceptConnection, broker));
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
    CLSID:      {PluginClsid}
    ProgID:     {PluginProgId}
    LS32:       {startCommand}
");
        }
    }

    class BrokerMessageLoop : PluginMessageLoop
    {
        public BrokerMessageLoop(Action<IAsyncDvcChannel> acceptHandler, ChannelBroker targetBroker)
            : base(new Dictionary<string, Action<IAsyncDvcChannel>>() { { BrokerChannelName, acceptHandler } },
                Guid.Parse(PluginClsid), false)
        {
            this.BrokerRegistration = new ClassObjectRegistration(Guid.Parse(BrokerClsid),
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
