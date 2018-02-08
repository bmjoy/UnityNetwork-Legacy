using System;
using System.Net;
using System.Collections.Generic;
using Lidgren.Network;

namespace MonoLightTech.UnityNetwork.P2P
{
    public sealed class Packet
    {
        public enum DataType
        {
            Unknown = 0,
            Byte,
            Ushort,
            Uint,
            Ulong,
            Sbyte,
            Short,
            Int,
            Long,
            String,
            Double,
            Float,
            Bool,
            IPEndPoint
        }

        private Dictionary<string, Pair<DataType, object>> _fields;

        public Packet()
        {
            _Empty();
        }

        private void _Empty()
        {
            _fields = new Dictionary<string, Pair<DataType, object>>();
            Count = 0;
            Keys = new string[0];
        }

        internal Packet(NetBuffer buffer)
        {
            Deserialize(buffer);
        }

        internal void Deserialize(NetBuffer buffer)
        {
            try
            {
                _fields = new Dictionary<string, Pair<DataType, object>>();

                while (true)
                {
                    string key = buffer.ReadString();
                    if (string.IsNullOrEmpty(key)) break;

                    DataType type = (DataType) buffer.ReadByte();
                    if (type == DataType.Unknown) throw new Exception();

                    switch (type)
                    {
                        case DataType.Byte:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadByte()));
                            break;
                        case DataType.Ushort:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadUInt16()));
                            break;
                        case DataType.Uint:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadUInt32()));
                            break;
                        case DataType.Ulong:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadUInt64()));
                            break;
                        case DataType.Sbyte:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadSByte()));
                            break;
                        case DataType.Short:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadInt16()));
                            break;
                        case DataType.Int:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadInt32()));
                            break;
                        case DataType.Long:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadInt64()));
                            break;
                        case DataType.String:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadString()));
                            break;
                        case DataType.Double:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadDouble()));
                            break;
                        case DataType.Float:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadFloat()));
                            break;
                        case DataType.Bool:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadBoolean()));
                            break;
                        case DataType.IPEndPoint:
                            _fields.Add(key, new Pair<DataType, object>(type, buffer.ReadIPEndPoint()));
                            break;
                    }
                }

                Count = _fields.Count;
                Keys = new string[_fields.Count];
                _fields.Keys.CopyTo(Keys, 0);
            }
            catch
            {
                _Empty();
            }
        }

        internal void Serialize(NetBuffer buffer)
        {
            foreach (KeyValuePair<string, Pair<DataType, object>> entry in _fields)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Value.Left == DataType.Unknown || entry.Value.Right == null)
                    continue;

                buffer.Write(entry.Key); // Key
                buffer.Write((byte) entry.Value.Left); // Type

                switch (entry.Value.Left) // Payload
                {
                    case DataType.Byte:
                        buffer.Write((byte) entry.Value.Right);
                        break;
                    case DataType.Ushort:
                        buffer.Write((ushort) entry.Value.Right);
                        break;
                    case DataType.Uint:
                        buffer.Write((uint) entry.Value.Right);
                        break;
                    case DataType.Ulong:
                        buffer.Write((ulong) entry.Value.Right);
                        break;
                    case DataType.Sbyte:
                        buffer.Write((sbyte) entry.Value.Right);
                        break;
                    case DataType.Short:
                        buffer.Write((short) entry.Value.Right);
                        break;
                    case DataType.Int:
                        buffer.Write((int) entry.Value.Right);
                        break;
                    case DataType.Long:
                        buffer.Write((long) entry.Value.Right);
                        break;
                    case DataType.String:
                        buffer.Write((string) entry.Value.Right);
                        break;
                    case DataType.Double:
                        buffer.Write((double) entry.Value.Right);
                        break;
                    case DataType.Float:
                        buffer.Write((float) entry.Value.Right);
                        break;
                    case DataType.Bool:
                        buffer.Write((bool) entry.Value.Right);
                        break;
                    case DataType.IPEndPoint:
                        buffer.Write((IPEndPoint) entry.Value.Right);
                        break;
                }
            }
        }

        public int Count { get; private set; }
        public string[] Keys { get; private set; }

        public void Set(string key, Pair<DataType, object> entry)
        {
            if (_fields.ContainsKey(key)) _fields[key] = entry;
            else
            {
                _fields.Add(key, entry);

                Count = _fields.Count;
                Keys = new string[_fields.Count];
                _fields.Keys.CopyTo(Keys, 0);
            }
        }

        public void SetByte(string key, byte value)
        {
            Set(key, new Pair<DataType, object>(DataType.Byte, value));
        }

        public void SetUshort(string key, ushort value)
        {
            Set(key, new Pair<DataType, object>(DataType.Ushort, value));
        }

        public void SetUint(string key, uint value)
        {
            Set(key, new Pair<DataType, object>(DataType.Uint, value));
        }

        public void SetUlong(string key, ulong value)
        {
            Set(key, new Pair<DataType, object>(DataType.Ulong, value));
        }

        public void SetSbyte(string key, sbyte value)
        {
            Set(key, new Pair<DataType, object>(DataType.Sbyte, value));
        }

        public void SetShort(string key, short value)
        {
            Set(key, new Pair<DataType, object>(DataType.Short, value));
        }

        public void SetInt(string key, int value)
        {
            Set(key, new Pair<DataType, object>(DataType.Int, value));
        }

        public void SetLong(string key, long value)
        {
            Set(key, new Pair<DataType, object>(DataType.Long, value));
        }

        public void SetString(string key, string value)
        {
            Set(key, new Pair<DataType, object>(DataType.String, value));
        }

        public void SetDouble(string key, double value)
        {
            Set(key, new Pair<DataType, object>(DataType.Double, value));
        }

        public void SetFloat(string key, float value)
        {
            Set(key, new Pair<DataType, object>(DataType.Float, value));
        }

        public void SetBool(string key, bool value)
        {
            Set(key, new Pair<DataType, object>(DataType.Bool, value));
        }

        public void SetIPEndPoint(string key, IPEndPoint value)
        {
            Set(key, new Pair<DataType, object>(DataType.IPEndPoint, value));
        }

        public Pair<DataType, object> Get(string key)
        {
            return _fields.ContainsKey(key) ? _fields[key] : null;
        }

        private object _Get(string key, DataType type)
        {
            if (!_fields.ContainsKey(key)) return null;
            Pair<DataType, object> entry = _fields[key];
            if (entry.Left != type) return null;
            return entry.Right;
        }

        public byte GetByte(string key)
        {
            return (byte) (_Get(key, DataType.Byte) ?? 0);
        }

        public ushort GetUshort(string key)
        {
            return (ushort) (_Get(key, DataType.Ushort) ?? 0);
        }

        public uint GetUint(string key)
        {
            return (uint) (_Get(key, DataType.Uint) ?? 0);
        }

        public ulong GetUlong(string key)
        {
            return (ulong) (_Get(key, DataType.Ulong) ?? 0);
        }

        public sbyte GetSbyte(string key)
        {
            return (sbyte) (_Get(key, DataType.Sbyte) ?? 0);
        }

        public short GetShort(string key)
        {
            return (short) (_Get(key, DataType.Short) ?? 0);
        }

        public int GetInt(string key)
        {
            return (int) (_Get(key, DataType.Int) ?? 0);
        }

        public long GetLong(string key)
        {
            return (long) (_Get(key, DataType.Long) ?? 0);
        }

        public string GetString(string key)
        {
            return (string) _Get(key, DataType.String);
        }

        public double GetDouble(string key)
        {
            return (double) (_Get(key, DataType.Double) ?? 0);
        }

        public float GetFloat(string key)
        {
            return (float) (_Get(key, DataType.Float) ?? 0);
        }

        public bool GetBool(string key)
        {
            return (bool) (_Get(key, DataType.Bool) ?? false);
        }

        public IPEndPoint GetIPEndPoint(string key)
        {
            return (IPEndPoint) _Get(key, DataType.IPEndPoint);
        }

        public override string ToString()
        {
            string result = "Packet with " + Count + " entries:";

            foreach (string key in Keys)
            {
                Pair<DataType, object> entry = Get(key);
                switch (entry.Left)
                {
                    case DataType.Unknown:
                        result += Environment.NewLine + key + " (Unknown) =>";
                        break;
                    case DataType.Byte:
                        result += Environment.NewLine + key + " (Byte) => " + ((byte) entry.Right);
                        break;
                    case DataType.Ushort:
                        result += Environment.NewLine + key + " (Ushort) => " + ((ushort) entry.Right);
                        break;
                    case DataType.Uint:
                        result += Environment.NewLine + key + " (Uint) => " + ((uint) entry.Right);
                        break;
                    case DataType.Ulong:
                        result += Environment.NewLine + key + " (Ulong) => " + ((ulong) entry.Right);
                        break;
                    case DataType.Sbyte:
                        result += Environment.NewLine + key + " (Sbyte) => " + ((sbyte) entry.Right);
                        break;
                    case DataType.Short:
                        result += Environment.NewLine + key + " (Short) => " + ((short) entry.Right);
                        break;
                    case DataType.Int:
                        result += Environment.NewLine + key + " (Int) => " + ((int) entry.Right);
                        break;
                    case DataType.Long:
                        result += Environment.NewLine + key + " (Long) => " + ((long) entry.Right);
                        break;
                    case DataType.String:
                        result += Environment.NewLine + key + " (String) => " + ((string) entry.Right);
                        break;
                    case DataType.Double:
                        result += Environment.NewLine + key + " (Double) => " + ((double) entry.Right);
                        break;
                    case DataType.Float:
                        result += Environment.NewLine + key + " (Float) => " + ((float) entry.Right);
                        break;
                    case DataType.Bool:
                        result += Environment.NewLine + key + " (Bool) => " + ((bool) entry.Right);
                        break;
                    case DataType.IPEndPoint:
                        result += Environment.NewLine + key + " (IPEndPoint) => " + ((IPEndPoint) entry.Right);
                        break;
                }
            }

            return result;
        }
    }
}