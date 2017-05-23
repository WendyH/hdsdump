using hdsdump.f4f;
using hdsdump.f4m;
using hdsdump.flv;
using System;
using System.Collections.Generic;

namespace hdsdump {
    public delegate void WorkerDoneDelegate(Media media);

    public class HDSWorker {
        private static bool encryptionInformed = false;

        WorkerDoneDelegate Done;
        public Media       media;
        public uint        fragIndex;

        // CONSTRUCTOR
        public HDSWorker(WorkerDoneDelegate workerDoneArg, Media media, uint fragIndex) {
            this.Done      = workerDoneArg;
            this.media     = media;
            this.fragIndex = fragIndex;
        }

        public static int MaxRetriesLoad = 5;
        private static Dictionary<Media, AdobeFragmentRandomAccessBox> lastARFAs = new Dictionary<Media, AdobeFragmentRandomAccessBox>();

        public void DownloadFragment(TagsStore tagsStore) {
            string fragmentUrl = media.GetFragmentUrl(fragIndex);

            Program.DebugLog("Fragment Url: " + fragmentUrl);

            byte[] data = HTTP.TryGETData(fragmentUrl, out int retCode, out string status);
            int retries = 0;
            while (retCode >= 500 && retries <= MaxRetriesLoad) {
                System.Threading.Thread.Sleep(1000);
                retries++;
                data = HTTP.TryGETData(fragmentUrl, out retCode, out status);
            }
            if (retCode != 200) {
                string msg = "Download fragment failed " + fragIndex + "/" + media.TotalFragments + " code: " + retCode + " status: " + status;
                Program.DebugLog(msg);
                if (Program.verbose)
                    Program.Message(msg);
                if (media.Bootstrap.live) {
                    //media.CurrentFragmentIndex = media.TotalFragments;
                    tagsStore.Complete = true;
                    Done(media);
                    return;
                } else {
                    throw new InvalidOperationException(status);
                }
            }

            Program.DebugLog("Downloaded: fragment=" + fragIndex + "/" + media.TotalFragments + " lenght: " + data.Length);

            var boxes = Box.GetBoxes(data);

            if (boxes.Find(i => i.Type == F4FConstants.BOX_TYPE_MDAT) is MediaDataBox mdat) {
                lock (tagsStore) {
                    FLVTag.GetVideoAndAudioTags(tagsStore, mdat.data);
                    tagsStore.ARFA = boxes.Find(i => i.Type == F4FConstants.BOX_TYPE_AFRA) as AdobeFragmentRandomAccessBox;
                    tagsStore.Complete = true;
                    if (!encryptionInformed && tagsStore.isAkamaiEncrypted) {
                        Program.Message("<c:Yellow>Encryption: Akamai DRM");
                        encryptionInformed = true;
                    }
                }
                HDSDownloader.LiveIsStalled = false;

            } else if (media.Bootstrap.live) {
                HDSDownloader.LiveIsStalled = true;

            } else {
                throw new InvalidOperationException("No found mdat box in fragment " + fragIndex + "/" + media.TotalFragments);
            }

            if (Program.verbose)
                 Program.Message(string.Format("Media: {0}  Downloaded: {1}  Data size: {2}", media.label, fragIndex, data.Length));

            media.CurrentFragmentIndex++;
            media.Downloaded++;

            Done(media);
        }



    }
}
