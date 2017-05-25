using hdsdump.f4m;
using hdsdump.flv;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using hdsdump.f4f;

namespace hdsdump {
    public class HDSDownloader {
        public int  maxThreads = 8;

        public static System.DateTime StartedStall;
        private static bool _liveIsStalled = false;
        public static bool LiveIsStalled {
            get { return _liveIsStalled; }
            set {
                if (value && !_liveIsStalled)
                    StartedStall = System.DateTime.Now;
                _liveIsStalled = value;
            }
        }

        static object waitLock    = new object();
        static object waitLockAlt = new object();

        Queue<HDSWorker>             workersWaiting    = new Queue<HDSWorker>();
        Dictionary<int, HDSWorker>   workersActive     = new Dictionary<int, HDSWorker>();
        Queue<HDSWorker>             workersWaitingAlt = new Queue<HDSWorker>();
        Dictionary<int, HDSWorker>   workersActiveAlt  = new Dictionary<int, HDSWorker>();
        Dictionary<Media, uint>      addedIndex        = new Dictionary<Media, uint>();

        public static Dictionary<Media, Queue<TagsStore>> FragmentsData = new Dictionary<Media, Queue<TagsStore>>();

        private const int MAX_LOADED_GRAGMENTS_IN_QUEUE = 5;

        ///<summary>Called on finish by HDSworker thread (after downloading fragment)</summary>
        private void WorkerDone(Media media) {
            lock (waitLock) {
                lock (workersActive) {
                    workersActive.Remove(Thread.CurrentThread.ManagedThreadId);
                }
                Monitor.Pulse(waitLock);
            }
        }

        ///<summary>Called on finish by HDSworker thread (after downloading fragment)</summary>
        private void WorkerDoneAlt(Media media) {
            lock (waitLockAlt) {
                lock (workersActiveAlt) {
                    workersActiveAlt.Remove(Thread.CurrentThread.ManagedThreadId);
                }
                Monitor.Pulse(waitLockAlt);
            }
        }

        private void DownloadThread(Media media) {
            lock (waitLock) {
                while (true) {
                    if (workersActive.Count >= maxThreads) {
                        Monitor.Wait(waitLock);
                    }

                    if (workersWaiting.Count > 0) {
                        if (FragmentsData[media].Count > MAX_LOADED_GRAGMENTS_IN_QUEUE) {
                            Monitor.Wait(waitLock); // wait for writing downloaded gragments
                        }

                        HDSWorker worker = workersWaiting.Dequeue();

                        TagsStore tagsStore = new TagsStore();

                        FragmentsData[media].Enqueue(tagsStore);

                        Thread thread = new Thread(() => worker.DownloadFragment(tagsStore));
                        lock (workersActive) {
                            workersActive[thread.ManagedThreadId] = worker;
                        }
                        thread.Start();

                    } else if (workersActive.Count > 0) {
                        Monitor.Wait(waitLock);

                    }
                    Thread.Sleep(1); // thread own quantum time fulfilled (for decrease CPU load)
                }
            }
        }

        private void DownloadThreadAlt(Media media) {
            lock (waitLockAlt) {
                while (true) {
                    if (workersActiveAlt.Count >= maxThreads) {
                        Monitor.Wait(waitLockAlt);
                    }

                    if (workersWaitingAlt.Count > 0) {
                        if (FragmentsData[media].Count > MAX_LOADED_GRAGMENTS_IN_QUEUE) {
                            Monitor.Wait(waitLockAlt); // wait for writing downloaded gragments
                        }

                        HDSWorker worker = workersWaitingAlt.Dequeue();

                        TagsStore tagsStore = new TagsStore();

                        FragmentsData[media].Enqueue(tagsStore);

                        Thread thread = new Thread(() => worker.DownloadFragment(tagsStore));
                        lock (workersActiveAlt) {
                            workersActiveAlt[thread.ManagedThreadId] = worker;
                        }
                        thread.Start();

                    } else if (workersActiveAlt.Count > 0) {
                        Monitor.Wait(waitLockAlt);

                    }
                    Thread.Sleep(1); // thread own quantum time fulfilled (for decrease CPU load)
                }
            }
        }

        public void AddMediaFragmentToDownload(Media media, uint fragIndex) {
            if (addedIndex.ContainsKey(media) && addedIndex[media] >= fragIndex) return;
            addedIndex[media] = fragIndex;
            if (media.alternate) {
                if (!FragmentsData.ContainsKey(media)) {
                    FragmentsData.Add(media, new Queue<TagsStore>());
                    Thread threadDownloadAlt = new Thread(() => DownloadThreadAlt(media)) {
                        IsBackground = true
                    };
                    threadDownloadAlt.Start();
                }
                workersWaitingAlt.Enqueue(new HDSWorker(new WorkerDoneDelegate(WorkerDoneAlt), media, fragIndex));

            } else {
                if (!FragmentsData.ContainsKey(media)) {
                    FragmentsData.Add(media, new Queue<TagsStore>());
                    Thread threadDownload = new Thread(() => DownloadThread(media)) {
                        IsBackground = true
                    };
                    threadDownload.Start();
                }
                workersWaiting.Enqueue(new HDSWorker(new WorkerDoneDelegate(WorkerDone), media, fragIndex));
            }
        }

