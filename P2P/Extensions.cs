using System;
using System.Net;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Lidgren.Network;

namespace MonoLightTech.UnityNetwork.P2P
{
    public static class Extensions
    {
        #region String / Byte Array

        public static byte[] GetBytes(this string source)
        {
            return Encoding.UTF8.GetBytes(source);
        }

        public static string GetString(this byte[] source)
        {
            return Encoding.UTF8.GetString(source);
        }

        #endregion

        #region IPEndPoint

        public static IPEndPoint ToIPEndPoint(this string endPoint)
        {
            string[] blocks = endPoint.Split(':');
            if (blocks.Length != 2)
                throw new FormatException("Invalid IPEndPoint format");

            IPAddress ip;
            if (!IPAddress.TryParse(blocks[0], out ip))
                throw new FormatException("Invalid IPAddress");

            int port;
            if (!int.TryParse(blocks[1], out port))
                throw new FormatException("Invalid port");

            return new IPEndPoint(ip, port);
        }

        #endregion

        #region MD5

        public static byte[] MD5Bytes(this byte[] source)
        {
            return MD5.Create().ComputeHash(source);
        }

        public static string MD5String(this byte[] source)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (byte b in source)
                stringBuilder.Append(b.ToString("X2"));
            return stringBuilder.ToString();
        }

        public static byte[] MD5Bytes(this string source)
        {
            return source.GetBytes().MD5Bytes();
        }

        public static string MD5String(this string source)
        {
            return source.GetBytes().MD5String();
        }

        #endregion

        #region Byte Array

        public static byte[] CombineBytes(params byte[][] arrays)
        {
            byte[] newArray = new byte[arrays.Sum(x => x.Length)];

            int offset = 0;
            foreach (byte[] bytes in arrays)
            {
                Buffer.BlockCopy(bytes, 0, newArray, offset, bytes.Length);
                offset += bytes.Length;
            }

            return newArray;
        }

        #endregion

        #region NetBuffer / Packet

        internal static NetBuffer ToNetBuffer(this string base64)
        {
            NetBuffer buffer = new NetBuffer();
            buffer.Data = Convert.FromBase64String(base64);
            buffer.LengthBytes = buffer.Data.Length;
            return buffer;
        }

        internal static NetBuffer ToNetBuffer(this Packet packet)
        {
            NetBuffer buffer = new NetBuffer();
            packet.Serialize(buffer);
            return buffer;
        }

        internal static string ToBase64String(this NetBuffer buffer)
        {
            return Convert.ToBase64String(buffer.Data);
        }

        internal static string ToBase64String(this Packet packet)
        {
            return Convert.ToBase64String(ToNetBuffer(packet).Data);
        }

        internal static Packet ToPacket(this string base64)
        {
            return new Packet(base64.ToNetBuffer());
        }

        internal static Packet ToPacket(this NetBuffer buffer)
        {
            return new Packet(buffer);
        }

        #endregion
    }
}