using System.Net;

namespace hdsdump {
    public static class HttpWebResponseExt {
        public static HttpWebResponse GetResponseNoException(HttpWebRequest req) {
            try {
                return (HttpWebResponse)req.GetResponse();
            } catch (WebException we) {
                Program.DebugLog("Error downloading the link: " + req.RequestUri + "\r\nException: " + we.Message);
                var resp = we.Response as HttpWebResponse;
                if (resp == null)
                    Program.Quit("<c:Red>" + we.Message + " (Request status: <c:Magenta>" + we.Status + "</c>)");
                //throw;
                return resp;
            }
        }
    }
}
