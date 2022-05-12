using SanicballCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using vtortola.WebSockets;

namespace SanicballServer
{
    public struct QueuedMessage
    {
        public WebSocketWrapper Source;
        public MessageTypes Type;
        public Memory<byte> Data;
    }

    public class WebSocketWrapper
    {
        private readonly WebSocket _socket;
        private readonly CancellationTokenSource _source;
        private readonly ConcurrentQueue<QueuedMessage> _socketRecieveQueue;
        private readonly ConcurrentQueue<QueuedMessage> _socketSendQueue;

        private DateTimeOffset _lastHeartBeat = DateTimeOffset.Now;

        public WebSocketWrapper(WebSocket socket)
        {
            _socket = socket;
            _source = new CancellationTokenSource();
            _socketRecieveQueue = new ConcurrentQueue<QueuedMessage>();
            _socketSendQueue = new ConcurrentQueue<QueuedMessage>();
        }

        public void Start()
        {
            Task.Run(RecieveMessageLoop);
            Task.Run(SendMessageLoop);
        }

        public void Send(MessageTypes type, byte[] data) => _socketSendQueue.Enqueue(new QueuedMessage() { Type = type, Data = data });

        public bool Dequeue(out QueuedMessage message) => _socketRecieveQueue.TryDequeue(out message);

        public async Task SendMessageLoop()
        {
            try
            {
                while (_socket.IsConnected)
                {
                    var message = await _socket.ReadMessageAsync(_source.Token);
                    var stream = new MemoryStream();
                    await message.CopyToAsync(stream);

                    var data = stream.ToArray();
                    _socketRecieveQueue.Enqueue(new QueuedMessage() { Source = this, Type = (MessageTypes)data[0], Data = data.AsMemory().Slice(1) });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        private async Task RecieveMessageLoop()
        {
            var wait = new SpinWait();
            try
            {
                while (_socket.IsConnected)
                {
                    while (_socketSendQueue.TryDequeue(out var queue))
                    {
                        var writer = _socket.CreateMessageWriter(WebSocketMessageType.Binary);
                        await writer.WriteAsync(new[] { (byte)queue.Type }, 0, 1);
                        await writer.WriteAsync(queue.Data.ToArray(), 0, queue.Data.Length);
                        await writer.CloseAsync();
                    }

                    if ((DateTimeOffset.Now - _lastHeartBeat).TotalSeconds > 50)
                        await _socket.SendPingAsync();

                    wait.SpinOnce();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public async Task DisconnectAsync(string reason = null)
        {
            _source.Cancel();

            if (reason != null)
            {
                using (var wrapper = new MessageWrapper(MessageTypes.Disconnect))
                {
                    wrapper.Writer.Write(reason);
                    var data = wrapper.GetBytes();

                    var writer = _socket.CreateMessageWriter(WebSocketMessageType.Binary);
                    await writer.WriteAsync(new[] { (byte)wrapper.Type }, 0, 1);
                    await writer.WriteAsync(data, 0, data.Length);
                    await writer.CloseAsync();
                }
            }

            await _socket.CloseAsync();
        }
    }
}
