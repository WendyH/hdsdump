using System.Collections.Generic;
using System.IO;

namespace hdsdump.flv {
    public class FLVTag {
        public const int TAG_HEADER_BYTE_COUNT = 11;
        public const int PREV_TAG_BYTE_COUNT   = 4;

        /// <summary>
        /// Indicates if packets are filtered.
        /// false = No pre-processing required.
        /// true = Pre-processing (such as decryption) of the packet is required before it can be rendered.
        /// </summary>
        public bool Filter = false;

        /// <summary>
        /// Type of contents in this tag. The following types are defined:
        /// 8 = audio
        /// 9 = video
        /// 18 = script data
        /// </summary>
        public TagType Type;

        /// <summary>
        /// Length of the message. Number of bytes after StreamID to
        /// end of tag (Equal to length of the tag – 11)
        /// </summary>
        public uint DataSize { get { return (uint)Data.Length; } }

        /// <summary>
        /// Time in milliseconds at which the data in this tag applies. This value is relative to the first tag in the FLV file, which always has a timestamp of 0
        /// </summary>
        public uint Timestamp;

        public byte[] Data;

        public uint SizeOfPreviousPacket = 0;

        public bool IsAkamaiEncrypted => Type == TagType.AKAMAI_ENC_AUDIO || Type == TagType.AKAMAI_ENC_VIDEO;
        public FLVTag(TagType type) {
            Type = type;
        }

        public static List<FLVTag> GetTags(byte[] data) {
            List<FLVTag> tags = new List<FLVTag>();
            MemoryStream stream = null;
            try {
                stream = new MemoryStream(data);
                using (HDSBinaryReader br = new HDSBinaryReader(stream)) {
                    stream = null;
                    FLVTag tag = Parse(br);
                    while (tag != null) {
                        tags.Add(tag);
                        tag = Parse(br);
                    }
                }
            } finally {
                if (stream != null)
                    stream.Dispose();
            }
            return tags;
        }

        public static FLVTag Parse(HDSBinaryReader br) {
            if (!br.BaseStream.CanRead || (br.BytesAvailable <= TAG_HEADER_BYTE_COUNT))
                return null;
            FLVTag tag;

            byte b = br.ReadByte();
            TagType type = (TagType)(b & 0x1f);

            switch (type) {
                case TagType.AUDIO:
                case TagType.AKAMAI_ENC_AUDIO: tag = new FLVTagAudio(type); break;
                case TagType.VIDEO:
                case TagType.AKAMAI_ENC_VIDEO: tag = new FLVTagVideo(type); break;
                default:                       tag = new FLVTag(type);      break;
            }
            tag.Filter    = (b & 0x20) > 0;
            uint dataSize = br.ReadUInt24();
            tag.Timestamp = br.ReadUInt24() + (uint)(br.ReadByte() << 24);
            uint StreamID = br.ReadUInt24();
            tag.Data      = br.ReadBytes((int)dataSize);
            if (br.BytesAvailable > 3) {
                tag.SizeOfPreviousPacket = br.ReadUInt32();
            }
            return tag;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public byte[] GetBytes() {
            using (var stream = new MemoryStream()) {
                using (var bw = new HDSBinaryWriter(stream)) {
                    bw.WriteByte((byte)Type);
                    bw.WriteUInt24((uint)Data.Length);
                    byte extendedTimestamp = (byte)((Timestamp & 0xFF000000) >> 24);
                    bw.WriteUInt24(Timestamp & 0x00FFFFFF);
                    bw.WriteByte(extendedTimestamp);
                    bw.WriteUInt24(0);
                    bw.Write(Data);
                    bw.WriteUInt32((uint)(Data.Length + TAG_HEADER_BYTE_COUNT));
                    return stream.ToArray();
                }
            }
        }

        public static void GetVideoAndAudioTags(TagsStore tagsStore, byte[] data) {
            MemoryStream stream = null;
            try {
                stream = new MemoryStream(data);
                using (HDSBinaryReader br = new HDSBinaryReader(stream)) {
                    stream = null;
                    FLVTag tag = Parse(br);
                    while (tag != null) {
                        // only audio or video and skipping small sized packet
                        if (tag.DataSize > 2 && (tag.Type == TagType.AUDIO || tag.Type == TagType.AKAMAI_ENC_AUDIO || tag.Type == TagType.VIDEO || tag.Type == TagType.AKAMAI_ENC_VIDEO)) {

                            if (!tagsStore.hasAudio && tag is FLVTagAudio audioTag) {
                                tagsStore.hasAudio      = true;
                                tagsStore.AudioFormat   = audioTag.SoundFormat;
                                tagsStore.AudioRate     = audioTag.SoundRate;
                                tagsStore.AudioChannels = audioTag.SoundChannels;
                            }

                            if (!tagsStore.hasVideo && tag is FLVTagVideo videoTag) {
                                tagsStore.hasVideo   = true;
                                tagsStore.VideoCodec = videoTag.CodecID;
                            }

                            tagsStore.Enqueue(tag);

                            if (tag.Timestamp > tagsStore.lastTS)
                                tagsStore.lastTS = tag.Timestamp;
                            if (!tagsStore.isAkamaiEncrypted && tag.IsAkamaiEncrypted)
                                tagsStore.isAkamaiEncrypted = true;
                        }
                        tag = Parse(br);
                    }
                }
            } finally {
                if (stream != null)
                    stream.Dispose();
            }
        }

        public enum TagType : int {
            AUDIO            = 0x08,
            VIDEO            = 0x09,
            SCRIPTDATAOBJECT = 0x12,
            AKAMAI_ENC_AUDIO = 0x0A,
            AKAMAI_ENC_VIDEO = 0x0B,
        }

    }
}
