using Esatto.Win32.Com;
using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
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
            DynamicVirtualClientApplication.Run(args,
                new[] { RawDynamicVirtualClientChannel.Create("TEST1", HandleTest1) });
        }

        private static async void HandleTest1(AsyncDynamicVirtualClientChannel obj)
        {
            await obj.SendMessageAsync(Encoding.UTF8.GetBytes("Connected"));
            while (true)
            {
                var text = Encoding.UTF8.GetString(await obj.ReadMessageAsync());
                Console.WriteLine($"{obj.GetHashCode():x8}\tReceived {text}");
                await obj.SendMessageAsync(Encoding.UTF8.GetBytes(text));
            }
        }

        private static async void HandleTest1(RawDynamicVirtualClientChannel obj)
        {
            Console.WriteLine($"{obj.GetHashCode():x8}\tConnected");
            obj.SenderDisconnected += (_2, e) => Console.WriteLine($"{obj.GetHashCode():x8}\tDisconnected {e}");
            obj.Exception += (_1, e) => Console.WriteLine($"{obj.GetHashCode():x8}\tException on background thread: {e.ExceptionObject}");
            obj.MessageReceived += (_1, e) =>
            {
                Console.WriteLine($"{obj.GetHashCode():x8}\t > " + Encoding.UTF8.GetString(e.Message));
            };

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
    }
}
