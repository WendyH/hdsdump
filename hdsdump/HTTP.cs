using System;
using System.IO;
using System.Net;
using System.Text;

namespace hdsdump {
    public static class HTTP {
        public static string Useragent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:13.0) Gecko/20100101 Firefox/13.0";
        public static string Referer   = "";
        public static string Proxy     = "";
        public static string ProxyUsername = "";
        public static string ProxyPassword = "";
        public static string Username = "";
        public static string Password = "";
        public static string Cookies  = "";
        public static bool   notUseProxy    = false;
        public static bool   UseSystemProxy = false;
        public static WebHeaderCollection Headers = new WebHeaderCollection();
        private static CookieContainer   _cookies = new CookieContainer();

        private const int bufferLenght = 1048576;

        public static byte[] Request(string url) {
            return Request(url, "GET", "", out int retCode, out string status);
        }

        public static byte[] Request(string url, string method, string content, out int retCode, out string status, bool noThrow=false) {
            retCode = 200; status = "";
            byte[] ResponseData = new byte[0];
            if (!url.StartsWith("http")) {   // if not http url - try load as file
                if (File.Exists(url)) {
                    ResponseData = File.ReadAllBytes(url);
                    status = "OK";
                    retCode = 200;
                } else {
                    status = "File not found.";
                    retCode = 404;
                }
                if (!noThrow) CheckReturnCode(retCode, status);
                return ResponseData;
            }
            Uri myUri = new Uri(url);
            LeaveDotsAndSlashesEscaped(myUri);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(myUri);
            if (Useragent != "") request.UserAgent   = Useragent;
            if (Referer   != "") request.Referer     = Referer;
            if (Username  != "") request.Credentials = new NetworkCredential(Username, Password);
            if (!string.IsNullOrEmpty(Proxy) && !notUseProxy) {
                if (!Proxy.StartsWith("http")) Proxy = "http://" + Proxy;
                WebProxy myProxy = new WebProxy() {
                    Address = new Uri(Proxy)
                };
                if (ProxyUsername != "")
                    myProxy.Credentials = new NetworkCredential(ProxyUsername, ProxyPassword);
                request.Proxy = myProxy;
            } else if (!UseSystemProxy) {
                request.Proxy = null;
            } 
            request.Method = method;
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Headers.Set(HttpRequestHeader.AcceptLanguage, "en-us,en;q=0.5");
            request.Headers.Set(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
            request.Headers.Set(HttpRequestHeader.AcceptCharset , "ISO-8859-1,utf-8;q=0.7,*;q=0.7");
            request.Timeout   = 18000;
            request.KeepAlive = true;
            request.CookieContainer = _cookies;

            foreach (string key in Headers.AllKeys) {
                switch (key) {
                    case "Content-Type": request.ContentType = Headers[key]; break;
                    case "Referer"     : request.Referer     = Headers[key]; break;
                    case "User-Agent"  : request.UserAgent   = Headers[key]; break;
                    case "Accept"      : request.Accept      = Headers[key]; break;
                    default: request.Headers[key] = Headers[key]; break;
                }
            }
            if (!string.IsNullOrEmpty(Cookies))
                request.Headers.Add("Cookie: " + Cookies);

            if (!string.IsNullOrEmpty(content)) {
                //byte[] bytesArray = Encoding.UTF8.GetBytes(Uri.EscapeDataString(content));
                byte[] bytesArray = Encoding.UTF8.GetBytes(content);
                request.ContentType   = "application/x-www-form-urlencoded; Charset=UTF-8";
                request.ContentLength = bytesArray.LongLength;
                request.GetRequestStream().Write(bytesArray, 0, bytesArray.Length);
            }

            using (HttpWebResponse response = HttpWebResponseExt.GetResponseNoException(request)) {
                status  = response.StatusDescription;
                retCode = (int)response.StatusCode;

                foreach (Cookie c in response.Cookies)
                    _cookies.Add(c);

                using (Stream dataStream = response.GetResponseStream()) {
                    byte[] buffer = new byte[bufferLenght];
                    using (MemoryStream ms = new MemoryStream()) {
                        int readBytes;
                        while ((readBytes = dataStream.Read(buffer, 0, buffer.Length)) > 0) {
                            ms.Write(buffer, 0, readBytes);
                        }
                        ResponseData = ms.ToArray();
                    }
                }
            }
            if (!noThrow) CheckReturnCode(retCode, status);
            return ResponseData;
        }

        private static void CheckReturnCode(int retCode, string status) {
            switch (retCode) {
                case 403: throw new Exception("ACCESS DENIED! Unable to download manifest. (Request status: <c:Magenta>" + status + "</c>)");
                case 404: throw new Exception("Manifest file not found! (Request status: <c:Magenta>" + status + "</c>)");
                default:
                    if (retCode != 200)
                        throw new Exception("Unable to download manifest (Request status: <c:Magenta>" + status + "</c>)");
                    break;
            }
        }

        public static byte[] TryGETData(string url) {
            return Request(url, "GET", "", out int retCode, out string status, true);
        }

        public static byte[] TryGETData(string url, out int retCode, out string status) {
            return Request(url, "GET", "", out retCode, out status, true);
        }

        public static string TryGET(string url) {
            return Encoding.UTF8.GetString(TryGETData(url));
        }

        public static string TryGET(string url, out int retCode, out string status) {
            return Encoding.UTF8.GetString(Request(url, "GET", "", out retCode, out status, true));
        }

        public static string GET(string sUrl) {
            return Encoding.UTF8.GetString(Request(sUrl));
        }

        public static byte[] GETData(string url) {
            return Request(url, "GET", "", out int retCode, out string status);
        }

        public static string POST(string sUrl, string content) {
            return Encoding.UTF8.GetString(Request(sUrl, "POST", content, out int retCode, out string status));
        }

        // System.UriSyntaxFlags is internal, so let's duplicate the flag privately
        private const int UnEscapeDotsAndSlashes = 0x2000000;
        public static void LeaveDotsAndSlashesEscaped(Uri uri) {
            if (uri == null) throw new ArgumentNullException("uri");
            System.Reflection.FieldInfo fieldInfo = uri.GetType().GetField("m_Syntax", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fieldInfo != null) {
                object uriParser = fieldInfo.GetValue(uri);
                fieldInfo = typeof(UriParser).GetField("m_Flags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fieldInfo != null) {
                    object uriSyntaxFlags = fieldInfo.GetValue(uriParser);
                    // Clear the flag that we don't want
                    uriSyntaxFlags = (int)uriSyntaxFlags & ~UnEscapeDotsAndSlashes;
                    fieldInfo.SetValue(uriParser, uriSyntaxFlags);
                }
            }
        }

        public static string JoinUrl(string url1, string url2) {
            if (url1.Length == 0) return url2;
            if (url2.Length == 0) return url1;
            url1 = url1.TrimEnd  ('/', '\\');
            url2 = url2.TrimStart('/', '\\');
            return string.Format("{0}/{1}", url1, url2);
        }
    }
}
