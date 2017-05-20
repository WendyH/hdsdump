using System;

namespace hdsdump.flv {
    public class FLVTagAudio : FLVTag {

		public FLVTagAudio(TagType type = TagType.AUDIO): base(type) {
		}
		
		public int soundFormatByte {
            get { return Data[0]; }
			set { Data[0] = (byte)value; }
		}
		
		public SoundFormat soundFormat {
            get { return (SoundFormat)((Data[0] >> 4) & 0x0f); }
			set {
                Data[0] &= 0x0f;    // clear upper 4 bits
                Data[0] |= (byte)(((int)value << 4) & 0xf0);

                if (value == SoundFormat.AAC) {
                    soundRate     = SoundRate._44K;
                    soundChannels = SoundChannels.STEREO;
                    isAACSequenceHeader = false;    // reasonable default
                }
            }
        }
		
		public SoundRate soundRate {
            get {
                switch ((Data[0] >> 2) & 0x03) {
                    case 0 : return SoundRate._5K;
                    case 1 : return SoundRate._11K;
                    case 2 : return SoundRate._22K;
                    default: return SoundRate._44K;
                }
            }
            set {
                int setting;
                switch (value) {
                    case SoundRate._5K : setting = 0; break;
                    case SoundRate._11K: setting = 1; break;
                    case SoundRate._22K: setting = 2; break;
                    case SoundRate._44K: setting = 3; break;
                    default:
                        throw new InvalidOperationException("set soundRate valid values 5512.5, 11025, 22050, 44100");
                }
                Data[0] &= 0xf3;   // clear upper two bits of lower 4 bits
                Data[0] |= (byte)(setting << 2);
            }
        }

		
		public SoundSize soundSize {
            get {
                if (((Data[0] >> 1) & 0x01) > 0) return SoundSize._16BITS;
                else                             return SoundSize._8BITS;
            }
            set {
                switch (value) {
                    case SoundSize._8BITS:
                        Data[0] &= 0xfd;   // clear second bit up
                        break;
                    case SoundSize._16BITS:
                        Data[0] |= 0x02;   // set second bit up
                        break;
                    default:
                        throw new InvalidOperationException("set soundSize valid values 8, 16");
                }

            }
        }
		
		public SoundChannels soundChannels {
            get { return ((Data[0] & 0x01) > 0) ? SoundChannels.STEREO : SoundChannels.MONO; }
            set {
                switch (value) {
                    case SoundChannels.MONO  : Data[0] &= 0xfe; break;  // clear lowest bit
                    case SoundChannels.STEREO: Data[0] |= 0x01; break;  // set lowest bit
                }
            }
        }
		
		public bool isAACSequenceHeader {
            get { return (soundFormat != SoundFormat.AAC) ? false : Data[1] == 0; }
            set {
                if (soundFormat != SoundFormat.AAC) {
                    soundFormat = SoundFormat.AAC;
                    throw new InvalidOperationException("set isAACSequenceHeader not valid if soundFormat != AAC");
                }
                Data[1] = (byte)(value ? 0 : 1);
            }
        }

        public enum SoundFormat : int {
            LINEAR = 0,
            ADPCM = 1,
            MP3 = 2,
            LINEAR_LE = 3,
            NELLYMOSER_16K = 4,
            NELLYMOSER_8K = 5,
            NELLYMOSER = 6,
            G711A = 7,
            G711U = 8,
            AAC = 10,
            SPEEX = 11,
            MP3_8K = 14,
            DEVICE_SPECIFIC = 15
        }

        public enum SoundRate : int {
            _5K  = 5512,
            _11K = 11025,
            _22K = 22050,
            _44K = 44100
        }

        public enum SoundSize : int {
            _8BITS  = 8,
            _16BITS = 16
        }

        public enum SoundChannels : int {
            MONO   = 1,
            STEREO = 2
        }
	}
}