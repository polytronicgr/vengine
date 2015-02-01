﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace VDGTech
{
    public static class Debugger
    {
        static UdpClient client = new UdpClient();
        public static void Send(string key, object value)
        {
            var bytes = Encoding.UTF8.GetBytes(key + "=" + value.ToString());
            client.BeginSend(bytes, bytes.Length, "127.0.0.1", 8765, null, null);
        }
    }
}