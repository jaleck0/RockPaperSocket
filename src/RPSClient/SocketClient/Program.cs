using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;


namespace SocketClient
{
    class Program
    {
        public enum Element
        {
            NONE,
            Rock,
            Paper,
            Scissor
        }

        public enum GameState
        {
            WAITFORCONNECTINPUT,
            WAITFORSERVERRESPONSE,
            WAITFORGAMEINPUT,
            WAITFORGAMERESULT,
            WAITFORREJOINCOMMAND
        };

        public enum GameEvent
        {
            NONE,
            IPENTERED,
            GAMESTARTSIGN,
            SENDGAMEINPUT,
            RECEIVESRESULT,
            RECONNECT,
            DISCONNECT,
        };

        static GameState currentState = GameState.WAITFORCONNECTINPUT;
        static GameState newState = GameState.WAITFORCONNECTINPUT;
        static GameEvent currentEvent = GameEvent.NONE;
        static IPAddress toIP = null;
        const int serverPort = 333;
        const int clientPort = 334;
        static string connectString = "";
        static string disconnectString = "";
        static int playerNr = 0;
        static TcpListener servSock = null;
        static TcpClient clientSock = null;
        const string askForInputString = "Select a Move:\n\n 1:Rock\n 2:Paper\n 3:Scissor\n";
        static Element yourMove = Element.NONE;

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

        public static void SendData(string sendLine, IPAddress connectIP, int connectPort)
        {
            TcpClient clientSock = new TcpClient();
            //Console.WriteLine("Connecting to Server ...");
            clientSock.Connect(connectIP, connectPort);

            NetworkStream stream = clientSock.GetStream();
            byte[] data = Encoding.ASCII.GetBytes(sendLine);
            //Console.WriteLine("Sending message to " + connectIP.ToString());
            stream.Write(data, 0, data.Length);
            clientSock.Close();
        }

        public static string ReceiveData(TcpListener servSock, TcpClient clientSock, byte[] bytes, int receivePort)
        {
            string returnString = "";
            servSock = new TcpListener(IPAddress.Any, receivePort);
            servSock.Start();
            clientSock = new TcpClient();
            //Console.WriteLine("Waiting for the Server to respond");
            clientSock = servSock.AcceptTcpClient();
            //Console.WriteLine("Connected !");
            NetworkStream stream = clientSock.GetStream();
            int num = stream.Read(bytes, 0, bytes.Length);
            returnString = Encoding.ASCII.GetString(bytes, 0, num);

            //Console.WriteLine(returnString);

            clientSock.Close();
            servSock.Stop();
            return returnString;
        }

        public static Element StringToElement(string input)
        {
            switch(input)
            {
                case "0":
                    return Element.NONE;
                    
                case "1":
                    return Element.Rock;
                    
                case "2":
                    return Element.Paper;
                   
                case "3":
                    return Element.Scissor;
                    
                default:
                    return Element.NONE;
                    
            }
        }

        public static string ReturnResult(Element playerMove, Element opponentMove)
        {
            if (playerMove == opponentMove)
            {
                return "It's a tie";
            }
            switch(playerMove)
            {
                case Element.Rock:
                    switch(opponentMove)
                    {
                        case Element.Paper:
                            return "You lose";
                        case Element.Scissor:
                            return "You win!";
                    }
                    break;
                case Element.Paper:
                    switch (opponentMove)
                    {
                        case Element.Scissor:
                            return "You lose";
                        case Element.Rock:
                            return "You win!";
                    }
                    break;
                case Element.Scissor:
                    switch (opponentMove)
                    {
                        case Element.Rock:
                            return "You lose";
                        case Element.Paper:
                            return "You win!";
                    }
                    break;
            }
            return "Invalid input processed";
        }

