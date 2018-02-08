using System.Net;
using Lidgren.Network;

namespace MonoLightTech.UnityNetwork.P2P
{
    public sealed class Connection
    {
        public ConnectionType Type { get; private set; }
        public ConnectionStatus Status { get; internal set; }

        public IPEndPoint IPEndPoint { get; private set; }
        internal NetConnection NetConnection { get; set; }

        public float Latency { get; internal set; }
        public Packet Packet { get; private set; }

        public Room Room { get; internal set; }

        internal Connection(ConnectionType type, NetConnection netConnection, ConnectionStatus status)
        {
            Type = type;
            NetConnection = netConnection;
            Status = status;

            IPEndPoint = netConnection.RemoteEndPoint;
            Latency = -1;
            Packet = new Packet();
            Room = null;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(Connection)) return false;

            Connection other = (Connection) obj;
            if (other.IPEndPoint == null || IPEndPoint == null) return false;

            return other.Type.Equals(Type) && other.IPEndPoint.Equals(IPEndPoint);
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode() ^ IPEndPoint.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Connection => [IPEndPoint: {0}] [Type: {1}] [Status: {2}] [Latency: {3}] ", IPEndPoint,
                Type, Status, Latency);
        }
    }
}