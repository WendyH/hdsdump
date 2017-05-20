using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace hdsdump.flv {
    class CAmf3Helper {
        protected enum DataType {
            Undefined = 0x00,
            Null      = 0x01,
            False     = 0x02,
            True      = 0x03,
            Integer   = 0x04,
            Double    = 0x05,
            String    = 0x06,
            XmlDoc    = 0x07,
            Date      = 0x08,
            Array     = 0x09,
            Object    = 0x0A,
            Xml       = 0x0B,
            ByteArray = 0x0C
        }

        protected class CObjTraits {
            public bool dynamic;
            public string name;
            public string[] keys;
        }

        protected class CRefTable {
            public List<string> str = new List<string>();
            public List<CNameObjDict> obj = new List<CNameObjDict>();
            public List<CObjTraits> ot = new List<CObjTraits>();
        }

        const int MaxU29 = 0x1FFFFFFF;

        public static object Read(Stream stm) {
            return ReadAmf(stm, new CRefTable());
        }

        protected static object ReadAmf(Stream stm, CRefTable rt) {
            int type = stm.ReadByte();
            switch ((DataType)type) {
                case DataType.Undefined: return null;
                case DataType.Null     : return null;
                case DataType.False    : return false;
                case DataType.True     : return true;
                case DataType.Integer  : return ReadInt(stm);
                case DataType.Double   : return CDataHelper.BE_ReadDouble(stm);
                case DataType.String   : return ReadString(stm, rt);
                case DataType.XmlDoc   : break;
                case DataType.Date     : break;
                case DataType.Array    : return ReadArray(stm, rt);
                case DataType.Object   : return ReadHashObject(stm, rt);
                case DataType.Xml      : break;
                case DataType.ByteArray: break;
                default                : break;
            }
            return null;
        }

        protected static uint ReadInt(Stream stm) {
            uint b = (uint)stm.ReadByte();
            int  num   = 0;
            uint value = 0;
            while (((b & 0x80) != 0) && (num < 3)) {
                value = (value << 7) | (b & 0x7F);
                ++num;
                b = (uint)stm.ReadByte();
            }

            if (num < 3)
                value = (value << 7) | (b & 0x7F);
            else
                value = (value << 8) | (b & 0xFF);

            return value;
        }

        protected static string ReadString(Stream stm, CRefTable rt) {
            uint head = ReadInt(stm);
            int  len  = (int)(head >> 1);
            if (len <= 0)
                return "";

            if (IsRefrence(head))
                return rt.str[len];

            string str = CDataHelper.ReadUtfStr(stm, len);
            rt.str.Add(str);

            return str;
        }

        protected static CMixArray ReadArray(Stream stm, CRefTable rt) {
            uint head = ReadInt(stm);

            int count = (int)(head >> 1);
            CMixArray ary = new CMixArray(count);
            for (string key = ReadString(stm, rt); key != ""; key = ReadString(stm, rt))
                ary[key] = ReadAmf(stm, rt);

            for (int i = 0; i < count; i++)
                ary[i] = ReadAmf(stm, rt);

            return ary;
        }

        protected static CNameObjDict ReadHashObject(Stream stm, CRefTable rt) {
            uint head = ReadInt(stm);
            CObjTraits ot = null;

            if (IsRefrence(head))
                return rt.obj[(int)(head >> 1)];

            if (IsRefrence(head >> 1)) {
                ot = rt.ot[(int)(head >> 2)];
            } else {
                ot = new CObjTraits();
                ot.dynamic = ((head >> 3) & 0x1) != 0;
                int count = (int)(head >> 4);
                ot.name = ReadString(stm, rt);
                ot.keys = new string[count];
                for (int i = 0; i < count; i++)
                    ot.keys[i] = ReadString(stm, rt);
                rt.ot.Add(ot);
            }

            CNameObjDict obj = new CNameObjDict(ot.name);
            for (int i = 0; i < ot.keys.Length; i++)
                obj[ot.keys[i]] = ReadAmf(stm, rt);

            if (ot.dynamic) {
                while (true) {
                    string key = ReadString(stm, rt);
                    if (key == "")
                        break;

                    obj[key] = ReadAmf(stm, rt);
                }
            }

            rt.obj.Add(obj);

            return obj;
        }

        protected static bool IsRefrence(uint header) {
            return (header & 0x1) == 0;
        }

        public static void Write(Stream stm, object obj) {
            WriteAmf(stm, obj);
        }

        protected static void WriteAmf(Stream stm, object obj) {
            if (obj == null) {
                stm.WriteByte((byte)DataType.Null);
            } else if (obj is byte || (obj is int && (uint)(int)obj < MaxU29) || (obj is uint && (uint)obj < MaxU29)) {
                stm.WriteByte((byte)DataType.Integer);
                WriteInt(stm, uint.Parse(obj.ToString()));
            } else if (obj is int || obj is uint || obj is float || obj is double) {
                stm.WriteByte((byte)DataType.Double);
                CDataHelper.BE_WriteDouble(stm, double.Parse(obj.ToString()));
            } else if (obj is bool) {
                if ((bool)obj)
                    stm.WriteByte((byte)DataType.True);
                else
                    stm.WriteByte((byte)DataType.False);
            } else if (obj is string) {
                stm.WriteByte((byte)DataType.String);
                WriteString(stm, obj as string);
            } else if (obj is CMixArray) {
                stm.WriteByte((byte)DataType.Array);
                CMixArray ary = obj as CMixArray;
                uint head = ((uint)ary.FixedLength << 1) | 1;
                WriteInt(stm, head);
                foreach (KeyValuePair<string, object> pair in ary.Dynamic) {
                    WriteString(stm, pair.Key);
                    WriteAmf(stm, pair.Value);
                }
                WriteString(stm, "");
                foreach (object o in ary.Fixed) {
                    WriteAmf(stm, o);
                }
            } else if (obj is Array) {
                stm.WriteByte((byte)DataType.Array);
                Array ary = obj as Array;
                uint head = ((uint)ary.Length << 1) | 1;
                WriteInt(stm, head);
                WriteString(stm, "");
                foreach (object o in ary) {
                    WriteAmf(stm, o);
                }
            } else if (obj is IDictionary) {
                stm.WriteByte((byte)DataType.Object);
                IDictionary dic = obj as IDictionary;
                uint head = 0x0B;
                WriteInt(stm, head);
                if (obj is CNameObjDict)
                    WriteString(stm, (obj as CNameObjDict).className);
                else
                    WriteString(stm, "");
                foreach (DictionaryEntry e in obj as IDictionary) {
                    if (e.Key.ToString() == "" && e.Value is string)
                        continue;
                    WriteString(stm, e.Key.ToString());
                    WriteAmf(stm, e.Value);
                }
                WriteString(stm, "");
            } else {
                stm.WriteByte((byte)DataType.Undefined);
            }
        }

        protected static void WriteInt(Stream stm, uint data) {
            if (data <= 0x7F) {
                stm.WriteByte((byte)data);

            } else if (data <= 0x3FFF) {
                stm.WriteByte((byte)((data >> 7) | 0x80));
                stm.WriteByte((byte)(data & 0x7F));

            } else if (data <= 0x001FFFFF) {
                stm.WriteByte((byte)((data >> 14) | 0x80));
                stm.WriteByte((byte)(((data >> 7) & 0x7F) | 0x80));
                stm.WriteByte((byte)(data & 0x7F));

            } else {
                stm.WriteByte((byte)((data >> 22) | 0x80));
                stm.WriteByte((byte)(((data >> 15) & 0x7F) | 0x80));
                stm.WriteByte((byte)(((data >> 8) & 0x7F) | 0x80));
                stm.WriteByte((byte)(data & 0xFF));

            }
        }

        protected static void WriteString(Stream stm, string str) {
            byte[] buf  = Encoding.UTF8.GetBytes(str);
            uint   head = ((uint)buf.Length << 1) | 1;
            WriteInt(stm, head);
            stm.Write(buf, 0, buf.Length);
        }

        public static object GetObject(byte[] buf) {
            return Read(new MemoryStream(buf));
        }

        public static byte[] GetBytes(object obj) {
            MemoryStream stm = new MemoryStream();
            Write(stm, obj);
            return stm.ToArray();
        }
    }
}
