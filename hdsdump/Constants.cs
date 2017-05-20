namespace hdsdump {
    static class Constants {
        public const int AUDIO                 = 0x08;
        public const int VIDEO                 = 0x09;
        public const int AKAMAI_ENC_AUDIO      = 0x0A;
        public const int AKAMAI_ENC_VIDEO      = 0x0B;
        public const int FLASHACCESS_ENC_AUDIO = 0x28;
        public const int FLASHACCESS_ENC_VIDEO = 0x29;
        public const int SCRIPT_DATA           = 0x12;
        public const int FRAME_TYPE_INFO       = 0x05;
        public const int CODEC_ID_AVC          = 0x07;
        public const int CODEC_ID_AAC          = 0x0A;
        public const int AVC_SEQUENCE_HEADER   = 0x00;
        public const int AAC_SEQUENCE_HEADER   = 0x00;
        public const int AVC_NALU              = 0x01;
        public const int AVC_SEQUENCE_END      = 0x02;
        public const int FRAMEFIX_STEP         = 0x28;
        public const int STOP_PROCESSING       = 0x02;
        public const int INVALID_TIMESTAMP     = -1;
        public const int TIMECODE_DURATION     = 8;
    }
}
