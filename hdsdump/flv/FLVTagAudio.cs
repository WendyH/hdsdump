using System;

namespace hdsdump.flv {
    public class FLVTagAudio : FLVTag {

        public FLVTagAudio(TagType type = TagType.AUDIO): base(type) {
        }
        
        public int SoundFormatByte {
            get { return Data[0]; }
            set { Data[0] = (byte)value; }
        }
        
        public Format SoundFormat {
            get { return (Format)((Data[0] >> 4) & 0x0f); }
            set {
                Data[0] &= 0x0f;    // clear upper 4 bits
                Data[0] |= (byte)(((int)value << 4) & 0xf0);

                if (value == Format.AAC) {
                    SoundRate     = Rate._44K;
                    SoundChannels = Channels.STEREO;
                    IsAACSequenceHeader = false;    // reasonable default
                }
            }
        }
        
        public Rate SoundRate {
            get {
                switch ((Data[0] >> 2) & 0x03) {
                    case 0 : return Rate._5K;
                    case 1 : return Rate._11K;
                    case 2 : return Rate._22K;
                    default: return Rate._44K;
                }
            }
            set {
                int setting;
                switch (value) {
                    case Rate._5K : setting = 0; break;
                    case Rate._11K: setting = 1; break;
                    case Rate._22K: setting = 2; break;
                    case Rate._44K: setting = 3; break;
                    default:
                        throw new InvalidOperationException("set soundRate valid values 5512.5, 11025, 22050, 44100");
                }
                Data[0] &= 0xf3;   // clear upper two bits of lower 4 bits
                Data[0] |= (byte)(setting << 2);
            }
        }

        
        public AudioSize SoundSize {
            get {
                if (((Data[0] >> 1) & 0x01) > 0) return AudioSize._16BITS;
                else                             return AudioSize._8BITS;
            }
            set {
                switch (value) {
                    case AudioSize._8BITS:
                        Data[0] &= 0xfd;   // clear second bit up
                        break;
                    case AudioSize._16BITS:
                        Data[0] |= 0x02;   // set second bit up
                        break;
                    default:
                        throw new InvalidOperationException("set soundSize valid values 8, 16");
                }

            }
        }
        
        public Channels SoundChannels {
            get { return ((Data[0] & 0x01) > 0) ? Channels.STEREO : Channels.MONO; }
            set {
                switch (value) {
                    case Channels.MONO  : Data[0] &= 0xfe; break;  // clear lowest bit
                    case Channels.STEREO: Data[0] |= 0x01; break;  // set lowest bit
                }
            }
        }
        
        public bool IsAACSequenceHeader {
            get { return (SoundFormat != Format.AAC) ? false : Data[1] == 0; }
            set {
                if (SoundFormat != Format.AAC) {
                    SoundFormat = Format.AAC;
                    throw new InvalidOperationException("set isAACSequenceHeader not valid if soundFormat != AAC");
                }
                Data[1] = (byte)(value ? 0 : 1);
            }
        }

        public enum Format : int {
            LINEAR_PCM      = 0,  // Linear PCM, platform endian
            ADPCM           = 1,  // ADPCM
            MP3             = 2,  // MP3
            LINEAR_PCM_LE   = 3,  // Linear PCM, little endian
            NELLYMOSER_16K  = 4,  // Nellymoser 16 kHz mono
            NELLYMOSER_8K   = 5,  // Nellymoser 8 kHz mono
            NELLYMOSER      = 6,  // Nellymoser
            G711A           = 7,  // G.711 A-law logarithmic PCM
            G711U           = 8,  // G.711 mu-law logarithmic PCM
            UNKNOWN_FORMAT  = 9,  // reserved
            AAC             = 10, // AAC
            SPEEX           = 11, // Speex
            MP3_8K          = 14, // MP3 8 kHz
            DEVICE_SPECIFIC = 15  // Device-specific sound
        }

        public enum Rate : int {
            _5K  = 5512,
            _11K = 11025,
            _22K = 22050,
            _44K = 44100
        }

        public enum AudioSize : int {
            _8BITS  = 8,
            _16BITS = 16
        }

        public enum Channels : int {
            MONO   = 1,
            STEREO = 2
        }

        public static string RateToString(Rate soundRate) {
            switch (soundRate) {
                case Rate._5K : return "5.5 kHz";
                case Rate._11K: return "11 kHz";
                case Rate._22K: return "22 kHz";
                case Rate._44K: return "44 kHz";
            }
            return "Unknown";
        }

        public static string FormatToString(Format soundFormat) {
            switch (soundFormat) {
                case Format.LINEAR_PCM     : return "Linear PCM, platform endian";
                case Format.ADPCM          : return "ADPCM";
                case Format.MP3            : return "MP3";
                case Format.LINEAR_PCM_LE  : return "Linear PCM, little endian";
                case Format.NELLYMOSER_16K : return "Nellymoser 16 kHz mono";
                case Format.NELLYMOSER_8K  : return "Nellymoser 8 kHz mono";
                case Format.NELLYMOSER     : return "Nellymoser";
                case Format.G711A          : return "G.711 A-law logarithmic PCM";
                case Format.G711U          : return "G.711 mu-law logarithmic PCM";
                case Format.AAC            : return "AAC";
                case Format.SPEEX          : return "Speex";
                case Format.MP3_8K         : return "MP3 8 kHz";
                case Format.DEVICE_SPECIFIC: return "Device-specific sound";
            }
            return "Unknown";
        }
    }
}