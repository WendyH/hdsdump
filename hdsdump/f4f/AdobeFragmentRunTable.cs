using System;
using System.Collections.Generic;

namespace hdsdump.f4f {
    public class AdobeFragmentRunTable : FullBox {
        public uint                       timeScale;
        public List<string>               qualitySegmentURLModifiers = new List<string>();
        public List<FragmentDurationPair> fragmentDurationPairs      = new List<FragmentDurationPair>();

        public override void Parse(BoxInfo bi, HDSBinaryReader br) {
            base.Parse(bi, br);

            timeScale = br.ReadUInt32();

            qualitySegmentURLModifiers.Clear();
            uint qualityEntryCount = br.ReadByte();
            for (uint i = 0; i < qualityEntryCount; i++) {
                qualitySegmentURLModifiers.Add(br.ReadString());
            }

            uint entryCount = br.ReadUInt32();
            for (uint i = 0; i < entryCount; i++) {
                FragmentDurationPair fdp = new FragmentDurationPair();
                fdp.Parse(br);
                fragmentDurationPairs.Add(fdp);
            }
        }

        /// <summary>
        /// Given a time spot in terms of the time scale used by the fragment table, returns the corresponding
        /// Id of the fragment that contains the time spot.
        /// </summary>
        public FragmentAccessInformation findFragmentIdByTime(uint time, uint totalDuration, bool live = false) {
            if (fragmentDurationPairs.Count <= 0) return null;

            FragmentDurationPair fdp = null;
            for (int i = 1; i < fragmentDurationPairs.Count; i++) {
                fdp = fragmentDurationPairs[i];
                if (fdp.durationAccrued >= time) {
                    return validateFragment(calculateFragmentId(fragmentDurationPairs[i - 1], time), totalDuration, live);
                }
            }
            return validateFragment(calculateFragmentId(fragmentDurationPairs[fragmentDurationPairs.Count - 1], time), totalDuration, live);
        }

        /// <summary>
		/// Given a fragment id, check whether the current fragment is valid or a discontinuity.
        /// If the latter, skip to the nearest fragment and return the new fragment id.
        /// 
        /// return the Id of the fragment that is valid.
        /// </summary>
        public FragmentAccessInformation validateFragment(uint fragId, ulong totalDuration, bool live = false) {
            int size = fragmentDurationPairs.Count - 1;
            FragmentAccessInformation fai = null;
            uint timeResidue, timeDistance, fragStartTime;
            for (int i = 0; i < size; i++) {
                FragmentDurationPair curFdp  = fragmentDurationPairs[i];
                FragmentDurationPair nextFdp = fragmentDurationPairs[i + 1];
                if ((curFdp.firstFragment <= fragId) && (fragId < nextFdp.firstFragment)) {
                    if (curFdp.duration <= 0) {
                        fai = getNextValidFragment(i + 1);
                    } else {
                        fai = new FragmentAccessInformation();
                        fai.fragId          = fragId;
                        fai.fragDuration    = curFdp.duration;
                        fai.fragmentEndTime = (uint)curFdp.durationAccrued + curFdp.duration * (fragId - curFdp.firstFragment + 1);
                    }
                    break;
                } else if ((curFdp.firstFragment <= fragId) && endOfStreamEntry(nextFdp)) {
                    if (curFdp.duration > 0) {
                        timeResidue   = (uint)(totalDuration - curFdp.durationAccrued);
                        timeDistance  = (fragId - curFdp.firstFragment + 1) * curFdp.duration;
                        fragStartTime = (fragId - curFdp.firstFragment) * curFdp.duration;
                        if (timeResidue > fragStartTime) {
                            if (!live || ((fragStartTime + curFdp.duration + curFdp.durationAccrued) <= totalDuration)) {
                                fai = new FragmentAccessInformation();
                                fai.fragId = fragId;
                                fai.fragDuration = curFdp.duration;
                                if (timeResidue >= timeDistance) {
                                    fai.fragmentEndTime = (uint)curFdp.durationAccrued + timeDistance;
                                } else {
                                    fai.fragmentEndTime = (uint)curFdp.durationAccrued + timeResidue;
                                }
                                break;
                            }
                        }
                    }

                }
            }
            if (fai == null) {
                FragmentDurationPair lastFdp = fragmentDurationPairs[size];
                if (lastFdp.duration > 0 && fragId >= lastFdp.firstFragment) {
                    timeResidue   = (uint)(totalDuration - lastFdp.durationAccrued);
                    timeDistance  = (fragId - lastFdp.firstFragment + 1) * lastFdp.duration;
                    fragStartTime = (fragId - lastFdp.firstFragment) * lastFdp.duration;
                    if (timeResidue > fragStartTime) {
                        if (!live || ((fragStartTime + lastFdp.duration + lastFdp.durationAccrued) <= totalDuration)) {
                            fai = new FragmentAccessInformation();
                            fai.fragId = fragId;
                            fai.fragDuration = lastFdp.duration;
                            if (timeResidue >= timeDistance) {
                                fai.fragmentEndTime = (uint)lastFdp.durationAccrued + timeDistance;
                            } else {
                                fai.fragmentEndTime = (uint)lastFdp.durationAccrued + timeResidue;
                            }
                        }
                    }
                }
            }
            return fai;
        }

