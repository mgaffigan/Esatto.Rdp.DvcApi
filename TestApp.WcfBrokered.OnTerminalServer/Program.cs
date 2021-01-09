using Esatto.Rdp.DvcApi.WcfDvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestApp.WcfBrokered.OnTerminalServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new ServerClient(new EndpointAddress("esbkdvc:///demoService"));
            client.Open();
            for (int i = 0; i < 1; i++)
            {
                var d = client.DoThing("bar");
                Console.WriteLine(d.Length);
            }
            client.Close();

            foreach (var file in Directory.GetFiles(@".\", $"{GetCurrentProcessId()}_*.txt", SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
            }
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetCurrentProcessId();
    }

    class ServerClient : ClientBase<IServer>, IServer
    {
        public ServerClient(EndpointAddress addy)
            : base(new DvcBinding(), addy)
        {
        }

        public byte[] DoThing(string foo) => Channel.DoThing(foo);
    }

    [ServiceContract(Namespace = "urn:example")]
    public interface IServer
    {
        [OperationContract]
        byte[] DoThing(string foo);
    }
}
