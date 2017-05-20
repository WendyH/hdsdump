using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using hdsdump.f4f;
using hdsdump.f4m;
using hdsdump.flv;

namespace hdsdump {
    public class HDSDumper: IDisposable {
        public string Status   = "";
        public string postData = "";
        public string baseUrl  = "";
        public string quality  = "high";
        public string auth     = "";
        public string lang     = "";
        public string alt      = "";
        public byte[] sessionKey;
        public uint duration      = 0;
        public uint filesize      = 0;
        public uint start         = 0;
        public uint fromTimestamp = 0;
        public bool testalt = false;

        public HDSDownloader Downloader = new HDSDownloader();
        public FLV           FLVFile    = new FLV();

        private DateTime startTime = DateTime.Now;

        private Manifest manifest;
        private Media    selectedMedia    = null;
        private Media    selectedMediaAlt = null;

        private static int    fixWindow   = 1000;
        private string avaliableBitrates  = " ";
        private string avaliableAlt       = " ";

        private const int MAX_LEVEL_NESTED_MANIFESTS = 5;

        // CONSTRUCTOR
        public HDSDumper() {
        }

        public void DetermineAudioVideoPresentInDownloadedFragment() {
            bool determinedVideoAudio = false;
            int  elapsed              = 0;
            int  timeoutDetermetine   = 10000;
            while (!determinedVideoAudio && (elapsed < timeoutDetermetine)) {
                Downloader.DetermineAudioVideo(selectedMedia, ref determinedVideoAudio, ref FLVFile.hasVideo, ref FLVFile.hasAudio);
                if (!determinedVideoAudio) {
                    Thread.Sleep(100);
                    elapsed += 100;
                }
            }
            if (!determinedVideoAudio) {
                FLVFile.hasAudio = true;
                FLVFile.hasVideo = true;
            }
            if (selectedMediaAlt != null) {
                FLVFile.hasAudio = true;
            }
        }

        private int _currentTime;
        private int _mediaTime;
        private int _alternateTime;
        private const int INVALID_TIME = -1;

        private int droppedAudioFrames;
        private int droppedVideoFrames;
        private int totalDroppedFrames = 0;

        AkamaiDecryptor AD1;
        AkamaiDecryptor AD2;