        private FragmentAccessInformation getNextValidFragment(int startIdx) {
            FragmentAccessInformation fai = null;
            for (int i = startIdx; i < fragmentDurationPairs.Count; i++) {
                FragmentDurationPair fdp = fragmentDurationPairs[i];
                if (fdp.duration > 0) {
                    fai = new FragmentAccessInformation();
                    fai.fragId = fdp.firstFragment;
                    fai.fragDuration = fdp.duration;
                    fai.fragmentEndTime = (uint)fdp.durationAccrued + fdp.duration;
                    break;
                }
            }
            return fai;
        }

        private bool endOfStreamEntry(FragmentDurationPair fdp) {
            return (fdp.duration == 0 && fdp.discontinuityIndicator == 0);
        }

        /// <summary>
        /// Given a fragment id, return the number of fragments after the 
        /// fragment with the id given.
        /// </summary>
        public uint fragmentsLeft(uint fragId, uint currentMediaTime) {
            if (fragmentDurationPairs == null || fragmentDurationPairs.Count == 0) {
                return 0;
            }
            FragmentDurationPair fdp = fragmentDurationPairs[fragmentDurationPairs.Count - 1] as FragmentDurationPair;
            uint fragments = (currentMediaTime - (uint)fdp.durationAccrued) / fdp.duration + fdp.firstFragment - fragId - 1;
            return fragments;
        }

        /// <summary>
        /// return whether the fragment table is complete.
        /// </summary>
        public bool tableComplete() {
            if (fragmentDurationPairs == null || fragmentDurationPairs.Count <= 0) {
                return false;
            }
            FragmentDurationPair fdp = fragmentDurationPairs[fragmentDurationPairs.Count - 1] as FragmentDurationPair;
            return (fdp.duration == 0 && fdp.discontinuityIndicator == 0);
        }

        public void adjustEndEntryDurationAccrued(uint value) {
            FragmentDurationPair fdp = fragmentDurationPairs[fragmentDurationPairs.Count - 1];
            if (fdp.duration == 0) {
                fdp.durationAccrued = value;
            }
        }

        public uint getFragmentDuration(uint fragId) {
            int i = 0;
            while ((i < fragmentDurationPairs.Count) && (fragmentDurationPairs[i].firstFragment <= fragId)) {
                i++;
            }
            if (i > 0)
                return fragmentDurationPairs[i - 1].duration;
            else
                return 0;
        }

        /// <summary>
        /// return the first FragmentDurationPair whose index >= i that is not a discontinuity,
        /// or null if no such FragmentDurationPair exists.
        /// </summary>
        private FragmentDurationPair findNextValidFragmentDurationPair(int index) {
            for (int i = index; i < fragmentDurationPairs.Count; ++i) {
                FragmentDurationPair fdp = fragmentDurationPairs[i];
                if (fdp.duration > 0) {
                    return fdp;
                }
            }
            return null;
        }

