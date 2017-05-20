namespace hdsdump.f4m {
    /// <summary>
    /// Describes the AdditionalHeader used by media objects.
    /// </summary>
    public class DRMAdditionalHeader {
        /// <summary>
        /// The actual AdditionalHeader information in binary format
        /// </summary>
        public byte[] data;

        /// <summary>
        /// The url that points to a remote location at which the actual binary data of the AdditionalHeader resides
        /// </summary>
        public string url;

        /// <summary>
		/// The ID of this &lt;AdditionalHeader&gt; element. It is optional. If it is not specified, 
		/// then this AdditionalHeader block will apply to all &lt;media&gt; elements that don't have an 
		/// AdditionalHeader property. If it is specified, then this AdditionalHeader block will apply 
		/// only to those &lt;media&gt; elements that use the same ID in their AddionalHeader object.
        /// </summary>
		public string id;

        // CONSTRUCTOR
        public DRMAdditionalHeader() {
        }

        public DRMAdditionalHeader(XmlNodeEx node, string baseURL = "", string idPrefix = "") {
            Parse(node, baseURL, idPrefix);
        }

        public void Parse(XmlNodeEx nodeDRM, string baseURL = "", string idPrefix = "") {
            id  = idPrefix + nodeDRM.GetAttributeStr("id", F4MUtils.GLOBAL_ELEMENT_ID);

            url = nodeDRM.GetAttributeStr("url");
            if (!string.IsNullOrEmpty(url)) {
                // DRM Metadata - we may make this load on demand in the future.
                url = URL.getAbsoluteUrl(baseURL, url);
            } else {
                data = nodeDRM.GetOwnData();
            }

        }
    }
}
