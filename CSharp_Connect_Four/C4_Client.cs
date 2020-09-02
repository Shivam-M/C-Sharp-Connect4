using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CSharp_Connect_Four {
    class Connect4 {
        public IPEndPoint RemoteEndPoint;
        public Socket GameSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public bool GameReady = false;
        public string[,] GameBoard = new string[6, 7];
        public string GameTeam = "0";
        public string GameTurn = "0";
        public bool ActivelyListening = true;
        public Dictionary<string, ConsoleColor> TranslateColour = new Dictionary<string, ConsoleColor>() {
            {"1", ConsoleColor.DarkRed},
            {"2", ConsoleColor.Blue}
        };

        public void SetupGame() {
            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 7; y++)
                    GameBoard[x, y] = "-";
            }
            try {
                Connect();
            } catch { Message("Could not connect to the server.", "ERROR", ConsoleColor.Red); }
        }

        public Connect4(string address = "127.0.0.1", int port = 5000) {
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
        }

        void Connect() {
            GameSocket.Connect(RemoteEndPoint);
            DisplayBoard();
            StartListening();
            Message("Waiting for the match to begin...", colour: ConsoleColor.Yellow);
        }

        void DisplayBoard() {
            Console.Clear();
            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 7; y++) {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write("| ");
                    if (GameBoard[x, y] == "-") {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write(GameBoard[x, y]);
                    } else {
                        Console.ForegroundColor = TranslateColour[GameBoard[x, y]];
                        Console.Write("O");
                    }
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write(" |");
                } Console.WriteLine();
            }
        }

        void Message(string message, string prefix = "GAME", ConsoleColor colour = ConsoleColor.Green) {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray; Console.Write(prefix); Console.ForegroundColor = colour; Console.Write(" >> "); Console.ForegroundColor = ConsoleColor.DarkGray; Console.Write(message);
        }

        void PlaceLocally(int column, string GameTeam) {
            if (GameBoard[5, column] == "-") {
                GameBoard[5, column] = GameTeam;
            } else {
                for (int x = 0; x < 6; x++) {
                    if (GameBoard[x, column] != "-") {
                        GameBoard[x - 1, column] = GameTeam;
                        break;
                    }
                }
            } DisplayBoard();
        }

        void RetrieveColumn() {
            try {
                Message("Please enter the column (0 - 6) you would like to drop in: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Send(("{'data-type': 'column', 'column': '" + int.Parse(Console.ReadLine()).ToString() + "', 'team': '" + GameTeam + "'}"));
            } catch (FormatException) {
                Message("Please enter a valid integer between (0 - 6)!", "ERROR", ConsoleColor.Red);
                RetrieveColumn();
            }
        }

        void Send(string message) {
            GameSocket.Send(Encoding.UTF8.GetBytes(message.ToString()));
        }

        void StartListening() {
            while (ActivelyListening) {
                try {
                    var bufferedData = new byte[1024];
                    var receivedData = GameSocket.Receive(bufferedData);
                    string stringData = Encoding.ASCII.GetString(bufferedData, 0, receivedData);
                    if (receivedData == 0) {
                        GameSocket.Close();
                    } else {
                        try {
                            HandleData(JObject.Parse(stringData));
                        } catch (Exception e) {
                            Console.WriteLine(e);
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine(e); break;
                }
            }
        }

        void HandleData(JObject data) {
            switch (data["data-type"].ToString()) {
                case "waiting":
                    Message("Waiting for one more player to join the server...", colour: ConsoleColor.Yellow); break;
                case "start":
                    Message("The match is now starting!"); break;
                case "team":
                    GameTeam = data["team"].ToString(); break;
                case "turn":
                    if (GameTeam == data["team"].ToString()) RetrieveColumn(); break;
                case "column":
                    PlaceLocally(int.Parse(data["column"].ToString()), data["team"].ToString()); break;
                case "kick":
                    Message(data["message"].ToString(), "KICKED", ConsoleColor.DarkMagenta); ActivelyListening = false; break;
                case "game-win":
                    if (data["team"].ToString() == GameTeam) {
                        Message("Four in a row! You won the game!");
                    } else {
                        Message("Four in a row! You lost the game!", colour: ConsoleColor.Red);
                    }
                    Message("Shutting down in three seconds...", "SERVER", ConsoleColor.DarkCyan);
                    Thread.Sleep(3000); break;
            }
        }
    }
}