        public static GameEvent CheckForNewEvent()
        {
            GameEvent newEvent = GameEvent.NONE;
            
            if (currentState == GameState.WAITFORREJOINCOMMAND)
            {
                string inputString = Console.ReadLine();

                if (inputString.ToLower() == "y")
                {
                    try
                    {
                        SendData(connectString, toIP, serverPort);
                    }
                    catch
                    {
                        return GameEvent.DISCONNECT;
                    }
                    newEvent = GameEvent.RECONNECT;
                } else if (inputString.ToLower() == "n")
                {
                    try
                    {
                        SendData("/disconnect%", toIP, serverPort);
                    }
                    catch
                    {
                        return GameEvent.DISCONNECT;
                    }
                    newEvent = GameEvent.DISCONNECT;
                } else
                {
                    Console.WriteLine("Enter valid response");
                }

            }


            if (currentState == GameState.WAITFORCONNECTINPUT)
            {
                string inputString = Console.ReadLine();

                try
                {
                    toIP = IPAddress.Parse(inputString);
                }
                catch
                {
                    Console.WriteLine("Enter a valid IP adress");
                }

                if (toIP != null)
                {
                    Console.WriteLine("Connecting...");
                    try
                    {
                        SendData(connectString, toIP, serverPort);
                    }
                    catch
                    {
                        Console.WriteLine("This IP is unreachable");
                        return GameEvent.NONE;
                    }
                    newEvent = GameEvent.IPENTERED;
                }
                

                
            }

            if (currentState == GameState.WAITFORSERVERRESPONSE)
            {
                byte[] buffer = new byte[1024];
                string assignstring = ReceiveData(servSock,clientSock,buffer,clientPort);
                if (assignstring == "/assign:1%")
                {
                    playerNr = 1;
                    newEvent = GameEvent.GAMESTARTSIGN;
                } 
                else 
                if (assignstring == "/assign:2%")
                {
                    playerNr = 2;
                    newEvent = GameEvent.GAMESTARTSIGN;
                }
            }

            if (currentState == GameState.WAITFORGAMEINPUT)
            {
                string inputString;
                inputString = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(inputString))
                {
                    if ((inputString == "1") || (inputString == "2") || (inputString == "3"))
                    {
                        yourMove = StringToElement(inputString);
                        Console.WriteLine("Your move is: " + yourMove.ToString());
                        string sendString = "/move:" + playerNr + ":" + inputString + "%";
                        Console.WriteLine("Sending to Server");
                        try
                        {
                            SendData(sendString, toIP, serverPort);
                        }
                        catch
                        {
                            return GameEvent.DISCONNECT;
                        }
                        newEvent = GameEvent.SENDGAMEINPUT;

                    }
                    else
                    {
                        Console.WriteLine("Enter a valid move");
                        inputString = "";
                    }
                }
            }

            if (currentState == GameState.WAITFORGAMERESULT) 
            {
                byte[] buffer = new byte[1024];
                string resultstring = ReceiveData(servSock, clientSock, buffer, clientPort);
                if (resultstring.StartsWith("/opmove"))
                {
                    string[] elements = resultstring.Split(':');
                    Element oponentMove = StringToElement(elements[2][0].ToString());
                    Console.WriteLine("Opponent move: "+oponentMove.ToString());
                    Console.WriteLine(ReturnResult(yourMove,oponentMove));
                    newEvent = GameEvent.RECEIVESRESULT;
                }
            }

            return newEvent;
        }

        public static void HandleEvent()
        {
            if (currentEvent == GameEvent.DISCONNECT)
            {
                Console.WriteLine("Disconnected from server");
                Console.WriteLine("Enter the IP adress of the server that you want to join");
                newState = GameState.WAITFORCONNECTINPUT;
            }
            switch (currentState)
            {
                case GameState.WAITFORCONNECTINPUT:
                    if (currentEvent == GameEvent.IPENTERED)
                    {
                        newState = GameState.WAITFORSERVERRESPONSE;
                    }
                    break;
                case GameState.WAITFORSERVERRESPONSE:
                    if (currentEvent == GameEvent.GAMESTARTSIGN)
                    {
                        Console.WriteLine("You are now Player "+playerNr.ToString());
                        Console.WriteLine(askForInputString);
                        newState = GameState.WAITFORGAMEINPUT;
                    }
                    break;
                case GameState.WAITFORGAMEINPUT:
                    if (currentEvent == GameEvent.SENDGAMEINPUT)
                    {
                        newState = GameState.WAITFORGAMERESULT;
                    }
                    break;
                case GameState.WAITFORGAMERESULT:
                    if (currentEvent == GameEvent.RECEIVESRESULT)
                    {
                        Console.WriteLine("Do you want to play again? (y or n)");
                        newState = GameState.WAITFORREJOINCOMMAND;
                    }
                    break;
                case GameState.WAITFORREJOINCOMMAND:
                    if (currentEvent == GameEvent.RECONNECT)
                    {
                        
                        newState = GameState.WAITFORSERVERRESPONSE;
                    } 
                    
                    break;
                default:

                    break;
            }
        }

        

        static void Main(string[] args)
        {
            IPAddress thisIP = null;

            try
            {
                thisIP = GetLocalIPAddress();
            }
            catch
            {
                Console.WriteLine("Your device is not connected to a network");
            }

            connectString = "/connect:"+thisIP.ToString()+"%";

            Console.WriteLine("Welcome to Rock Paper Socket client\nThis is your current IP: " + thisIP.ToString());
            Console.WriteLine("Enter the IP adress of the server that you want to join");

            while (true)
            {
                currentEvent = CheckForNewEvent();

                if (currentEvent != GameEvent.NONE)
                {
                    HandleEvent();

                    if (newState != currentState)
                    {
                        currentState = newState;
                        //Console.WriteLine("entered state " + ((long)currentState).ToString());
                    }
                }
            }

        }
    }
}
