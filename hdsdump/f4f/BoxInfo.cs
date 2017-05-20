namespace hdsdump.f4f {
    public class BoxInfo {
        public uint   Size;
		public string Type;
        public uint   Length = 0;

        // CONSTUCTOR
        public BoxInfo(uint size, string type, uint length) {
            Size   = size;
            Type   = type;
            Length = length;
        }

        public static BoxInfo getNextBoxInfo(HDSBinaryReader br) {
            if (!br.BaseStream.CanRead || (br.BytesAvailable < F4FConstants.FIELD_SIZE_LENGTH + F4FConstants.FIELD_TYPE_LENGTH))
                return null;
            uint   size = br.ReadUInt32();
            string type = br.ReadUtfBytes(F4FConstants.FIELD_TYPE_LENGTH);
            uint length = F4FConstants.FIELD_SIZE_LENGTH + F4FConstants.FIELD_TYPE_LENGTH;

            if (size == F4FConstants.FLAG_USE_LARGE_SIZE) {
                size = (uint)br.ReadUInt64();
                length += F4FConstants.FIELD_LARGE_SIZE_LENGTH;
            }

            if (type == F4FConstants.EXTENDED_TYPE) {
                // Read past the extended type.
                br.Position += F4FConstants.FIELD_EXTENDED_TYPE_LENGTH;
                length      += F4FConstants.FIELD_EXTENDED_TYPE_LENGTH;
            }

            return new BoxInfo(size, type, length);
        }
    }
}
