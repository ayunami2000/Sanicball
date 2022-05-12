using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SanicballCore
{
    public static class Utils
    {
        public static string GetTimeString(TimeSpan timeToUse)
        {
            return string.Format("{0:00}:{1:00}.{2:000}", timeToUse.Minutes, timeToUse.Seconds, timeToUse.Milliseconds);
        }

        public static string GetPosString(int pos)
        {
            if (pos % 10 == 1 && pos % 100 != 11) return pos + "st";
            if (pos % 10 == 2 && pos % 100 != 12) return pos + "nd";
            if (pos % 10 == 3 && pos % 100 != 13) return pos + "rd";
            return pos + "th";
        }


        public static void Write(this BinaryWriter target, Guid guid)
        {
            byte[] guidBytes = guid.ToByteArray();
            target.Write(guidBytes.Length);
            target.Write(guidBytes);
        }

        public static void Write(this BinaryWriter target, Vector3 vector)
        {
            target.Write(vector.x);
            target.Write(vector.y);
            target.Write(vector.z);
        }

        public static Vector3 ReadVector3(this BinaryReader target)
        {
            return new Vector3(target.ReadSingle(), target.ReadSingle(), target.ReadSingle());
        }

        public static void Write(this BinaryWriter target, Quaternion vector)
        {
            target.Write(vector.x);
            target.Write(vector.y);
            target.Write(vector.z);
            target.Write(vector.w);
        }

        public static Quaternion ReadQuaternion(this BinaryReader target)
        {
            return new Quaternion(target.ReadSingle(), target.ReadSingle(), target.ReadSingle(), target.ReadSingle());
        }

        public static Guid ReadGuid(this BinaryReader target)
        {
            int guidLength = target.ReadInt32();
            byte[] guidBytes = target.ReadBytes(guidLength);
            return new Guid(guidBytes);
        }

        /// <summary>
        /// Gets a random float between -1.0f and 1.0f
        /// </summary>
        /// <param name="rand"></param>
        /// <returns></returns>
        public static float NextFloatUniform(this System.Random rand)
        {
            return ((float)rand.NextDouble() - 0.5f) * 2f;
        }
    }
}