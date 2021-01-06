using Esatto.Win32.Rdp.DvcApi.SessionHostApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.Raw.OnTerminalServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var dvc = DynamicVirtualServerChannel.Open("TEST1"); //"ECHO");
            var resp = ReadAsync(dvc);
            while (true)
            {
                var cb = Encoding.UTF8.GetBytes(Console.ReadLine());
                if (cb.Length == 0) break;
                await dvc.WritePacketAsync(cb, 0, cb.Length);
            }

            Console.WriteLine("Shutting down.");
            dvc.Dispose();

            try
            {
                await resp;
            }
            catch (ObjectDisposedException)
            {
                // no-op
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception on read thread: {ex}");
            }
        }

        private static async Task ReadAsync(DynamicVirtualServerChannel dvc)
        {
            while (true)
            {
                var packet = await dvc.ReadPacketAsync();

                Console.WriteLine("> " + Encoding.UTF8.GetString(packet));
            }
        }
    }
}
