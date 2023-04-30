using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace SocketServer
{
    class Program
    {
        public enum GameState
        {
            WAITFORTWO,
            WAITFORONE,
            WAITFORINPUTS,
        };

        public enum GameEvent
        {
            NONE,
            PLAYERCONNECTS,
            PLAYERDISCONNECTS,
            PLAYERMOVESAREIN,
        };

        static GameState currentState = GameState.WAITFORTWO;
        static GameState newState = GameState.WAITFORTWO;
        static GameEvent currentEvent = GameEvent.NONE;
        
        static TcpListener servSock = null;
        static TcpClient clientSock = null;

        const int serverPort = 333;
        const int clientPort = 334;

        static IPAddress player1address = null;
        static IPAddress player2address = null;

        static string p1ResultString = "";
        static string p2ResultString = "";

        static IPAddress thisIP = null;
        
        public static IPAddress GetLocalIPAddress()
        {

            IPAddress localIP = null;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address;
            }
            return localIP;
        }

        public static string ReceiveData(TcpListener servSock, TcpClient clientSock, byte[] bytes, int receivePort)
        {
            string returnString = "";
            servSock = new TcpListener(IPAddress.Any, receivePort);
            servSock.Start();
            clientSock = new TcpClient();
            Console.WriteLine("Waiting for the client to respond");
            clientSock = servSock.AcceptTcpClient();
            Console.WriteLine("Connected !");
            NetworkStream stream = clientSock.GetStream();
            int num = stream.Read(bytes, 0, bytes.Length);
            returnString = Encoding.ASCII.GetString(bytes, 0, num);

            Console.WriteLine(returnString);

            clientSock.Close();
            servSock.Stop();
            return returnString;
        }

        public static void SendData(string sendLine, IPAddress connectIP, int connectPort)
        {
            TcpClient clientSock = new TcpClient();
            Console.WriteLine("Connecting to client ...");
            clientSock.Connect(connectIP, connectPort);

            NetworkStream stream = clientSock.GetStream();
            byte[] data = Encoding.ASCII.GetBytes(sendLine);
            Console.WriteLine("Sending message to " + connectIP.ToString()  + " - " + sendLine);
            stream.Write(data, 0, data.Length);

            clientSock.Close();
        }

        public static void SetResultStrings()
        {
            byte[] buffer = new byte[1024];
            string assignstring = ReceiveData(servSock, clientSock, buffer, serverPort);

            if (assignstring.StartsWith("/move"))
            {
                string[] elements = assignstring.Split(':');
                int playerNr = 0;
                playerNr = Int32.Parse(elements[1]);
                string moveString = elements[2][0].ToString();
                if (playerNr == 1)
                {
                    p2ResultString = "/opmove:" + elements[1] + ":" + moveString + "%";
                    Console.WriteLine(p2ResultString);
                }
                if (playerNr == 2)
                {
                    p1ResultString = "/opmove:" + elements[1] + ":" + moveString + "%";
                    Console.WriteLine(p1ResultString);
                }
            }
            
        }

        public static GameEvent CheckForNewEvent()
        {
            GameEvent newEvent = GameEvent.NONE;

            if (currentState == GameState.WAITFORTWO || currentState == GameState.WAITFORONE)
            {
                byte[] buffer = new byte[1024];
                string getString = ReceiveData(servSock, clientSock, buffer, serverPort);
                if (getString.StartsWith("/connect:") ==true)
                {
                    string connectIPString;
                    connectIPString = getString.Remove( 0, 9);
                    connectIPString = connectIPString.Remove(connectIPString.Length-1);
                    Console.WriteLine(connectIPString);
                    
                    if ( currentState == GameState.WAITFORTWO )
                    {
                        player1address = IPAddress.Parse(connectIPString);
                        connectIPString = "";
                        
                        newEvent = GameEvent.PLAYERCONNECTS;
                    }
                    if (currentState == GameState.WAITFORONE)
                    {
                        
                        player2address = IPAddress.Parse(connectIPString);
                        connectIPString = "";
                        
                        
                        newEvent = GameEvent.PLAYERCONNECTS;
                    }
                }

            }

            if (currentState == GameState.WAITFORINPUTS)
            {
                SetResultStrings();
                SetResultStrings();
                if (!string.IsNullOrWhiteSpace(p1ResultString) && !string.IsNullOrWhiteSpace(p2ResultString))
                {
                    newEvent = GameEvent.PLAYERMOVESAREIN;
                }
                
            }

            return newEvent;
        }

        public static void HandleEvent()
        {
            switch(currentState)
            {
                case GameState.WAITFORTWO:
                    if (currentEvent == GameEvent.PLAYERCONNECTS)
                    {
                        
                        newState = GameState.WAITFORONE;
                    }
                    break;
                case GameState.WAITFORONE:
                    if (currentEvent == GameEvent.PLAYERCONNECTS)
                    {
                        try
                        {
                            SendData("/assign:1%", player1address, clientPort);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("SocketException: {0}", e.ToString());
                        }
                        try
                        {
                            SendData("/assign:2%", player2address, clientPort);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("SocketException: {0}", e.ToString());
                        }
                        
                        newState = GameState.WAITFORINPUTS;
                    }
                    if (currentEvent == GameEvent.PLAYERDISCONNECTS)
                    {
                        newState = GameState.WAITFORTWO;
                    }
                    break;
                case GameState.WAITFORINPUTS:
                    if (currentEvent == GameEvent.PLAYERMOVESAREIN)
                    {
                        try
                        {
                            SendData(p1ResultString, player1address, clientPort);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("SocketException: {0}", e.ToString());
                        }

                        try
                        {
                            SendData(p2ResultString, player2address, clientPort);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("SocketException: {0}", e.ToString());
                        }

                        newState = GameState.WAITFORTWO;
                    }
                    break;
            }
            
        }

        static void Main(string[] args)
        {

            try
            {
                thisIP = GetLocalIPAddress();
            }
            catch 
            {
                Console.WriteLine("This device is not connected to a network");
            }

            Console.WriteLine("Welcome to Rock Paper Socket server\nThis is your current IP: " + thisIP.ToString());

            while (true)
            {
                currentEvent = CheckForNewEvent();

                if (currentEvent != GameEvent.NONE)
                {
                    HandleEvent();

                    if (newState != currentState)
                    {
                        currentState = newState;
                        Console.WriteLine("entered new state " + currentState.ToString());
                    }
                }
            }
           

        }
    }
}
