using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcBroker
{
    static class DvcBrokerConstants
    {
        public const string
            PluginClsid = "{33436F3F-1D6C-4DFA-A913-9589A24FE0AD}",
            BrokerClsid = "{684AB7F2-D70C-473F-9D0D-7A1A602EA48F}";
    }

    class Program
    {
        static void Main(string[] args)
        {
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
                Application.Run(new DvcMessageLoop(chfs, wtsPluginClsid));
            }
        }
    }
}
