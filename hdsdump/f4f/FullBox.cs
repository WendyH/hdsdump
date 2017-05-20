namespace hdsdump.f4f {
    public class FullBox: Box {
        public uint Version;
        public uint Flags;

        public override void Parse(BoxInfo bi, HDSBinaryReader br) {
            base.Parse(bi, br);
            Version = br.ReadByte();
            Flags   = br.ReadUInt24();
        }
    }
}
