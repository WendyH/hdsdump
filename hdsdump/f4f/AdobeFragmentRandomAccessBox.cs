using System.Collections.Generic;

namespace hdsdump.f4f {
    public class AdobeFragmentRandomAccessBox: FullBox {
        public uint timeScale;
        public List<LocalRandomAccessEntry>  localRandomAccessEntries  = new List<LocalRandomAccessEntry>();
        public List<GlobalRandomAccessEntry> globalRandomAccessEntries = new List<GlobalRandomAccessEntry>();

        private const uint FULL_BOX_FIELD_FLAGS_LENGTH = 3;
		private const uint AFRA_MASK_LONG_ID           = 128;
		private const uint AFRA_MASK_LONG_OFFSET       = 64;
		private const uint AFRA_MASK_GLOBAL_ENTRIES    = 32;

        public override void Parse(BoxInfo bi, HDSBinaryReader br) {
            base.Parse(bi, br);

            uint sizes = br.ReadByte();
            bool longIdFields     = ((sizes & AFRA_MASK_LONG_ID) > 0);
            bool longOffsetFields = ((sizes & AFRA_MASK_LONG_OFFSET) > 0);
            bool globalEntries    = ((sizes & AFRA_MASK_GLOBAL_ENTRIES) > 0);

            timeScale = br.ReadUInt32();

            localRandomAccessEntries.Clear();
            uint entryCount = br.ReadUInt32();
            for (uint i = 0; i < entryCount; i++) {
                LocalRandomAccessEntry lrae = new LocalRandomAccessEntry();
                lrae.Parse(br, longOffsetFields);
                localRandomAccessEntries.Add(lrae);
            }

            globalRandomAccessEntries.Clear();
            if (globalEntries) {
                entryCount = br.ReadUInt32();
                for (int i = 0; i < entryCount; i++) {
                    GlobalRandomAccessEntry grae = new GlobalRandomAccessEntry();
                    grae.Parse(br, longIdFields, longOffsetFields);
                    globalRandomAccessEntries.Add(grae);
                }
            }

        }

        /// <summary>
        /// Given a seekTime, return the offset of the key frame that is nearest from the 
        /// left. This is done among localRandomAccessEntries only.
        /// </summary>
        public LocalRandomAccessEntry findNearestKeyFrameOffset(uint seekToTime) {
            int i = localRandomAccessEntries.Count - 1;
			while (i >= 0) {
                LocalRandomAccessEntry entry = localRandomAccessEntries[i];
				if (entry.time <= seekToTime) {
                    return entry;
                }
                i--;
			}
			return null;
		}

    }
}