        public void StartDownload(string manifestUrl) {
            GetManifestAndSelectMedia(manifestUrl);
            // downloader already running in UpdateBootstrapInfo()
            DetermineAudioVideoPresentInDownloadedFragment();
            var DecoderState = new DecoderLastState();
            bool useAltAudio = selectedMediaAlt != null;
            TagsFilter tagFilter = TagsFilter.ALL;

            if (useAltAudio) {
                tagFilter = TagsFilter.VIDEO; // only video for main media if alternate media is selected
            }
            _currentTime   = INVALID_TIME;
            _mediaTime     = INVALID_TIME;
            _alternateTime = INVALID_TIME;
            droppedAudioFrames = 0;
            droppedVideoFrames = 0;

            FLVTag mediaTag     = null;
            FLVTag alternateTag = null;
            bool needSynchronizationAudio = true;
            // --------------- MAIN LOOP DECODE FRAGMENTS ----------------
            while (Downloader.TagsAvaliable(selectedMedia) || selectedMedia.Bootstrap.live) {
                if (mediaTag == null) {
                    mediaTag = Downloader.GetNextTag(selectedMedia);
                    if (mediaTag == null)
                        Thread.Sleep(100); // for decrease CPU load
                }

                if (useAltAudio && _mediaTime != INVALID_TIME && Downloader.TagsAvaliable(selectedMediaAlt)) {
                    if (alternateTag == null)
                        alternateTag = Downloader.GetNextTag(selectedMediaAlt);

                    if (useAltAudio && alternateTag != null && alternateTag.IsAkamaiEncrypted) {
                        if (AD2 == null) { // create and init only if need
                            AD2 = new AkamaiDecryptor();
                        }
                        AD2.DecryptFLVTag(alternateTag, manifest.baseURL, auth);
                    }

                    if (needSynchronizationAudio && (_mediaTime != INVALID_TIME || _alternateTime != INVALID_TIME)) {
                        int alternateSynchronizationTime = _alternateTime != INVALID_TIME ? _alternateTime : _mediaTime;
                        alternateTag = Downloader.SeekAudioByTime(selectedMediaAlt, alternateSynchronizationTime);
                        if (alternateTag != null) {
                            needSynchronizationAudio = false;
                        }
                    }
                }

                if (mediaTag != null && mediaTag.IsAkamaiEncrypted) {
                    if (AD1 == null) { // create and init only if need
                        AD1 = new AkamaiDecryptor();
                    }
                    AD1.DecryptFLVTag(mediaTag, manifest.baseURL, auth);
                }
                if (useAltAudio && alternateTag != null && alternateTag.IsAkamaiEncrypted) {
                    if (AD2 == null) { // create and init only if need
                        AD2 = new AkamaiDecryptor();
                    }
                    AD2.DecryptFLVTag(alternateTag, manifest.baseURL, auth);
                }

                if (mediaTag != null) {
                }

                if (ShouldFilterTag(mediaTag, tagFilter)) {
                    if (mediaTag != null) {
                        if (mediaTag is FLVTagVideo)
                            droppedVideoFrames++;
                        totalDroppedFrames++;
                    }
                    mediaTag = null;
                } else {
                    UpdateTimes(mediaTag);
                }

                if (ShouldFilterTag(alternateTag, TagsFilter.AUDIO)) {
                    if (alternateTag != null) {
                        droppedAudioFrames++;
                        totalDroppedFrames++;
                    }
                    alternateTag = null;
                } else {
                    UpdateTimes(alternateTag);
                }

                if (!useAltAudio) {
                    if (mediaTag != null) {
                        _currentTime = (int)mediaTag.Timestamp;
                        FLVFile.Write(mediaTag);
                        mediaTag = null;
                    }

                } else {
                    if (_mediaTime != INVALID_TIME || _alternateTime != INVALID_TIME) {
                        if (alternateTag != null && (alternateTag.Timestamp >= _currentTime) && (alternateTag.Timestamp <= _mediaTime)) {
                            _currentTime = (int)alternateTag.Timestamp;
                            FLVFile.Write(alternateTag);
                            alternateTag = null;

                        } else if (mediaTag != null && (mediaTag.Timestamp >= _currentTime) && (mediaTag.Timestamp <= _alternateTime)) {
                            _currentTime = (int)mediaTag.Timestamp;
                            FLVFile.Write(mediaTag);
                            mediaTag = null;
                        }
                   }

                }
                if ((duration > 0) && (FLVFile.LastTimestamp >= duration)) { Status = "Duration limit reached" ; break; }
                if ((filesize > 0) && (FLVFile.Filesize      >= filesize)) { Status = "File size limit reached"; break; }
            }
        }

        private void UpdateTimes(FLVTag tag) {
			if (tag != null) {
				if (tag is FLVTagAudio) {
					_alternateTime = (int)tag.Timestamp;
				} else {
					_mediaTime = (int)tag.Timestamp;
				}
			}
		}
        
        private void FixTime(FLVTag tag) {
            if (tag is FLVTagAudio) {

            } else {

            }
        }

        private bool ShouldFilterTag(FLVTag tag, TagsFilter filterTags) {
			if (tag == null) return true;
			
			// if the timestamp is lower than the current time
			if (tag.Timestamp < _currentTime) {
                tag.Timestamp = (uint)_currentTime;
                //return true;
			}
			
			switch (tag.Type) {
				case FLVTag.TagType.AUDIO:
				case FLVTag.TagType.AKAMAI_ENC_AUDIO:
					return (TagsFilter.AUDIO & filterTags) == 0 || (tag.Timestamp < _alternateTime);
				
				case FLVTag.TagType.VIDEO:
				case FLVTag.TagType.AKAMAI_ENC_VIDEO:
					return (TagsFilter.VIDEO & filterTags) == 0 || (tag.Timestamp < _mediaTime);

			}	
		
			return true;
		}

