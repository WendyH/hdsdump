using System.Collections.Generic;

namespace hdsdump.f4f {
    public class AdobeSegmentRunTable: FullBox {
        public List<string>              qualitySegmentURLModifiers = new List<string>();
        public List<SegmentFragmentPair> segmentFragmentPairs       = new List<SegmentFragmentPair>();

        public override void Parse(BoxInfo bi, HDSBinaryReader br) {
            base.Parse(bi, br);

            qualitySegmentURLModifiers.Clear();
            uint qualityEntryCount = br.ReadByte();
            for (uint i = 0; i < qualityEntryCount; i++) {
                qualitySegmentURLModifiers.Add(br.ReadString());
            }

            uint entryCount = br.ReadUInt32();
            for (uint i = 0; i < entryCount; i++) {
                addSegmentFragmentPair(new SegmentFragmentPair(br.ReadUInt32(), br.ReadUInt32()));
            }
        }

        /// <summary>
        /// Adds the given SegmentFragmentPair to this run table.
        /// </summary>
        private void addSegmentFragmentPair(SegmentFragmentPair sfp) {
            SegmentFragmentPair prevSfp = segmentFragmentPairs.Count <= 0 ? null : segmentFragmentPairs[segmentFragmentPairs.Count - 1];
            uint fragmentsAccrued = 0;
            if (prevSfp != null) {
                fragmentsAccrued = prevSfp.fragmentsAccrued + (sfp.firstSegment - prevSfp.firstSegment) * prevSfp.fragmentsPerSegment;
            }
            sfp.fragmentsAccrued = fragmentsAccrued;
            segmentFragmentPairs.Add(sfp);
        }

        public uint findSegmentIdByFragmentId(uint fragmentId) {
            SegmentFragmentPair curSfp;
            if (fragmentId < 1) {
                // fragmentId should never be smaller than 1, same for segmentId. So 
                // return 0 to signal an error condition.
                return 0;
            }
            for (int i = 1; i < segmentFragmentPairs.Count; i++) {
                curSfp = segmentFragmentPairs[i];
                if (curSfp.fragmentsAccrued >= fragmentId) {
                    return calculateSegmentId(segmentFragmentPairs[i - 1], fragmentId);
                }
            }
            return calculateSegmentId(segmentFragmentPairs[segmentFragmentPairs.Count - 1], fragmentId);
        }
        
        public uint totalFragments() {
            if (segmentFragmentPairs.Count < 1) return 0;
            SegmentFragmentPair sfp = segmentFragmentPairs[segmentFragmentPairs.Count - 1];
            return sfp.fragmentsPerSegment + sfp.fragmentsAccrued;
        }

        private uint calculateSegmentId(SegmentFragmentPair sfp, uint fragmentId) {
            return sfp.firstSegment + ((fragmentId - sfp.fragmentsAccrued - 1) / sfp.fragmentsPerSegment);
        }	
    }
}
