using System;
using System.Collections.Generic;

namespace hdsdump {
    public class HDSBinaryReader: System.IO.BinaryReader {
        public long Position {
            get { return BaseStream.Position; }
            set { BaseStream.Position = value; }
        }

        public long BytesAvailable {
            get { return BaseStream.Length - BaseStream.Position; }
        }

        // CONSTRUCTOR
        public HDSBinaryReader(System.IO.Stream stream): base(stream) {
        }
        public HDSBinaryReader(byte[] data): base(new System.IO.MemoryStream(data)) {
        }

        public override string ReadString() {
            string s = ""; int b = 0;
            while (BaseStream.CanRead) {
                b = BaseStream.ReadByte();
                if (b == 0) break;
                s += (char)b;
            }
            return s;
        }

        public byte[] ReadToEnd() {
            return ReadBytes((int)BytesAvailable);
        }

        public string ReadUtfBytes(uint len) {
            return System.Text.Encoding.UTF8.GetString(ReadBytes((int)len));
        }

        public string ReadNullTerminatedString() {
            List<byte> s = new List<byte>();
            for (byte b = ReadByte(); b != 0; b = ReadByte())
                s.Add(b);
            if (s.Count == 0)
                return null;
            return System.Text.Encoding.UTF8.GetString(s.ToArray());
        }

        public override short ReadInt16() {
            return ReadInt16BigEndian();
        }

        public override ushort ReadUInt16() {
            return ReadUInt16BigEndian();
        }

        public override int ReadInt32() {
            return ReadInt32BigEndian();
        }

        public override uint ReadUInt32() {
            return ReadUInt32BigEndian();
        }

        public override long ReadInt64() {
            return ReadInt64BigEndian();
        }

        public override ulong ReadUInt64() {
            return ReadUInt64BigEndian();
        }

        public uint ReadUInt24() {
            int b1 = ReadByte();
            int b2 = ReadByte();
            int b3 = ReadByte();
            return (((uint)b1) << 16) |
                   (((uint)b2) << 8 ) |
                   ( (uint)b3);
        }

        private byte[] ReadBytesBc(int n) {
            byte[] b = ReadBytes(n);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            return b;
        }

        public short ReadInt16BigEndian() {
            return BitConverter.ToInt16(ReadBytesBc(2), 0);
        }

        public ushort ReadUInt16BigEndian() {
            return BitConverter.ToUInt16(ReadBytesBc(2), 0);
        }

        public int ReadInt32BigEndian() {
            return BitConverter.ToInt32(ReadBytesBc(4), 0);
        }

        public uint ReadUInt32BigEndian() {
            return BitConverter.ToUInt32(ReadBytesBc(4), 0);
        }

        public long ReadInt64BigEndian() {
            return BitConverter.ToInt64(ReadBytesBc(8), 0);
        }

        public ulong ReadUInt64BigEndian() {
            return BitConverter.ToUInt64(ReadBytesBc(8), 0);
        }

        public IEnumerable<byte[]> ReadChunkedBytes(ulong u, int buffersize = 4096) {
            while (u > 0) {
                var bytesToRead = u < (ulong)buffersize ? (int)u : buffersize;
                u -= (ulong)bytesToRead;
                yield return ReadBytes(bytesToRead);
            }
        }

        public void SkipBytes(ulong count) {
            foreach (var b in ReadChunkedBytes(count)) ;
        }

        public static uint ReadUInt24(byte[] data, uint pos) {
            uint iValLo = (uint)(data[pos + 2] + (data[pos + 1] * 256));
            uint iValHi = (uint)(data[pos + 0]);
            uint iVal = iValLo + (iValHi * 65536);
            return iVal;
        }
    }
}
