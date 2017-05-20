using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace hdsdump.flv {
    [Serializable]
    public class CNameObjDict : Dictionary<string, object> {
        public long Position = 0;
        string m_className;

        public string className { get { return m_className; } }

        public CNameObjDict(string name = "") {
            m_className = name;
        }

        protected CNameObjDict(SerializationInfo info, StreamingContext context): base(info, context) {
        }

        public string Str(string key) {
            return this[key].ToString();
        }

        public int Int(string key) {
            object o = this[key];
            if (!(o is UInt32))
                return Convert.ToInt32(o);
            uint v = (uint)o;
            if ((v >> 28) > 0)
                v |= (uint)7 << 29;
            return (int)v;
        }

        public CNameObjDict Obj(string key) {
            return this[key] as CNameObjDict;
        }

        public CMixArray Ary(string key) {
            return this[key] as CMixArray;
        }

        public override string ToString() {
            return string.Format("CNameObjDict[{0}]", Count);
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            base.GetObjectData(info, context);
        }
    }
}
