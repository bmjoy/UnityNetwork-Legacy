using System;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;
using MonoLightTech.UnityNetwork.S2C;

internal static class ChatServer
{
    private enum Command
    {
        Unknown = 0,
        PublicMessage = 1,
        PrivateMessage = 2,
        Time = 3,
        Clients = 4
    }

    private static Server _server;

    private static void Main()
    {
        Handler handler = new Handler(
            _HandleInitialize,
            _HandleTerminate,
            _HandleLogin,
            _HandleConnect,
            _HandleLatency,
            _HandleDisconnect,
            _HandleData,
            _HandleForeignData,
            _HandleLog);

        Config config = new Config("S2C_Chat", 9696);

        _server = new Server(handler, config);
        _server.Initialize();

        Console.WriteLine("Press [ESC] to terminate.");
        while (Console.ReadKey(true).Key != ConsoleKey.Escape)
        {
        }
        
        Console.WriteLine("Terminating...");
        _server.Terminate();
    }

    private static void _HandleInitialize(bool success, Exception exception)
    {
        Console.WriteLine("Initialize => [Success: " + success + "] [Exception: " + (exception == null ? "null" : exception.ToString()) + "]");

        if (!success)
        {
            Environment.Exit(1);
        }
    }

    private static void _HandleTerminate(bool failure, Exception exception)
    {
        Console.WriteLine("Terminate => [Failure: " + failure + "] [Exception: " + (exception == null ? "null" : exception.ToString()) + "]");
    }

    private static Pair<bool, Packet> _HandleLogin(Connection connection, Packet packet)
    {
        string username = packet.GetString("Username");
        Packet loginPacket = new Packet();

        if (string.IsNullOrEmpty(username))
        {
            loginPacket.SetString("Reason", "Username required!");
            return new Pair<bool, Packet>(false, loginPacket);
        }

        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9]{4,16}$"))
        {
            loginPacket.SetString("Reason", "Bad username!");
            return new Pair<bool, Packet>(false, loginPacket);
        }

        if (_server.Connections.Any(x => x.Packet.GetString("Username").ToUpper().Equals(username.ToUpper())))
        {
            loginPacket.SetString("Reason", "Username in use!");
            return new Pair<bool, Packet>(false, loginPacket);
        }

        return new Pair<bool, Packet>(true, null);
    }

    private static void _HandleConnect(Connection connection)
    {
        Console.WriteLine("Connect => [IPEndPoint: " + connection.IPEndPoint + "] [Username: " + connection.Packet.GetString("Username") + "]");
    }


    private static void _HandleLatency(Connection connection, float latency)
    {

    }

    private static void _HandleDisconnect(Connection connection, Packet packet)
    {
        Console.WriteLine("Disconnect => [IPEndPoint: " + connection.IPEndPoint + "] [Username: " + connection.Packet.GetString("Username") + "] [Reason: " + packet.GetString("Reason") + "]");
    }

    private static void _HandleData(Connection connection, Delivery delivery, Packet packet, Channel channel)
    {
        Console.WriteLine("Data => [IPEndPoint: " + connection.IPEndPoint + "] [Username: " + connection.Packet.GetString("Username") + "] [Delivery: " + delivery + "] [Channel: " + channel + "]");
        Console.WriteLine(packet);

        switch ((Command)packet.GetByte("Command"))
        {
            default:
                {
                }
                return;
            case Command.PublicMessage:
                {
                    string message = packet.GetString("Message");

                    if (string.IsNullOrEmpty(message))
                    {
                        return;
                    }

                    Packet messagePacket = new Packet();
                    messagePacket.SetByte("Command", (byte)Command.PublicMessage);
                    messagePacket.SetString("From", message);
                    messagePacket.SetString("Message", message);

                    _server.SendToAll(Delivery.ReliableOrdered, messagePacket);
                }
                return;
            case Command.PrivateMessage:
                {
                    string message = packet.GetString("Message");

                    if (string.IsNullOrEmpty(message))
                    {
                        return;
                    }

                    string targetUsername = packet.GetString("Target");
                    Connection targetConnection = _server.Connections.FirstOrDefault(x => x.Packet.GetString("Username").ToUpper().Equals(targetUsername.ToUpper()));

                    if (targetConnection == null)
                    {
                        return;
                    }

                    Packet messagePacket = new Packet();
                    messagePacket.SetByte("Command", (byte)Command.PrivateMessage);
                    messagePacket.SetString("From", message);
                    messagePacket.SetString("Message", message);

                    _server.SendTo(targetConnection, Delivery.ReliableOrdered, messagePacket);
                }
                return;
            case Command.Time:
                {
                    Packet timePacket = new Packet();
                    timePacket.SetByte("Command", (byte)Command.Time);
                    timePacket.SetString("Time", "Time is " + DateTime.Now.ToString());

                    _server.SendTo(connection, Delivery.ReliableSequenced, timePacket);
                }
                return;
            case Command.Clients:
                {
                    Connection[] connections = _server.Connections;

                    Packet clientsPacket = new Packet();
                    clientsPacket.SetByte("Command", (byte)Command.Clients);
                    clientsPacket.SetByte("Length", (byte)connections.Length);

                    for (int i = 0; i < connections.Length; i++)
                    {
                        clientsPacket.SetString("Client" + i, connections[i].Packet.GetString("Username"));
                    }

                    _server.SendTo(connection, Delivery.ReliableOrdered, clientsPacket);
                }
                return;
        }
    }

    private static void _HandleForeignData(IPEndPoint ipEndPoint, byte[] data)
    {
        Console.WriteLine("ForeignData => [IPEndPoint: " + ipEndPoint + "] [Length: " + data.Length + "] [Text: " + Extensions.GetString(data) + "]");
    }

    private static void _HandleLog(LogType type, string message, object argument)
    {
        Console.WriteLine("Log => [Type: " + type + "] [Message: " + message + "] [Argument: " + (argument == null ? "null" : argument.ToString()) + "]");
    }
}