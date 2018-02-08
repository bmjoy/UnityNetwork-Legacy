using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Lidgren.Network;

namespace MonoLightTech.UnityNetwork.P2P
{
    public sealed class Peer
    {
        public bool IsAlive { get; private set; }
        public bool IsHosting { get; private set; }

        public Connection Master { get { return _master; } }
        public Connection Server { get { return _server; } }
        public Connection[] Clients { get { return _clientsCache; } }

        private Handler _handler = null;

        private NetPeer _peer = null;

        private Connection _master = null;

        private Connection _server = null;
        private Packet _serverRoom = null;

        private List<Connection> _clients = null;
        private Connection[] _clientsCache = null;

        private List<Pair<IPEndPoint, string>> _auths = null;
        private Pair<IPEndPoint, string>[] _authsCache = null;

        private string _targetRoomId = null;
        private Packet _joinPacket = null;

        private byte _roomsPageReqId = 0;

        public Peer(Handler handler, Config config)
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
                NetworkThreadName = "MonoLightTech.UnityNetwork.P2P",
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

            _clients = new List<Connection>();
            _clientsCache = new Connection[0];

            _auths = new List<Pair<IPEndPoint, string>>();
            _authsCache = new Pair<IPEndPoint, string>[0];
        }

        ~Peer()
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
            NetOutgoingMessage outBuffer = _peer.CreateMessage();
            packet.Serialize(outBuffer);

