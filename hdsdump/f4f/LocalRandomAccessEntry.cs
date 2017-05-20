namespace hdsdump.f4f {
    public class LocalRandomAccessEntry {
        public ulong time;
        public ulong offset;

        public void Parse(HDSBinaryReader br, bool longOffsetFields) {
            time = br.ReadUInt64();
            if (longOffsetFields) {
                offset = br.ReadUInt64();
            } else {
                offset = br.ReadUInt32();
            }
        }
    }
}
