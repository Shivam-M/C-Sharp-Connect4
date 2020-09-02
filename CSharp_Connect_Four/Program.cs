using System;

namespace CSharp_Connect_Four {
    class Program {

        static void Main(string[] args) {
            if (args.Length == 0) {
                new Connect4().SetupGame();
            } else {
                if (args[0].ToString() == "-server") {
                    new Connect4Server(5000).RunServer();
                } else if (args[0].ToString() == "-client" && args.Length >= 2) {
                    new Connect4(args[1]).SetupGame();
                } else {
                    new Connect4().SetupGame();
                }
            }
        }
    }
}
