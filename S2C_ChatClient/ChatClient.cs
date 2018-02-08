using System;
using System.Net;
using MonoLightTech.UnityNetwork.S2C;

internal static class ChatClient
{
    private enum Command
    {
        Unknown = 0,
        PublicMessage = 1,
        PrivateMessage = 2,
        Time = 3,
        Clients = 4
    }

    private static Client _client;

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

        Config config = new Config("S2C_Chat");

        _client = new Client(handler, config);
        _client.Initialize();

        bool up = true;
        string input;
        string[] blocks;

        while (up)
        {
            input = Console.ReadLine();
            blocks = input.Split(' ');

            switch (blocks[0].ToUpper())
            {
                default:
                    {
                        Console.WriteLine("Unknown command: " + blocks[0]);
                    }
                    break;
                case "EXIT":
                    {
                        up = false;
                    }
                    break;
                case "CONNECT":
                    {
                        if (_client.Connection.Status != ConnectionStatus.Disconnected)
                        {
                            Console.WriteLine("Must be disconnected!");
                            break;
                        }

                        // TODO
                    }
                    break;
                case "DISCONNECT":
                    {
                        if (_client.Connection.Status != ConnectionStatus.Connected)
                        {
                            Console.WriteLine("Already not connected!");
                            break;
                        }

                        // TODO
                    }
                    break;
                case "IPEP":
                    {
                        Console.WriteLine("Connection IPEndPoint: " + _client.Connection.IPEndPoint);
                    }
                    break;
                case "LATENCY":
                    {
                        Console.WriteLine("Connection latency: " + _client.Connection.Latency);
                    }
                    break;
                case "STATUS":
                    {
                        Console.WriteLine("Connection status: " + _client.Connection.Status);
                    }
                    break;
                case "PUBLICMESSAGE":
                    {
                        Packet messagePacket = new Packet();
                        messagePacket.SetByte("Command", (byte)Command.PublicMessage);
                        messagePacket.SetString("Message", input.Substring(14));

                        _client.Send(Delivery.ReliableOrdered, messagePacket);
                    }
                    break;
                case "PRIVATEMESSAGE":
                    {
                        // TODO
                    }
                    break;
                case "TIME":
                    {
                        Packet timePacket = new Packet();
                        timePacket.SetByte("Command", (byte)Command.Time);

                        _client.Send(Delivery.ReliableUnordered, timePacket);
                    }
                    break;
                case "CLIENTS":
                    {
                        Packet clientsPacket = new Packet();
                        clientsPacket.SetByte("Command", (byte)Command.Clients);

                        _client.Send(Delivery.ReliableUnordered, clientsPacket);
                    }
                    break;
            }
        }

        Console.WriteLine("Terminating...");
        _client.Terminate();
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
        // No need to implement (it's server-side)
        return null;
    }

    private static void _HandleConnect(Connection connection)
    {
        Console.WriteLine("Connect => [IPEndPoint: " + connection.IPEndPoint + "]");
    }


    private static void _HandleLatency(Connection connection, float latency)
    {

    }

    private static void _HandleDisconnect(Connection connection, Packet packet)
    {
        Console.WriteLine("Disconnect => [IPEndPoint: " + connection.IPEndPoint + "] [Reason: " + packet.GetString("Reason") + "]");
    }

    private static void _HandleData(Connection connection, Delivery delivery, Packet packet, Channel channel)
    {
        Console.WriteLine("Data => [IPEndPoint: " + connection.IPEndPoint + "] [Delivery: " + delivery + "] [Channel: " + channel + "]");
        Console.WriteLine(packet);
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