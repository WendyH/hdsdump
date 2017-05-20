using System;
using System.IO;
using System.Linq;
using System.Text;

namespace hdsdump.flv {
    class CDataHelper {
        ////// Big-Endian
        public static ushort BE_ReadUInt16(Stream stm) {
            int b1 = stm.ReadByte();
            int b2 = stm.ReadByte();
            return (ushort)((b1 << 8) + b2);
        }

        public static uint BE_ReadUInt32(Stream stm) {
            byte[] buf = new byte[4];
            stm.Read(buf, 0, buf.Length);
            return BitConverter.ToUInt32(buf.Reverse().ToArray(), 0);
        }

        public static double BE_ReadDouble(Stream stm) {
            byte[] buf = new byte[8];
            stm.Read(buf, 0, buf.Length);
            return BitConverter.ToDouble(buf.Reverse().ToArray(), 0);
        }

        public static void BE_WriteUInt16(Stream stm, ushort value) {
            stm.WriteByte((byte)(value >> 8));
            stm.WriteByte((byte)value);
        }

        public static void BE_WriteUInt32(Stream stm, uint value) {
            byte[] buf = BitConverter.GetBytes(value).Reverse().ToArray();
            stm.Write(buf, 0, buf.Length);
        }

        public static void BE_WriteDouble(Stream stm, double value) {
            byte[] buf = BitConverter.GetBytes(value).Reverse().ToArray();
            stm.Write(buf, 0, buf.Length);
        }

        public static String BE_ReadShortStr(Stream stm) {
            int len = BE_ReadUInt16(stm);
            return ReadUtfStr(stm, len);
        }

        public static String BE_ReadLongStr(Stream stm) {
            int len = (int)BE_ReadUInt32(stm);
            return ReadUtfStr(stm, len);
        }

        public static void BE_WriteShortStr(Stream stm, string str) {
            byte[] buf = Encoding.UTF8.GetBytes(str);
            BE_WriteUInt16(stm, (ushort)buf.Length);
            stm.Write(buf, 0, buf.Length);
        }

        public static void BE_WriteLongStr(Stream stm, string str) {
            byte[] buf = Encoding.UTF8.GetBytes(str);
            BE_WriteUInt32(stm, (uint)buf.Length);
            stm.Write(buf, 0, buf.Length);
        }

        public static String ReadUtfStr(Stream stm, int len) {
            byte[] buf = new byte[len];
            stm.Read(buf, 0, len);
            string str = Encoding.UTF8.GetString(buf);
            return str;
        }
    }
}
