using NLog;
using PAO.Core.Loggers;
using PAO.Core.Reflection;
using PAO.Core.Threading;
using PAO.Core.Xml.Attributes;
using PAO.Core.Xml.Config;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime;

namespace PAO.Server.Base.Network
{
    public class BaseServer : Singleton<BaseServer>
    {
        public static Logger Logger = LogManager.GetCurrentClassLogger();

        #region Variables
        private const string configFilePath = ".//config.xml";

        private Socket socketListener;
        private bool runing = false;

        public static bool isUnderMaintenance = false;

        public CyclicTaskRunner IOTaskPool { get; set; }

        public XmlConfig Config { get; set; }

        public BaseServer()
        {
            DrawAscii();

            NLogHelper.DefineLogProfile(true, true);
            NLogHelper.EnableLogging();

            socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Config = new XmlConfig(configFilePath);
            Config.AddAssemblies(AppDomain.CurrentDomain.GetAssemblies());
            if (!File.Exists(configFilePath))
            {
                Config.Create();
                Logger.Info("config file created & loaded !");   
            }
            else
            {
                Config.Load();
                Logger.Info("config file loaded !");
            }

            GCSettings.LatencyMode = GCLatencyMode.Batch;

            IOTaskPool = new CyclicTaskRunner(100, "Server");
            IOTaskPool.Start();
        }

        #endregion

        [Variable]
        public static string ProxyIP { get; set; } = "127.0.0.1";

        [Variable]
        public static short ProxyPort { get; set; } = 443;

        [Variable]
        public static string AuthIP { get; set; } = "127.0.0.1";

        [Variable]
        public static short AuthPort { get; set; } = 443;

        #region Methods
        public static void DrawAscii()
        {
            string[] logo = new[] {
                "██████╗ ██████╗  ██████╗ ██╗  ██╗██╗   ██╗",
                "██╔══██╗██╔══██╗██╔═══██╗╚██╗██╔╝╚██╗ ██╔╝",
                "██████╔╝██████╔╝██║   ██║ ╚███╔╝  ╚████╔╝ ",
                "██╔═══╝ ██╔══██╗██║   ██║ ██╔██╗   ╚██╔╝  ",
                "██║     ██║  ██║╚██████╔╝██╔╝ ██╗   ██║   ",
                "╚═╝     ╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═╝   ╚═╝   "
            };
                                                        
            foreach (var line in logo)
            {
                Console.SetCursorPosition((Console.WindowWidth - line.Length) / 2, Console.CursorTop);
                Console.WriteLine(line);
            }
        }
        public void Start(string ip, short listenPort)
        {
            runing = true;
            socketListener.Bind(new IPEndPoint(IPAddress.Parse(ip), listenPort));
            socketListener.Listen(5);
            socketListener.BeginAccept(ProcessAccept, socketListener);
        }

        public void Stop()
        {
            runing = false;
            socketListener.Shutdown(SocketShutdown.Both);
        }

        public BaseClient CreateClient(Socket socket)
        {
            return new BaseClient(socket);
        }

        private void ProcessAccept(IAsyncResult result)
        {
            if (runing)
            {
                Socket listener = (Socket)result.AsyncState;
                Socket acceptedSocket = listener.EndAccept(result);

                BaseClient client = CreateClient(acceptedSocket);

                socketListener.BeginAccept(ProcessAccept, socketListener);

                Logger.Info($"client <{client.RealIP}> connected !");
            }
        }

        public static void Shutdown()
        {
            Environment.Exit(0);
        }
        #endregion
    }
}
