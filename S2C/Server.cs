using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Lidgren.Network;

namespace MonoLightTech.UnityNetwork.S2C
{
    public sealed class Server
    {
        public bool IsAlive { get; private set; }

        public Connection[] Connections
        {
            get { return _connectionsCache; }
        }

        private Handler _handler;
        private NetPeer _peer;
        private List<Connection> _connections;
        private Connection[] _connectionsCache;

        public Server(Handler handler, Config config)
        {
            IsAlive = false;
            _handler = handler;

            #region Config

            NetPeerConfiguration netConfig = new NetPeerConfiguration(config.AppId)
            {
                AutoExpandMTU = false,
                AutoFlushSendQueue = true,
                AcceptIncomingConnections = true,
                EnableUPnP = false,
                NetworkThreadName = "MonoLightTech.UnityNetwork.S2C",
                UseMessageRecycling = true,
                DefaultOutgoingMessageCapacity = Environment.ProcessorCount * 2,
                Port = config.Port,
                MaximumHandshakeAttempts = config.MaximumHandshakeAttempts,
                ResendHandshakeInterval = config.ResendHandshakeInterval,
                MaximumConnections = config.MaximumConnections,
                ConnectionTimeout = config.ConnectionTimeout,
                PingInterval = config.PingInterval,
                MaximumTransmissionUnit = config.MaximumTransmissionUnit,
                RecycledCacheMaxCount = config.MessageCacheSize
            };

            #region Messages

            foreach (NetIncomingMessageType type in Enum.GetValues(typeof(NetIncomingMessageType)))
                netConfig.DisableMessageType(type);

            netConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            netConfig.EnableMessageType(NetIncomingMessageType.StatusChanged);
            netConfig.EnableMessageType(NetIncomingMessageType.Data);
            netConfig.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            netConfig.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            netConfig.EnableMessageType(NetIncomingMessageType.DebugMessage);
            netConfig.EnableMessageType(NetIncomingMessageType.WarningMessage);
            netConfig.EnableMessageType(NetIncomingMessageType.ErrorMessage);

            #endregion

            #endregion

            _peer = new NetPeer(netConfig);

            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            _peer.RegisterReceivedCallback(_Callback);

            _connections = new List<Connection>();
            _connectionsCache = new Connection[0];
        }

        ~Server()
        {
            _Reset();
        }

        private void _Reset()
        {
            IsAlive = false;
            if (_peer != null) _peer.Shutdown("Terminated");
        }

        public void Initialize()
        {
            try
            {
                IsAlive = true;
                _peer.Start();

                _handler.Initialize(true);
            }
            catch (Exception exception)
            {
                _Reset();
                _handler.Initialize(false, exception);
            }
        }

        public void Terminate()
        {
            _Reset();
            _handler.Terminate(false);
        }

        public void Disconnect(Connection connection, Packet packet)
        {
            connection.Status = ConnectionStatus.Disconnecting;
            if (string.IsNullOrEmpty(packet.GetString("Reason")))
                packet.SetString("Reason", "Server closed the connection");
            connection.NetConnection.Disconnect("_" + packet.ToBase64String());
        }

        public void SendTo(Connection connection, Delivery delivery, Packet packet, Channel channel = Channel.Default)
        {
            try
            {
                if (connection.Status != ConnectionStatus.Connected) return;

                NetOutgoingMessage outMessage = _peer.CreateMessage();
                packet.Serialize(outMessage);
                connection.NetConnection.SendMessage(outMessage, (NetDeliveryMethod)delivery, (int)channel);
            }
            catch (NetException)
            {
            }
        }

        public void SendTo(Connection[] connections, Delivery delivery, Packet packet, Channel channel = Channel.Default)
        {
            try
            {
                NetOutgoingMessage outMessage = _peer.CreateMessage();
                packet.Serialize(outMessage);
                _peer.SendMessage(
                    outMessage,
                    connections.
                        Where(x => x.Status == ConnectionStatus.Connected).
                        Select(x => x.NetConnection).
                        ToArray(),
                    (NetDeliveryMethod)delivery,
                    (int)channel);
            }
            catch (NetException)
            {
            }
        }

        public void SendToAll(Delivery delivery, Packet packet, Channel channel = Channel.Default)
        {
            try
            {
                NetOutgoingMessage outMessage = _peer.CreateMessage();
                packet.Serialize(outMessage);
                _peer.SendMessage(
                    outMessage,
                    _connectionsCache.
                        Where(x => x.Status == ConnectionStatus.Connected).
                        Select(x => x.NetConnection).
                        ToArray(),
                    (NetDeliveryMethod)delivery,
                    (int)channel);
            }
            catch (NetException)
            {
            }
        }

