namespace MonoLightTech.UnityNetwork.P2P
{
    public enum ConnectionType
    {
        Master = 0,
        Server,
        Client
    }

    public enum ConnectionStatus
    {
        Disconnected = 0,
        Connecting,
        Connected,
        Disconnecting
    }

    internal enum DataType
    {
        Unknown = 0,
        Room = 96,
        Data = 123
    }

    public enum RoomUpdateType
    {
        Unknown = 0,
        Change,
        Additive
    }

    public enum RoomOperation
    {
        Unknown = 0,
        Create, // Server to Master
        Destroy, // Server to Master
        Update // Server to Master, Server to Client
    }

    internal enum RoomCommand
    {
        Unknown = 0,

        Create, // Server to Master
        Update, // Server to Master
        Destroy, // Server to Master

        GetRoom, // Peer to Master
        GetRoomsPage, // Peer to Master

        Join, // Client to Master (A)
        Auth, // Master to Server (B)
        Accept, // Server to Master (C)
        Connect // Master to Client (D)
    }

    public enum PageSize
    {
        Unknown = 0,
        _5 = 5,
        _10 = 10,
        _25 = 25,
        _50 = 50
    }

    public enum Delivery
    {
        Unknown = 0,
        ReliableOrdered = 67,
        ReliableUnordered = 34,
        ReliableSequenced = 35,
        Unreliable = 1,
        UnreliableSequenced = 2
    }

    public enum Channel
    {
        Default = 0,
        _1,
        _2,
        _3,
        _4,
        _5,
        _6,
        _7,
        _8,
        _9,
        _10,
        _11,
        _12,
        _13,
        _14,
        _15,
        _16,
        _17,
        _18,
        _19,
        _20,
        _21,
        _22,
        _23,
        _24,
        _25,
        _26,
        _27,
        _28,
        _29,
        _30,
        _31
    }

    public enum LogType
    {
        Info = 0,
        Warning,
        Error
    }
}