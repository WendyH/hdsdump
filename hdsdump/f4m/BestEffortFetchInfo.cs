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
    /// <summary>
    /// Configuration parameters for BestEffortFetch that are embedded in the f4m.
    /// </summary>
    public class BestEffortFetchInfo {
        public static uint DEFAULT_MAX_FORWARD_FETCHES  = 2;
        
        public static uint DEFAULT_MAX_BACKWARD_FETCHES = 2;

        public uint maxForwardFetches  = DEFAULT_MAX_FORWARD_FETCHES;
        
        public uint maxBackwardFetches = DEFAULT_MAX_BACKWARD_FETCHES;

        /// <summary>
        /// The typical duration of a segment (in milliseconds)
        /// </summary>
        public uint segmentDuration = 0;
        
        /// <summary>
        /// The typical duration of a fragment (in milliseconds)
        /// </summary>
        public uint fragmentDuration = 0;

        // CONSTRUCTOR
        public BestEffortFetchInfo(XmlNodeEx node, string baseURL = "", string idPrefix = "") {
            Parse(node, baseURL, idPrefix);
        }

        public void Parse(XmlNodeEx node, string baseURL = "", string idPrefix = "") {
            segmentDuration    = (uint)(node.GetAttributeFloat("segmentDuration" ) * 1000);
            fragmentDuration   = (uint)(node.GetAttributeFloat("fragmentDuration") * 1000);
            maxForwardFetches  = (uint)node.GetAttributeInt("maxForwardFetches" );
            maxBackwardFetches = (uint)node.GetAttributeInt("maxBackwardFetches");
        }

    }
}