        public void SendToForeign(IPEndPoint ipEndPoint, byte[] data, int offset, int length)
        {
            _peer.RawSend(data, offset, length, ipEndPoint);
        }

        private void _Callback(object peer)
        {
            if (!IsAlive) return;

            try
            {
                NetIncomingMessage inMessage = _peer.ReadMessage();
                if (inMessage == null) return;

                switch (inMessage.MessageType)
                {
                    case NetIncomingMessageType.ConnectionApproval:
                        {
                            Connection connection = new Connection(ConnectionType.Client, inMessage.SenderConnection,
                                ConnectionStatus.Connecting);
                            Pair<bool, Packet> result = _handler.Login(connection, new Packet(inMessage));

                            if (result == null)
                            {
                                inMessage.SenderConnection.Deny("Failed to perform login");
                                break;
                            }

                            if (!result.Left)
                            {
                                if (string.IsNullOrEmpty(result.Right.GetString("Reason")))
                                    result.Right.SetString("Reason", "Login refused");
                                inMessage.SenderConnection.Deny("_" + result.Right.ToBase64String());
                                break;
                            }

                            lock (_connections)
                            {
                                if (!_connections.Contains(connection))
                                {
                                    _connections.Add(connection);
                                    _connectionsCache = _connections.ToArray();
                                }
                            }

                            inMessage.SenderConnection.Approve();
                        }
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        {
                            NetConnectionStatus status = (NetConnectionStatus)inMessage.ReadByte();
                            if (status != NetConnectionStatus.Connected && status != NetConnectionStatus.Disconnected)
                                break;

                            Connection connection =
                                _connectionsCache.FirstOrDefault(x => x.IPEndPoint.Equals(inMessage.SenderEndPoint));

                            if (connection == null)
                            {
                                if (status == NetConnectionStatus.Connected)
                                    inMessage.SenderConnection.Disconnect("Connection not found");
                                break;
                            }

                            if (status == NetConnectionStatus.Connected)
                            {
                                connection.Status = ConnectionStatus.Connected;
                                _handler.Connect(connection);
                            }
                            else
                            {
                                lock (_connections)
                                {
                                    if (_connections.Remove(connection))
                                        _connectionsCache = _connections.ToArray();
                                }

                                connection.Status = ConnectionStatus.Disconnected;
                                string reason = inMessage.ReadString();
                                if (reason.StartsWith("_"))
                                {
                                    Packet packet = reason.Substring(1).ToPacket();
                                    if (string.IsNullOrEmpty(packet.GetString("Reason")))
                                        packet.SetString("Reason", "Unknown");
                                    _handler.Disconnect(connection, packet);
                                }
                                else
                                {
                                    Packet packet = new Packet();
                                    packet.SetString("Reason", reason);
                                    _handler.Disconnect(connection, packet);
                                }
                            }
                        }
                        break;
                    case NetIncomingMessageType.Data:
                        {
                            Connection connection = _connectionsCache.FirstOrDefault(x => x.IPEndPoint.Equals(inMessage.SenderEndPoint));

                            if (connection == null)
                            {
                                inMessage.SenderConnection.Disconnect("Connection not found");
                                break;
                            }

                            _handler.Data(connection, (Delivery)inMessage.DeliveryMethod, new Packet(inMessage), (Channel)inMessage.SequenceChannel);
                        }
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        {
                            Connection connection =
                                _connectionsCache.FirstOrDefault(x => x.IPEndPoint.Equals(inMessage.SenderEndPoint));

                            if (connection == null)
                            {
                                inMessage.SenderConnection.Disconnect("Connection not found");
                                break;
                            }

                            connection.Latency = inMessage.ReadFloat();
                            _handler.Latency(connection, connection.Latency);
                        }
                        break;

                    case NetIncomingMessageType.UnconnectedData:
                        {
                            _handler.ForeignData(inMessage.SenderEndPoint, inMessage.Data);
                        }
                        break;
                    case NetIncomingMessageType.DebugMessage:
                        _handler.Log(LogType.Info, inMessage.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        _handler.Log(LogType.Warning, inMessage.ReadString());
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        _handler.Log(LogType.Error, inMessage.ReadString());
                        break;
                }

                _peer.Recycle(inMessage);
            }
            catch (Exception exception)
            {
                _Reset();
                _handler.Terminate(true, exception);
            }
        }
    }
}