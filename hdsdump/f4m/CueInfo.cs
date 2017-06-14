using System.Collections.Generic;

namespace hdsdump.f4m {
    public class CueInfo {
        public string id;
        public List<Cue> Cues = new List<Cue>();

        // CONSTRUCTOR
        public CueInfo(string id) {
            this.id = id;
        }
    }
}
