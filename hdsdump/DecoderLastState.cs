namespace hdsdump {
    public class DecoderLastState {
        public const uint INVALID_TIMESTAMP = 0xFFFFFFFF;

        public uint baseTSA = INVALID_TIMESTAMP;
        public uint baseTS  = INVALID_TIMESTAMP;
        public uint negTS   = INVALID_TIMESTAMP;
        public uint prevAudioTS = INVALID_TIMESTAMP;
        public uint prevVideoTS = INVALID_TIMESTAMP;
        public uint prevTagLength = 0;
        public bool hasVideo;
        public bool hasAudio;
        public bool prevAVC_Header;
        public bool prevAAC_Header;
        public bool AVC_HeaderWritten;
        public bool AAC_HeaderWritten;
    }
}
