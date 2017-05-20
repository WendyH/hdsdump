using System.IO;

namespace hdsdump.flv {
    public class FLVTagScriptBody {

        public string       Name = "";
        public CNameObjDict Data = new CNameObjDict();

        // CONSTRUCTOR
        public FLVTagScriptBody(Stream stream) {
            Name = AMF0.Read(stream).ToString();
            Data = AMF0.Read(stream) as CNameObjDict;
        }

        // CONSTRUCTOR
        public FLVTagScriptBody(byte[] data) {

            if (data == null || data.Length < 20) {
                data = new byte[] { 0x02, 0x00, 0x0A, 0x6F, 0x6E, 0x4D, 0x65, 0x74, 0x61, 0x44, 0x61, 0x74, 0x61, 0x03, 0x00, 0x08, 0x64, 0x75, 0x72, 0x61, 0x74, 0x69, 0x6F, 0x6E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09 };
            }

            using (MemoryStream stream = new MemoryStream(data)) {
                Name = AMF0.Read(stream).ToString();
                Data = AMF0.Read(stream) as CNameObjDict;
            }

            if (!Data.ContainsKey("duration")) {
                Data["duration"] = 0; // for the fix in future
            }
        }

        public byte[] ToByteArray() {
            byte[] dataName = AMF0.GetBytes(Name);
            byte[] dataData = AMF0.GetBytes(Data);
            byte[] allData  = new byte[dataName.Length + dataData.Length];
            System.Buffer.BlockCopy(dataName, 0, allData, 0              , dataName.Length);
            System.Buffer.BlockCopy(dataData, 0, allData, dataName.Length, dataData.Length);
            return allData;
        }

        public void Write(Stream stream) {
            AMF0.Write(stream, Name);
            AMF0.Write(stream, Data);
        }
    }
}
