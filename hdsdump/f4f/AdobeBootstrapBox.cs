using System.Collections.Generic;

namespace hdsdump.f4f {
    public class AdobeBootstrapBox: FullBox {
        public uint   bootstrapVersion;
        public uint   profile;
        public bool   live;
        public bool   update;
        public uint   timeScale;
        public ulong  currentMediaTime;
        public ulong  smpteTimeCodeOffset;
        public string movieIdentifier;
        public uint   serverEntryCount;
        public string drmData;
        public string metadata;
        public List<string> serverBaseURLs                   = new List<string>();
        public List<string> qualitySegmentURLModifiers       = new List<string>();
        public List<AdobeSegmentRunTable>  segmentRunTables  = new List<AdobeSegmentRunTable>();
        public List<AdobeFragmentRunTable> fragmentRunTables = new List<AdobeFragmentRunTable>();

        public override void Parse(BoxInfo bi, HDSBinaryReader br) {
            base.Parse(bi, br);

            bootstrapVersion = br.ReadUInt32();

            byte temp = br.ReadByte();
            profile = (uint)(temp >> 6);
            live    = ((temp & 0x20) > 0);
            update  = ((temp & 0x01) > 0);

            timeScale           = br.ReadUInt32();
            currentMediaTime    = br.ReadUInt64();
            smpteTimeCodeOffset = br.ReadUInt64();
            movieIdentifier     = br.ReadString();

            serverBaseURLs.Clear();
            int serverEntryCount = br.ReadByte();
            for (int i = 0; i < serverEntryCount; i++) {
                serverBaseURLs.Add(br.ReadString());
            }

            qualitySegmentURLModifiers.Clear();
            int qualityEntryCount = br.ReadByte();
            for (int i = 0; i < qualityEntryCount; i++) {
                qualitySegmentURLModifiers.Add(br.ReadString());
            }

            drmData  = br.ReadString();
            metadata = br.ReadString();

            segmentRunTables.Clear();
            uint segmentRunTableCount = br.ReadByte();
            for (uint i = 0; i < segmentRunTableCount; i++) {
                BoxInfo boxInfo = BoxInfo.getNextBoxInfo(br);
                if (boxInfo == null) break;
                if (boxInfo.Type == F4FConstants.BOX_TYPE_ASRT) {
                    AdobeSegmentRunTable asrt = new AdobeSegmentRunTable();
                    asrt.Parse(boxInfo, br);
                    segmentRunTables.Add(asrt);
                }
            }

            fragmentRunTables.Clear();
            uint fragmentRunTableCount = br.ReadByte();
            for (uint i = 0; i < fragmentRunTableCount; i++) {
                BoxInfo boxInfo = BoxInfo.getNextBoxInfo(br);
                if (boxInfo == null) break;
                if (boxInfo.Type == F4FConstants.BOX_TYPE_AFRT) {
                    AdobeFragmentRunTable afrt = new AdobeFragmentRunTable();
                    afrt.Parse(boxInfo, br);
                    fragmentRunTables.Add(afrt);
                }
            }

            // Check if live stream is still live
            if (live && (fragmentRunTables.Count > 0) && ContentComplete()) {
                live = false;
                RemoveLastFragment();
            }
        }

        public SegmentFragmentPair GetFirstSegment() {
            if (segmentRunTables.Count > 0) {
                AdobeSegmentRunTable segTable = segmentRunTables[0];
                if (segTable.segmentFragmentPairs.Count > 0) {
                    return segTable.segmentFragmentPairs[0];
                }
            }
            return null;
        }

        public SegmentFragmentPair GetLastSegment() {
            if (segmentRunTables.Count > 0) {
                AdobeSegmentRunTable segTable = segmentRunTables[segmentRunTables.Count - 1];
                if (segTable.segmentFragmentPairs.Count > 0) {
                    return segTable.segmentFragmentPairs[segTable.segmentFragmentPairs.Count - 1];
                } else if (segmentRunTables.Count > 1) {
                    segTable = segmentRunTables[segmentRunTables.Count - 2];
                    if (segTable.segmentFragmentPairs.Count > 0)
                        return segTable.segmentFragmentPairs[segTable.segmentFragmentPairs.Count - 1];
                }
            }
            return null;
        }

