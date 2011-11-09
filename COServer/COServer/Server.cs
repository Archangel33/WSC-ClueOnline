using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace COServer
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
        // location in data to start reading new content
        public int contentIndex = 0;
    }

    public class Server
    {
        // Thread signal.
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        
        private static string localIP = "127.0.0.1";

        public Server()
        {
        }

        public static void StartListening()
        {
            // Data buffer for incoming data.
            byte[] bytes = new Byte[1024];

            // Find local ipAddress.
            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                }
            }
            Console.WriteLine("local IP: {0}",localIP);


            // Establish the local endpoint for the socket.
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(localIP), 11000);

            // Create a TCP/IP socket.
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();

        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            allDone.Set();

            // Get the socket that handles the client request.
            Socket listener = (Socket)ar.AsyncState;
            
            Socket handler = listener.EndAccept(ar);


            // display that someone has connected to the server
            Console.WriteLine("Accepted new client on RemoteEndPoint: {0}", handler.RemoteEndPoint);

            // Create the state object.
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
            String command = String.Empty;

            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket. 
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.
                state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read 
                // more data.
                content = state.sb.ToString();
                //Console.WriteLine("Original Content: {0} \n ", content);

                if (content.IndexOf("\n") > -1)
                {
                    content.Trim('\n');

                    // All the data has been read from the 
                    // client. Display it on the console.
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                        content.Length, content);

                    Console.WriteLine("Socket : {0} ", handler.RemoteEndPoint);

                    // clear the stringBuilder
                    state.sb.Clear();

                    // parse out what is needed use Send() inside once something needs to be sent.
                    parseCommand(content,handler);


                    Send(handler, content);
                    sendDone.WaitOne();

                    content = String.Empty;
                }

                    // Not all data received. Get more.
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                
            }
        }

        private static void parseCommand(string input, Socket sender)
        {
            Console.WriteLine("parsing command: {0}", input);
            String[] command = input.Split(new Char[] {'\n','\t',' ','\r'});

            switch (command[0])
            {
                case "REQJO":
                    Console.WriteLine("Recieved Join Request");
                    break;
                case "/t":
                case "/c":
                    String txt = String.Empty;
                    for (int i = 1; i < command.Length; i++) txt = txt + " " + command[i]; 
                    Console.WriteLine("{0} says: {1}",sender.RemoteEndPoint.ToString(), txt);
                    break;
                default:
                    Console.WriteLine("\"{0}\" is not a recgonized command please try again", command[0]);
                    break;

            }
        }

        private static void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                sendDone.Set();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        public static int Main(String[] args)
        {
            Console.Title = "Server - Clue Online";
            StartListening();
            return 0;
        }
    }

}
