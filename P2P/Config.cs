namespace MonoLightTech.UnityNetwork.P2P
{
    public sealed class Config
    {
        public string AppId { get; private set; }
        public ushort Port { get; private set; }
        public int MaximumHandshakeAttempts { get; private set; }
        public float ResendHandshakeInterval { get; private set; }
        public int MaximumConnections { get; private set; }
        public float ConnectionTimeout { get; private set; }
        public float PingInterval { get; private set; }
        public int MaximumTransmissionUnit { get; private set; }
        public int MessageCacheSize { get; private set; }

        public Config(
            string appId,
            ushort port = 0,
            int maximumHandshakeAttempts = 5,
            float resendHandshakeInterval = 3,
            int maximumConnections = 64,
            float connectionTimeout = 15,
            float pingInterval = 5,
            int maximumTransmissionUnit = 512,
            int messageCacheSize = 1024)
        {
            AppId = appId;
            Port = port;
            MaximumHandshakeAttempts = maximumHandshakeAttempts;
            ResendHandshakeInterval = resendHandshakeInterval;
            MaximumConnections = maximumConnections;
            ConnectionTimeout = connectionTimeout;
            PingInterval = pingInterval;
            MaximumTransmissionUnit = maximumTransmissionUnit;
            MessageCacheSize = messageCacheSize;
        }
    }
}