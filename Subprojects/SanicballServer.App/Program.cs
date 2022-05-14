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
            ServerConfig serverConfig = new ServerConfig() { ServerName = "ayunami2000's Ball Pit", MaxPlayers = 16 };

            Server server = new Server(commandQueue, serverConfig);
            Task.Run(server.Start);

            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://+:25080/");
            httpListener.Start();
            while (true)
            {
                HttpListenerContext context = await httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(null);
                    WebSocket webSocket = webSocketContext.WebSocket;
                    await server.ConnectClientAsync(webSocket);
                }
                else
                {
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(serverConfig.ServerName + "<br>" + server.InGame.ToString() + "<br>" + serverConfig.MaxPlayers + "<br>" + server.ConnectedClients);
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