        private void ShowDownloadStatus(Media media) {
            string msg;
            if (media.Bootstrap.live && HDSDownloader.LiveIsStalled) {
                TimeSpan time = TimeSpan.FromTicks(DateTime.Now.Subtract(HDSDownloader.StartedStall).Ticks);
                msg = string.Format("          <c:Magenta>Live is stalled...</c> {0:00}:{1:00}:{2:00}   ", time.Hours, time.Minutes, time.Seconds);
                if (!Program.verbose)
                    msg += "\r";
                Program.Message(msg);
                return;
            }
            int fragsToDownload = (int)(media.TotalFragments - media.CurrentFragmentIndex + 1);
            string remaining = "";
            if (Program.showtime && !media.Bootstrap.live && media.Downloaded > 0 && fragsToDownload > 0) {
                TimeSpan remainingTimeSpan = TimeSpan.FromTicks(DateTime.Now.Subtract(startTime).Ticks / media.Downloaded * fragsToDownload);
                remaining = String.Format("<c:DarkCyan>Time remaining: </c>{0:00}<c:Cyan>:</c>{1:00}<c:Cyan>:</c>{2:00}", remainingTimeSpan.Hours, remainingTimeSpan.Minutes, remainingTimeSpan.Seconds);
            }
            uint fileDur = FLVFile.LastTimestamp / 1000;
            string fileTimestamp = string.Format("<c:DarkCyan>File duration: </c>{0:00}:{1:00}:{2:00} ", fileDur / 3600, (fileDur / 60) % 60, fileDur % 60);
            msg = String.Format("{0,-46} {1}{2}", "Downloaded <c:White>" + (media.CurrentFragmentIndex - 1) + "</c>/" + media.TotalFragments + " fragments", fileTimestamp, remaining);
            if (!Program.verbose)
                msg += "\r";
            Program.Message(msg);
        }

        private void GetManifestAndSelectMedia(string manifestUrl, int nestedBitrate = 0, int level = 0) {
            if (level > MAX_LEVEL_NESTED_MANIFESTS)
                throw new InvalidOperationException("Maximum nesting level reached of multi-level manifests.");

            XmlNodeEx xmlManifest = LoadXml(manifestUrl);

            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = URL.ExtractBaseUrl(manifestUrl);

            // parse the manifest
            manifest = new Manifest(xmlManifest, baseUrl, "", nestedBitrate);

            if (string.IsNullOrEmpty(manifest.baseURL))
                throw new InvalidOperationException("Not found <c:Magenta>baseURL</c> value in manifest or in parameter <c:White>--urlbase</c>.");

            if (manifest.media.Count < 1)
                throw new InvalidOperationException("No media entry found in the manifest");

            Program.DebugLog("Manifest entries:\n");
            Program.DebugLog(String.Format(" {0,-8}{1}", "Bitrate", "URL"));

            // TEST for alternate selection
            if (testalt)
                manifest.alternativeMedia.AddRange(manifest.media);

            // Quality selection
            selectedMedia = QualitySelectionForMedia(manifest.media, ref avaliableBitrates, level < 1);

            // Quality selection for alternative media
            selectedMediaAlt = QualitySelectionForMedia(manifest.alternativeMedia, ref avaliableAlt, level < 1, true);

            if (selectedMedia == null)
                selectedMedia = manifest.media[0];

            // check for multi-level manifest
            if (!string.IsNullOrEmpty(selectedMedia.href)) {
                string nestedManifestUrl = URL.getAbsoluteUrl(manifest.baseURL, selectedMedia.href);
                baseUrl = URL.ExtractBaseUrl(nestedManifestUrl);
                nestedBitrate = selectedMedia.bitrate;
                selectedMedia = null;
                GetManifestAndSelectMedia(nestedManifestUrl, nestedBitrate, level + 1);
                return;
            }

            string sQuality = selectedMedia.bitrate.ToString();
            int n = Math.Max(0, avaliableBitrates.IndexOf(sQuality));
            avaliableBitrates = avaliableBitrates.Replace(" " + sQuality + " ", " <c:Cyan>" + sQuality + "</c> ");
            Program.Message("Quality Selection:");
            Program.Message("Available:" + avaliableBitrates);
            Program.Message("Selected : <c:Cyan>" + sQuality.PadLeft(n + sQuality.Length - 1));
            if (manifest.alternativeMedia.Count > 0) {
                Program.Message("Alternatives:" + avaliableAlt);
                if (selectedMediaAlt != null) {
                    string label = selectedMediaAlt.label;
                    n = avaliableAlt.IndexOf(label);
                    avaliableAlt = avaliableAlt.Replace(" " + label + " ", " <c:Cyan>" + label + "</c> ");
                    Program.Message("Selected    : <c:Cyan>" + label.PadLeft(n + label.Length - 1));
                    // get bootstrap for media from manifest by id
                    if (!string.IsNullOrEmpty(selectedMediaAlt.bootstrapInfo?.id) && (selectedMediaAlt.bootstrapInfo.data == null)) {
                        selectedMediaAlt.bootstrapInfo = manifest.bootstrapInfos.Find(i => i.id == selectedMediaAlt.bootstrapInfo.id);
                    }
                }
            }

            // get bootstrap for media from manifest by id
            if (!string.IsNullOrEmpty(selectedMedia.bootstrapInfo?.id) && (selectedMedia.bootstrapInfo.data == null)) {
                selectedMedia.bootstrapInfo = manifest.bootstrapInfos.Find(i => i.id == selectedMedia.bootstrapInfo.id);
            }

            if (selectedMedia.bootstrapInfo == null)
                throw new InvalidOperationException("No bootstrapInfo for selected media entry");

            if (!Program.fproxy)
                HTTP.notUseProxy = true;

            // Use embedded auth information when available
            int idx = selectedMedia.url.IndexOf('?');
            if (idx > 0) {
                auth = selectedMedia.url.Substring(idx);
                selectedMedia.url = selectedMedia.url.Substring(0, idx);
            }

            if (selectedMedia.metadata != null) {
                FLVFile.onMetaData = new FLVTagScriptBody(selectedMedia.metadata);
            }

            selectedMedia.UpdatingBootstrap      += Media_UpdatingBootstrap;
            selectedMedia.UpdatingBootstrapRetry += Media_UpdatingBootstrapRetry;
            selectedMedia.AfterUpdateBootstrap   += Media_AfterUpdateBootstrap;
            selectedMedia.DownloadedChanged      += SelectedMedia_DownloadedChanged;

            selectedMedia.UpdateBootstrapInfo();

            if (selectedMedia.Bootstrap.live) {
                Program.Message("<c:Magenta>[Live stream]");
            }

            if (selectedMediaAlt == selectedMedia)
                selectedMediaAlt = null;

            if (selectedMediaAlt != null) {
                selectedMediaAlt.alternate = true;
                FLVFile.AltMedia           = true;
                // Use embedded auth information when available
                idx = selectedMediaAlt.url.IndexOf('?');
                if (idx > 0) {
                    auth = selectedMediaAlt.url.Substring(idx);
                    selectedMediaAlt.url = selectedMediaAlt.url.Substring(0, idx);
                }
                selectedMediaAlt.AfterUpdateBootstrap += Media_AfterUpdateBootstrap;

                selectedMediaAlt.UpdateBootstrapInfo();
            }
        }