        /// <summary>
        /// return the first FragmentDurationPair whose index less i that is not a discontinuity,
        /// or null if no such FragmentDurationPair exists.
        /// </summary>
        private FragmentDurationPair findPrevValidFragmentDurationPair(int index) {
            int i = index;
            if (i > fragmentDurationPairs.Count) {
                i = fragmentDurationPairs.Count;
            }
            for (; i > 0; --i) {
                FragmentDurationPair fdp = fragmentDurationPairs[i - 1];
                if (fdp.duration > 0) {
                    return fdp;
                }
            }
            return null;
        }

        private uint calculateFragmentId(FragmentDurationPair fdp, uint time) {
            if (fdp.duration <= 0) {
                return fdp.firstFragment;
            }
            uint deltaTime = time - (uint)fdp.durationAccrued;
            uint count = (deltaTime > 0)? deltaTime / fdp.duration : 1;
            if ((deltaTime % fdp.duration) > 0) {
                count++;
            }
            return fdp.firstFragment + count - 1;
        }


        /// <summary>
        /// return the id of the first (non-discontinuity) fragment in the FRT, or 0 if no such fragment exists
        /// </summary>
        public uint firstFragmentId {
            get {
			    FragmentDurationPair fdp = findNextValidFragmentDurationPair(0);
			    if(fdp == null) {
				    return 0;
			    }
			    return fdp.firstFragment;
            }
        }

        /// <summary>
        /// return true if the fragment is in a true gap within the middle of the content (discontinuity type 2).
        /// returns false if fragment less first fragment number
        /// returns false if fragment greater or equal last fragment number
        /// </summary>
        public bool isFragmentInGap(uint fragmentId) {
			bool inGap = false;
            forEachGap(delegate(FragmentDurationPair fdp, FragmentDurationPair prevFdp, FragmentDurationPair nextFdp) {
				uint gapStartFragmentId = fdp.firstFragment;
                uint gapEndFragmenId    = nextFdp.firstFragment;
				if (gapStartFragmentId <= fragmentId && fragmentId<gapEndFragmenId) {
					inGap = true;
				}
				return !inGap;
			});
			return inGap;
        }

        /// <summary>
		/// return true if the fragment is time is in a true gap within the middle of the content (discontinuity type 2).
		/// returns false if time is less time of the first fragment
		/// returns false if time is greater or equal time the last fragment
        /// </summary>
        public bool isTimeInGap(uint time, uint fragmentInterval) {
			bool inGap = false;

            forEachGap(delegate (FragmentDurationPair fdp, FragmentDurationPair prevFdp, FragmentDurationPair nextFdp) {
				uint prevEndTime       = (uint)prevFdp.durationAccrued + prevFdp.duration* (fdp.firstFragment - prevFdp.firstFragment);
                uint nextStartTime     = (uint)nextFdp.durationAccrued;
                uint idealGapStartTime = (Math.Max(fdp.firstFragment, 1)-1) * fragmentInterval;
                uint idealGapEndTime   = (Math.Max(Math.Max(nextFdp.firstFragment, fdp.firstFragment + 1), 1) - 1) * fragmentInterval;
                uint gapStartTime      = Math.Min(prevEndTime, idealGapStartTime);
                uint gapEndTime        = Math.Max(nextStartTime, idealGapEndTime);
				if(gapStartTime <= time && time < gapEndTime) {
					inGap = true;
				}
				return !inGap;
			});
			return inGap;
        }
		
        /// <summary>
		/// return the number of fragments within a gap (discontinuity 2)
        /// </summary>
		public uint countGapFragments() {
            uint count = 0;

            forEachGap(delegate (FragmentDurationPair fdp, FragmentDurationPair prevFdp, FragmentDurationPair nextFdp) {
                uint gapStartFragmentId = fdp.firstFragment;
                uint gapEndFragmentId   = (uint)(Math.Max(nextFdp.firstFragment, gapStartFragmentId));
				count += gapEndFragmentId - gapStartFragmentId;
                return true;
			});
			return count;
        }

