using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
