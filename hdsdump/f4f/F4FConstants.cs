namespace hdsdump.f4f {
    internal class F4FConstants {
        internal const string BOX_TYPE_UUID = "uuid";
        internal const string BOX_TYPE_ABST = "abst";
        internal const string BOX_TYPE_ASRT = "asrt";
        internal const string BOX_TYPE_AFRT = "afrt";
        internal const string BOX_TYPE_AFRA = "afra";
        internal const string BOX_TYPE_MDAT = "mdat";
        internal const string BOX_TYPE_MOOF = "moof";
        
        internal const string EXTENDED_TYPE = "uuid";
                
        internal const uint FIELD_SIZE_LENGTH = 4;
        internal const uint FIELD_TYPE_LENGTH = 4;
        internal const uint FIELD_LARGE_SIZE_LENGTH = 8;
        internal const uint FIELD_EXTENDED_TYPE_LENGTH = 16;
        
        internal const uint FLAG_USE_LARGE_SIZE = 1;
    }
}