        /// <summary>
        /// calls f for each true gap (discontinuity of type 2) found within the FRT. f will be passed
        /// an Object argument (arg) with 3 fields.
        /// 
        /// arg.fdp will be the discontinuity entry.
        /// arg.prevFdp will be the previous non-discontinuity entry
        /// arg.nextFdp will be the next non-discontinuity entry
        /// 
        /// if f returns false, iteration will halt
        /// if f returns true, iteration will continue
        /// </summary>
        private void forEachGap(Func<FragmentDurationPair, FragmentDurationPair, FragmentDurationPair, bool> f) {
			if (fragmentDurationPairs.Count <= 0) {
				return;
			}
			
			// search for gaps, then check if the desired time is in that gap
			for(int i = 0; i < fragmentDurationPairs.Count; ++i) {
                FragmentDurationPair fdp = fragmentDurationPairs[i];
				
				if (fdp.duration != 0 || fdp.discontinuityIndicator != 2) {
					// skip until we find a discontinuity of type 2
					continue;
				}
				
				// gaps should only be present in the middle of content,
				// so there should always be a previous valid entry and 
				// a next valid entry.
				
				// figure out the previous valid entry
				FragmentDurationPair prevFdp = findPrevValidFragmentDurationPair(i);
				if (prevFdp == null // very uncommon case: there are no non-discontinuities before the discontinuity
					|| prevFdp.firstFragment > fdp.firstFragment) // very uncommon case: fragment numbers are out of order
				{
					continue;
				}

                // search forwards for the first non-discontinuity
                FragmentDurationPair nextFdp = findNextValidFragmentDurationPair(i+1);
				if(nextFdp == null // very uncommon case: there are no valid fragments after the discontinuity
					|| fdp.firstFragment > nextFdp.firstFragment) // very uncommon case: fragment numbers are out of order
				{
					continue;
				}
				
				bool shouldContinue = f(fdp, prevFdp, nextFdp);
				if (!shouldContinue)
					return;
			}
        }

        /// <summary>
        /// return the fragment information for the first fragment in the FRT whose fragment number
        /// is greater than or equal to fragment id. special cases:
        /// 
        /// if fragmentId is in a gap, the first fragment after the gap will be returned.
        /// if fragmentId is in a skip, the first fragment after the skip will be returned.
        /// if fragmentId is before the first fragment-duration-pair, the first fragment will be returned.
        /// if fragmentId is after the last fragment-duration-pair, it will be assumed to exist.
        ///       (in other words, the live point is ignored).
        /// 
        /// if there are no valid entries in the FRT, returns null. this is the only situation that returns null.
        /// </summary>
        public FragmentAccessInformation getFragmentWithIdGreq(uint fragmentId) {
            FragmentDurationPair desiredFdp = null;
            uint desiredFragmentId = 0;

            forEachInterval(delegate (FragmentDurationPair fdp, bool isLast, uint startFragmentId, uint endFragmentId, uint startTime, uint endTime) {
				if (fragmentId<startFragmentId) {
					// before the given interval
					desiredFdp = fdp;
					desiredFragmentId = startFragmentId;
					return false; // stop iterating
				} else if(isLast) {
					// catch all in the last entry
					desiredFdp = fdp;
					desiredFragmentId = fragmentId;
					return false;
				} else if(fragmentId<endFragmentId) {
					// between the start and end of this interval
					desiredFdp = fdp;
					desiredFragmentId = fragmentId;
					return false; // stop iterating
				} else {
					// beyond this interval, but not the last entry 
					return true; // keep iterating
				}
			});
			
			if(desiredFdp == null) {
				// no fragment entries case
				return null;
			}
			
			if(desiredFragmentId<desiredFdp.firstFragment) {
				// probably won't ever hit this
				// just make sure that we're before the start 
				desiredFragmentId = desiredFdp.firstFragment;
			}

            FragmentAccessInformation fai = new FragmentAccessInformation();
            fai.fragId          = desiredFragmentId;
			fai.fragDuration    = desiredFdp.duration;
			fai.fragmentEndTime = (uint)desiredFdp.durationAccrued + (desiredFragmentId - desiredFdp.firstFragment + 1) * desiredFdp.duration;
			return fai;
        }


