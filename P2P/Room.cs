namespace MonoLightTech.UnityNetwork.P2P
{
    public sealed class Room
    {
        public string Id { get; private set; }
        public Connection Connection { get; private set; }
        public Packet Packet { get; internal set; }

        internal Room(string id, Connection connection, Packet packet)
        {
            Id = id;
            Connection = connection;
            Packet = packet;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(Room)) return false;

            Room other = (Room) obj;
            if (other.Connection == null || Connection == null) return false;

            return other.Connection.Equals(Connection);
        }

        public override int GetHashCode()
        {
            return Connection.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Room => [Owner: {0}]", Connection.IPEndPoint);
        }
    }
}