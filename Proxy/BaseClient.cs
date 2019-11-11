using NLog;
using System;
using System.Net;
using System.Net.Sockets;

namespace PAO.Server.Base.Network
{
    public class BaseClient
    {
        public static Logger Logger = LogManager.GetCurrentClassLogger();

        #region Variables

        private byte[] m_receiveBuffer;
        private byte[] m_receiveProxyBuffer;

        private Socket m_socket = null;

        public const int bufferLength = 8192;

        public static int BufferSize => bufferLength;

        public string IP => ((IPEndPoint)this.m_socket.RemoteEndPoint) == null ? "0.0.0.0" : ((IPEndPoint)this.m_socket.RemoteEndPoint).Address.ToString();
   
        public string RealIP => ((IPEndPoint)this.m_socket?.RemoteEndPoint)?.Address?.ToString() ?? "ip not found";

        public Socket ProxyClient { get; set; }

        #endregion

        #region Constructor & Deconstructor

        public BaseClient(Socket socket)
        {
            Init();
            Start(socket);
        }

        #endregion

        #region Methods

        public void Start(Socket socket)
        {
            try
            {
                m_socket = socket;
                m_socket.BeginReceive(m_receiveBuffer, 0, bufferLength, SocketFlags.None, ProcessReceive, m_socket);

                ProxyClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                ProxyClient.BeginConnect(BaseServer.AuthIP, BaseServer.AuthPort, ProcessConnect, ProxyClient);
            }
            catch (System.Exception ex)
            {
                OnError(ex);
            }
        }

        private void Stop()
        {
            try
            {
                m_socket.BeginDisconnect(false, ProcessDisconnect, m_socket);
            }
            catch (System.Exception ex)
            {
                OnError(ex);
            }
        }

        public virtual void Dispose()
        {
            m_socket = null;
            ProxyClient = null;

            m_receiveBuffer = null;

            m_receiveProxyBuffer = null;

            UnBindEvents();
        }

        protected virtual void UnBindEvents()
        {
            Connected = null;
            Disconnected = null;
            ErrorThrown = null;
        }

        #endregion

        #region Private Methods

        private void Init()
        {
            try
            {
                OnConnected();

                m_receiveBuffer = new byte[bufferLength];
                m_receiveProxyBuffer = new byte[bufferLength];
                m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (System.Exception ex)
            {
                OnError(ex);
            }
        }

        #endregion

        #region CallBacks

        private void ProcessConnect(IAsyncResult asyncResult)
        {
            Socket client = (Socket)asyncResult.AsyncState;
            client.EndConnect(asyncResult);

            client.BeginReceive(m_receiveProxyBuffer, 0, bufferLength, SocketFlags.None, ProcessReceive, client);
        }

        private void ProcessDisconnect(IAsyncResult asyncResult)
        {
            try
            {
                Socket client = (Socket)asyncResult.AsyncState;
                client.EndDisconnect(asyncResult);
                OnDisconnected();

                Logger.Info($"{this} disconnected !");
            }
            catch (System.Exception ex)
            {
                OnError(ex);
            }
        }

        private void ProcessReceive(IAsyncResult asyncResult)
        {
            Socket client = (Socket)asyncResult.AsyncState;

            if (!client.Connected)
                return;

            int bytesRead = 0;
            try
            {
                if (client == null || !client.Connected)
                    return;

                bytesRead = client.EndReceive(asyncResult);

                if (bytesRead == 0)
                {
                    this.Disconnect();
                    return;
                }

                byte[] data = new byte[bytesRead];
                Array.Copy(client == m_socket ? m_receiveBuffer : m_receiveProxyBuffer, data, bytesRead);

                if (client == m_socket)
                {
                    ProxyClient.Send(data);
                    Logger.Info($"[Client => Auth] IP <{this.RealIP}>");
                }
                else
                {
                    m_socket.Send(data);
                    Logger.Info($"[Auth => Client] IP <{this.RealIP}>");
                }

                client.BeginReceive(client == m_socket ? m_receiveBuffer : m_receiveProxyBuffer, 0, bufferLength, SocketFlags.None, ProcessReceive, client);
            }
            catch (System.Exception ex)
            {
                OnError(ex);
                this.Disconnect();
            }
        }

        #endregion

        #region Events

        public event EventHandler<ConnectedEventArgs> Connected;
        public event EventHandler<DisconnectedEventArgs> Disconnected;
        public event EventHandler<ErrorEventArgs> ErrorThrown;

        private void OnDisconnected()
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(this.m_socket));
        }

        private void OnError(Exception exception)
        {
            Logger.Error(exception);
            ErrorThrown?.Invoke(this, new ErrorEventArgs(exception));
        }

        private void OnConnected()
        {
            Connected?.Invoke(this, new ConnectedEventArgs());
        }
        #endregion

        #region EventArgs

        public class ConnectedEventArgs : EventArgs
        {
        }

        public class DisconnectedEventArgs : EventArgs
        {
            public Socket Socket { get; private set; }

            public DisconnectedEventArgs(Socket socket)
            {

                Socket = socket;
            }
        }

        public class ErrorEventArgs : EventArgs
        {
            public System.Exception Exception { get; private set; }

            public ErrorEventArgs(System.Exception ex)
            {
                Exception = ex;
            }
        }
        public void Disconnect()
        {
            try
            {
                this.Dispose();
                this.Stop();
            }
            catch (System.Exception ex)
            {
                OnError(ex);
            }
        }

        #endregion
    }

}
