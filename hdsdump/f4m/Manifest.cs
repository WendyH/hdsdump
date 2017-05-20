/*****************************************************
*  
*  Copyright 2009 Adobe Systems Incorporated.  All Rights Reserved.
*  
*****************************************************
*  The contents of this file are subject to the Mozilla Public License
*  Version 1.1 (the "License"); you may not use this file except in
*  compliance with the License. You may obtain a copy of the License at
*  http://www.mozilla.org/MPL/
*   
*  Software distributed under the License is distributed on an "AS IS"
*  basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
*  License for the specific language governing rights and limitations
*  under the License.
*   
*  
*  The Initial Developer of the Original Code is Adobe Systems Incorporated.
*  Portions created by Adobe Systems Incorporated are Copyright (C) 2009 Adobe Systems 
*  Incorporated. All Rights Reserved. 
*  
*****************************************************/
using System;
using System.Collections.Generic;
using System.Xml;

namespace hdsdump.f4m {
    public class Manifest {
        /// <summary>
        /// The id element represents a unique identifier for the media. It is optional.
        /// </summary>
        public string id;

        /// <summary>
        /// The label element represents a user-friendly description for the media. It is optional.
        /// </summary>
        public string label;

        /// <summary>
		/// The lang element represents a language code identifier for the media. It is optional.
        /// </summary>
		public string lang;

        /// <summary>
		/// The &lt;baseURL&gt; element contains the base URL for all relative (HTTP-based) URLs 
		/// in the manifest. It is optional. When specified, its value is prepended to all 
		/// relative URLs (i.e. those URLs that don't begin with "http://" or "https://" 
		/// within the manifest file. (Such URLs may include &lt;media&gt; URLs, &lt;bootstrapInfo&gt; 
		/// URLs, and &lt;drmMetadata&gt; URLs.) 
        /// </summary>
		public string baseURL;

        /// <summary>
        /// Indicate whether the media URL includes FMS application instance. This is only applicable to RTMP URLs.
        /// </summary>
        public bool urlIncludesFMSApplicationInstance = false;

        /// <summary>
		/// The &lt;duration&gt; element represents the duration of the media, in seconds. 
		/// It is assumed that all representations of the media have the same duration, 
		/// hence its placement under the document root. It is optional.
        /// </summary>
		public float duration;

        /// <summary>
        /// The &lt;mimeType&gt; element represents the MIME type of the media file. It is assumed 
        /// that all representations of the media have the same MIME type, hence its 
        /// placement under the document root. It is optional.
        /// </summary>
        public string mimeType;

        /// <summary>
        /// The &lt;streamType&gt; element is a string representing the way in which the media is streamed.
        /// Valid values include "live", "recorded", and "liveOrRecorded". It is assumed that all representations 
        /// of the media have the same stream type, hence its placement under the document root. 
        /// It is optional.
        /// </summary>
        public string streamType;

        /// <summary>
        /// Indicates the means by which content is delivered to the player.  Valid values include 
        /// "streaming" and "progressive". It is optional. If unspecified, then the delivery 
        /// type is inferred from the media protocol. For media with an RTMP protocol, 
        /// the default deliveryType is "streaming". For media with an HTTP protocol, the default 
        /// deliveryType is also "streaming". In the latter case, the &lt;bootstrapInfo&gt; field must be 
        /// present.
        /// </summary>
        public string deliveryType;

        /// <summary>
        /// Represents the date/time at which the media was first (or will first be) made available. 
        /// It is assumed that all representations of the media have the same start time, hence its 
        /// placement under the document root. The start time must conform to the "date-time" production 
        /// in RFC3339. It is optional.
        /// </summary>
        public DateTime startTime;

        /// <summary>
        /// The set of different bootstrap information objects associated with this manifest.
        /// </summary>
        public List<BootstrapInfo> bootstrapInfos = new List<BootstrapInfo>();

        /// <summary>
        /// The set of different |AddionalHeader objects associated with this manifest.
        /// </summary>
        public List<DRMAdditionalHeader> drmAdditionalHeaders = new List<DRMAdditionalHeader>();

        /// <summary>
		/// The set of different bitrate streams associated with this media.
        /// </summary>
		public List<Media> media = new List<Media>();

        /// <summary>
        /// The set of alternative streams associated with this media.
        /// </summary>
        public List<Media> alternativeMedia = new List<Media>();
		
		/// <summary>
        /// The dvrInfo element. It is needed to play DVR media.
        /// </summary>
		public DVRInfo dvrInfo = null;
		
		public BestEffortFetchInfo bestEffortFetchInfo = null;

        public List<CueInfo> cueInfos = new List<CueInfo>();

        // CONSTRUCTOR
        public Manifest(XmlNodeEx nodeManifest, string rootURL = "", string idPrefix = "", int nestedBitrate = 0) {
            Parse(nodeManifest, rootURL, idPrefix, nestedBitrate);
        }

