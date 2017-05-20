namespace hdsdump.f4m {
    public class Cue {
        public string id;
        public string type;
        public uint   time;
        public uint   duration;
        public uint   programId;
        public uint   availNum;
        public uint   availsExpected;

        // CONSTRUCTOR
        public Cue(XmlNodeEx node, string rootURL = "", string idPrefix = "") {
            Parse(node, rootURL, idPrefix);
        }

        public void Parse(XmlNodeEx node, string rootURL = "", string idPrefix = "") {
            id   = idPrefix + node.GetAttributeStr("id", F4MUtils.GLOBAL_ELEMENT_ID);
            type = node.GetAttributeStr("type"); // SHALL be “spliceOut”
            time           = (uint)node.GetAttributeInt("time");
            duration       = (uint)node.GetAttributeInt("duration");
            programId      = (uint)node.GetAttributeInt("programId");
            availNum       = (uint)node.GetAttributeInt("availNum");
            availsExpected = (uint)node.GetAttributeInt("availsExpected");
        }
    }
}
