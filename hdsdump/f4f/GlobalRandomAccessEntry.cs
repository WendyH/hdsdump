namespace hdsdump.f4f {
    public class GlobalRandomAccessEntry {
        public ulong time;
        public uint  segment;
        public uint  fragment;
        public ulong afraOffset;
        public ulong offsetFromAfra;

        public void Parse(HDSBinaryReader br, bool longIdFields, bool longOffsetFields) {
            time = br.ReadUInt64();

            if (longIdFields) {
                segment  = br.ReadUInt32();
                fragment = br.ReadUInt32();
            } else {
                segment  = br.ReadUInt16();
                fragment = br.ReadUInt16();
            }

            if (longOffsetFields) {
                afraOffset     = br.ReadUInt64();
                offsetFromAfra = br.ReadUInt64();
            } else {
                afraOffset     = br.ReadUInt32();
                offsetFromAfra = br.ReadUInt32();
            }
        }

    }
}
