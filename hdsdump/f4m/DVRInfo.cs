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
namespace hdsdump.f4m {
    public class DVRInfo {
        /// <summary>
        /// The ID of this &lt;dvrInfo&gt; element.It is optional.If it is not specified,
        /// then none of the media elements is of DVR content.If it is specified, it is applicable to all
        /// the media elements are DVR contents.
        /// </summary>
        public string id;

        /// <summary>
        /// The url that points to a remote location at which the &lt;dvrInfo&gt; element is available 
        /// for download
        /// </summary>
        public string url;

        /// <summary>
        /// The offset, in seconds, from the beginning of the recorded stream. Client can begin viewing
        /// the stream at this location. It is optional, and defaults to zero.
        /// </summary>
        public int beginOffset = 0;

        /// <summary>
        /// The amoutn of data, in seconds, that client can begin viewing
        /// from the current media time. It is optional, and defaults to zero.
        /// </summary>
        public int endOffset = 0;

        /// <summary>
        /// The window length on the server, in seconds: represents the maximum 
        /// length of the content.
        /// </summary>
        public int windowDuration = -1;

        /// <summary>
        /// Indicates whether the stream is offline, or available for playback. It is optional, and defaults to false. 
        /// </summary>
        public bool offline;

        /// <summary>
        /// Indicates whether the stream is recording. 
        /// </summary>
        public bool isRecording;

        /// <summary>
        /// Indicates the current total length of the content. 
        /// </summary>
        public uint curLength;

        /// <summary>
        /// Indicates the starting position when the DVR content is loaded and about to play. 
        /// </summary>
        public uint startTime;

        // Constructor
        public DVRInfo(XmlNodeEx node, string baseURL = "", string idPrefix = "") {
            Parse(node, baseURL, idPrefix);
        }

        public void Parse(XmlNodeEx node, string baseURL = "", string idPrefix = "") {

            id  = node.GetAttributeStr("id", F4MUtils.GLOBAL_ELEMENT_ID);
            url = node.GetAttributeStr("url");
            url = URL.getAbsoluteUrl(baseURL, url);

            int majorVersion = F4MUtils.getVersion(node).Major;
            if (majorVersion <= 1) {
                beginOffset = System.Math.Max(0, node.GetAttributeInt("beginOffset"));
                endOffset   = System.Math.Max(0, node.GetAttributeInt("endOffset"  ));
                windowDuration = -1;
            } else { // F4M 2.0
                windowDuration = node.GetAttributeInt("windowDuration");
                if (windowDuration == 0)
                    windowDuration = -1;
            }

            offline = node.GetAttributeBoolean("offline");
        }

    }
}
