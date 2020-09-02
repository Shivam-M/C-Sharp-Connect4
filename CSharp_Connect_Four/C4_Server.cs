using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CSharp_Connect_Four {
    class Connect4Server {
        public const string IP = "0.0.0.0";
        public int Port;
        public IPEndPoint LocalEndPoint;
        public Socket ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public List<Socket> ClientSockets = new List<Socket>();
        public Dictionary<Socket, string> GameTeams = new Dictionary<Socket, string>();
        public Dictionary<string, Socket> GameSockets;
        public string GameTurn = "1";
        public string[,] GameBoard = new string[6, 7];

        public Connect4Server(int p) {
            Port = p;
        }

        public void SetupGame() {
            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 7; y++) {
                    GameBoard[x, y] = "-";
                }
            }
        }

        public bool CheckWin(string team) {
            for (int y = 0; y < 6; y++) {
                for (int x = 0; x < 4; x++)
                    if (GameBoard[y, x] == team && GameBoard[y, x + 1] == team)
                        if (GameBoard[y, x + 2] == team && GameBoard[y, x + 3] == team)
                            return true;
            }

            for (int y = 0; y < 3; y++) {
                for (int x = 0; x < 7; x++)
                    if (GameBoard[y, x] == team && GameBoard[y + 1, x] == team)
                        if (GameBoard[y + 2, x] == team && GameBoard[y + 3, x] == team)
                            return true;
            }

            for (int y = 0; y < 3; y++) {
                for (int x = 0; x < 7; x++) {
                    if (GameBoard[y, x] == team) {
                        try {
                            if (GameBoard[y + 1, x - 1] == team) {
                                if (GameBoard[y + 2, x - 2] == team)
                                    if (GameBoard[y + 3, x - 3] == team)
                                        return true;
                            }
                        } catch (IndexOutOfRangeException) { }
                        try {
                            if (GameBoard[y + 1, x + 1] == team) {
                                if (GameBoard[y + 2, x + 2] == team)
                                    if (GameBoard[y + 3, x + 3] == team)
                                        return true;
                            }
                        } catch (IndexOutOfRangeException) { }
                    }
                }
            } return false;
        }

        void PlaceCounter(int column, string team) {
            if (GameBoard[5, column] == "-") {
                GameBoard[5, column] = team;
                return;
            }
            for (int x = 0; x < 6; x++) {
                if (GameBoard[x, column] != "-") {
                    GameBoard[x - 1, column] = team;
                    break;
                }
            }
        }

        public void RunServer() {
            LocalEndPoint = new IPEndPoint(IPAddress.Parse(IP), Port);
            ServerSocket.NoDelay = true;
            SetupGame();
            ClientSearch();
            StartListening();
        }

        public void ClientSearch() {
            ServerSocket.Bind(LocalEndPoint);
            ServerSocket.Listen(10);
            while (ClientSockets.Count < 2) {
                Socket clientSocket = ServerSocket.Accept();
                ClientSockets.Add(clientSocket);
                if (ClientSockets.Count == 1)
                    GameSend("waiting", clientSocket);
            }
            GameSend("start");
            GameSend("team", ClientSockets[0], new string[] { "team", "1" });
            GameSend("team", ClientSockets[1], new string[] { "team", "2" });
            GameSend("turn", null, new string[] { "team", "1" });
            GameSockets = GameTeams.ToDictionary((i) => i.Value, (i) => i.Key);
            GameTeams.Add(ClientSockets[0], "1");
            GameTeams.Add(ClientSockets[1], "2");
        }

        public void Send(String message) {
            foreach (Socket client in ClientSockets) {
                byte[] bytes = Encoding.ASCII.GetBytes(message.ToString());
                client.Send(bytes);
            } Thread.Sleep(750);
        }

        public void GameSend(string dataType, Socket socket = null, string[] information = null) {
            JObject data = new JObject { ["data-type"] = dataType };
            if (information != null)
                data[information[0]] = information[1];
            if (socket == null) {
                foreach (Socket client in ClientSockets)
                    client.Send(Encoding.ASCII.GetBytes(data.ToString()));
            } else {
                socket.Send(Encoding.ASCII.GetBytes(data.ToString()));
            } Thread.Sleep(750);
        }

        // Would probably be easier to use.
        public void GameSend_2(string dataType, Socket socket = null, String key = null, String value = null) {
            JObject data = new JObject { ["data-type"] = dataType };
            if (key != null && value != null)
                data[key] = value;
            if (socket == null) {
                foreach (Socket client in ClientSockets)
                    client.Send(Encoding.ASCII.GetBytes(data.ToString()));
            } else {
                socket.Send(Encoding.ASCII.GetBytes(data.ToString()));
            }
            Thread.Sleep(750);
        }

        public void StartListening() {
            while (true) {
                ArrayList clonedSockets = new ArrayList(ClientSockets);
                Socket.Select(clonedSockets, null, null, 10000000);
                foreach (Socket client in clonedSockets) {
                    var bufferedData = new byte[1024];
                    var receivedData = client.Receive(bufferedData);
                    string stringData = Encoding.ASCII.GetString(bufferedData, 0, receivedData);
                    if (receivedData == 0) {
                        client.Close();
                        ClientSockets.Remove(client);
                    } else {
                        JObject json = JObject.Parse(stringData);
                        if (json["data-type"].ToString() == "column") {
                            if (json["team"].ToString() == GameTurn) {
                                int columnNumber = int.Parse(json["column"].ToString());
                                if (GameBoard[0, columnNumber] != "-" || !(0 <= columnNumber && columnNumber <= 6)) {
                                    GameSend("turn", null, new string[] { "team", GameTurn });
                                    continue;
                                }
                                Send(stringData);
                                PlaceCounter(int.Parse(json["column"].ToString()), GameTurn);
                                if (CheckWin(GameTurn)) {
                                    GameSend("game-win", null, new string[] { "team", GameTurn });
                                    Thread.Sleep(3000);
                                    GameSend("kick", null, new string[] { "message", "Kicked from server (server shutting down)" });
                                    break;
                                }
                                if (GameTurn == "1") { GameTurn = "2"; } else { GameTurn = "1"; }
                                GameSend("turn", null, new string[] { "team", GameTurn });
                            }
                        }
                    }
                }
            }
        }
    }
}
