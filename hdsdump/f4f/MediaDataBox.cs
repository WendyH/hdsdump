namespace hdsdump.f4f {
    public class MediaDataBox: Box {
        public byte[] data;

        public override void Parse(BoxInfo bi, HDSBinaryReader br) {
            base.Parse(bi, br);
            data = br.ReadBytes((int)(Size - Length));
        }
    }
}