        public FragmentDurationPair GetFirstFragment() {
            if (fragmentRunTables.Count > 0) {
                AdobeFragmentRunTable fragTable = fragmentRunTables[0];
                if (fragTable.fragmentDurationPairs.Count > 0) {
                    return fragTable.fragmentDurationPairs[0];
                }
            }
            return null;
        }

        public FragmentDurationPair GetLastFragment() {
            if (fragmentRunTables.Count > 0) {
                AdobeFragmentRunTable fragTable = fragmentRunTables[fragmentRunTables.Count - 1];
                if (fragTable.fragmentDurationPairs.Count > 0) {
                    return fragTable.fragmentDurationPairs[fragTable.fragmentDurationPairs.Count - 1];
                } else if (fragmentRunTables.Count > 1) {
                    fragTable = fragmentRunTables[fragmentRunTables.Count - 2];
                    if (fragTable.fragmentDurationPairs.Count > 0)
                        return fragTable.fragmentDurationPairs[fragTable.fragmentDurationPairs.Count - 1];
                }
            }
            return null;
        }

        public void RemoveLastFragment() {
            if (fragmentRunTables.Count > 0) {
                AdobeFragmentRunTable fragTable = fragmentRunTables[fragmentRunTables.Count - 1];
                if (fragTable.fragmentDurationPairs.Count > 0) {
                    fragTable.fragmentDurationPairs.RemoveAt(fragTable.fragmentDurationPairs.Count - 1);
                } else if (fragmentRunTables.Count > 1) {
                    fragTable = fragmentRunTables[fragmentRunTables.Count - 2];
                    if (fragTable.fragmentDurationPairs.Count > 0)
                        fragTable.fragmentDurationPairs.RemoveAt(fragTable.fragmentDurationPairs.Count - 1);
                }
            }
        }

        ///<summary>The total number of fragments in the movie.</summary>
        public uint GetFragmentsCount()
        {
            AdobeFragmentRunTable      lastFragmentTable = fragmentRunTables[fragmentRunTables.Count - 1];
            List<FragmentDurationPair> fdps              = lastFragmentTable.fragmentDurationPairs;

            if (fdps.Count < 1) {
                SegmentFragmentPair lastSegment = GetLastSegment();
                return lastSegment.fragmentsAccrued + lastSegment.fragmentsPerSegment - 1;
            }

            FragmentDurationPair lastValidFdp = fdps[fdps.Count - 1];
            if (lastValidFdp.duration == 0) {
                lastValidFdp = fdps[fdps.Count - 2];
            }

            int  deltaTime = (int)(currentMediaTime - lastValidFdp.durationAccrued);
            uint fragCount = (uint)((deltaTime <= 0) ? 0 : (deltaTime / lastValidFdp.duration));
            return lastValidFdp.firstFragment + fragCount - 1;
        }


        public uint GetSegmentFromFragment(uint fragN) {
            if (segmentRunTables.Count == 0) return 1;

            if (!live) {
                SegmentFragmentPair firstSegment = GetFirstSegment();

                if (firstSegment == null) return 1;

                uint segNum = firstSegment.firstSegment;
                foreach (var tab in segmentRunTables) {
                    foreach (var s in tab.segmentFragmentPairs) {
                        if ((segNum >= s.firstSegment) && (fragN < (s.fragmentsAccrued + s.fragmentsPerSegment))) {
                            return segNum;
                        }
                    }
                }

            }
            return GetLastSegment().firstSegment;
        }

        public uint GetSegmentByTimestamp(uint timestamp) {
            foreach (var tab in fragmentRunTables) {
                foreach (var fdp in tab.fragmentDurationPairs) {
                    if (fdp.durationAccrued + fdp.duration > timestamp) {
                        return fdp.firstFragment;
                    }
                }
            }
            return 1;
        }

        public FragmentDurationPair GetFragmentInfo(uint fragIndex) {
            foreach (var tab in fragmentRunTables) {
                foreach (var fdp in tab.fragmentDurationPairs) {
                    if (fdp.firstFragment >= fragIndex)
                        return fdp;
                }
            }
            return new FragmentDurationPair();
        }

        public bool ContentComplete() {
            AdobeFragmentRunTable lastFrt = fragmentRunTables[fragmentRunTables.Count - 1];
            return lastFrt.tableComplete();			
        }
    }
}
