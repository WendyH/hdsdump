namespace hdsdump {
    public class DecoderLastState {
        public const int INVALID_TIMESTAMP = -1;

        public int baseTSA = INVALID_TIMESTAMP;
        public int baseTS  = INVALID_TIMESTAMP;
        public int negTS   = INVALID_TIMESTAMP;
        public int prevAudioTS = INVALID_TIMESTAMP;
        public int prevVideoTS = INVALID_TIMESTAMP;
        public uint prevTagLength = 0;
        public bool hasVideo;
        public bool hasAudio;
        public bool prevAVC_Header;
        public bool prevAAC_Header;
        public bool AVC_HeaderWritten;
        public bool AAC_HeaderWritten;
    }
}
