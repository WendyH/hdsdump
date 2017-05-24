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
            LINEAR_PCM      = 0,  // Linear PCM, platform endian
            ADPCM           = 1,  // ADPCM
            MP3             = 2,  // MP3            LINEAR_PCM_LE   = 3,  // Linear PCM, little endian
            NELLYMOSER_16K  = 4,  // Nellymoser 16 kHz mono
            NELLYMOSER_8K   = 5,  // Nellymoser 8 kHz mono
            NELLYMOSER      = 6,  // Nellymoser            G711A           = 7,  // G.711 A-law logarithmic PCM
            G711U           = 8,  // G.711 mu-law logarithmic PCM
            UNKNOWN_FORMAT  = 9,  // reserved
            AAC             = 10, // AAC            SPEEX           = 11, // Speex            MP3_8K          = 14, // MP3 8 kHz
            DEVICE_SPECIFIC = 15  // Device-specific sound
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

        public static string RateToString(SoundRate soundRate) {
            switch (soundRate) {
                case SoundRate._5K : return "5.5 kHz";
                case SoundRate._11K: return "11 kHz";
                case SoundRate._22K: return "22 kHz";
                case SoundRate._44K: return "44 kHz";
            }
            return "Unknown";
        }

        public static string FormatToString(SoundFormat soundFormat) {
            switch (soundFormat) {
                case SoundFormat.LINEAR_PCM     : return "Linear PCM, platform endian";
                case SoundFormat.ADPCM          : return "ADPCM";
                case SoundFormat.MP3            : return "MP3";
                case SoundFormat.LINEAR_PCM_LE  : return "Linear PCM, little endian";
                case SoundFormat.NELLYMOSER_16K : return "Nellymoser 16 kHz mono";
                case SoundFormat.NELLYMOSER_8K  : return "Nellymoser 8 kHz mono";
                case SoundFormat.NELLYMOSER     : return "Nellymoser";
                case SoundFormat.G711A          : return "G.711 A-law logarithmic PCM";
                case SoundFormat.G711U          : return "G.711 mu-law logarithmic PCM";
                case SoundFormat.AAC            : return "AAC";
                case SoundFormat.SPEEX          : return "Speex";
                case SoundFormat.MP3_8K         : return "MP3 8 kHz";
                case SoundFormat.DEVICE_SPECIFIC: return "Device-specific sound";
            }
            return "Unknown";
        }
    }
}