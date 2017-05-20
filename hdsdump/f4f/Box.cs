using System.Collections.Generic;

namespace hdsdump.f4f {
    public class Box {
        public uint   Size    = 0;
        public string Type    = "";
        public uint   Length  = 0;

        public static List<Box> GetBoxes(byte[] data, string boxType="") {
            List<Box> boxes = new List<Box>();
            System.IO.MemoryStream stream = null;
            try {
                stream = new System.IO.MemoryStream(data);
                using (HDSBinaryReader br = new HDSBinaryReader(stream)) {
                    stream = null;
                    BoxInfo bi = BoxInfo.getNextBoxInfo(br);
                    while (bi != null) {
                        if (!string.IsNullOrEmpty(boxType) && bi.Type != boxType)
                            bi.Type = ""; // for skip other boxes

                        switch (bi.Type) {
                            case F4FConstants.BOX_TYPE_ABST:
                                AdobeBootstrapBox abst = new AdobeBootstrapBox();
                                abst.Parse(bi, br);
                                boxes.Add(abst);
                                break;

                            case F4FConstants.BOX_TYPE_AFRA:
                                AdobeFragmentRandomAccessBox arfa = new AdobeFragmentRandomAccessBox();
                                arfa.Parse(bi, br);
                                boxes.Add(arfa);
                                break;

                            case F4FConstants.BOX_TYPE_MDAT:
                                MediaDataBox mdat = new MediaDataBox();
                                mdat.Parse(bi, br);
                                boxes.Add(mdat);
                                break;

                            default:
                                br.Position += bi.Size - bi.Length;
                                break;
                        }
                        bi = BoxInfo.getNextBoxInfo(br);
                        if (bi != null && bi.Size <= 0)
                            break;
                    }
                }
            } finally {
                if (stream != null)
                    stream.Dispose();
            }
            return boxes;
        }

        public static Box FindBox(byte[] data, string type) {
            if (data == null) return null;
            List<Box> boxes = GetBoxes(data);
            return boxes.Find(i => i.Type == type);
        }

        public virtual void Parse(BoxInfo bi, HDSBinaryReader br) {
            Size   = bi.Size;
            Type   = bi.Type;
            Length = bi.Length;
        }
    }
}