        private Media QualitySelectionForMedia(List<Media> mediaList, ref string avaliable, bool collectBitrates, bool isAlt = false) {
            if (collectBitrates)
                avaliable = " ";
            Media selected = null;
            // sorting media by quality
            mediaList.Sort((a, b) => b.bitrate.CompareTo(a.bitrate));
            if (mediaList.Count > 0) {
                foreach (Media media in mediaList) {
                    if (isAlt) {
                        if (collectBitrates)
                            avaliable += media.label + " ";
                        Program.DebugLog(String.Format(" {0,-8}{1}", media.label, media.url));
                        //selected by language, codec, bitrate or label
                        if (!string.IsNullOrEmpty(lang) && media.lang.ToLower() == lang.ToLower()) {
                            selected = media;
                        } else if (!string.IsNullOrEmpty(alt)) {
                            string altLow = alt.ToLower();
                            if (altLow == media.label.ToLower() || altLow == media.lang.ToLower() || altLow == media.bitrate.ToString() || altLow == media.streamId.ToLower()) {
                                selected = media;
                            } else if (media.audioCodec.ToLower().IndexOf(altLow) >= 0) {
                                selected = media;
                            } else if (media.videoCodec.ToLower().IndexOf(altLow) >= 0) {
                                selected = media;
                            }
                        }
                    } else {
                        if (collectBitrates)
                            avaliable += media.bitrate + " ";
                        Program.DebugLog(String.Format(" {0,-8}{1}", media.bitrate, media.url));
                        if (media.bitrate.ToString() == quality) {
                            selected = media;
                        }
                    }
                }
                if (selected == null && !isAlt) {
                    if (int.TryParse(quality, out int iQuality)) {
                        // search nearest bitrate
                        while (iQuality >= 0) {
                            selected = mediaList.Find(i => i.bitrate == iQuality);
                            if (selected != null)
                                break;
                            iQuality--;
                        }
                    } else {
                        switch (quality.ToLower()) {
                            case "low"   : selected = mediaList[mediaList.Count - 1]; break;
                            case "medium": selected = mediaList[mediaList.Count / 2]; break;
                            default      : selected = mediaList[0]; break; // first
                        }
                    }
                }
            }
            return selected;
        }

