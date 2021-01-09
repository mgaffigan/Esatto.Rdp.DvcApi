using Esatto.Rdp.DvcApi;
using Esatto.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestApp.Raw.OnRdpClient
{
    class Program
    {
        static void Main(string[] args)
        {
            PluginApplication.Run(args, new Dictionary<string, Action<IAsyncDvcChannel>> { { "TEST1", HandleTest2 } });
        }

        private static async void HandleTest1(IAsyncDvcChannel obj)
        {
            await obj.SendMessageAsync(Encoding.UTF8.GetBytes("Connected"));
            while (true)
            {
                var text = Encoding.UTF8.GetString(await obj.ReadMessageAsync());
                Console.WriteLine($"{obj.GetHashCode():x8}\tReceived {text}");
                await obj.SendMessageAsync(Encoding.UTF8.GetBytes(text));
            }
        }

        private static async void HandleTest2(IAsyncDvcChannel obj)
        {
            Console.WriteLine($"{obj.GetHashCode():x8}\tConnected");
            obj.Disconnected += (_2, e) => Console.WriteLine($"{obj.GetHashCode():x8}\tDisconnected");
            ReceiveAsync(obj);

            try
            {
                while (true)
                {
                    await Task.Delay(5000);
                    obj.SendMessage(Encoding.UTF8.GetBytes($"{obj.GetHashCode():x8}\t HB {DateTime.Now}"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{obj.GetHashCode():x8}\tCannot reply {ex}");
            }
        }

        private static async void ReceiveAsync(IAsyncDvcChannel obj)
        {
            while (true)
            {
                Console.WriteLine($"{obj.GetHashCode():x8}\t > " + Encoding.UTF8.GetString(await obj.ReadMessageAsync()));
            }
        }
    }
}
