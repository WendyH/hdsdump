namespace hdsdump.f4f {
    public class SegmentFragmentPair {
        public uint firstSegment;
        public uint fragmentsPerSegment;
        public uint fragmentsAccrued;

        // CONSTRUCTOR
        public SegmentFragmentPair(uint firstSegment, uint fragmentsPerSegment) {
            this.firstSegment        = firstSegment;
            this.fragmentsPerSegment = fragmentsPerSegment;
        }

    }
}
