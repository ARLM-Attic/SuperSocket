using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using SuperSocket.Common;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Command;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketBase.Protocol;

namespace SuperSocket.SocketEngine
{
    /// <summary>
    /// Socket Session, all application session should base on this class
    /// </summary>
    abstract class SocketSession : ISocketSession
    {
        public IAppSession AppSession { get; private set; }

        protected readonly object SyncRoot = new object();

        private int m_InSending = 0;

        protected bool SyncSend { get; private set; }

        public SocketSession(Socket client)
            : this(Guid.NewGuid().ToString())
        {
            if (client == null)
                throw new ArgumentNullException("client");

            m_Client = client;
            LocalEndPoint = (IPEndPoint)client.LocalEndPoint;
            RemoteEndPoint = (IPEndPoint)client.RemoteEndPoint;
        }

        public SocketSession(string sessionID)
        {
            SessionID = sessionID;
        }

        public virtual void Initialize(IAppSession appSession)
        {
            AppSession = appSession;
            SyncSend = appSession.Config.SyncSend;
        }

        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        /// <value>The session ID.</value>
        public string SessionID { get; private set; }


        /// <summary>
        /// Gets or sets the config.
        /// </summary>
        /// <value>
        /// The config.
        /// </value>
        public IServerConfig Config { get; set; }

        /// <summary>
        /// Starts this session.
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Says the welcome information when a client connectted.
        /// </summary>
        protected virtual void StartSession()
        {
            AppSession.StartSession();
        }

        /// <summary>
        /// Called when [close].
        /// </summary>
        protected virtual void OnClose(CloseReason reason)
        {
            m_IsClosed = true;

            Interlocked.CompareExchange(ref m_InSending, 0, 1);

            var closedHandler = Closed;
            if (closedHandler != null)
            {
                closedHandler(this, reason);
            }
        }

        /// <summary>
        /// Occurs when [closed].
        /// </summary>
        public Action<ISocketSession, CloseReason> Closed { get; set; }

        IPosList<ArraySegment<byte>> m_SendingItems;

        private IPosList<ArraySegment<byte>> GetSendingItems()
        {
            if (m_SendingItems == null)
            {
                m_SendingItems = new PosList<ArraySegment<byte>>();
            }

            return m_SendingItems;
        }

        /// <summary>
        /// Starts the sending.
        /// </summary>
        public void StartSend()
        {
            if (Interlocked.CompareExchange(ref m_InSending, 1, 0) == 1)
                return;

            var sendingItems = GetSendingItems();

            if (!AppSession.TryGetSendingData(sendingItems))
                Interlocked.Decrement(ref m_InSending);

            SendResponse(sendingItems);
        }

        protected abstract void SendAsync(IPosList<ArraySegment<byte>> items);

        protected abstract void SendSync(IPosList<ArraySegment<byte>> items);

        private void SendResponse(IPosList<ArraySegment<byte>> items)
        {
            if (SyncSend)
            {
                SendSync(items);
            }
            else
            {
                SendAsync(items);
            }
        }

        protected virtual void OnSendingCompleted()
        {
            var sendingItems = GetSendingItems();
            sendingItems.Clear();
            sendingItems.Position = 0;

            if (AppSession.TryGetSendingData(sendingItems))
            {
                SendResponse(sendingItems);
            }
            else
            {
                Interlocked.Decrement(ref m_InSending);
            }
        }

        public abstract void ApplySecureProtocol();

        public Stream GetUnderlyStream()
        {
            return new NetworkStream(Client);
        }


        private Socket m_Client;
        /// <summary>
        /// Gets or sets the client.
        /// </summary>
        /// <value>The client.</value>
        public Socket Client
        {
            get { return m_Client; }
        }

        private bool m_IsClosed = false;

        protected bool IsClosed
        {
            get { return m_IsClosed; }
        }

        /// <summary>
        /// Gets the local end point.
        /// </summary>
        /// <value>The local end point.</value>
        public virtual IPEndPoint LocalEndPoint { get; protected set; }

        /// <summary>
        /// Gets the remote end point.
        /// </summary>
        /// <value>The remote end point.</value>
        public virtual IPEndPoint RemoteEndPoint { get; protected set; }

        /// <summary>
        /// Gets or sets the secure protocol.
        /// </summary>
        /// <value>The secure protocol.</value>
        public SslProtocols SecureProtocol { get; set; }

        public virtual void Close(CloseReason reason)
        {
            var client = m_Client;

            if(client == null)
                return;

            if (Interlocked.CompareExchange(ref m_Client, null, client) == client)
            {
                client.SafeClose();
                OnClose(reason);
            }
        }
    }
}