            _master = new Connection(
                ConnectionType.Master,
                _peer.Connect(ipEndPoint, outBuffer),
                ConnectionStatus.Connecting);
        }

        public void Disconnect(Connection connection, Packet packet)
        {
            connection.Status = ConnectionStatus.Disconnecting;
            switch (connection.Type)
            {
                default:
                case ConnectionType.Master:
                    if (string.IsNullOrEmpty(packet.GetString("Reason")))
                        packet.SetString("Reason", "Peer closed the connection");
                    break;
                case ConnectionType.Server:
                    if (string.IsNullOrEmpty(packet.GetString("Reason")))
                        packet.SetString("Reason", "Client closed the connection");
                    break;
                case ConnectionType.Client:
                    if (string.IsNullOrEmpty(packet.GetString("Reason")))
                        packet.SetString("Reason", "Server closed the connection");
                    break;
            }
            connection.NetConnection.Disconnect("_" + packet.ToBase64String());
        }

        #region Host

        public void CreateRoom(Packet packet)
        {
            IsHosting = true;
            _serverRoom = packet;

            NetOutgoingMessage outMessage = _peer.CreateMessage();
            outMessage.Write((byte)DataType.Room);
            outMessage.Write((byte)RoomCommand.Create);
            packet.Serialize(outMessage);
            _master.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void UpdateRoom(RoomUpdateType type, Packet packet)
        {
            if (!IsHosting) return;

            if (type == RoomUpdateType.Additive)
            {
                foreach (string key in packet.Keys)
                    _serverRoom.Set(key, packet.Get(key));
            }
            else _serverRoom = packet;

            // Send update to Master
            NetOutgoingMessage outMessageM = _peer.CreateMessage();
            outMessageM.Write((byte)DataType.Room);
            outMessageM.Write((byte)RoomCommand.Update);
            outMessageM.Write((byte)type);
            packet.Serialize(outMessageM);
            _master.NetConnection.SendMessage(outMessageM, NetDeliveryMethod.ReliableOrdered, 0);

            // Send update to connected Clients
            NetOutgoingMessage outMessageC = _peer.CreateMessage();
            outMessageC.Write((byte)DataType.Room);
            outMessageC.Write((byte)RoomCommand.Update);
            _serverRoom.Serialize(outMessageC);

            NetConnection[] clientConns =
                _clientsCache.
                    Where(x => x.Status == ConnectionStatus.Connected).
                    Select(x => x.NetConnection).ToArray();

            if (clientConns.Length > 0)
                _peer.SendMessage(outMessageC, clientConns, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void DestroyRoom()
        {
            IsHosting = false;
            _serverRoom = null;

            lock (_clients)
            {
                foreach (Connection connection in _clients)
                {
                    connection.NetConnection.Disconnect("Server closed the room and connection");
                    connection.Status = ConnectionStatus.Disconnecting;
                }

                _clients.Clear();
                _clientsCache = new Connection[0];
            }

            lock (_auths)
            {
                _auths.Clear();
                _authsCache = new Pair<IPEndPoint, string>[0];
            }

            NetOutgoingMessage outMessage = _peer.CreateMessage();
            outMessage.Write((byte)DataType.Room);
            outMessage.Write((byte)RoomCommand.Destroy);
            _master.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableOrdered, 0);
        }

        #endregion

        public void GetRoom(string id)
        {
            NetOutgoingMessage outMessage = _peer.CreateMessage();
            outMessage.Write((byte)DataType.Room);
            outMessage.Write((byte)RoomCommand.GetRoom);
            outMessage.Write(id);
            _master.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void GetRoomsPage(Packet packet, byte page = 0, PageSize size = PageSize._10)
        {
            _roomsPageReqId++;
            if (_roomsPageReqId == 0) _roomsPageReqId = 1;

            NetOutgoingMessage outMessage = _peer.CreateMessage();
            outMessage.Write((byte)DataType.Room);
            outMessage.Write((byte)RoomCommand.GetRoomsPage);
            outMessage.Write(_roomsPageReqId);
            outMessage.Write(page);
            outMessage.Write((byte)size);
            packet.Serialize(outMessage);
            _master.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableSequenced, 1);
        }

        public void JoinRoom(string id, Packet packet)
        {
            if (IsHosting ||
                (_server != null &&
                 (_server.Status == ConnectionStatus.Connecting || _server.Status == ConnectionStatus.Connected)) ||
                packet == null)
                return;

            _targetRoomId = id;
            _joinPacket = packet;

            NetOutgoingMessage outMessage = _peer.CreateMessage();
            outMessage.Write((byte)DataType.Room);
            outMessage.Write((byte)RoomCommand.Join);
            outMessage.Write(id);
            _master.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void LeaveRoom()
        {
            _targetRoomId = null;

            if (_server == null)
                return;

            Packet packet = new Packet();
            packet.SetString("Reason", "Left");
            Disconnect(_server, packet);
        }

        public void SendTo(Connection connection, Delivery delivery, Packet packet, Channel channel = Channel.Default)
        {
            try
            {
                if (connection.Status != ConnectionStatus.Connected) return;

                NetOutgoingMessage outMessage = _peer.CreateMessage();
                outMessage.Write((byte)DataType.Data);
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
                outMessage.Write((byte)DataType.Data);
                packet.Serialize(outMessage);
                _peer.SendMessage(
                    outMessage,
                    connections.
                        Where(x => x.Status == ConnectionStatus.Connected).
                        Select(x => x.NetConnection).ToArray(),
                    (NetDeliveryMethod)delivery,
                    (int)channel);
            }
            catch (NetException)
            {
            }
        }

        public void SendToClients(Delivery delivery, Packet packet, Channel channel = Channel.Default)
        {
            try
            {
                NetOutgoingMessage outMessage = _peer.CreateMessage();
                outMessage.Write((byte)DataType.Data);
                packet.Serialize(outMessage);
                _peer.SendMessage(
                    outMessage,
                    _clientsCache.
                        Where(x => x.Status == ConnectionStatus.Connected).
                        Select(x => x.NetConnection).ToArray(),
                    (NetDeliveryMethod)delivery,
                    (int)channel);
            }
            catch (NetException)
            {
            }
        }

        public void SendToMaster(Delivery delivery, Packet packet, Channel channel = Channel.Default)
        {
            try
            {
                NetOutgoingMessage outMessage = _peer.CreateMessage();
                outMessage.Write((byte)DataType.Data);
                packet.Serialize(outMessage);
                _master.NetConnection.SendMessage(outMessage, (NetDeliveryMethod)delivery, (int)channel);
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
                    default:
                        break;

                    case NetIncomingMessageType.ConnectionApproval: // Client to Server
                        {
                            if (!IsHosting)
                            {
                                inMessage.SenderConnection.Deny("Peer not hosting!");
                                break;
                            }

                            string token = null;
                            try
                            {
                                token = inMessage.ReadString();
                            }
                            catch
                            {
                            }
                            if (token == null)
                            {
                                inMessage.SenderConnection.Deny("Bad authentication data");
                                break;
                            }

                            Pair<IPEndPoint, string> auth =
                                _authsCache.FirstOrDefault(x => x.Left.Equals(inMessage.SenderEndPoint));
                            if (auth == null)
                            {
                                inMessage.SenderConnection.Deny("Authentication not found");
                                break;
                            }

                            lock (_auths)
                            {
                                _auths.Remove(auth);
                                _authsCache = _auths.ToArray();
                            }

                            if (auth.Right != token)
                            {
                                inMessage.SenderConnection.Deny("Authentication failed");
                                break;
                            }

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

                            lock (_clients)
                            {
                                if (!_clients.Contains(connection))
                                {
                                    _clients.Add(connection);
                                    _clientsCache = _clients.ToArray();
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

                            Connection connection = null;
                            if (_master != null && _master.IPEndPoint.Equals(inMessage.SenderEndPoint))
                                connection = _master;
                            else if (_server != null && _server.IPEndPoint.Equals(inMessage.SenderEndPoint))
                                connection = _server;
                            else
                                connection =
                                    _clientsCache.FirstOrDefault(x => x.IPEndPoint.Equals(inMessage.SenderEndPoint));

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
                                // Drop/Remove connection by 'Type'
                                switch (connection.Type)
                                {
                                    case ConnectionType.Master:
                                        {
                                            _master = null;

                                            // Server
                                            if (IsHosting)
                                                IsHosting = false;

                                            _serverRoom = null;

                                            lock (_clients)
                                            {
                                                foreach (Connection conn in _clients)
                                                {
                                                    conn.NetConnection.Disconnect("Server closed the room and connection");
                                                    conn.Status = ConnectionStatus.Disconnecting;
                                                }

                                                _clients.Clear();
                                                _clientsCache = new Connection[0];
                                            }

                                            lock (_auths)
                                            {
                                                _auths.Clear();
                                                _authsCache = new Pair<IPEndPoint, string>[0];
                                            }

                                            // Client
                                            if (_server != null)
                                            {
                                                _server.NetConnection.Disconnect("Left");
                                                _server = null;
                                            }
                                        }
                                        break;
                                    case ConnectionType.Server:
                                        _server = null;
                                        break;
                                    case ConnectionType.Client:
                                        {
                                            lock (_clients)
                                            {
                                                if (_clients.Remove(connection))
                                                    _clientsCache = _clients.ToArray();
                                            }
                                        }
                                        break;
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
                            Connection connection = null;
                            if (_master != null && _master.IPEndPoint.Equals(inMessage.SenderEndPoint))
                                connection = _master;
                            else if (_server != null && _server.IPEndPoint.Equals(inMessage.SenderEndPoint))
                                connection = _server;
                            else
                                connection =
                                    _clientsCache.FirstOrDefault(x => x.IPEndPoint.Equals(inMessage.SenderEndPoint));

                            if (connection == null)
                            {
                                inMessage.SenderConnection.Disconnect("Connection not found");
                                break;
                            }

                            DataType type = DataType.Unknown;

                            try
                            {
                                type = (DataType)inMessage.ReadByte();
                            }
                            catch
                            {
                            }

                            switch (type)
                            {
                                default:
                                case DataType.Unknown:
                                    break;
                                case DataType.Room:
                                    if (inMessage.DeliveryMethod == NetDeliveryMethod.ReliableOrdered ||
                                        inMessage.DeliveryMethod == NetDeliveryMethod.ReliableSequenced)
                                        _Room(connection, inMessage);
                                    break;
                                case DataType.Data:
                                    _handler.Data(connection, (Delivery)inMessage.DeliveryMethod, new Packet(inMessage),
                                        (Channel)inMessage.SequenceChannel);
                                    break;
                            }
                        }
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        {
                            Connection connection = null;
                            if (_master != null && _master.IPEndPoint.Equals(inMessage.SenderEndPoint))
                                connection = _master;
                            else if (_server != null && _server.IPEndPoint.Equals(inMessage.SenderEndPoint))
                                connection = _server;
                            else
                                connection =
                                    _clientsCache.FirstOrDefault(x => x.IPEndPoint.Equals(inMessage.SenderEndPoint));

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

        private void _Room(Connection connection, NetIncomingMessage inMessage)
        {
            RoomCommand command = RoomCommand.Unknown;

            try
            {
                command = (RoomCommand)inMessage.ReadByte();
            }
            catch
            {
            }

            switch (command)
            {
                default:
                case RoomCommand.Unknown:
                    break;

                case RoomCommand.Create:
                case RoomCommand.Destroy:
                    // Unexpected
                    break;

                case RoomCommand.Update: // Server to Client
                    _handler.RoomOperation(
                        RoomOperation.Update,
                        new Room(
                            connection.IPEndPoint.ToString().MD5String(),
                            connection,
                            new Packet(inMessage)));
                    break;
                case RoomCommand.GetRoom:
                    {
                        string roomId = null;
                        try
                        {
                            roomId = inMessage.ReadString();
                        }
                        catch
                        {
                        }
                        if (roomId == null) break;

                        _handler.Room(new Room(roomId, null, new Packet(inMessage)));
                    }
                    break;
                case RoomCommand.GetRoomsPage:
                    {
                        byte reqId = 0;
                        byte page = 0;
                        byte pages = 0;
                        byte roomCount = 0;

                        try
                        {
                            reqId = inMessage.ReadByte();
                            page = inMessage.ReadByte();
                            pages = inMessage.ReadByte();
                            roomCount = inMessage.ReadByte();
                        }
                        catch
                        {
                        }

                        if (reqId == 0 ||
                            reqId != _roomsPageReqId ||
                            page == 0 ||
                            roomCount == 0)
                            return;

                        string[] roomIds = new string[roomCount];
                        try
                        {
                            for (int i = 0; i < roomCount; i++)
                                roomIds[i] = inMessage.ReadString();
                        }
                        catch
                        {
                            return;
                        }

                        _handler.RoomsPage(roomIds, page, pages);
                    }
                    break;
                case RoomCommand.Auth: // Master to Server
                    {
                        if (!IsHosting) return;

                        IPEndPoint ipEndPoint = null;
                        try
                        {
                            ipEndPoint = inMessage.ReadIPEndPoint();
                        }
                        catch
                        {
                        }
                        if (ipEndPoint == null) return;

                        // Create auth
                        string token = (ipEndPoint + DateTime.Now.ToString()).MD5String();
                        Pair<IPEndPoint, string> auth = new Pair<IPEndPoint, string>(ipEndPoint, token);

                        lock (_auths)
                        {
                            Pair<IPEndPoint, string> oldAuth = _auths.FirstOrDefault(x => x.Left.Equals(ipEndPoint));
                            if (oldAuth != null)
                                _auths.Remove(oldAuth);

                            _auths.Add(auth);
                            _authsCache = _auths.ToArray();
                        }

                        // Send 'Hello' to client (NAT Punchthrough)
                        byte[] data = "SA".GetBytes();
                        SendToForeign(ipEndPoint, data, 0, data.Length);

                        // Send accept to Master
                        NetOutgoingMessage outMessage = _peer.CreateMessage();
                        outMessage.Write((byte)DataType.Room);
                        outMessage.Write((byte)RoomCommand.Accept);
                        outMessage.Write(ipEndPoint);
                        outMessage.Write(token);
                        _master.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableOrdered, 0);
                    }
                    break;
                case RoomCommand.Connect: // Master to Client
                    {
                        string id = null;
                        IPEndPoint ipEndPoint = null;
                        string token = null;

                        try
                        {
                            id = inMessage.ReadString();
                            ipEndPoint = inMessage.ReadIPEndPoint();
                            token = inMessage.ReadString();
                        }
                        catch
                        {
                        }

                        if (id == null || ipEndPoint == null || token == null ||
                            string.IsNullOrEmpty(_targetRoomId) || id != _targetRoomId ||
                            _joinPacket == null)
                            return;

                        // Connect to Server
                        NetOutgoingMessage outMessage = _peer.CreateMessage();
                        outMessage.Write(token);
                        _joinPacket.Serialize(outMessage);

                        _server = new Connection(ConnectionType.Server, _peer.Connect(ipEndPoint, outMessage),
                            ConnectionStatus.Connecting);

                        _targetRoomId = null;
                        _joinPacket = null;
                    }
                    break;
            }
        }
    }
}