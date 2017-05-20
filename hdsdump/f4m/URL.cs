namespace hdsdump.f4m {
    /// <summary>
    /// URL parses a Uniform Resource Identifier (URI/URL) into individual properties and provides easy access
    /// to query string parameters.This also works with rtmp:// urls, but will assume the instance isn't specified.  
    /// To use rtmp:// urls with an instance name, use the FMSURL class instead of URL.
    /// </summary>
    public class URL {

        /// <summary>
        /// Normalizes a root URL. It adds a trailing slash (/) if not present.
		/// It is assumed that the passed url is an absolute root url.No checks will be performed to validate this.
        /// </summary>
        /// <param name="url">base url string</param>
        /// <returns>Returns url with "/" in the end.</returns>
        public static string normalizeRootURL(string url) {
            return (!string.IsNullOrEmpty(url) && !url.EndsWith("/")) ? (url + "/") : url;
        }

        /// <summary>
		/// Normalizes a relative URL. It removes the leading slash (/) if present.
		/// It is assumed that the passed url is a relative one. No checks will be performed to validate this.
        /// </summary>
        public static string normalizeRelativeURL(string url) {
            return (!string.IsNullOrEmpty(url) && !url.StartsWith("/")) ? url.Substring(1) : url;
        }

        /// <summary>
        /// Normalizes the path for the specified URL.
		/// Removes any query parameters or file.
        /// </summary>
        public static string normalizePathForURL(string url, bool removeFilePart) {
            if (string.IsNullOrEmpty(url))
                return string.Empty;
			string result = url;
            System.Uri uri = new System.Uri(url);
            if (uri.IsAbsoluteUri) {
				result = uri.Scheme + "://" + uri.Host;
				if (((uri.Scheme=="http") && (uri.Port!=80)) || ((uri.Scheme == "https") && (uri.Port != 443))) {
					result += ":" + uri.Port;
				}
                string path = uri.LocalPath;
				if (path != null && path.Length > 0) {
					if (removeFilePart) {
						int index = path.LastIndexOf("/");
                        if (index >= 0)
    						path = path.Substring(0, index+1);
					}
                    if ((path.Length > 0) && (path[0]=='/')) {
                        result += path;
                    } else {
                        result += "/" + normalizeRelativeURL(path);
                    }
                }
			}
			return normalizeRootURL(result);
		}

        public static string getAbsoluteUrl(string baseUrl, string url) {
            if (string.IsNullOrEmpty(url))
                return string.Empty;
            if (string.IsNullOrEmpty(baseUrl))
                return url;
            return new System.Uri(new System.Uri(normalizeRootURL(baseUrl)), url).AbsoluteUri;
        }

        public static bool isAbsoluteURL(string url) {
            if (string.IsNullOrEmpty(url))
                return false;
            return new System.Uri(url).IsAbsoluteUri;
		}

        public static string getRootUrl(string url) {
            if (string.IsNullOrEmpty(url))
                return string.Empty;
            return url.Substring(0, url.LastIndexOf("/"));
		}

        public static string ExtractBaseUrl(string dataUrl) {
            if (string.IsNullOrEmpty(dataUrl))
                return string.Empty;
            string baseUrl = dataUrl; int indx;
            indx = baseUrl.IndexOf("?");
            if (indx > 0)
                baseUrl = baseUrl.Substring(0, indx);
            indx = baseUrl.LastIndexOf("/");
            if (indx >= 0)
                baseUrl = baseUrl.Substring(0, indx);
            return baseUrl;
        }
    }
}
