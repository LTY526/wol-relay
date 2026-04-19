using System.Net;
using System.Net.Sockets;

namespace WOLRelay.Services
{
    public class WakeOnLanService
    {
        public void Send(string macAddress, string broadcastIp = "255.255.255.255", int port = 9)
        {
            var macBytes = ParseMac(macAddress);

            var packet = BuildMagicPacket(macBytes);

            using var client = new UdpClient();
            client.EnableBroadcast = true;

            var endpoint = new IPEndPoint(IPAddress.Parse(broadcastIp), port);
            client.Send(packet, packet.Length, endpoint);
        }

        private static byte[] ParseMac(string mac)
        {
            var cleaned = mac.Replace(":", "").Replace("-", "");

            if (cleaned.Length != 12)
                throw new ArgumentException("Invalid MAC address");

            var bytes = new byte[6];

            for (int i = 0; i < 6; i++)
            {
                bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static byte[] BuildMagicPacket(byte[] mac)
        {
            var packet = new byte[6 + (16 * 6)];

            // 6 x 0xFF
            for (int i = 0; i < 6; i++)
                packet[i] = 0xFF;

            // MAC repeated 16 times
            for (int i = 6; i < packet.Length; i += 6)
                Buffer.BlockCopy(mac, 0, packet, i, 6);

            return packet;
        }
    }
}
