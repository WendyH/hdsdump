namespace hdsdump.flv {
    public class FLVHeader {
        private byte[] _data = new byte[] { 0x46, 0x4c, 0x56, 0x01, 0x00, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x00 };

        public bool HasAudio = true;
        public bool HasVideo = true;

        public byte[] Data {
            get {
                byte flags = 0;
                if (HasAudio) flags |= 0x04;
                if (HasVideo) flags |= 0x01;
                _data[4] = flags;
                return _data;
            }
        }
    }
}
