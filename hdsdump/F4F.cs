/* Ported to .NET AdobeHDS.php by K-S-V https://github.com/K-S-V/Scripts/blob/master/AdobeHDS.php
 * GNU General Public License v3.0
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace hdsdump {
    public sealed class F4F: IDisposable {
        public bool usePipe = false;
        public FileStream   pipeStream = null;
        public BinaryWriter pipeWriter = null;
        public Microsoft.Win32.SafeHandles.SafeFileHandle pipeHandle = null;

        public int    manifesttype    = 0; // 0 - hds, 1 - xml playlist, 2 - m3u playlist, 3 - json manifest with template
        public string fragUrlTemplate = "Seg<SEGNUM>-Frag<FRAGNUM>";
        public string auth            = "";
        public long   fromTimestamp = -1;
        public string bootstrapUrl  = "";
        public string baseUrl       = "";
        public bool   decoderTest   = false;
        public int    duration   = 0;
        public int    fileCount  = 1;
        public int    start      = 0;
        public string format     = " {0,-8}{1,-16}{2,-16}{3,-8}";
        public bool   live       = false;
        public bool   metadata   = true;
        public int    threads    = 1;
        public bool   play       = false;
        public bool   redir2Proc = false;
        public string quality    = "high";
        public string sessionID  = "";
        public int    segNum      = 1;
        public int    fragNum     = 0;
        public int    fragCount   = 0;
        public int    fragsPerSeg = 0;
        public int    lastFrag    = 0;
        public int    discontinuity = 0;
        public string fragUrl   = "";
        public bool   hasVideo  = false;
        public bool   hasAudio  = false;
        public long   baseTS    = 0;
        public long   negTS     = 0;
        public int    filesize  = 0;
        public int    fixWindow = 1000;
        public int    prevTagSize  = 4;
        public int    tagHeaderLen = 11;
        public long   prevAudioTS  = -1;
        public long   prevVideoTS  = -1;
        public long   currentTS    = 0;
        public bool   prevAVC_Header = false;
        public bool   prevAAC_Header = false;
        public bool   AVC_HeaderWritten = false;
        public bool   AAC_HeaderWritten = false;
        private bool  FLVHeaderWritten  = false;
        private bool  FLVContinue = false;
        private int   threadsRun  = 0;
        private int   fragmentsComplete = 0;
        private int   currentDuration   = 0;
        private long  currentFilesize   = 0;
        public string PostData = "";
        public string Lang = "";
        public string alt = "";
        public AkamaiDecryptor ad = new AkamaiDecryptor();

        private Fragment2Dwnld[] Fragments2Download;
        private XmlNamespaceManager nsMgr;
        private Dictionary<string, Media> listMedia = new Dictionary<string, Media>();
        private Dictionary<string, Media> listAudio = new Dictionary<string, Media>();
        private List<string> _serverEntryTable  = new List<string>();
        private List<string> _qualityEntryTable = new List<string>();
        private List<string> _qualitySegmentUrlModifiers = new List<string>();
        private List<Segment>  segTable  = new List<Segment>();
        private List<Fragment> fragTable = new List<Fragment>();
        private int segStart  = -1;
        private int fragStart = -1;
        private Media selectedMedia;
        private struct Segment {
            public int firstSegment;
            public int fragmentsPerSegment;
        }
        private struct Fragment {
            public int firstFragment;
            public long firstFragmentTimestamp;
            public int fragmentDuration;
            public int discontinuityIndicator;
        }
        private struct Manifest {
            public string bitrate;
            public string url;
            public XmlElement xml;
        }
        private struct Media {
            public string baseUrl;
            public string url;
            public string bootstrapUrl;
            public byte[] bootstrap;
            public byte[] metadata;
        }
        private struct Fragment2Dwnld {
            public string url;
            public byte[] data;
            public bool running;
            public bool ready;
        }

        // constructor
        public F4F() {
            this.InitDecoder();
        }

        #region IDisposable Support
        private bool disposedValue = false; // Для определения избыточных вызовов

        void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    ad.Dispose();
                    pipeWriter.Close();
                    pipeStream.Close();
                    if (!pipeHandle.IsClosed)
                        pipeHandle.Close();
                }
                pipeStream = null;
                pipeWriter = null;
                pipeHandle = null;
                ad = null;
                disposedValue = true;
            }
        }

        ~F4F() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private void InitDecoder() {
            if (this.FLVContinue)
                this.baseTS = 0;
            else
                this.baseTS = Constants.INVALID_TIMESTAMP;
            this.prevTagSize = 4;
            this.tagHeaderLen = 11;
            this.hasVideo = false;
            this.hasAudio = false;
            this.negTS = Constants.INVALID_TIMESTAMP;
            this.prevAudioTS = Constants.INVALID_TIMESTAMP;
            this.prevVideoTS = Constants.INVALID_TIMESTAMP;
            this.prevAVC_Header = false;
            this.prevAAC_Header = false;
            this.AVC_HeaderWritten = false;
            this.AAC_HeaderWritten = false;
        }

        public static string NormalizePath(string path) {
            string[] inSegs = Regex.Split(path, @"(?<!\/)\/(?!\/)");
            List<string> outSegs = new List<string>();
            foreach (string seg in inSegs) {
                if (seg == "" || seg == ".")
                    continue;
                if (seg == "..")
                    outSegs.RemoveAt(outSegs.Count - 1);
                else
                    outSegs.Add(seg);
            }
            string outPath = string.Join("/", outSegs.ToArray());
            if (path.StartsWith("/")) outPath = "/" + outPath;
            if (path.EndsWith("/")) outPath += "/";
            return outPath;
        }

        public static string DecodeFrom64(string encodedData) {
            byte[] encodedDataAsBytes = System.Convert.FromBase64String(encodedData);
            string returnValue;
            returnValue = System.Text.Encoding.ASCII.GetString(encodedDataAsBytes);
            return returnValue;
        }

        public static byte ReadByte(ref byte[] bytesData, long pos) {
            return bytesData[pos];
        }

        public static int ReadInt16(ref byte[] bytesData, long pos) {
            return (int)(bytesData[pos + 1] + (bytesData[pos + 0] * 256));
        }

        public static uint ReadInt24(ref byte[] bytesData, long pos) {
            uint iValLo = (uint)(bytesData[pos + 2] + (bytesData[pos + 1] * 256));
            uint iValHi = (uint)(bytesData[pos + 0]);
            uint iVal = iValLo + (iValHi * 65536);
            return iVal;
        }

        public static uint ReadInt32(ref byte[] bytesData, long pos) {
            uint iValLo = (uint)(bytesData[pos + 3] + (bytesData[pos + 2] * 256));
            uint iValHi = (uint)(bytesData[pos + 1] + (bytesData[pos + 0] * 256));
            uint iVal = iValLo + (iValHi * 65536);
            return iVal;
        }

        public static long ReadInt64(ref byte[] bytesData, long pos) {
            uint iValLo = ReadInt32(ref bytesData, pos + 4);
            uint iValHi = ReadInt32(ref bytesData, pos + 0);
            long iVal = iValLo + (iValHi * 4294967296);
            return iVal;
        }

        private static string GetString(XmlNode xmlObject) {
            return xmlObject.InnerText.Trim();
        }

        private static bool IsHttpUrl(string url) {
            bool boolValue = (url.Length > 4) && (url.ToLower().Substring(0, 4) == "http");
            return boolValue;
        }

        private static bool IsRtmpUrl(string url) {
            return Regex.IsMatch(url, @"^rtm(p|pe|pt|pte|ps|pts|fp):", RegexOptions.IgnoreCase);
        }

        private static void ReadBoxHeader(ref byte[] bytesData, ref long pos, ref string boxType, ref long boxSize) {
            boxSize = ReadInt32(ref bytesData, pos);
            boxType = ReadStringBytes(ref bytesData, pos + 4, 4);
            if (boxSize == 1) {
                boxSize = ReadInt64(ref bytesData, pos + 8) - 16;
                pos += 16;
            } else {
                boxSize -= 8;
                pos += 8;
            }
        }

        private static string ReadStringBytes(ref byte[] bytesData, long pos, long len) {
            string resultValue = "";
            for (int i = 0; i < len; i++) {
                resultValue += (char)bytesData[pos + i];
            }
            return resultValue;
        }

        public static string ReadString(ref byte[] bytesData, ref long pos) {
            string resultValue = "";
            int bytesCount = bytesData.Length;
            while ((pos < bytesCount) && (bytesData[pos] != 0)) {
                resultValue += (char)bytesData[pos++];
            }
            pos++;
            return resultValue;
        }

        public static void WriteByte(ref byte[] bytesData, long pos, byte byteValue) {
            bytesData[pos] = byteValue;
        }

        public static void WriteInt24(ref byte[] bytesData, long pos, long intValue) {
            bytesData[pos + 0] = (byte)((intValue & 0xFF0000) >> 16);
            bytesData[pos + 1] = (byte)((intValue & 0xFF00) >> 8);
            bytesData[pos + 2] = (byte)(intValue & 0xFF);
        }

        public static void WriteInt32(ref byte[] bytesData, long pos, long intValue) {
            bytesData[pos + 0] = (byte)((intValue & 0xFF000000) >> 24);
            bytesData[pos + 1] = (byte)((intValue & 0xFF0000) >> 16);
            bytesData[pos + 2] = (byte)((intValue & 0xFF00) >> 8);
            bytesData[pos + 3] = (byte)(intValue & 0xFF);
        }

        private static void WriteBoxSize(ref byte[] bytesData, long pos, string type, long size) {
            string realtype = Encoding.ASCII.GetString(bytesData, (int)pos - 4, 4);
            if (realtype == type) {
                WriteInt32(ref bytesData, pos - 8, size);
            } else {
                WriteInt32(ref bytesData, pos - 8, 0);
                WriteInt32(ref bytesData, pos - 4, size);
            }
        }

        private static void ByteBlockCopy(ref byte[] bytesData1, long pos1, ref byte[] bytesData2, long pos2, long len) {
            int len1 = bytesData1.Length;
            int len2 = bytesData2.Length;
            for (int i = 0; i < len; i++) {
                if ((pos1 >= len1) || (pos2 >= len2)) break;
                bytesData1[pos1++] = bytesData2[pos2++];
            }
        }

        private static string GetNodeProperty(XmlNode node, string propertyName, string defaultvalue = "") {
            bool found = false;
            string value = defaultvalue;
            string[] names = propertyName.Split('|');
            for (int i = 0; i < names.Length; i++) {
                propertyName = names[i].ToLower();
                // Scpecial 4 caseless check of name
                for (int n = 0; n < node.Attributes.Count; n++) {
                    if (node.Attributes[n].Name.ToLower() == propertyName) {
                        value = node.Attributes[n].Value;
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
            return value;
        }

        private string ExtractBaseUrl(string dataUrl) {
            string baseUrl = dataUrl;
            if (this.baseUrl != "")
                baseUrl = this.baseUrl;
            else {
                if (baseUrl.IndexOf("?") > 0)
                    baseUrl = baseUrl.Substring(0, baseUrl.IndexOf("?"));
                int i = baseUrl.LastIndexOf("/");
                if (i >= 0) baseUrl = baseUrl.Substring(0, baseUrl.LastIndexOf("/"));
                else baseUrl = "";
            }
            return baseUrl;
        }

        private void WriteFlvTimestamp(ref byte[] frag, long fragPos, long packetTS) {
            WriteInt24(ref frag, fragPos + 4, (packetTS & 0x00FFFFFF));
            WriteByte(ref frag, fragPos + 7, (byte)((packetTS & 0xFF000000) >> 24));
        }

        private int FindFragmentInTabe(int needle) {
            return this.fragTable.FindIndex(m => { return m.firstFragment == needle; });
        }

        private void CheckRequestRerutnCode(int statusCode, string statusMsg) {
            switch (statusCode) {
                case 403:
                    Program.Quit("<c:Red>ACCESS DENIED! Unable to download manifest. (Request status: <c:Magenta>" + statusMsg + "</c>)");
                    break;

                case 404:
                    Program.Quit("<c:Red>Manifest file not found! (Request status: <c:Magenta>" + statusMsg + "</c>)");
                    break;

                default:
                    if (statusCode != 200)
                        Program.Quit("<c:Red>Unable to download manifest (Request status: <c:Magenta>" + statusMsg + "</c>)");
                    break;
            }
        }

        private static bool AttrExist(XmlNode node, string name) {
            if (node == null) return false;
            return (GetNodeProperty(node, name, "<no>") != "<no>");
        }

        private XmlElement GetManifest(ref string manifestUrl) {
            string xmlText = "";
            if (string.IsNullOrEmpty(PostData))
                xmlText = HTTP.GET(manifestUrl);
            else
                xmlText = HTTP.POST(manifestUrl, PostData);

            if (Program.RegExMatch(@"<r>\s*?<to>(.*?)</to>", xmlText, out string sDomain)) {
                if (Program.RegExMatch(@"^.*?://.*?/.*?/(.*)", manifestUrl, out manifestUrl)) {
                    manifestUrl = sDomain + manifestUrl;
                    xmlText = HTTP.GET(manifestUrl);
                }
            }

            xmlText = Regex.Replace(xmlText, "&(?!amp;)", "&amp;");

            if (xmlText.IndexOf("</") < 0)
                Program.Quit("<c:Red>Error loading manifest: <c:Green>" + manifestUrl);
            f4m.XmlDocumentEx xmldoc = new f4m.XmlDocumentEx();
            try {
                xmldoc.LoadXml(xmlText);
            } catch (Exception e) {
                if (Regex.IsMatch(xmlText, @"<html.*?<body", RegexOptions.Singleline)) {
                    throw new XmlException("Error loading manifest. Url redirected to html page. Check the manifest url.", e);
                } else {
                    throw new XmlException("Error loading manifest. It's no valid xml file.", e);
                }
            }
            nsMgr = new XmlNamespaceManager(xmldoc.NameTable);
            nsMgr.AddNamespace("ns", xmldoc.DocumentElement.NamespaceURI);
            return xmldoc.DocumentElement;
        }

        // Get manifest and parse - extract medias info and select quality
        private void ParseManifest(string manifestUrl) {
#pragma warning disable 0219
            string baseUrl = "", defaultQuality = ""; int i = 0;

            Program.Message("Processing manifest info....");
            XmlElement xmlManifest = GetManifest(ref manifestUrl);

            XmlNode node = xmlManifest.SelectSingleNode("/ns:manifest/ns:baseURL", nsMgr);
            if (node != null)  baseUrl = node.InnerText.Trim();
            if (baseUrl == "") baseUrl = ExtractBaseUrl(manifestUrl);

            if ((baseUrl == "") && !IsHttpUrl(manifestUrl))
                Program.Quit("<c:Red>Not found <c:Magenta>baseURL</c> value in manifest or in parameter <c:White>--urlbase</c>.");

            XmlNodeList nodes = xmlManifest.SelectNodes("/ns:manifest/ns:media[@*]", nsMgr);
            Dictionary<string, Manifest> manifests = new Dictionary<string, Manifest>();
            int countBitrate = 0;
            bool readChildManifests = false;
            if (nodes.Count > 0) readChildManifests = AttrExist(nodes[0], "href");
            if (readChildManifests) {
                foreach (XmlNode ManifestNode in nodes) {
                    if (!AttrExist(ManifestNode, "bitrate")) countBitrate++;
                    Manifest manifest = new Manifest() {
                        bitrate = GetNodeProperty(ManifestNode, "bitrate", countBitrate.ToString()),
                        url     = NormalizePath(baseUrl + "/" + GetNodeProperty(ManifestNode, "href"))
                    };
                    manifest.xml = GetManifest(ref manifest.url);
                    manifests[manifest.bitrate] = manifest;
                }
            } else {
                Manifest manifest = new Manifest() {
                    bitrate = "0",
                    url = manifestUrl,
                    xml = xmlManifest
                };
                manifests[manifest.bitrate] = manifest;
                defaultQuality = manifest.bitrate;
            }
            countBitrate = 0;
            foreach (KeyValuePair<string, Manifest> pair in manifests) {
                Manifest manifest = pair.Value; string sBitrate = "";

                // Extract baseUrl from manifest url
                node = manifest.xml.SelectSingleNode("/ns:manifest/ns:baseURL", nsMgr);
                if (node != null) baseUrl = node.InnerText.Trim();
                else baseUrl = ExtractBaseUrl(manifest.url);

                XmlNodeList MediaNodes = manifest.xml.SelectNodes("/ns:manifest/ns:media", nsMgr);
                int nManifestBirate = Int32.Parse(manifest.bitrate);
                foreach (f4m.XmlNodeEx stream in MediaNodes) {
                    bool isAudioType = GetNodeProperty(stream, "type") == "audio";
                    string lang = GetNodeProperty(stream, "lang");
                    string streamId = GetNodeProperty(stream, "streamId");

                    sBitrate = GetNodeProperty(stream, "bitrate");
                    Int32.TryParse(sBitrate, out int nChildBitrate);
                    if (nManifestBirate > nChildBitrate)
                        sBitrate = manifest.bitrate;
                    if (sBitrate.Length == 0) {
                        Match m = Regex.Match(streamId, @"(\d+)$");
                        if (m.Success)
                            sBitrate = m.Groups[1].Value;
                        else
                            sBitrate = (countBitrate++).ToString();
                    }

                    if (!isAudioType)
                        while (listMedia.ContainsKey(sBitrate))
                            sBitrate = (Int32.Parse(sBitrate) + 1).ToString();

                    Media mediaEntry = new Media() {
                        baseUrl = baseUrl,
                        url     = GetNodeProperty(stream, "url")
                    };
                    if (IsRtmpUrl(mediaEntry.baseUrl) || IsRtmpUrl(mediaEntry.url))
                        Program.Quit("<c:Red>Provided manifest is not a valid HDS manifest. (Media url is <c:Magenta>rtmp</c>?)");

                    // Use embedded auth information when available
                    int idx = mediaEntry.url.IndexOf('?');
                    if (idx > 0) {
                        this.auth = mediaEntry.url.Substring(idx);
                        mediaEntry.url = mediaEntry.url.Substring(0, idx);
                    }

                    if (AttrExist(stream, "bootstrapInfoId"))
                        node = manifest.xml.SelectSingleNode("/ns:manifest/ns:bootstrapInfo[@id='" + GetNodeProperty(stream, "bootstrapInfoId") + "']", nsMgr);
                    else
                        node = manifest.xml.SelectSingleNode("/ns:manifest/ns:bootstrapInfo", nsMgr);
                    if (node != null) {
                        if (AttrExist(node, "url")) {
                            mediaEntry.bootstrapUrl = NormalizePath(mediaEntry.baseUrl + "/" + GetNodeProperty(node, "url"));
                            mediaEntry.bootstrap = HTTP.TryGETData(mediaEntry.bootstrapUrl, out int retCode, out string status);
                            if (retCode != 200)
                                Program.Quit("<c:Red>Failed to download bootstrap info. (Request status: <c:Magenta>" + status + "</c>)\n\r<c:DarkCyan>bootstrapUrl: <c:DarkRed>" + mediaEntry.bootstrapUrl);
                        } else
                            mediaEntry.bootstrap = Convert.FromBase64String(node.InnerText.Trim());
                    }

                    node = manifest.xml.SelectSingleNode("/ns:manifest/ns:media[@url='" + mediaEntry.url + "']/ns:metadata", nsMgr);
                    if (node != null)
                        mediaEntry.metadata = Convert.FromBase64String(node.InnerText.Trim());
                    else
                        mediaEntry.metadata = null;

                    if (isAudioType)
                        this.listAudio[streamId] = mediaEntry;
                    else
                        this.listMedia[sBitrate] = mediaEntry;
                }
            }
            // Available qualities
            if (this.listMedia.Count < 1)
                Program.Quit("<c:Red>No media entry found");

            Program.DebugLog("Manifest Entries:\n");
            Program.DebugLog(String.Format(" {0,-8}{1}", "Bitrate", "URL"));
            string sBitrates = " ";
            foreach (KeyValuePair<string, Media> pair in this.listMedia) {
                sBitrates += pair.Key + " ";
                Program.DebugLog(String.Format(" {0,-8}{1}", pair.Key, pair.Value.url));
            }

            Program.DebugLog("");
            // Sort quality keys - from high to low
            string[] keys = new string[this.listMedia.Keys.Count];
            this.listMedia.Keys.CopyTo(keys, 0);
            Array.Sort(keys, delegate (string b, string a) {
                if (Int32.TryParse(a, out int x) && Int32.TryParse(b, out int y)) return x - y;
                else return a.CompareTo(b);
            });
            string sQuality = defaultQuality;
            // Quality selection
            if (this.listMedia.ContainsKey(this.quality))
                sQuality = this.quality;
            else {
                this.quality = this.quality.ToLower();
                switch (this.quality) {
                    case "low":
                        this.quality = keys[keys.Length - 1]; // last
                        break;
                    case "medium":
                        this.quality = keys[keys.Length / 2];
                        break;
                    default:
                        this.quality = keys[0]; // first
                        break;
                }
                int iQuality = Convert.ToInt32(this.quality);
                while (iQuality >= 0) {
                    if (this.listMedia.ContainsKey(iQuality.ToString()))
                        break;
                    iQuality--;
                }
                sQuality = iQuality.ToString();
            }
            this.selectedMedia = this.listMedia[sQuality];
            int n = sBitrates.IndexOf(sQuality);
            sBitrates = sBitrates.Replace(" " + sQuality + " ", " <c:Cyan>" + sQuality + "</c> ");
            Program.Message("Quality Selection:");
            Program.Message("Available:" + sBitrates);
            Program.Message("Selected : <c:Cyan>" + sQuality.PadLeft(n + sQuality.Length - 1));
            this.baseUrl = this.selectedMedia.baseUrl;
            HTTP.notUseProxy = !Program.fproxy;
            if (!String.IsNullOrEmpty(this.selectedMedia.bootstrapUrl)) {
                this.bootstrapUrl = this.selectedMedia.bootstrapUrl;
                this.UpdateBootstrapInfo(this.bootstrapUrl);
            } else {
                long   pos     = 0;
                long   boxSize = 0;
                string boxType = "";
                ReadBoxHeader(ref this.selectedMedia.bootstrap, ref pos, ref boxType, ref boxSize);
                if (boxType == "abst")
                    this.ParseBootstrapBox(ref this.selectedMedia.bootstrap, pos);
                else
                    Program.Quit("<c:Red>Failed to parse bootstrap info.");
            }

            if (this.fragsPerSeg == 0) this.fragsPerSeg = this.fragCount;

            if (this.live) {
                //this.threads = 1;
                this.fromTimestamp = -1;
                Program.Message("<c:Magenta>[Live stream]");
            }
#pragma warning restore 0219
        }

        private void UpdateBootstrapInfo(string bootstrapUrl) {
            int fragNum = fragCount;
            int retries = 0;
            HTTP.Headers.Set("Cache-Control", "no-cache");
            HTTP.Headers.Set("Pragma"       , "no-cache");
            while ((fragNum == this.fragCount) && (retries < 30)) {
                long bootstrapPos = 0;
                long boxSize = 0;
                string boxType = "";
                Program.DebugLog("Updating bootstrap info, Available fragments: " + this.fragCount.ToString());
                byte[] data = HTTP.TryGETData(bootstrapUrl, out int retCode, out string status);
                if (retCode != 200)
                    Program.Quit("<c:Red>Failed to refresh bootstrap info");
                ReadBoxHeader(ref data, ref bootstrapPos, ref boxType, ref boxSize);
                if (boxType == "abst")
                    this.ParseBootstrapBox(ref data, bootstrapPos);
                else
                    Program.Quit("<c:Red>Failed to parse bootstrap info");

                Program.DebugLog("Update complete, Available fragments: " + this.fragCount.ToString());
                if (fragNum == this.fragCount) {
                    retries++;
                    Program.Message(String.Format("{0,-80}\r", "<c:DarkCyan>Updating bootstrap info, Retries: " + retries.ToString()));
                    Thread.Sleep(2000); // 2 sec
                }
            }
            //cc.Headers.Remove("Cache-Control");
            //cc.Headers.Remove("Pragma");
        }

        private void ParseBootstrapBox(ref byte[] bootstrapInfo, long pos) {
#pragma warning disable 0219
            byte version = ReadByte(ref bootstrapInfo, pos);
            int flags = (int)ReadInt24(ref bootstrapInfo, pos + 1);
            int bootstrapVersion = (int)ReadInt32(ref bootstrapInfo, pos + 4);
            byte Byte = ReadByte(ref bootstrapInfo, pos + 8);
            int profile = (Byte & 0xC0) >> 6;
            int update = (Byte & 0x10) >> 4;
            if (((Byte & 0x20) >> 5) > 0) {
                this.live = true;
                this.metadata = false;
            }
            if (update == 0) {
                this.segTable.Clear();
                this.fragTable.Clear();
            }
            int timescale = (int)ReadInt32(ref bootstrapInfo, pos + 9);
            Int64 currentMediaTime = ReadInt64(ref bootstrapInfo, 13);
            Int64 smpteTimeCodeOffset = ReadInt64(ref bootstrapInfo, 21);
            pos += 29;
            string movieIdentifier = ReadString(ref bootstrapInfo, ref pos);
            byte serverEntryCount = ReadByte(ref bootstrapInfo, pos++);
            for (int i = 0; i < serverEntryCount; i++)
                _serverEntryTable.Add(ReadString(ref bootstrapInfo, ref pos));
            byte qualityEntryCount = ReadByte(ref bootstrapInfo, pos++);
            for (int i = 0; i < qualityEntryCount; i++)
                _qualityEntryTable.Add(ReadString(ref bootstrapInfo, ref pos));
            string drmData = ReadString(ref bootstrapInfo, ref pos);
            string metadata = ReadString(ref bootstrapInfo, ref pos);
            byte segRunTableCount = ReadByte(ref bootstrapInfo, pos++);

            long boxSize = 0;
            string boxType = "";
            Program.DebugLog("Segment Tables:");
            for (int i = 0; i < segRunTableCount; i++) {
                Program.DebugLog(String.Format("\nTable {0}:", i + 1));
                ReadBoxHeader(ref bootstrapInfo, ref pos, ref boxType, ref boxSize);
                if (boxType == "asrt")
                    ParseAsrtBox(ref bootstrapInfo, pos);
                pos += boxSize;
            }
            byte fragRunTableCount = ReadByte(ref bootstrapInfo, pos++);
            Program.DebugLog("Fragment Tables:");
            for (int i = 0; i < fragRunTableCount; i++) {
                Program.DebugLog(String.Format("\nTable {0}:", i + 1));
                ReadBoxHeader(ref bootstrapInfo, ref pos, ref boxType, ref boxSize);
                if (boxType == "afrt")
                    ParseAfrtBox(ref bootstrapInfo, pos);
                pos += (int)boxSize;
            }
            ParseSegAndFragTable();
#pragma warning restore 0219
        }

        private void ParseAsrtBox(ref byte[] asrt, long pos) {
#pragma warning disable 0219
            byte version = ReadByte(ref asrt, pos);
            int flags = (int)ReadInt24(ref asrt, pos + 1);
            int qualityEntryCount = ReadByte(ref asrt, pos + 4);
#pragma warning restore 0219
            this.segTable.Clear();
            pos += 5;
            for (int i = 0; i < qualityEntryCount; i++) {
                this._qualitySegmentUrlModifiers.Add(ReadString(ref asrt, ref pos));
            }
            int segCount = (int)ReadInt32(ref asrt, pos);
            pos += 4;
            Program.DebugLog(String.Format("{0}:\n\n {1,-8}{2,-10}", "Segment Entries", "Number", "Fragments"));
            for (int i = 0; i < segCount; i++) {
                int firstSegment = (int)ReadInt32(ref asrt, pos);
                Segment segEntry = new Segment() {
                    firstSegment        = firstSegment,
                    fragmentsPerSegment = (int)ReadInt32(ref asrt, pos + 4)
                };
                if ((segEntry.fragmentsPerSegment & 0x80000000) > 0)
                    segEntry.fragmentsPerSegment = 0;
                pos += 8;
                this.segTable.Add(segEntry);
                Program.DebugLog(String.Format(" {0,-8}{1,-10}", segEntry.firstSegment, segEntry.fragmentsPerSegment));
            }
            Program.DebugLog("");
        }

        private void ParseAfrtBox(ref byte[] afrt, long pos) {
            this.fragTable.Clear();
#pragma warning disable 0219
            int version = ReadByte(ref afrt, pos);
            int flags = (int)ReadInt24(ref afrt, pos + 1);
            int timescale = (int)ReadInt32(ref afrt, pos + 4);
            int qualityEntryCount = ReadByte(ref afrt, pos + 8);
#pragma warning restore 0219
            pos += 9;
            for (int i = 0; i < qualityEntryCount; i++) {
                this._qualitySegmentUrlModifiers.Add(ReadString(ref afrt, ref pos));
            }
            int fragEntries = (int)ReadInt32(ref afrt, pos);
            pos += 4;
            Program.DebugLog(String.Format(" {0,-8}{1,-16}{2,-16}{3,-16}", "Number", "Timestamp", "Duration", "Discontinuity"));
            for (int i = 0; i < fragEntries; i++) {
                int firstFragment = (int)ReadInt32(ref afrt, pos);
                Fragment fragEntry = new Fragment() {
                    firstFragment          = firstFragment,
                    firstFragmentTimestamp = ReadInt64(ref afrt, pos + 4),
                    fragmentDuration       = (int)ReadInt32(ref afrt, pos + 12),
                    discontinuityIndicator = 0
                };
                pos += 16;
                if (fragEntry.fragmentDuration == 0)
                    fragEntry.discontinuityIndicator = ReadByte(ref afrt, pos++);
                this.fragTable.Add(fragEntry);
                Program.DebugLog(String.Format(" {0,-8}{1,-16}{2,-16}{3,-16}", fragEntry.firstFragment, fragEntry.firstFragmentTimestamp, fragEntry.fragmentDuration, fragEntry.discontinuityIndicator));
                if ((this.fromTimestamp > 0) && (fragEntry.firstFragmentTimestamp > 0) && (fragEntry.firstFragmentTimestamp < this.fromTimestamp))
                    this.start = fragEntry.firstFragment + 1;
                //this.start = i+1;
            }
            Program.DebugLog("");
        }

        private void ParseSegAndFragTable() {
            if ((this.segTable.Count == 0) || (this.fragTable.Count == 0)) return;
            Segment  firstSegment  = this.segTable[0];
            Segment  lastSegment   = this.segTable[this.segTable.Count - 1];
            Fragment firstFragment = this.fragTable[0];
            Fragment lastFragment  = this.fragTable[this.fragTable.Count - 1];

            // Check if live stream is still live
            if ((lastFragment.fragmentDuration == 0) && (lastFragment.discontinuityIndicator == 0)) {
                this.live = false;
                if (this.fragTable.Count > 0)
                    this.fragTable.RemoveAt(this.fragTable.Count - 1);
                if (this.fragTable.Count > 0)
                    lastFragment = this.fragTable[this.fragTable.Count - 1];
            }

            // Count total fragments by adding all entries in compactly coded segment table
            bool invalidFragCount = false;
            Segment prev = this.segTable[0];
            this.fragCount = prev.fragmentsPerSegment;
            for (int i = 0; i < this.segTable.Count; i++) {
                Segment current = this.segTable[i];
                this.fragCount += (current.firstSegment - prev.firstSegment - 1) * prev.fragmentsPerSegment;
                this.fragCount += current.fragmentsPerSegment;
                prev = current;
            }
            if ((this.fragCount & 0x80000000) == 0)
                this.fragCount += firstFragment.firstFragment - 1;
            if ((this.fragCount & 0x80000000) != 0) {
                this.fragCount = 0;
                invalidFragCount = true;
            }
            if (this.fragCount < lastFragment.firstFragment)
                this.fragCount = lastFragment.firstFragment;
            Program.DebugLog("fragCount: " + this.fragCount);

            // Determine starting segment and fragment
            if (this.segStart < 0) {
                if (this.live)
                    this.segStart = lastSegment.firstSegment;
                else
                    this.segStart = firstSegment.firstSegment;
                if (this.segStart < 1)
                    this.segStart = 1;
            }
            if (this.fragStart < 0) {
                if (this.live && !invalidFragCount)
                    this.fragStart = this.fragCount - 2;
                else
                    this.fragStart = firstFragment.firstFragment - 1;
                if (this.fragStart < 0)
                    this.fragStart = 0;
            }
            Program.DebugLog("segStart : " + this.segStart );
            Program.DebugLog("fragStart: " + this.fragStart);
        }

        private void StartNewThread2DownloadFragment() {
            if (this.fragNum < 1) return;
            this.threadsRun++;

            for (int i = this.fragNum - 1; i < this.fragCount; i++) {
                if ((this.fragmentsComplete - this.fragNum) > 5) break;
                if (!this.Fragments2Download[i].running) {
                    this.Fragments2Download[i].running = true;
                    //Program.Message("Starting new thread download " + (i + 1));
                    this.Fragments2Download[i].data = HTTP.TryGETData(this.Fragments2Download[i].url, out int retCode, out string status);
                    if (retCode != 200) {
                        this.Fragments2Download[i].running = false;
                        this.Fragments2Download[i].ready   = false;
                        Program.DebugLog("Error download fragment " + (i + 1) + " in thread. Status: " + status);
                    } else {
                        this.Fragments2Download[i].ready = true;
                        this.fragmentsComplete++;
                        //Program.Message("Download complete " + (i+1));
                    }
                    break;
                };
            }
            this.threadsRun--;
        }

        private void ThreadDownload() {
            while (this.fragmentsComplete < this.fragCount) {
                if ((this.fragCount - this.fragmentsComplete) < this.threads) this.threads = this.fragCount - this.fragmentsComplete;
                if (this.threadsRun < this.threads) {
                    Thread t = new Thread(StartNewThread2DownloadFragment) {
                        IsBackground = true
                    };
                    t.Start();
                }
                Thread.Sleep(300);
            }
        }

        public string GetFragmentUrl(int segNum, int fragNum) {
            string fragUrlTemplate = this.fragUrlTemplate;
            string fragUrl = this.fragUrl;
            fragUrlTemplate = fragUrlTemplate.Replace("<SEGNUM>" , segNum .ToString());
            fragUrlTemplate = fragUrlTemplate.Replace("<FRAGNUM>", fragNum.ToString());
            if (fragUrl.Contains("?"))
                fragUrl = fragUrl.Replace("?", fragUrlTemplate + "?");
            else
                fragUrl = fragUrl + fragUrlTemplate;
            return fragUrl + this.auth;
        }

        public int GetSegmentFromFragment(int fragN) {
            if ((this.segTable.Count == 0) || (this.fragTable.Count == 0)) return 1;
            Segment  firstSegment  = this.segTable[0];
            Segment  lastSegment   = this.segTable[this.segTable.Count - 1];
            Fragment firstFragment = this.fragTable[0];
            Fragment lastFragment  = this.fragTable[this.fragTable.Count - 1];

            if (this.segTable.Count == 1)
                return firstSegment.firstSegment;
            else {
                Segment seg, prev = firstSegment;
                int end, start = firstFragment.firstFragment;
                for (int i = firstSegment.firstSegment; i <= lastSegment.firstSegment; i++) {
                    if (this.segTable.Count >= (i - 1))
                        seg = this.segTable[i];
                    else
                        seg = prev;
                    end = start + seg.fragmentsPerSegment;
                    if ((fragN >= start) && (fragN < end))
                        return i;
                    prev = seg;
                    start = end;
                }
            }
            return lastSegment.firstSegment;
        }

        public void CheckLastTSExistingFile() {
            string sFile = Program.outDir + Program.outFile;
            if (!File.Exists(sFile)) return;
            int b1, b2, b3, b4;
            using (FileStream fs = new FileStream(sFile, FileMode.Open)) {
                if (fs.Length > 600) {
                    fs.Position = fs.Length - 4;
                    b1 = fs.ReadByte();
                    b2 = fs.ReadByte();
                    b3 = fs.ReadByte();
                    b4 = fs.ReadByte();
                    int blockLength = b2 * 256 * 256 + b3 * 256 + b4;
                    if (fs.Length - blockLength > 600) {
                        fs.Position = fs.Length - blockLength;
                        b1 = fs.ReadByte();
                        b2 = fs.ReadByte();
                        b3 = fs.ReadByte();
                        this.fromTimestamp = b1 * 256 * 256 + b2 * 256 + b3;
                        this.FLVHeaderWritten = true;
                        this.FLVContinue = true;
                        Program.DebugLog("Continue downloading with exiting file from timestamp: " + this.fromTimestamp.ToString());
                    }
                }
            }
        }

        public void DownloadFragments(string manifestUrl) {
            this.ParseManifest(manifestUrl);

            this.segNum  = this.segStart;
            this.fragNum = this.fragStart;
            if (this.start > 0) {
                this.segNum    = this.GetSegmentFromFragment(start);
                this.fragNum   = this.start - 1;
                this.segStart  = this.segNum;
                this.fragStart = this.fragNum;
            }
            string remaining  = "";
            string sDuration  = "";
            int    downloaded = 0;
            this.filesize     = 0;
            bool usedThreads  = (this.threads > 1) && !this.live;
            //bool usedThreads  = (this.threads > 1);
            byte[] fragmentData = new byte[0];
            this.lastFrag = this.fragNum;
            if (this.fragNum >= this.fragCount)
                Program.Quit("<c:Red>No fragment available for downloading");

            if (IsHttpUrl(this.selectedMedia.url))
                this.fragUrl = this.selectedMedia.url;
            else
                this.fragUrl = NormalizePath(this.baseUrl + "/" + this.selectedMedia.url);

            this.fragmentsComplete = this.fragNum;
            Program.DebugLog("Downloading Fragments:");
            this.InitDecoder();
            DateTime startTime = DateTime.Now;
            if (usedThreads) {
                this.Fragments2Download = new Fragment2Dwnld[this.fragCount];
                int curSegNum, curFragNum;
                for (int i = 0; i < this.fragCount; i++) {
                    curFragNum = i + 1;
                    curSegNum  = this.GetSegmentFromFragment(curFragNum);
                    this.Fragments2Download[i].url     = GetFragmentUrl(curSegNum, i + 1);
                    this.Fragments2Download[i].ready   = (curFragNum < this.fragNum); // if start > 0 skip 
                    this.Fragments2Download[i].running = false;
                }
                Thread MainThread = new Thread(ThreadDownload) {
                    IsBackground = true
                };
                MainThread.Start();
            }
            // --------------- MAIN LOOP DOWNLOADING FRAGMENTS ----------------
            int fragsToDownload = this.fragCount - this.fragNum;
            while (this.fragNum < this.fragCount) {
                this.fragNum++;
                this.segNum = this.GetSegmentFromFragment(this.fragNum);

                //if (this.duration > 0) 
                int ts = (int)Math.Round((double)this.currentTS / 1000);
                sDuration = string.Format("<c:DarkCyan>Current timestamp: </c>{0:00}:{1:00}:{2:00} ", ts / 3600, (ts / 60) % 60, ts % 60);

                if (Program.showtime && !this.live) {
                    TimeSpan timeRemaining = TimeSpan.FromTicks(DateTime.Now.Subtract(startTime).Ticks * (fragsToDownload - (downloaded + 1)) / (downloaded + 1));
                    remaining = String.Format("<c:DarkCyan>Time remaining: </c>{0:00}<c:Cyan>:</c>{1:00}<c:Cyan>:</c>{2:00}", timeRemaining.Hours, timeRemaining.Minutes, timeRemaining.Seconds);
                }
                Program.Message(String.Format("{0,-46} {1}{2}\r", "Downloading <c:White>" + fragNum + "</c>/" + this.fragCount + " fragments", sDuration, remaining));
                int fragIndex = FindFragmentInTabe(this.fragNum);
                if (fragIndex >= 0)
                    this.discontinuity = this.fragTable[fragIndex].discontinuityIndicator;
                else {
                    // search closest
                    for (int i = 0; i < this.fragTable.Count; i++) {
                        if (this.fragTable[i].firstFragment < this.fragNum) continue;
                        this.discontinuity = this.fragTable[i].discontinuityIndicator;
                        break;
                    }
                }
                if (this.discontinuity != 0) {
                    Program.DebugLog("Skipping fragment " + this.fragNum.ToString() + " due to discontinuity, Type: " + this.discontinuity.ToString());
                    continue;
                }

                if (usedThreads) {
                    // use threads
                    DateTime DataTimeOut = DateTime.Now.AddSeconds(200);
                    while (!this.Fragments2Download[this.fragNum - 1].ready) {
                        System.Threading.Thread.Sleep(100);
                        if (DateTime.Now > DataTimeOut) break;
                    }
                    if (!this.Fragments2Download[this.fragNum - 1].ready) {
                        Program.Quit("<c:Red>Timeout downloading fragment " + this.fragNum + " ".PadLeft(38));
                    }
                    fragmentData = this.Fragments2Download[this.fragNum - 1].data;
                    Program.DebugLog("threads fragment loaded: " + this.Fragments2Download[this.fragNum - 1].url);
                } else {
                    string fragUrl = GetFragmentUrl(this.segNum, this.fragNum);
                    Program.DebugLog("Fragment Url: " + fragUrl);
                    fragmentData = HTTP.TryGETData(fragUrl, out int retCode, out string status);
                    if (retCode != 200) {
                        if ((retCode == 403) && !String.IsNullOrEmpty(HTTP.Proxy) && !Program.fproxy) {
                            string msg = "<c:Red>Access denied for downloading fragment <c:White>" + this.fragNum.ToString() + "</c>. (Request status: <c:Magenta>" + status + "</c>)";
                            msg += "\nTry switch <c:Green>--fproxy</c>.";
                            Program.Quit(msg);
                        } else if (retCode == 503) {
                            Program.DebugLog("Fragment " + this.fragNum + " seems temporary unavailable");
                            this.fragNum--;
                            continue;
                        } else
                            Program.Quit("<c:Red>Failed to download fragment <c:White>" + this.fragNum.ToString() + "</c>. (Request status: <c:Magenta>" + status + "</c>)");
                    }
                }
                //Program.Message("\nWriteFragment " + this.fragNum);
                WriteFragment(ref fragmentData, this.fragNum);

                /* Resync with latest available fragment when we are left behind due to slow *
                 * connection and short live window on streaming server. make sure to reset  *
                 * the last written fragment.                                                */
                if (this.live && (this.fragNum >= this.fragCount)) {
                    Program.DebugLog("Trying to resync with latest available fragment");
                    this.UpdateBootstrapInfo(this.bootstrapUrl);
                    //this.fragNum  = this.fragCount - 1;
                    this.lastFrag = this.fragNum;
                }
                downloaded++;
                Program.DebugLog("Downloaded: serment=" + this.segNum + " fragment=" + this.fragNum + "/" + this.fragCount + " lenght: " + fragmentData.Length);
                fragmentData = null;
                if (usedThreads) this.Fragments2Download[this.fragNum - 1].data = null;
                if ((this.duration > 0) && (this.currentDuration >= this.duration)) break;
                if ((this.filesize > 0) && (this.currentFilesize >= this.filesize)) break;
            }
            sDuration = string.Format("\n<c:DarkCyan>Downloaded duration: </c>{0:00}:{1:00}:{2:00} ", this.currentDuration / 3600, (this.currentDuration / 60) % 60, this.currentDuration % 60);
            Program.Message(sDuration);
            Program.DebugLog("\nAll fragments downloaded successfully.");
        }

        private void Write2File(string outFile, ref byte[] data, FileMode fileMode = FileMode.Append, long pos = 0, long datalen = 0) {
            if ((datalen == 0) || (datalen > (data.Length - pos))) datalen = data.Length - pos;
            try {
                if (this.play) {
                    Stream stdout = (this.redir2Proc) ? Program.redir2Prog.StandardInput.BaseStream : Console.OpenStandardOutput();
                    stdout.Write(data, (int)pos, (int)datalen);
                    stdout.Flush();
                } else {
                    if (this.pipeWriter == null) {
                        if (usePipe) {
                            if (this.pipeStream != null) this.pipeStream.Close();
                            if (this.pipeHandle != null) this.pipeHandle.Close();
                            this.pipeHandle = NativeMethods.CreateFile(outFile, NativeMethods.GENERIC_WRITE, 0, IntPtr.Zero, NativeMethods.OPEN_EXISTING, NativeMethods.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                            if (this.pipeHandle.IsInvalid) Program.Quit("<c:Red>Cannot create pipe for writting.");
                            this.pipeStream = new FileStream(this.pipeHandle, FileAccess.Write, 4096, true);
                            this.pipeWriter = new BinaryWriter(this.pipeStream);
                        } else {
                            if (this.pipeStream != null) this.pipeStream.Close();
                            if (this.pipeHandle != null) this.pipeHandle.Close();
                            this.pipeStream = new FileStream(outFile, fileMode);
                            this.pipeWriter = new BinaryWriter(this.pipeStream);
                        }
                    }
                    this.pipeWriter.Write(data, (int)pos, (int)datalen);
                    this.pipeWriter.Flush();
                }
                this.currentFilesize += datalen;
            } catch (Exception e) {
                if (Program.ConsolePresent) {
                    Program.DebugLog("Error while writing to file! Message: " + e.Message);
                    Program.DebugLog("Exception: " + e.ToString());
                    Program.Quit("<c:Red>Error while writing to file! <c:DarkCyan>Message: <c:Magenta>" + e.Message);
                }
            }
        }

        private void WriteFlvHeader(string outFile) {
            filesize = 0;
            byte[] flvHeader = new byte[] { 0x46, 0x4c, 0x56, 0x01, 0x00, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 0x00 };
            if (hasVideo) flvHeader[4] |= 0x01;
            if (hasAudio) flvHeader[4] |= 0x04;
            Write2File(outFile, ref flvHeader, FileMode.Create);
            if (metadata) WriteMetadata(outFile);

            FLVHeaderWritten = true;
        }

        private void WriteMetadata(string outFile) {
            if ((this.selectedMedia.metadata != null) && (this.selectedMedia.metadata.Length > 0)) {
                int mediaMetadataSize = this.selectedMedia.metadata.Length;
                byte[] metadata = new byte[this.tagHeaderLen + mediaMetadataSize + 4];
                WriteByte(ref metadata, 0, Constants.SCRIPT_DATA);
                WriteInt24(ref metadata, 1, mediaMetadataSize);
                WriteInt24(ref metadata, 4, 0);
                WriteInt32(ref metadata, 7, 0);
                ByteBlockCopy(ref metadata, this.tagHeaderLen, ref this.selectedMedia.metadata, 0, mediaMetadataSize);
                WriteByte(ref metadata, this.tagHeaderLen + mediaMetadataSize - 1, 0x09);
                WriteInt32(ref metadata, this.tagHeaderLen + mediaMetadataSize, this.tagHeaderLen + mediaMetadataSize);
                this.Write2File(outFile, ref metadata);
            }
        }

        private void WriteFragment(ref byte[] data, int fragNum) {
            if (data == null) return;
            if (!this.FLVHeaderWritten) {
                bool debugstate = Program.debug;
                Program.debug = false;
                this.InitDecoder();
                DecodeFragment(ref data, true);
                WriteFlvHeader(Program.outDir + Program.outFile);
                this.InitDecoder();
                Program.debug = debugstate;
            }
            DecodeFragment(ref data);
        }

        bool VerifyFragment(ref byte[] frag) {
            string boxType = "";
            long boxSize = 0;
            long fragPos = 0;

            /* Some moronic servers add wrong boxSize in header causing fragment verification *
             * to fail so we have to fix the boxSize before processing the fragment.          */
            while (fragPos < frag.Length) {
                ReadBoxHeader(ref frag, ref fragPos, ref boxType, ref boxSize);
                if (boxType == "mdat") {
                    if ((fragPos + boxSize) > frag.Length) {
                        boxSize = frag.Length - fragPos;
                        WriteBoxSize(ref frag, fragPos, boxType, boxSize);
                    }
                    return true;
                }
                fragPos += boxSize;
            }
            return false;
        }

        public static byte[] AppendBuf(byte[] buf1, byte[] buf2) {
            byte[] resultBuf = new byte[buf1.Length + buf2.Length];
            if (buf1.Length > 0) Buffer.BlockCopy(buf1, 0, resultBuf, 0, buf1.Length);
            if (buf2.Length > 0) Buffer.BlockCopy(buf2, 0, resultBuf, buf1.Length, buf2.Length);
            return resultBuf;
        }

        public static byte[] BlockCopy(byte[] src, long pos = 0, long len = 0) {
            if (len == 0) len = src.Length - pos;
            byte[] newBlock = new byte[len];
            Buffer.BlockCopy(src, (int)pos, newBlock, 0, (int)len);
            return newBlock;
        }

        private void DecodeFragment(ref byte[] frag, bool testDecode = false) {
            if (frag == null) return;

            string outFile = Program.outDir + Program.outFile;
            string boxType = "";
            long boxSize  = 0;
            long fragLen  = frag.Length;
            long fragPos  = 0;
            long packetTS = 0;
            long lastTS   = 0;
            long fixedTS  = 0;
            int AAC_PacketType = 0;
            int AVC_PacketType = 0;

            if (!VerifyFragment(ref frag)) {
                Program.Message("<c:Red>Skipping failed fragment " + fragNum + " ".PadLeft(48));
                return;
            };

            while (fragPos < fragLen) {
                ReadBoxHeader(ref frag, ref fragPos, ref boxType, ref boxSize);
                if (boxType == "mdat") break;
                fragPos += boxSize;
            }
            long posBox = fragPos;

            // Initialize akamai decryptor
            ad.InitDecryptor();

            Program.DebugLog(String.Format("Fragment {0}:\n", fragNum));
            Program.DebugLog(String.Format(this.format + "{4,-16}", "Type", "CurrentTS", "PreviousTS", "Size", "Position"));
            int packetCount = 0;
            while (fragPos < fragLen) {
                packetCount++;
                int packetType = ReadByte(ref frag, fragPos);
                int packetSize = (int)ReadInt24(ref frag, fragPos + 1);
                packetTS = ReadInt24(ref frag, fragPos + 4);
                packetTS = (uint)packetTS | (uint)(ReadByte(ref frag, fragPos + 7) << 24);

                if ((packetTS & 0x80000000) == 0) packetTS &= 0x7FFFFFFF;
                long totalTagLen = this.tagHeaderLen + packetSize + this.prevTagSize;
                byte[] tagHeader = BlockCopy(frag, fragPos, tagHeaderLen);
                byte[] tagData   = BlockCopy(frag, fragPos + tagHeaderLen, packetSize);

                // Remove Akamai encryption
                if ((packetType == Constants.AKAMAI_ENC_AUDIO) || (packetType == Constants.AKAMAI_ENC_VIDEO)) {
                    tagData = ad.Decrypt(tagData, 0, baseUrl, auth);
                    packetType = (packetType == Constants.AKAMAI_ENC_AUDIO ? Constants.AUDIO : Constants.VIDEO);
                    packetSize = tagData.Length;
                    WriteByte (ref tagHeader, 0, (byte)packetType);
                    WriteInt24(ref tagHeader, 1, packetSize);
                }

                // Try to fix the odd timestamps and make them zero based
                currentTS = packetTS;
                lastTS = this.prevVideoTS >= this.prevAudioTS ? this.prevVideoTS : this.prevAudioTS;
                fixedTS = lastTS + Constants.FRAMEFIX_STEP;
                if ((this.baseTS == Constants.INVALID_TIMESTAMP) && ((packetType == Constants.AUDIO) || (packetType == Constants.VIDEO)))
                    this.baseTS = packetTS;
                if ((this.baseTS > 1000) && (packetTS >= this.baseTS))
                    packetTS -= this.baseTS;

                if (lastTS != Constants.INVALID_TIMESTAMP) {
                    long timeShift = packetTS - lastTS;
                    if (timeShift > this.fixWindow) {
                        Program.DebugLog(String.Format("Timestamp gap detected: PacketTS={0} LastTS={1} Timeshift={2}", packetTS, lastTS, timeShift));
                        if (this.baseTS < packetTS)
                            this.baseTS += timeShift - Constants.FRAMEFIX_STEP;
                        else
                            this.baseTS = timeShift - Constants.FRAMEFIX_STEP;
                        packetTS = fixedTS;
                    } else {
                        lastTS = packetType == Constants.VIDEO ? this.prevVideoTS : this.prevAudioTS;
                        if (packetTS < (lastTS - this.fixWindow)) {
                            if ((this.negTS != Constants.INVALID_TIMESTAMP) && ((packetTS + this.negTS) < (lastTS - this.fixWindow)))
                                this.negTS = Constants.INVALID_TIMESTAMP;
                            if (this.negTS == Constants.INVALID_TIMESTAMP) {
                                this.negTS = (int)(fixedTS - packetTS);
                                Program.DebugLog(String.Format("Negative timestamp detected: PacketTS={0} LastTS={1} NegativeTS={2}", packetTS, lastTS, this.negTS));
                                packetTS = fixedTS;
                            } else {
                                if ((packetTS + this.negTS) <= (lastTS + this.fixWindow))
                                    packetTS += this.negTS;
                                else {
                                    this.negTS = (int)(fixedTS - packetTS);
                                    Program.DebugLog(String.Format("Negative timestamp override: PacketTS={0} LastTS={1} NegativeTS={2}", packetTS, lastTS, this.negTS));
                                    packetTS = fixedTS;
                                }
                            }
                        }
                    }
                }
                if (packetTS != this.currentTS)
                    WriteFlvTimestamp(ref tagHeader, 0, packetTS);

                switch (packetType) {
                    case Constants.AUDIO:
                        if (packetTS > this.prevAudioTS - this.fixWindow) {
                            int FrameInfo = ReadByte(ref tagData, 0);
                            int CodecID = (FrameInfo & 0xF0) >> 4;
                            if (CodecID == Constants.CODEC_ID_AAC) {
                                AAC_PacketType = ReadByte(ref tagData, 1);
                                if (AAC_PacketType == Constants.AAC_SEQUENCE_HEADER) {
                                    if (this.AAC_HeaderWritten) {
                                        Program.DebugLog("Skipping AAC sequence header");
                                        Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                                        break;
                                    } else {
                                        Program.DebugLog("Writing AAC sequence header");
                                        this.AAC_HeaderWritten = true;
                                    }
                                } else if (!this.AAC_HeaderWritten) {
                                    Program.DebugLog("Discarding audio packet received before AAC sequence header");
                                    Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                                    break;
                                }
                            }
                            if (packetSize > 0) {
                                // Check for packets with non-monotonic audio timestamps and fix them
                                if (!((CodecID == Constants.CODEC_ID_AAC) && ((AAC_PacketType == Constants.AAC_SEQUENCE_HEADER) || this.prevAAC_Header))) {
                                    if ((this.prevAudioTS != Constants.INVALID_TIMESTAMP) && (packetTS <= this.prevAudioTS)) {
                                        Program.DebugLog("Fixing audio timestamp");
                                        Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                                        packetTS += (Constants.FRAMEFIX_STEP / 5) + (this.prevAudioTS - packetTS);
                                        this.WriteFlvTimestamp(ref tagHeader, 0, packetTS);
                                    }
                                }

                                long packetPos = this.currentFilesize;
                                if (!testDecode && ((this.currentTS > this.fromTimestamp) || !this.FLVContinue)) {
                                    byte[] flvTag = new byte[tagHeader.Length + tagData.Length + 4];
                                    Buffer.BlockCopy(tagHeader, 0, flvTag, 0, tagHeader.Length);
                                    Buffer.BlockCopy(tagData  , 0, flvTag, tagHeader.Length, tagData.Length);
                                    WriteInt32(ref flvTag, flvTag.Length - 4, flvTag.Length - 4);
                                    this.Write2File(outFile, ref flvTag);
                                    Program.DebugLog(String.Format(this.format + "{4,-16}", "AUDIO", packetTS, this.prevAudioTS, packetSize, packetPos));
                                } else {
                                    Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                                }

                                if ((CodecID == Constants.CODEC_ID_AAC) && (AAC_PacketType == Constants.AAC_SEQUENCE_HEADER))
                                    this.prevAAC_Header = true;
                                else
                                    this.prevAAC_Header = false;
                                this.prevAudioTS = packetTS;
                            } else {
                                Program.DebugLog("Skipping small sized audio packet");
                                Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                            }
                        } else {
                            Program.DebugLog("Skipping audio packet in fragment fragNum");
                            Program.DebugLog(String.Format(this.format, "AUDIO", packetTS, this.prevAudioTS, packetSize));
                        }
                        if (!this.hasAudio) this.hasAudio = true;
                        break;

                    case Constants.VIDEO:
                        if (packetTS > this.prevVideoTS - this.fixWindow) {
                            int FrameInfo = ReadByte(ref tagData, 0);
                            int FrameType = (FrameInfo & 0xF0) >> 4;
                            int CodecID = FrameInfo & 0x0F;
                            if (FrameType == Constants.FRAME_TYPE_INFO) {
                                Program.DebugLog("Skipping video info frame");
                                Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                break;
                            }
                            if (CodecID == Constants.CODEC_ID_AVC) {
                                AVC_PacketType = ReadByte(ref tagData, 1);
                                if (AVC_PacketType == Constants.AVC_SEQUENCE_HEADER) {
                                    if (this.AVC_HeaderWritten) {
                                        Program.DebugLog("Skipping AVC sequence header");
                                        Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                        break;
                                    } else {
                                        Program.DebugLog("Writing AVC sequence header");
                                        this.AVC_HeaderWritten = true;
                                    }
                                } else if (!this.AVC_HeaderWritten) {
                                    Program.DebugLog("Discarding video packet received before AVC sequence header");
                                    Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                    break;
                                }
                            }
                            if (packetSize > 0) {
                                if (Program.debug) {
                                    long pts = packetTS;
                                    if ((CodecID == Constants.CODEC_ID_AVC) && (AVC_PacketType == Constants.AVC_NALU)) {
                                        long cts = ReadInt24(ref tagData, 2);
                                        cts = (cts + 0xff800000) ^ 0xff800000;
                                        pts = packetTS + cts;
                                        if (cts != 0) Program.DebugLog(String.Format("DTS: {0} CTS: {1} PTS: {2}", packetTS, cts, pts));
                                    }
                                }

                                // Check for packets with non-monotonic video timestamps and fix them
                                if (!((CodecID == Constants.CODEC_ID_AVC) && ((AVC_PacketType == Constants.AVC_SEQUENCE_HEADER) || (AVC_PacketType == Constants.AVC_SEQUENCE_END) || this.prevAVC_Header))) {
                                    if ((this.prevVideoTS != Constants.INVALID_TIMESTAMP) && (packetTS <= this.prevVideoTS)) {
                                        Program.DebugLog("Fixing video timestamp");
                                        Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                        packetTS += (Constants.FRAMEFIX_STEP / 5) + (this.prevVideoTS - packetTS);
                                        this.WriteFlvTimestamp(ref tagHeader, 0, packetTS);
                                    }
                                }

                                long packetPos = this.currentFilesize;
                                if (!testDecode && ((this.currentTS > this.fromTimestamp) || !this.FLVContinue)) {
                                    byte[] flvTag = new byte[tagHeader.Length + tagData.Length + 4];
                                    Buffer.BlockCopy(tagHeader, 0, flvTag, 0, tagHeader.Length);
                                    Buffer.BlockCopy(tagData, 0, flvTag, tagHeader.Length, tagData.Length);
                                    WriteInt32(ref flvTag, flvTag.Length - 4, flvTag.Length - 4);
                                    this.Write2File(outFile, ref flvTag);
                                    Program.DebugLog(String.Format(this.format + "{4,-16}", "VIDEO", packetTS, this.prevVideoTS, packetSize, packetPos));
                                } else {
                                    Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                                }

                                if ((CodecID == Constants.CODEC_ID_AVC) && (AVC_PacketType == Constants.AVC_SEQUENCE_HEADER))
                                    this.prevAVC_Header = true;
                                else
                                    this.prevAVC_Header = false;

                                this.prevVideoTS = packetTS;
                            } else {
                                Program.DebugLog("Skipping small sized video packet");
                                Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                            }
                        } else {
                            Program.DebugLog("Skipping video packet in fragment fragNum");
                            Program.DebugLog(String.Format(this.format, "VIDEO", packetTS, this.prevVideoTS, packetSize));
                        }
                        if (!this.hasVideo) this.hasVideo = true;
                        break;

                    case Constants.SCRIPT_DATA:
                        break;

                    default:
                        if ((packetType == Constants.FLASHACCESS_ENC_AUDIO) || (packetType == Constants.FLASHACCESS_ENC_VIDEO))
                            Program.Quit("<c:Red>This stream is encrypted with <c:Magenta>FlashAccess DRM</c>. Decryption of such streams isn't currently possible with this program.");
                        else
                            Program.Quit("<c:Red>Unknown packet type <c:Magenta>" + packetType + "</c> encountered! Encrypted fragments can't be recovered. I'm so sorry.");
                        break;
                }
                fragPos += totalTagLen;
            }
            this.currentDuration = (int)Math.Round((double)packetTS / 1000);
        }

    }
}
