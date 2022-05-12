using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanicballCore
{
   public class MessageWrapper : IDisposable
    {
        private readonly MemoryStream _stream;

        public Guid Source { get; set; }
       
        public BinaryWriter Writer { get; private set; }
        public BinaryReader Reader { get; private set; }

        public MessageTypes Type { get; private set; }

        public MessageWrapper(byte[] bytes)
        {
            _stream = new MemoryStream(bytes);
            Reader = new BinaryReader(_stream);
            Type = (MessageTypes)Reader.ReadByte();
        }

        public MessageWrapper(MessageTypes type)
        {
            _stream = new MemoryStream();
            Writer = new BinaryWriter(_stream, Encoding.UTF8);
            Writer.Write((byte)type);
            Type = type;
        }

        public byte[] GetBytes()
        {
            Writer?.Flush();
            return _stream.ToArray();
        }

        public void Dispose()
        {
            Writer?.Dispose();
            Reader?.Dispose();
        }
    }
}
