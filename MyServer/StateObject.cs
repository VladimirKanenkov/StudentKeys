using System.Text;
using System.Net.Sockets;

namespace MyServer
{
    // State object for reading client data asynchronously
    public class StateObject
    {
        // Client  socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 1024;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();

        public int clientNum = 1;

        public long sizeReceived = 0;

        public static int countClient = 0;

        public long sizePacket = 0;

    }
}
