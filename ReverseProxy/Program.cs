using PAO.Server.Base.Network;
using System;

namespace ReverseProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            BaseServer server = new BaseServer();
            server.Start(BaseServer.ProxyIP, BaseServer.ProxyPort);

            Console.ReadLine();
        }
    }
}
