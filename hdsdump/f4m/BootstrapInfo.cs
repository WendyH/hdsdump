using hdsdump.f4f;

namespace hdsdump.f4m {

    /// <summary>
    /// Describes the BootstrapInfo used by media objects.
    /// </summary>
    public class BootstrapInfo {
        /// <summary>
        /// The actual bootstrap information in binary format
        /// </summary>
        public byte[] data;

        /// <summary>
        /// The url that points to a remote location at which the actual binary data of the bootstrap information resides
        /// </summary>
        public string url;

        /// <summary>
        /// The profile, or type of bootstrapping represented by this element. 
        /// For the Named Access profile, use "named". For the Range Access Profile, 
        /// use "range". For other bootstrapping profiles, use some other string (i.e. 
        /// the field is extensible). It is required.
        /// </summary>
        public string profile;

        /// <summary>
        /// The ID of this &lt;bootstrapInfo&gt; element. It is optional. If it is not specified, 
        /// then this bootstrapping block will apply to all &lt;media&gt; elements that don't have a 
        /// bootstrapInfoId property. If it is specified, then this bootstrapping block will apply 
        /// only to those &lt;media&gt; elements that use the same ID in their bootstrapInfoId property.
        /// </summary>
        public string id;

        public float fragmentDuration;
        public float segmentDuration;

        // CONSTRUCTOR
        public BootstrapInfo() {
        }

        public BootstrapInfo(XmlNodeEx node, string baseURL = "", string idPrefix = "") {
            Parse(node, baseURL, idPrefix);
        }

        public void Parse(XmlNodeEx node, string baseURL = "", string idPrefix = "") {
            profile = node.GetAttributeStr("profile");
            id      = idPrefix + node.GetAttributeStr("id", F4MUtils.GLOBAL_ELEMENT_ID);
            url     = node.GetAttributeStr("url");

            if (!string.IsNullOrEmpty(url)) {
                // We may make this load on demand in the future.
                url = URL.getAbsoluteUrl(baseURL, url);
            } else {
                data = node.GetOwnData();
            }

            fragmentDuration = node.GetAttributeFloat("fragmentDuration");
            segmentDuration  = node.GetAttributeFloat("segmentDuration");
        }

    }
}
