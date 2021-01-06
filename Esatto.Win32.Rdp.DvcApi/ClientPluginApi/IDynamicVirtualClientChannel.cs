﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    public interface IDynamicVirtualClientChannel
    {
        void ReadMessage(byte[] data);

        void Close();
    }
}