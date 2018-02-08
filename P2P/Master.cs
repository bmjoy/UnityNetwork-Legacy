using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Lidgren.Network;

namespace MonoLightTech.UnityNetwork.P2P
{
    public sealed class Master
    {
        public bool IsAlive { get; private set; }

        public Connection[] Connections { get { return _connectionsCache; } }
        public Room[] Rooms { get { return _roomsCache; } }

        private Handler _handler = null;
        private NetPeer _peer = null;
        private List<Connection> _connections = null;
        private Connection[] _connectionsCache = null;
        private List<Room> _rooms = null;
        private Room[] _roomsCache = null;

        public Master(Handler handler, Config config)
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

            _connections = new List<Connection>();
            _connectionsCache = new Connection[0];

            _rooms = new List<Room>();
            _roomsCache = new Room[0];
        }

        ~Master()
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
                packet.SetString("Reason", "Master closed the connection");
            connection.NetConnection.Disconnect("_" + packet.ToBase64String());
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
                outMessage.Write((byte)DataType.Data);
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
                    default:
                        break;

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

                                if (connection.Room != null)
                                {
                                    lock (_rooms)
                                    {
                                        if (_rooms.Remove(connection.Room))
                                            _roomsCache = _rooms.ToArray();
                                    }

                                    _handler.RoomOperation(RoomOperation.Destroy, connection.Room);

                                    connection.Room = null;
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
                            Connection connection =
                                _connectionsCache.FirstOrDefault(x => x.IPEndPoint.Equals(inMessage.SenderEndPoint));

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
                    {
                        Packet packet = new Packet(inMessage);
                        Room room = new Room(connection.IPEndPoint.ToString().MD5String(), connection, packet);
                        connection.Room = room;

                        lock (_rooms)
                        {
                            if (!_rooms.Contains(room))
                            {
                                _rooms.Add(room);
                                _roomsCache = _rooms.ToArray();
                            }
                        }

                        _handler.RoomOperation(RoomOperation.Create, room);
                    }
                    break;
                case RoomCommand.Update:
                    {
                        if (connection.Room == null)
                            break;

                        RoomUpdateType type = RoomUpdateType.Unknown;

                        try
                        {
                            type = (RoomUpdateType)inMessage.ReadByte();
                        }
                        catch
                        {
                        }

                        Packet packet = new Packet(inMessage);
                        switch (type)
                        {
                            default:
                            case RoomUpdateType.Unknown:
                                break;
                            case RoomUpdateType.Change:
                                connection.Room.Packet = packet;
                                break;
                            case RoomUpdateType.Additive:
                                foreach (string key in packet.Keys)
                                    connection.Room.Packet.Set(key, packet.Get(key));
                                break;
                        }

                        _handler.RoomOperation(RoomOperation.Update, connection.Room);
                    }
                    break;
                case RoomCommand.Destroy:
                    {
                        lock (_rooms)
                        {
                            if (_rooms.Remove(connection.Room))
                                _roomsCache = _rooms.ToArray();
                        }

                        _handler.RoomOperation(RoomOperation.Destroy, connection.Room);

                        connection.Room = null;
                    }
                    break;
                case RoomCommand.GetRoom:
                    {
                        string id = null;
                        try
                        {
                            id = inMessage.ReadString();
                        }
                        catch
                        {
                        }
                        if (string.IsNullOrEmpty(id)) return;

                        Room room = _roomsCache.FirstOrDefault(x => x.Id == id);
                        if (room == null) return;

                        NetOutgoingMessage outMessage = _peer.CreateMessage();
                        outMessage.Write((byte)DataType.Room);
                        outMessage.Write((byte)RoomCommand.GetRoom);
                        outMessage.Write(room.Id);
                        room.Packet.Serialize(outMessage);
                        connection.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableOrdered, 0);
                    }
                    break;
                case RoomCommand.GetRoomsPage:
                    {
                        byte reqId = 0;
                        byte page = 0;
                        PageSize size = PageSize.Unknown;
                        Packet packet = null;

                        try
                        {
                            reqId = inMessage.ReadByte();
                            page = inMessage.ReadByte();
                            size = (PageSize)inMessage.ReadByte();
                        }
                        catch
                        {
                        }

                        if (reqId == 0 ||
                            page == 0 ||
                            size == PageSize.Unknown ||
                            !Enum.IsDefined(typeof(PageSize), size))
                            return;

                        packet = new Packet(inMessage);

                        Room[] filteredRooms =
                            _roomsCache.Where(
                                x =>
                                {
                                    foreach (string key in packet.Keys)
                                    {
                                        Pair<Packet.DataType, object> entry = x.Packet.Get(key);
                                        if (entry == null ||
                                            !entry.Right.Equals(packet.Get(key).Right))
                                            return false;
                                    }

                                    return true;
                                }).ToArray();

                        if (filteredRooms.Length == 0) return;

                        int pages = (int)Math.Ceiling(filteredRooms.Length / (double)size);
                        if (page > pages) page = (byte)pages;

                        NetOutgoingMessage outMessage = _peer.CreateMessage();
                        outMessage.Write((byte)DataType.Room);
                        outMessage.Write((byte)RoomCommand.GetRoomsPage);
                        outMessage.Write(reqId);
                        outMessage.Write(page);
                        outMessage.Write((byte)pages);

                        Room[] roomsToSend = filteredRooms.Skip((page - 1) * (byte)size).Take((byte)size).ToArray();
                        outMessage.Write((byte)roomsToSend.Length);

                        foreach (Room room in roomsToSend)
                            outMessage.Write(room.Id);

                        connection.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableSequenced, 1);
                    }
                    break;
                case RoomCommand.Join: // Client to Master
                    {
                        string id = null;
                        try
                        {
                            id = inMessage.ReadString();
                        }
                        catch
                        {
                        }
                        if (string.IsNullOrEmpty(id)) return;

                        Room room = _roomsCache.FirstOrDefault(x => x.Id == id);
                        if (room == null) return;

                        // Send 'Auth' request to target room server
                        NetOutgoingMessage outMessage = _peer.CreateMessage();
                        outMessage.Write((byte)DataType.Room);
                        outMessage.Write((byte)RoomCommand.Auth);
                        outMessage.Write(connection.IPEndPoint);
                        room.Connection.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableOrdered, 0);

                        // Console.WriteLine("Join room received from <{0}> targeting room #{1} which is hosted by <{2}>",
                        // connection.IPEndPoint, room.Id, room.Connection.IPEndPoint);
                    }
                    break;
                case RoomCommand.Accept: // Server to Master
                    {
                        if (connection.Room == null) return;

                        IPEndPoint ipEndPoint = null;
                        string token = null;

                        try
                        {
                            ipEndPoint = inMessage.ReadIPEndPoint();
                            token = inMessage.ReadString();
                        }
                        catch
                        {
                        }

                        if (ipEndPoint == null || token == null) return;

                        Connection clientConnection = _connectionsCache.FirstOrDefault(x => x.IPEndPoint.Equals(ipEndPoint));
                        if (clientConnection == null) return;

                        // Send 'Connect' to Client
                        NetOutgoingMessage outMessage = _peer.CreateMessage();
                        outMessage.Write((byte)DataType.Room);
                        outMessage.Write((byte)RoomCommand.Connect);
                        outMessage.Write(connection.Room.Id);
                        outMessage.Write(connection.IPEndPoint);
                        outMessage.Write(token);
                        clientConnection.NetConnection.SendMessage(outMessage, NetDeliveryMethod.ReliableOrdered, 0);

                        // Console.WriteLine("Accept connection received from <{0}> for <{1}>", connection.IPEndPoint, clientConnection.IPEndPoint);
                    }
                    break;
            }
        }
    }
}