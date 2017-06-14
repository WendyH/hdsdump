using System;
using System.Threading;

namespace hdsdump.f4m {

    /// <summary>
    /// Describes a specific piece of media.
    /// </summary>
    public class Media: IDisposable {
        /// <summary>
        /// Location of the media.
        /// </summary>
        public string url;

        /// <summary>
        /// The bitrate of the media in kilobits per second.
        /// </summary>
        public int bitrate;

        /// <summary>
        /// The type of the media.
        /// </summary>
        public string type;

        /// <summary>
        /// The label of the media.
        /// </summary>
        public string label;

        /// <summary>
        /// The language of the media.
        /// </summary>
        public string lang;

        /// <summary>
        /// Flag indicating that this is an alternate media.
        /// </summary>
        public bool alternate;

        public string audioCodec;
        public string videoCodec;
        public string cueInfoId;
        public string href;

        /// <summary>
        /// Information about the AdditionalHeader used with the media. AdditionalHeader contains DRM metadata.
        /// </summary>
        public DRMAdditionalHeader drmAdditionalHeader;

        /// <summary>
        /// Represents all information needed to bootstrap playback of 
        /// HTTP streamed media. It contains either a byte array
        /// of, or a URL to, the bootstrap information in the format that corresponds 
        /// to the bootstrap profile. It is optional.
        /// </summary>
        public BootstrapInfo bootstrapInfo;

        /// <summary>
        /// The stream metadata in its binary representation.
        /// </summary>
        public byte[] metadata;

        /// <summary>
        /// The XMP metadata in its binary representation.
        /// </summary>
        public byte[] xmp;

        /// <summary>
        /// Represents the Movie Box, or "moov" atom, for one representation of 
        /// the piece of media. It is an optional child element of &lt;media&gt;.
        /// </summary>
        public byte[] moov;

        /// <summary>
        /// Width of the resource in pixels.
        /// </summary>
        public int width;

        /// <summary>
        /// Height of the resource in pixels.
        /// </summary>
        public int height;

        /// <summary>
        /// Store multicast group spec string
        /// </summary>
        public string multicastGroupspec;

        /// <summary>
        /// Store multicast stream name
        /// </summary>
        public string multicastStreamName;

        public string baseURL  = "";
        public string streamId = "";

        // Constructor
        public Media(XmlNodeEx node, string serverBaseURL = "", string idPrefix = "", int nestedBitrate = 0) {
            Parse(node, serverBaseURL, idPrefix, nestedBitrate);
        }

        /// <summary>
        /// Parses media from XML node.
        /// </summary>
        public void Parse(XmlNodeEx node, string serverBaseURL = "", string idPrefix = "", int nestedBitrate = 0) {
            baseURL = serverBaseURL;

            url = node.GetAttributeStr("url");
            url = URL.getAbsoluteUrl(baseURL, url);

            streamId = node.GetAttributeStr("streamId");
            bitrate  = node.GetAttributeInt("bitrate");
            if (bitrate == 0)
                bitrate = nestedBitrate;
            if (bitrate == 0) {
                XmlNodeEx parentManifest = (XmlNodeEx)node.ParentNode;
                bitrate = parentManifest.GetAttributeInt("bitrate");
            }
            if (bitrate == 0) {
                bitrate = node.GetAttributeInt("width");
            }
            if (bitrate == 0) {
                if (!string.IsNullOrEmpty(streamId)) {
                    var m = System.Text.RegularExpressions.Regex.Match(streamId, @"(\d+)", System.Text.RegularExpressions.RegexOptions.RightToLeft);
                    if (m.Success)
                        int.TryParse(m.Groups[1].Value, out bitrate);
                    else {
                        // by index
                        int idx = 0;
                        foreach (XmlNodeEx n in node.ParentNode.ChildNodes) {
                            if (n.Name != node.Name) continue;
                            if (n == node) {
                                bitrate = idx;
                                break;
                            }
                            idx++;
                        }
                    }
                }
            }

            drmAdditionalHeader = new DRMAdditionalHeader() {
                id = idPrefix + node.GetAttributeStr("drmAdditionalHeaderId", F4MUtils.GLOBAL_ELEMENT_ID)
            };
            bootstrapInfo = new BootstrapInfo() {
                id = idPrefix + node.GetAttributeStr("bootstrapInfoId", F4MUtils.GLOBAL_ELEMENT_ID)
            };
            height = node.GetAttributeInt("height");
            width  = node.GetAttributeInt("width" );

            multicastGroupspec  = node.GetAttributeStr("groupspec");
            multicastStreamName = node.GetAttributeStr("multicastStreamName");
            label      = node.GetAttributeStr("label");
            type       = node.GetAttributeStr("type", StreamingItemType.VIDEO);
            lang       = node.GetAttributeStr("lang");
            audioCodec = node.GetAttributeStr("audioCodec");
            videoCodec = node.GetAttributeStr("videoCodec");
            cueInfoId  = node.GetAttributeStr("cueInfoId");
            href       = node.GetAttributeStr("href");
            alternate  = (node.GetAttributeStr("alternate").Length > 0);

            moov     = node.GetData("moov"    );
            metadata = node.GetData("metadata");
            if ((metadata!=null) && metadata.Length > 0) {
                // if width and height are not already set by the media
                // attributes and they are already present in metadata 
                // object, then copy their values to the media properties
                if (width == 0) {
                    width = node.GetChildNodeAttributeInt("metadata", "width");
                }
                if (height == 0) {
                    height = node.GetChildNodeAttributeInt("metadata", "height");
                }
            }

            xmp = node.GetData("xmpMetadata");

            if (string.IsNullOrEmpty(label)) {
                if (!string.IsNullOrEmpty(lang))
                    label = lang;
                else
                    label = string.IsNullOrEmpty(streamId) ? bitrate.ToString() : streamId;
            }
        }

