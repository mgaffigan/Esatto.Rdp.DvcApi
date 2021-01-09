#define DEBUG
#define TRACE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Rdp.DvcBroker
{
    public static class Logger
    {
        public static void Error(Exception ex, string message)
        {
            Debug.WriteLine($"[DVCB] {message}\r\n{ex}");
        }
    }
}
