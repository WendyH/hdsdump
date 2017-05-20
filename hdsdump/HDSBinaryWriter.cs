namespace hdsdump {
    public class HDSBinaryWriter: System.IO.BinaryWriter {

        // CONSTRUCTOR
        public HDSBinaryWriter(System.IO.Stream output) : base(output) {
        }

        public void WriteByte(byte b) {
            base.Write(b);
        }

        public void WriteUInt24(uint value) {
            base.Write((byte)((value & 0xFF0000) >> 16));
            base.Write((byte)((value & 0xFF00) >> 8));
            base.Write((byte) (value & 0xFF));
        }

        public void WriteUInt32(uint value) {
            base.Write((byte)((value & 0xFF000000) >> 24));
            base.Write((byte)((value & 0xFF0000) >> 16));
            base.Write((byte)((value & 0xFF00) >> 8));
            base.Write((byte) (value & 0xFF));
        }

    }
}