        /// <summary>
        /// return the fragment information for the first fragment in the FRT that contains a time
        /// greater than or equal to fragment time. special cases:
        /// 
        /// if time is in a gap, the first fragment after the gap will be returned.
        /// if time is in a skip, the first fragment after the skip will be returned.
        /// if time is before the first fragment-duration-pair, the first fragment will be returned.
        /// if time is after the last fragment-duration-pair, it will be assumed to exist.
        ///       (in other words, the live point is ignored).
        /// 
        /// if there are no valid entries in the FRT, returns null. this is the only situation that returns null.
        /// </summary>
        public FragmentAccessInformation getFragmentWithTimeGreq(uint fragmentTime) {
            FragmentDurationPair desiredFdp = null;
			uint desiredFragmentStartTime   = 0;

            forEachInterval(delegate (FragmentDurationPair fdp, bool isLast, uint startFragmentId, uint endFragmentId, uint startTime, uint endTime) {
				if (fragmentTime < startTime) {
					// before the given interval
					desiredFdp = fdp;
					desiredFragmentStartTime = startTime;
					return false; // stop iterating
				} else if (isLast) {
					// catch all in the last entry
					desiredFdp = fdp;
					desiredFragmentStartTime = fragmentTime;
					return false;
				} else if(fragmentTime<endTime) {
					// between the start and end of this interval
					desiredFdp = fdp;
					desiredFragmentStartTime = fragmentTime;
					return false; // stop iterating
				} else {
					// beyond this interval, but not the last entry 
					return true; // keep iterating
				}
			});
			
			if (desiredFdp == null) {
				// no fragment entries case
				return null;
			}

            uint desiredFragmentId = calculateFragmentId(desiredFdp, desiredFragmentStartTime);
            FragmentAccessInformation fai = new FragmentAccessInformation();
            fai.fragId          = desiredFragmentId;
			fai.fragDuration    = desiredFdp.duration;
			fai.fragmentEndTime = (uint)desiredFdp.durationAccrued + (desiredFragmentId - desiredFdp.firstFragment + 1) * desiredFdp.duration;
			return fai;
        }

        /// <summary>
        /// calls f for each set of fragments advertised by the FRT. f will be passed
        /// an Object argument (arg) with 6 fields.
        /// 
        /// arg.fdp will be the entry corresponding to the fragment range.
        /// arg.isLast will be true if this is the last entry in the table
        /// arg.startFragmentId will be the id of the first fragment in the interval
        /// arg.endFragmentId will be the id of the last fragment in the interval + 1
        /// arg.startTime will be the start time of the first fragment in the interval
        /// arg.endTime will be the end time of the last fragment in the interval
        ///  
        /// if f returns false, iteration will halt
        /// if f returns true, iteration will continue
        /// 
        /// if will be called in ascending startFragmentId order.
        /// </summary>
        private void forEachInterval(Func<FragmentDurationPair, bool, uint, uint, uint, uint, bool> f) {
			// search for gaps, then check if the desired time is in that gap
			for(int i = 0; i < fragmentDurationPairs.Count; ++i) {
                FragmentDurationPair fdp = fragmentDurationPairs[i];
				if(fdp.duration == 0) {
					// some kind of discontinuity
					continue;
				}

                uint startFragmentId = fdp.firstFragment;
                uint startTime       = (uint)fdp.durationAccrued;
				
				// find the valid entry or the next skip, gap, or skip+gap
				bool isLast = true; int j;
                for (j = i + 1; j < fragmentDurationPairs.Count; ++j) {
					if(fragmentDurationPairs[j].duration != 0 || // next is valid entry
					   fragmentDurationPairs[j].discontinuityIndicator == 1 || // next is skip
					   fragmentDurationPairs[j].discontinuityIndicator == 2 || // next is gap
					   fragmentDurationPairs[j].discontinuityIndicator == 3)   // next is skip+gap
					{
						isLast = false;
						break;
					} else {
						// eof or some unknown kind of discontinuity
					}
				}

                uint endFragmentId;
                uint endTime;
				if (isLast) {
					// there's no next entry
					endFragmentId = 0;
					endTime       = 0;
				} else {
					endFragmentId = fragmentDurationPairs[j].firstFragment;
					if(startFragmentId > endFragmentId) // very uncommon case: fragment numbers are out of order
						continue;
					endTime = startTime + (endFragmentId - startFragmentId) * fdp.duration;
				}
				
				bool shouldContinue = f(fdp, isLast, startFragmentId, endFragmentId, startTime, endTime);
				if (!shouldContinue || isLast)
					return;
			}
		}

    }
}
