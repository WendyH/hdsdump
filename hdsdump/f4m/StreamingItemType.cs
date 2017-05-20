/*****************************************************
 *  
 *  Copyright 2011 Adobe Systems Incorporated.  All Rights Reserved.
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
 *  Portions created by Adobe Systems Incorporated are Copyright (C) 2011 Adobe Systems 
 *  Incorporated. All Rights Reserved. 
 *  
 *****************************************************/
namespace hdsdump.f4m {
    /// <summary>
    /// The StreamingItemType class is an enumeration of constant values that you can
    /// use to set the type property of the StreamingItem class. 
    /// </summary>
    public static class StreamingItemType {
        /// <summary>
        /// The <code>VIDEO</code> stream type represents a video only or a video-audio stream.
        /// </summary>
        public static string VIDEO = "video";

        /// <summary>
        /// The <code>AUDIO</code> stream type represents an audio-only stream.
        /// </summary>
        public static string AUDIO = "audio";
    }
}