        private uint _downloaded = 0;

        public bool Updating = false;
        public uint CurrentFragmentIndex = 0;
        public uint LastWritenFragment   = 0;
        public uint TotalFragments       = 0;
        public uint Downloaded {
            get { return _downloaded; }
            set {
                if (value != _downloaded) {
                    _downloaded = value;
                    DownloadedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public f4f.AdobeBootstrapBox Bootstrap;

        // Events
        public event EventHandler AfterUpdateBootstrap;
        public event EventHandler DownloadedChanged;

        private int bootstrapUpdateInterval = 2000; // 4.0 sec - recommend default fragment duration
        private int hdsMinimumBootstrapRefreshInterval = 1000;

        Timer bootstrapUpdateTimer;

        private void StartBootstrapUpdateTimer() {

            if (Bootstrap != null) {
                // for slow internet - disable sets interval
                //if (Bootstrap.fragmentRunTables.Count > 0) {
                //    var lastFrag = Bootstrap.GetLastFragment();
                //    bootstrapUpdateInterval = (int)(lastFrag.duration / Bootstrap.timeScale * 1000);
                //}

                if (bootstrapUpdateInterval < hdsMinimumBootstrapRefreshInterval) {
                    bootstrapUpdateInterval = hdsMinimumBootstrapRefreshInterval;
                }
            }

            if (bootstrapUpdateTimer == null) {
                bootstrapUpdateTimer = new Timer(OnBootstrapUpdateTimer, null, 0, bootstrapUpdateInterval);
            }
        }

        private void DestroyBootstrapUpdateTimer() {
            if (bootstrapUpdateTimer != null) {
                bootstrapUpdateTimer.Dispose();
                bootstrapUpdateTimer = null;
            }
        }

        private void OnBootstrapUpdateTimer(object state) {
            bootstrapInfo.data = HTTP.TryGETData(bootstrapInfo.url, out int retCode, out string status);

            if (retCode != 200) {
                Program.DebugLog("Error while loading UpdateBootstrapBox. Code: " + retCode + " Status: " + status);
            }

            UpfateBootstrapBox();
        }

        private void UpfateBootstrapBox() {
            if (f4f.Box.FindBox(bootstrapInfo.data, f4f.F4FConstants.BOX_TYPE_ABST) is f4f.AdobeBootstrapBox abst) {
                Bootstrap = abst;

                // Count total fragments by adding all entries in compactly coded segment table
                TotalFragments = Bootstrap.GetFragmentsCount();

                if (CurrentFragmentIndex < 1) {
                    CurrentFragmentIndex = Bootstrap.live ? (TotalFragments - 1) : Bootstrap.GetFirstFragment().firstFragment;
                }
            }

            if (CurrentFragmentIndex < 1)
                CurrentFragmentIndex = 1;

            if (CurrentFragmentIndex < TotalFragments)
                Updating = false;

            AfterUpdateBootstrap?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateBootstrapInfo() {
            bool itWasLive = Bootstrap != null ? Bootstrap.live : false;

            if (!string.IsNullOrEmpty(bootstrapInfo.url)) {
                Updating = true;
                bootstrapInfo.data = HTTP.TryGETData(bootstrapInfo.url, out int retCode, out string status);

                if (retCode != 200) {
                    Program.DebugLog("Error while loading UpdateBootstrapBox. Code: " + retCode + " Status: " + status);
                }
            }

            UpfateBootstrapBox();

            if (!itWasLive && Bootstrap == null)
                throw new InvalidOperationException("Failed to parse bootstrap info. Not found the abst box.");

            if (Bootstrap != null && Bootstrap.live)
                StartBootstrapUpdateTimer();
        }

        public string GetFragmentUrl(uint fragId) {
            uint segId = Bootstrap.GetSegmentFromFragment(fragId);
            return ConstructFragmentRequest(baseURL, url, segId, fragId);
        }

        private string ConstructFragmentRequest(string serverBaseURL, string streamName, uint segmentId, uint fragmentId) {
            string requestUrl = "";
            if (streamName != null && streamName.IndexOf("http") != 0) {
                requestUrl = serverBaseURL + "/";
            }
            requestUrl += streamName;
            Uri uri = new Uri(requestUrl);
            requestUrl = uri.Scheme + "://" + uri.Host;
            if (((uri.Scheme == "http") && (uri.Port != 80)) || ((uri.Scheme == "https") && (uri.Port != 443))) {
                requestUrl += ":" + uri.Port;
            }
            requestUrl += uri.LocalPath + "Seg" + segmentId + "-Frag" + fragmentId;
            if (!string.IsNullOrEmpty(uri.Query)) {
                requestUrl += "?" + uri.Query;
            }
            if (!string.IsNullOrEmpty(uri.Fragment)) {
                requestUrl += "#" + uri.Fragment;
            }
            return requestUrl;
        }

        public void SetCurrentFragmentIndexByTimestamp(uint timestamp) {
            if (Bootstrap != null && !Bootstrap.live) {
                CurrentFragmentIndex = Bootstrap.GetSegmentByTimestamp(timestamp);
            }
        }

        public override string ToString() {
            return "Media: " + label.Trim();
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (bootstrapUpdateTimer != null)
                        bootstrapUpdateTimer.Dispose();
                }
                bootstrapUpdateTimer = null;
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion

    }
}