        #region Events
        private void SelectedMedia_DownloadedChanged(object sender, EventArgs e) {
            ShowDownloadStatus(sender as Media);
        }

        private void Media_UpdatingBootstrapRetry(object sender, UpdatingBootstrapEventArgs args) {
            Program.Message(string.Format("{0,-26}\r", "<c:DarkCyan>Update: " + args.Retries+"</c>"));
        }

        private void Media_AfterUpdateBootstrap(object sender, EventArgs e) {
            Media media = sender as Media;
            if (media == null) return;

            if (fromTimestamp > 0) {
                media.SetCurrentFragmentIndexByTimestamp(fromTimestamp);
            }

            if (Program.debug) {
                Program.DebugLog("Segment Tables:");
                for (int i = 0; i < media.Bootstrap.segmentRunTables.Count; i++) {
                    Program.DebugLog(String.Format("  Table {0} (Count={1})", i + 1, media.Bootstrap.segmentRunTables[i].segmentFragmentPairs.Count));
                    Program.DebugLog("       FirstSegment FragPerSegment FragmentsAccrued");
                    foreach (var p in media.Bootstrap.segmentRunTables[i].segmentFragmentPairs) {
                        Program.DebugLog(String.Format("       {0,-12} {1,-14} {2,-16}", p.firstSegment, p.fragmentsPerSegment, p.fragmentsAccrued));
                    }
                }
                Program.DebugLog("Fragment Tables:");
                for (int i = 0; i < media.Bootstrap.fragmentRunTables.Count; i++) {
                    Program.DebugLog(String.Format("  Table {0} (Count={1})", i + 1, media.Bootstrap.fragmentRunTables[i].fragmentDurationPairs.Count));
                    Program.DebugLog("       FirstFragment Duration DurationAccrued DiscontinuityIndicator");
                    foreach (var p in media.Bootstrap.fragmentRunTables[i].fragmentDurationPairs) {
                        Program.DebugLog(String.Format("       {0,-13} {1,-8} {2,-15} {3,-22}", p.firstFragment, p.duration, p.durationAccrued, p.discontinuityIndicator));
                    }
                }
                Program.DebugLog("Start fragment : " + media.CurrentFragmentIndex);
                Program.DebugLog("Total fragments: " + media.TotalFragments + "\n");
            }

            // Add all fragments to download queue
            for (uint fragIndex = media.CurrentFragmentIndex; fragIndex <= media.TotalFragments; fragIndex++) {
                FragmentDurationPair fragmentInfo = media.Bootstrap.GetFragmentInfo(fragIndex);
                if (fragmentInfo == null)
                    throw new InvalidOperationException("No info for fragment " + fragIndex);

                if (fragmentInfo.discontinuityIndicator != 0) {
                    Program.DebugLog("Skipping fragment " + fragIndex + " due to discontinuity, Type: " + fragmentInfo.discontinuityIndicator);
                    continue;
                }

                Downloader.AddMediaFragmentToDownload(media, fragIndex);
            }
        }

        private void Media_UpdatingBootstrap(object sender, EventArgs e) {
            Media media = sender as Media;
            Program.DebugLog("\nUpdating bootstrap info... Available fragments: " + media.TotalFragments);
        }

        #endregion Events

        public XmlNodeEx LoadXml(string manifestUrl) {
            string xmlText = "";

            if (string.IsNullOrEmpty(postData))
                xmlText = HTTP.GET(manifestUrl);
            else
                xmlText = HTTP.POST(manifestUrl, postData);

            if (Program.RegExMatch(@"<r>\s*?<to>(.*?)</to>", xmlText, out string sDomain)) {
                if (Program.RegExMatch(@"^.*?://.*?/.*?/(.*)", manifestUrl, out manifestUrl)) {
                    manifestUrl = sDomain + manifestUrl;
                    xmlText = HTTP.GET(manifestUrl);
                }
            }

            xmlText = XmlValidate(xmlText);

            XmlDocumentEx xmldoc = new XmlDocumentEx();
            try {
                xmldoc.LoadXml(xmlText);
            } catch (Exception e) {
                if (Regex.IsMatch(xmlText, @"<html.*?<body", RegexOptions.Singleline)) {
                    throw new XmlException("Error loading manifest. Url redirected to html page. Check the manifest url.", e);
                } else {
                    throw new XmlException("Error loading manifest. It's no valid xml file.", e);
                }
            }
            return (XmlNodeEx)xmldoc.DocumentElement;
        }

