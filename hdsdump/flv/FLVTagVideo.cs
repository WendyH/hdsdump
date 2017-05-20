using System;

namespace hdsdump.flv {
    public class FLVTagVideo: FLVTag {

        // CONSTRUCTOR
        public FLVTagVideo(TagType type = TagType.VIDEO): base(type) {
		}
		
		public FrameType frameType {
            get { return (FrameType)((Data[0] >> 4) & 0x0f); }
            set {
                Data[0] &= 0x0f;   // clear top 4 bits
                Data[0] |= (byte)(((int)value & 0x0f) << 4);
            }
        }
		
		public CodecID codecID {
            get { return (CodecID)(Data[0] & 0x0f); }
			set {
                Data[0] &= 0xf0;    // clear bottom 4 bits
                Data[0] |= (byte)((int)value & 0x0f);
            }
        }
		
		public int infoPacketValue {
            get { return Data[1]; }
            set { Data[1] = (byte)value; }
        }

		public AVCPacketType avcPacketType {
            get { return (AVCPacketType)((codecID != CodecID.AVC) ? 0 : Data[1]); }
            set {
                Data[1] = (byte)value;
                if (avcPacketType != AVCPacketType.NALU) {
                    // zero the composition time offset
                    Data[2] = 0;
                    Data[3] = 0;
                    Data[4] = 0;
                }
            }
        }
		
		public uint avcCompositionTimeOffset {
            get {
                // throw error if frameType == FRAME_TYPE_INFO?
                if ((codecID != CodecID.AVC) || (avcPacketType != AVCPacketType.NALU)) return 0;

                uint value = (uint)((Data[2] << 16) | (Data[3] << 8) | Data[4]);
                if ((value & 0x00800000) > 0) {
                    value |= 0xff000000;    // sign-extend the 24-bit read for a 32-bit int
                }
                return value;
            }
            set {
                // throw error if frameType == FRAME_TYPE_INFO?
                if ((codecID != CodecID.AVC) || (avcPacketType != AVCPacketType.NALU)) {
                    throw new InvalidOperationException("set avcCompositionTimeOffset() not permitted unless codecID is CODEC_ID_AVC and avcPacketType is AVC NALU");
                }
                Data[2] = (byte)((value >> 16) & 0xff);
                Data[3] = (byte)((value >> 8 ) & 0xff);
                Data[4] = (byte)((value      ) & 0xff);
            }
        }
		
        public enum FrameType: int {
		    KEYFRAME           = 1,
		    INTER              = 2,
		    DISPOSABLE_INTER   = 3,
		    GENERATED_KEYFRAME = 4,
		    INFO               = 5
        }

        public enum CodecID : int {
            JPEG      = 1,
		    SORENSON  = 2,
		    SCREEN    = 3,
		    VP6       = 4,
		    VP6_ALPHA = 5,
		    SCREEN_V2 = 6,
		    AVC       = 7
        }
        public enum AVCPacketType : int { SEQUENCE_HEADER = 0, NALU = 1, END_OF_SEQUENCE = 2 }
        public enum InfoPacketSeek: int { START = 0, END = 1 }
	}
}