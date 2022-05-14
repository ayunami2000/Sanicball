using SanicballCore;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace SanicballServer
{
    public class WebSocketWrapper
    {
        private readonly WebSocket _socket;
        private readonly CancellationTokenSource _source;
        private readonly ConcurrentQueue<MessageWrapper> _socketRecieveQueue;
        private readonly ConcurrentQueue<byte[]> _socketSendQueue;
        private const int BUFFER_SIZE = 32768;
        private Task _socketQueueManager;
        private bool started;

        public Guid Id { get; private set; }

        public WebSocketWrapper(WebSocket socket)
        {
            _socket = socket;
            _source = new CancellationTokenSource();
            _socketRecieveQueue = new ConcurrentQueue<MessageWrapper>();
            _socketSendQueue = new ConcurrentQueue<byte[]>();

            Id = Guid.NewGuid();
        }

        public void Start()
        {
            if (!started)
                Task.Run(RecieveLoop);

            started = true;
        }

        public bool Dequeue(out MessageWrapper message)
        {
            return _socketRecieveQueue.TryDequeue(out message);
        }

        public ValueTask SendAsync(MessageTypes type, byte[] data)
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(new[] { (byte)type }, 0, 1);
                stream.Write(data, 0, data.Length);
                _socketSendQueue.Enqueue(stream.ToArray());
            }

            if (_socketQueueManager == null || _socketQueueManager.IsCompleted)
                _socketQueueManager = Task.Run(SendLoop);

            return default;
        }

        public ValueTask SendAsync(MessageWrapper wrapper)
        {
            _socketSendQueue.Enqueue(wrapper.GetBytes());

            if (_socketQueueManager == null || _socketQueueManager.IsCompleted)
                _socketQueueManager = Task.Run(SendLoop);

            return default;
        }

        public ValueTask Send(MessageTypes type, BinaryWriter writer, MemoryStream dest)
        {
            writer.Flush();
            return SendAsync(type, dest.ToArray());
        }

        public async Task RecieveLoop()
        {
            await Task.Yield();

            var buff = new byte[BUFFER_SIZE];
            var buffseg = new ArraySegment<byte>(buff);

            byte[] resultbuff = null;
            WebSocketReceiveResult result = null;

            try
            {
                while (true)
                {
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await _socket.ReceiveAsync(buffseg, default);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                var wrapper = new MessageWrapper(MessageTypes.Disconnect);
                                wrapper.Writer.Write(result.CloseStatusDescription ?? "Client disconnected");
                                wrapper.Source = Id;
                                _socketRecieveQueue.Enqueue(wrapper);
                            }
                            else
                                ms.Write(buff, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        resultbuff = ms.ToArray();
                        _socketRecieveQueue.Enqueue(new MessageWrapper(resultbuff) { Source = Id });
                    }
                }

            }
            catch (Exception)
            {
                try
                {
                    if (result != null)
                    {
                        var wrapper = new MessageWrapper(MessageTypes.Disconnect);
                        wrapper.Writer.Write(result.CloseStatusDescription ?? "Client disconnected");
                        wrapper.Source = Id;
                        _socketRecieveQueue.Enqueue(wrapper);
                    }

                    _socket.Abort();
                }
                catch { }
            }
        }

        internal async Task SendLoop()
        {
            await Task.Yield();

            try
            {
                while (_socket.State == WebSocketState.Open)
                {
                    if (_socketSendQueue.IsEmpty)
                        break;

                    if (!_socketSendQueue.TryDequeue(out var message))
                        break;

                    await _socket.SendAsync(message, WebSocketMessageType.Binary, true, default);
                }
            }
            catch (Exception)
            {
                try
                {
                    var wrapper = new MessageWrapper(MessageTypes.Disconnect);
                    wrapper.Writer.Write("Client disconnected");
                    wrapper.Source = Id;
                    _socketRecieveQueue.Enqueue(wrapper);

                    _socket.Abort();
                }
                catch { }
            }

            _socketQueueManager = null;
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

                    await SendAsync(wrapper);
                }
            }

            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, default);
        }

    }
}
