using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Com
{
    internal static class Contract
    {
        internal static void Requires(bool v)
        {
            if (!v)
            {
                throw new InvalidOperationException();
            }
        }
    }
}
