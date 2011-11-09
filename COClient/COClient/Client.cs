using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

namespace COClient
{


    // State object for receiving data from remote device.
    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        public StringBuilder sb = new StringBuilder();
    }

    public class Client
    {
        // The port number for the remote device.
        private const int port = 11000;

        // ManualResetEvent instances signal completion.
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        // The response from the remote device.
        private static String response = String.Empty;

        // Input from user.
        private static String input;

        private static Socket serverSocket;

        private static String ipAddress = "127.0.0.1";


        public static bool validIP(string addr)
        {
            //create our match pattern
            string pattern = @"^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$";
            //create our Regular Expression object
            Regex check = new Regex(pattern);
            //boolean variable to hold the status
            bool valid = false;
            //check to make sure an ip address was provided
            //address provided so use the IsMatch Method
            //of the Regular Expression object
            valid = check.IsMatch(addr, 0);

            //return the results
            return valid;
        }

        private static void StartClient()
        {
            // Connect to a remote device.
            try
            {
                Console.WriteLine("Enter IP address(leave blank to use loopback): ");

                // If ip address given is not valid just give
                // the current ip Address else use given address.
                if (!validIP((ipAddress = Console.ReadLine())))
                {
                    IPHostEntry host;
                    host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (IPAddress ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipAddress = ip.ToString();
                        }
                    }
                }

                // Establish the remote endpoint for the socket.
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ipAddress), port);

                // Create a TCP/IP socket.
                serverSocket = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.
                serverSocket.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), serverSocket);
                connectDone.WaitOne();

                // start receiving
                Receive(serverSocket);

                while ((input = promptRL("> ")) != "quit")
                {

                    input += "\n";

                    // Send test data to the remote device.
                    Send(serverSocket, input);
                    sendDone.WaitOne();

                    // Write the response to the console.
                    // NOTE: This should wait to write till an apporpiate time to write.
                    // Console.WriteLine("Response received : {0}", response);

                }


            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static String promptRL(String prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}",
                    client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Receive(Socket client)
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = client;

                Console.WriteLine("Receive(); \n");
                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);
                //Console.WriteLine("ReceiveCallback({0}); \n", bytesRead);


                // There might be more data, so store the data received so far.
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // All the data has arrived; put it in response.

                if (state.sb.Length > 1)
                {
                    response = state.sb.ToString();
                    Console.WriteLine("Server: {0}", response);
                    parseServerCommands(response);
                    state.sb.Clear();
                }

                // Get the rest of the data.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);

                // Signal that all bytes have been received.
                receiveDone.Set();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void parseServerCommands(string response)
        {
            Console.WriteLine("working...");
        }

        private static void Send(Socket client, String data)
        {
            sendDone.Reset();

            // Convert the string data to byte data using ASCII encoding.
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static int Main(String[] args)
        {
            Console.Title = "Client - Clue Online";
            StartClient();
            return 0;
        }
    }

}