        public FLVTag GetNextTag(Media media) {
            return GetNextTag(media, out uint lastTS);
        }

        public FLVTag GetNextTag(Media media, out uint lastTS) {
            lastTS = 0; FLVTag tag = null;
            if (FragmentsData[media].Count > 0) {
                var tagsStore = FragmentsData[media].Peek();
                lock (tagsStore) {
                    if (tagsStore.Count > 0) {
                        lastTS = tagsStore.lastTS;
                        tag = tagsStore.Dequeue();
                    }
                    if (tagsStore.Count < 1 && tagsStore.Complete) {
                        lock (FragmentsData) {
                            FragmentsData[media].Dequeue(); // delete empty TagsStore
                        }
                    }
                }
            }
            return tag;
        }

        public void DetermineAudioVideo(Media media, ref bool isDetermined, ref bool hasVideo, ref bool hasAudio) {
            if (!isDetermined) {
                if (FragmentsData.ContainsKey(media) && FragmentsData[media].Count > 0) {
                    var tagStore = FragmentsData[media].Peek();
                    if (tagStore.Complete) {
                        Program.ClearLine();
                        hasVideo = tagStore.hasVideo;
                        hasAudio = tagStore.hasAudio;
                        isDetermined = true;
                        string videoInfo, audioInfo;
                        videoInfo = hasVideo ? "<c:DarkGreen>" + FLVTagVideo.CodecToString(tagStore.VideoCodec) : "<c:DarkRed>None";
                        if (hasAudio) {
                            audioInfo =
                                "<c:DarkGreen>" + FLVTagAudio.FormatToString(tagStore.AudioFormat) +
                                " " + FLVTagAudio.RateToString(tagStore.AudioRate) +
                                " " + (tagStore.AudioChannels == FLVTagAudio.Channels.MONO ? "Mono" : "Stereo");
                        } else {
                            audioInfo = "<c:DarkRed>None";
                        }
                        Program.Message(string.Format("<c:DarkCyan>{0}: {1}", "Video", videoInfo));
                        Program.Message(string.Format("<c:DarkCyan>{0}: {1}", "Audio", audioInfo));
                        if (tagStore.isAkamaiEncrypted) {
                            Program.Message("<c:Yellow>Encryption: Akamai DRM");
                        }
                    }
                }
            }
        }

        public bool TagsAvaliable(Media media) {
            if (FragmentsData.ContainsKey(media) && FragmentsData[media].Count > 0) {
                return true;
            }
            if (media.alternate) {
                lock (workersActiveAlt) {
                    if (workersWaitingAlt.Count > 0 || workersActiveAlt.Values.Any(a => a.media == media)) {
                        return true;
                    }
                }
            } else {
                lock (workersActive) {
                    if (workersWaiting.Count > 0 || workersActive.Values.Any(a => a.media == media)) {
                        return true;
                    }
                }
            }
            // all downloaded and decoded
            return false;
        }

        public FLVTag SeekAudioByTime(Media media, uint time) {
            while (TagsAvaliable(media)) {
                var tag = GetNextTag(media, out uint lastTS);
                if (tag == null)
                    Thread.Sleep(100);
                else if ((lastTS >= time) && tag is FLVTagAudio) {
                    var tagsStore  = FragmentsData[media].Peek();
                    var lastHeader = tag;
                    foreach (var t in tagsStore.ToArray()) {
                        if (t.Timestamp > time)
                            break;
                        if (t is FLVTagAudio) {
                            var audio = t as FLVTagAudio;
                            if (audio.SoundFormat == FLVTagAudio.Format.AAC) {
                                if (audio.IsAACSequenceHeader)
                                    lastHeader = t;
                            } else {
                                lastHeader = t;
                            }
                        }
                    }
                    return lastHeader;
                }
            }
            return null;
        }
    }

    public class TagsStore: Queue<FLVTag> {
        public bool Complete = false;
        public bool hasVideo = false;
        public bool hasAudio = false;
        public uint lastTS   = 0;
        public bool isAkamaiEncrypted = false;
        public AdobeFragmentRandomAccessBox ARFA;
        public FLVTagAudio.Format   AudioFormat;
        public FLVTagAudio.Channels AudioChannels;
        public FLVTagAudio.Rate     AudioRate;
        public FLVTagVideo.Codec       VideoCodec;
    }

}
