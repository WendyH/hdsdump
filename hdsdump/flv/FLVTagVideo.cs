using System;

namespace hdsdump.flv {
    public class FLVTagVideo: FLVTag {

        // CONSTRUCTOR
        public FLVTagVideo(TagType type = TagType.VIDEO): base(type) {
        }
        
        public Frame FrameType {
            get { return (Frame)((Data[0] >> 4) & 0x0f); }
            set {
                Data[0] &= 0x0f;   // clear top 4 bits
                Data[0] |= (byte)(((int)value & 0x0f) << 4);
            }
        }
        
        public Codec CodecID {
            get { return (Codec)(Data[0] & 0x0f); }
            set {
                Data[0] &= 0xf0;    // clear bottom 4 bits
                Data[0] |= (byte)((int)value & 0x0f);
            }
        }
        
        public int InfoPacketValue {
            get { return Data[1]; }
            set { Data[1] = (byte)value; }
        }

        public AVCPacket AvcPacketType {
            get { return (AVCPacket)((CodecID != Codec.AVC) ? 0 : Data[1]); }
            set {
                Data[1] = (byte)value;
                if (AvcPacketType != AVCPacket.NALU) {
                    // zero the composition time offset
                    Data[2] = 0;
                    Data[3] = 0;
                    Data[4] = 0;
                }
            }
        }
        
        public uint AVCCompositionTimeOffset {
            get {
                // throw error if frameType == FRAME_TYPE_INFO?
                if ((CodecID != Codec.AVC) || (AvcPacketType != AVCPacket.NALU)) return 0;

                uint value = (uint)((Data[2] << 16) | (Data[3] << 8) | Data[4]);
                if ((value & 0x00800000) > 0) {
                    value |= 0xff000000;    // sign-extend the 24-bit read for a 32-bit int
                }
                return value;
            }
            set {
                // throw error if frameType == FRAME_TYPE_INFO?
                if ((CodecID != Codec.AVC) || (AvcPacketType != AVCPacket.NALU)) {
                    throw new InvalidOperationException("set avcCompositionTimeOffset() not permitted unless codecID is CODEC_ID_AVC and avcPacketType is AVC NALU");
                }
                Data[2] = (byte)((value >> 16) & 0xff);
                Data[3] = (byte)((value >> 8 ) & 0xff);
                Data[4] = (byte)((value      ) & 0xff);
            }
        }
        
        public enum Frame: int {
            UNKNOWN            = 0,
            KEYFRAME           = 1,
            INTER              = 2,
            DISPOSABLE_INTER   = 3,
            GENERATED_KEYFRAME = 4,
            INFO               = 5
        }

        public enum Codec : int {
            UNKNOWN   = 0,
            JPEG      = 1,
            SORENSON  = 2,
            SCREEN    = 3,
            VP6       = 4,
            VP6_ALPHA = 5,
            SCREEN_V2 = 6,
            AVC       = 7
        }
        public enum AVCPacket     : int { SEQUENCE_HEADER = 0, NALU = 1, END_OF_SEQUENCE = 2 }
        public enum InfoPacketSeek: int { START = 0, END = 1 }

        public static string CodecToString(Codec id) {
            switch (id) {
                case Codec.JPEG     : return "MJPEG";
                case Codec.SORENSON : return "Sorenson H.263";
                case Codec.SCREEN   : return "Screen video";
                case Codec.VP6      : return "On2 VP6";
                case Codec.VP6_ALPHA: return "On2 VP6 with alpha channel";
                case Codec.SCREEN_V2: return "Screen video version 2";
                case Codec.AVC      : return "AVC";
            }
            return "Unknown";
        }

    }
}