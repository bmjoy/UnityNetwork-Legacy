using System;
using System.Net;
using System.Threading;
using Lidgren.Network;

namespace MonoLightTech.UnityNetwork.S2C
{
    public sealed class Client
    {
        public bool IsAlive { get; private set; }

        public Connection Connection
        {
            get { return _connection; }
        }

        private Handler _handler;
        private NetPeer _peer;
        private Connection _connection;

        public Client(Handler handler, Config config)
        {
            IsAlive = false;
            _handler = handler;

            #region Config

            NetPeerConfiguration netConfig = new NetPeerConfiguration(config.AppId)
            {
                AutoExpandMTU = false,
                AutoFlushSendQueue = true,
                AcceptIncomingConnections = false,
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
        }

        ~Client()
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

        public void Connect(IPEndPoint ipEndPoint, Packet packet)
        {
            NetOutgoingMessage outMessage = _peer.CreateMessage();
            packet.Serialize(outMessage);

            _connection = new Connection(
                ConnectionType.Server,
                _peer.Connect(ipEndPoint, outMessage),
                ConnectionStatus.Connecting);
        }

        public void Disconnect(Packet packet)
        {
            _connection.Status = ConnectionStatus.Disconnecting;
            if (string.IsNullOrEmpty(packet.GetString("Reason")))
                packet.SetString("Reason", "Client closed the connection");
            _connection.NetConnection.Disconnect("_" + packet.ToBase64String());
        }

        public void Send(Delivery delivery, Packet packet, Channel channel = Channel.Default)
        {
            try
            {
                NetOutgoingMessage outMessage = _peer.CreateMessage();
                packet.Serialize(outMessage);
                _connection.NetConnection.SendMessage(outMessage, (NetDeliveryMethod)delivery, (int)channel);
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
                    case NetIncomingMessageType.StatusChanged:
                        {
                            NetConnectionStatus status = (NetConnectionStatus)inMessage.ReadByte();
                            if (status != NetConnectionStatus.Connected && status != NetConnectionStatus.Disconnected)
                                break;

                            if (status == NetConnectionStatus.Connected)
                            {
                                _connection.Status = ConnectionStatus.Connected;
                                _handler.Connect(_connection);
                            }
                            else
                            {
                                _connection.Status = ConnectionStatus.Disconnected;
                                string reason = inMessage.ReadString();
                                if (reason.StartsWith("_"))
                                {
                                    Packet packet = reason.Substring(1).ToPacket();
                                    if(string.IsNullOrEmpty(packet.GetString("Reason")))
                                        packet.SetString("Reason", "Unknown");
                                    _handler.Disconnect(_connection, packet);
                                }
                                else
                                {
                                    Packet packet = new Packet();
                                    packet.SetString("Reason", reason);
                                    _handler.Disconnect(_connection, packet);
                                }
                            }
                        }
                        break;
                    case NetIncomingMessageType.Data:
                        {
                            _handler.Data(_connection, (Delivery)inMessage.DeliveryMethod, new Packet(inMessage), (Channel)inMessage.SequenceChannel);
                        }
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        {
                            _connection.Latency = inMessage.ReadFloat();
                            _handler.Latency(_connection, _connection.Latency);
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