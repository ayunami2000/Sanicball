using System.Net;
using System.Net.WebSockets;
using SanicballCore.Server;
using System;
using System.Threading.Tasks;

namespace SanicballServer.App
{
    public class Program
    {
        public static void Main(string[] args)
        {
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
            
            Server server = new Server(commandQueue, new ServerConfig() { ServerName = "ayunami2000's Ball Pit", MaxPlayers = 16 });
            Task.Run(server.Start);

            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:25080/");
            httpListener.Start();
            while (true)
            {
                HttpListenerContext context = await httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                    WebSocket webSocket = webSocketContext.WebSocket;
                    Console.WriteLine("123");
                    await server.ConnectClientAsync(webSocket);
                    Console.WriteLine("456");
                }
            }
        }
    }
}