        #region Parser
        public void Parse(XmlNodeEx nodeManifest, string rootURL = "", string idPrefix = "", int nestedBitrate = 0) {
            id           = nodeManifest.GetText("id");
            label        = nodeManifest.GetText("label");
            lang         = nodeManifest.GetText("lang");
            duration     = nodeManifest.GetFloat("duration");
            startTime    = nodeManifest.GetDateTime("startTime");
            mimeType     = nodeManifest.GetText("mimeType");
            streamType   = nodeManifest.GetText("streamType");
            deliveryType = nodeManifest.GetText("deliveryType");
            baseURL      = nodeManifest.GetText("baseURL");
            urlIncludesFMSApplicationInstance = nodeManifest.GetAttributeBoolean("urlIncludesFMSApplicationInstance");
            if (string.IsNullOrEmpty(baseURL)) {
                baseURL = rootURL;
            }
            baseURL = URL.normalizePathForURL(baseURL, false);

            XmlNodeEx nodeDVR = nodeManifest.GetChildNode("dvrInfo") as XmlNodeEx;
            dvrInfo = (nodeDVR != null) ? new DVRInfo(nodeDVR) : null;

            // cueInfo
            cueInfos.Clear();
            List<XmlNodeEx> cueInfoNodes = nodeManifest.GetChildNodesByName("cueInfo");
            foreach (XmlNodeEx nodeCueInfo in cueInfoNodes) {
                string  cueInfoId = nodeCueInfo.GetAttributeStr("id", F4MUtils.GLOBAL_ELEMENT_ID);
                CueInfo cueInfo   = new CueInfo(cueInfoId);
                foreach (XmlNodeEx node in nodeCueInfo.GetChildNodesByName("cue")) {
                    cueInfo.Cues.Add(new Cue(node, baseURL, idPrefix));
                }
                cueInfos.Add(cueInfo);
            }

            media               .Clear();
            alternativeMedia    .Clear();
            drmAdditionalHeaders.Clear();
            bootstrapInfos      .Clear();

            foreach (XmlNode childNode in nodeManifest.ChildNodes) {
                XmlNodeEx childNodeEx = childNode as XmlNodeEx;
                if (childNodeEx == null) continue;
                switch (childNodeEx.Name) {
                    case "media":
                        Media mediaItem = new Media(childNodeEx, baseURL, idPrefix, nestedBitrate);
                        if (mediaItem.bitrate == 0) {
                            mediaItem.bitrate = nodeManifest.GetAttributeInt("bitrate");
                        }
                        if (mediaItem.alternate)
                            alternativeMedia.Add(mediaItem);
                        else
                            media.Add(mediaItem);
                        break;

                    case "drmAdditionalHeader":
                        drmAdditionalHeaders.Add(new DRMAdditionalHeader(childNodeEx, baseURL, idPrefix));
                        break;

                    case "bootstrapInfo":
                        bootstrapInfos.Add(new BootstrapInfo(childNodeEx, baseURL, idPrefix));
                        break;
                }
            }

            XmlNodeEx nodeBEF = nodeManifest.GetChildNode("bestEffortFetchInfo") as XmlNodeEx;
            bestEffortFetchInfo = (nodeBEF != null) ? new BestEffortFetchInfo(nodeBEF) : null;

            // Adaptive sets search
            List<XmlNodeEx> adaptiveSet = nodeManifest.GetChildNodesByName("adaptiveSet");
            foreach(XmlNodeEx nodeSet in adaptiveSet) {
                string alternate  = nodeSet.GetAttributeStr("alternate");
                string audioCodec = nodeSet.GetAttributeStr("audioCodec");
                string label      = nodeSet.GetAttributeStr("label");
                string lang       = nodeSet.GetAttributeStr("lang");
                string type       = nodeSet.GetAttributeStr("type");
                List<XmlNodeEx> mediaInSet = nodeSet.GetChildNodesByName("media");
                foreach (XmlNodeEx nodeMedia in mediaInSet) {
                    Media mediaItem = new Media(nodeMedia, baseURL, idPrefix, nestedBitrate);
                    if (mediaItem.bitrate == 0)
                        mediaItem.bitrate = nodeManifest.GetAttributeInt("bitrate");
                    if (!string.IsNullOrEmpty(alternate))
                        mediaItem.alternate = true;
                    if (!string.IsNullOrEmpty(audioCodec) && string.IsNullOrEmpty(mediaItem.audioCodec))
                        mediaItem.audioCodec = audioCodec;
                    if (!string.IsNullOrEmpty(label) && string.IsNullOrEmpty(mediaItem.label))
                        mediaItem.label = label;
                    if (!string.IsNullOrEmpty(lang) && string.IsNullOrEmpty(mediaItem.lang))
                        mediaItem.lang = lang;
                    if (!string.IsNullOrEmpty(type) && string.IsNullOrEmpty(mediaItem.type))
                        mediaItem.type = type;

                    if (mediaItem.alternate)
                        alternativeMedia.Add(mediaItem);
                    else
                        media.Add(mediaItem);
                }
            }

            GenerateRTMPBaseURL();
        }

        /// <summary>
        /// Ensures that an RTMP based Manifest has the same server for all
        /// streaming items, and extracts the base URL from the streaming items
        /// if not specified.
        /// </summary>
        private void GenerateRTMPBaseURL() {
			if (string.IsNullOrEmpty(baseURL)) {
                foreach (var mediaItem in media) {
                    if (NetStreamUtils.isRTMPStream(mediaItem.url)) {
                        baseURL = mediaItem.url;
                        break;
                    }
                }
            }
        }

        #endregion Parser

    }
}