        public string XmlValidate(string xml) {
            string xmlText = Regex.Replace(xml, "&(?!amp;)", "&amp;").Trim(new char[] { '\uFEFF', '\u200B' });
            return xmlText;
        }

        public static void FixTimestamp(DecoderLastState DecoderState, FLVTag tag) {
            int lastTS  = DecoderState.prevVideoTS >= DecoderState.prevAudioTS ? DecoderState.prevVideoTS : DecoderState.prevAudioTS;
            int fixedTS = lastTS + fixWindow;

            if ((DecoderState.baseTS == DecoderLastState.INVALID_TIMESTAMP) && ((tag.Type == FLVTag.TagType.AUDIO) || (tag.Type == FLVTag.TagType.VIDEO)))
                DecoderState.baseTS = (int)tag.Timestamp;

            if ((DecoderState.baseTS > fixWindow) && (tag.Timestamp >= DecoderState.baseTS))
                tag.Timestamp -= (uint)DecoderState.baseTS;

            if (lastTS != DecoderLastState.INVALID_TIMESTAMP) {

                int timeShift = (int)tag.Timestamp - lastTS;
                if (timeShift > fixWindow) {
                    Program.DebugLog(string.Format("Timestamp gap detected: PacketTS={0} LastTS={1} Timeshift={2}", tag.Timestamp, lastTS, timeShift));
                    if (DecoderState.baseTS < tag.Timestamp)
                        DecoderState.baseTS += timeShift - fixWindow;
                    else
                        DecoderState.baseTS = timeShift - fixWindow;
                    tag.Timestamp = (uint)fixedTS;
                } else {
                    lastTS = (int)(tag.Type == FLVTag.TagType.VIDEO ? DecoderState.prevVideoTS : DecoderState.prevAudioTS);
                    if ((lastTS != DecoderLastState.INVALID_TIMESTAMP) && (int)tag.Timestamp < (lastTS - fixWindow)) {
                        if ((DecoderState.negTS != DecoderLastState.INVALID_TIMESTAMP) && ((tag.Timestamp + DecoderState.negTS) < (lastTS - fixWindow)))
                            DecoderState.negTS = DecoderLastState.INVALID_TIMESTAMP;
                        if (DecoderState.negTS == DecoderLastState.INVALID_TIMESTAMP) {
                            DecoderState.negTS = fixedTS - (int)tag.Timestamp;
                            Program.DebugLog(string.Format("Negative timestamp detected: PacketTS={0} LastTS={1} NegativeTS={2}", tag.Timestamp, lastTS, DecoderState.negTS));
                            tag.Timestamp = (uint)fixedTS;
                        } else {
                            if ((tag.Timestamp + DecoderState.negTS) <= (lastTS + fixWindow))
                                tag.Timestamp += (uint)DecoderState.negTS;
                            else {
                                DecoderState.negTS = fixedTS - (int)tag.Timestamp;
                                Program.DebugLog(string.Format("Negative timestamp override: PacketTS={0} LastTS={1} NegativeTS={2}", tag.Timestamp, lastTS, DecoderState.negTS));
                                tag.Timestamp = (uint)fixedTS;
                            }
                        }
                    }
                }
            }
            if (tag is FLVTagAudio)
                DecoderState.prevAudioTS = (int)tag.Timestamp;
            else
                DecoderState.prevVideoTS = (int)tag.Timestamp;
        }

        public void FixFileMetadata() {
            FLVFile.FixFileMetadata();
        }

        #region IDisposable Support
        private bool disposedValue = false; // Для определения избыточных вызовов

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    FLVFile.Dispose();
                    if (AD1 != null)
                        AD1.Dispose();
                    if (AD2 != null)
                        AD2.Dispose();
                }
                disposedValue = true;
                AD1 = null;
                AD2 = null;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }

    public enum TagsFilter: int {
        NONE  = 0,
        VIDEO = 1,
        AUDIO = 2,
        ALL   = 3
    }
}
