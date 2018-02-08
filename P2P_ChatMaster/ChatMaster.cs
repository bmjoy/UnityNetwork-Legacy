using System;
using System.Net;
using MonoLightTech.UnityNetwork.P2P;

internal static class ChatMaster
{
    private static Master _master;

    private static void Main()
    {
        Handler handler = new Handler(
            _HandleInitialize,
            _HandleTerminate,
            _HandleLogin,
            _HandleConnect,
            _HandleLatency,
            _HandleDisconnect,
            _HandleRoom,
            _HandleRoomsPage,
            _HandleRoomOperation,
            _HandleData,
            _HandleForeignData,
            _HandleLog);

        Config config = new Config("P2P_Chat", 9696);

        _master = new Master(handler, config);
    }

    private static void _HandleInitialize(bool success, Exception exception)
    {

    }

    private static void _HandleTerminate(bool failure, Exception exception)
    {

    }

    private static Pair<bool, Packet> _HandleLogin(Connection connection, Packet packet)
    {
        return null;
    }

    private static void _HandleConnect(Connection connection)
    {

    }


    private static void _HandleLatency(Connection connection, float latency)
    {

    }

    private static void _HandleDisconnect(Connection connection, Packet packet)
    {

    }

    private static void _HandleRoom(Room room)
    {
        
    }

    private static void _HandleRoomsPage(string[] roomIds, byte page, byte pages)
    {
        
    }

    private static void _HandleRoomOperation(RoomOperation operation, Room room)
    {
        
    }

    private static void _HandleData(Connection connection, Delivery delivery, Packet packet, Channel channel)
    {

    }

    private static void _HandleForeignData(IPEndPoint ipEndPoint, byte[] data)
    {

    }

    private static void _HandleLog(LogType type, string message, object argument)
    {

    }
}