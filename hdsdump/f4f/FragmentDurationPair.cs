namespace hdsdump.f4f {
    public class FragmentDurationPair {
        public uint  firstFragment;
        public uint  duration;
        public ulong durationAccrued;
        public uint  discontinuityIndicator = 0;

        public void Parse(HDSBinaryReader br) {
            firstFragment   = br.ReadUInt32();
            durationAccrued = br.ReadUInt64();
            duration        = br.ReadUInt32();
            
            if (duration == 0) {
                discontinuityIndicator = br.ReadByte();
            }
        }
    }
}
