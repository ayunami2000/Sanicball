using System.Net;
using System.Net.WebSockets;
using SanicballCore.Server;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace SanicballServer.App
{
    public class Program
    {
        private static int port = 25080;
        private static int playerCap = 16;
        private static string serverName = "Sanicball Server";

        public static void Main(string[] args)
        {
            Console.WriteLine("Command-line args: port playerCap serverName");
            if (args.Length >= 1) port = int.Parse(args[0]);
            if (args.Length >= 2) playerCap = int.Parse(args[1]);
            if (args.Length >= 3) serverName = string.Join(" ", args, 2, args.Length - 2);
            CommandQueue commandQueue = new CommandQueue();
            StartServer(commandQueue);
            ReadInput(commandQueue);
        }

        public static async Task ReadInput(CommandQueue commandQueue)
        {
            while (true)
            {
                commandQueue.Add(new Command(Console.ReadLine()));
            }
        }

        public static async Task StartServer(CommandQueue commandQueue)
        {
            ServerConfig serverConfig = new ServerConfig() { ServerName = serverName, MaxPlayers = playerCap };

            Server server = new Server(commandQueue, serverConfig);
            Task.Run(server.Start);

            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://*:" + port + "/");
            httpListener.Start();
            while (true)
            {
                HttpListenerContext context = await httpListener.GetContextAsync();
                context.Response.AppendHeader("Access-Control-Allow-Origin", "*");
                if (context.Request.IsWebSocketRequest)
                {
                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                    WebSocket webSocket = webSocketContext.WebSocket;
                    await server.ConnectClientAsync(webSocket);
                }
                else
                {
                    context.Response.AppendHeader("Content-Type", "text/html; charset=utf-8");
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(server.Config.ServerName + "<br>" + server.InGame.ToString() + "<br>" + server.ConnectedClients + "<br>" + server.Config.MaxPlayers);
                    // Get a response stream and write the response to it.
                    context.Response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = context.Response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                }
            }
        }
    }
}