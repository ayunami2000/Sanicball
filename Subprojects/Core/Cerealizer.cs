using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SanicballCore
{
    public class Cerealizer
    {
        public static byte[] ReadAllBytes(BinaryReader reader)
        {
            const int bufferSize = 4096;
            using (var ms = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                    ms.Write(buffer, 0, count);
                return ms.ToArray();
            }
        }

        public static MemoryStream ReadAllBytesMS(BinaryReader reader)
        {
            const int bufferSize = 4096;
            var ms = new MemoryStream();
            byte[] buffer = new byte[bufferSize];
            int count;
            while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                ms.Write(buffer, 0, count);
            return ms;
        }

        public static T UnCerealReader<T>(BinaryReader reader)
        {
            MemoryStream ms = ReadAllBytesMS(reader);
            var res = UnCerealMS<T>(ms);
            ms.Dispose();
            return res;
        }

        public static byte[] Cereal<T>(T thing)
        {
            Stream cereal = CerealOne();
            CerealTwo<T>(cereal, thing);
            return CerealThree(cereal);
        }

        public static Stream CerealOne()
        {
            return new MemoryStream();
        }

        public static void CerealTwo<T>(Stream message, T thing)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(message, thing);
        }

        public static T UnCereal<T>(byte[] thing)
        {
            MemoryStream ms = new MemoryStream(thing);
            var item = UnCerealMS<T>(ms);
            ms.Dispose();
            return item;
        }

        public static T UnCerealMS<T>(MemoryStream thing)
        {
            try
            {
                BinaryFormatter bf = new BinaryFormatter();
                var dataObj = (T)bf.Deserialize(thing);
                return dataObj;
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                System.Console.WriteLine(ex);
                //Debug.LogError("Failed to parse! Binary converter info: " + ex.Message);
                return default(T);
            }
        }

        public static byte[] CerealThree(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
