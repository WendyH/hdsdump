using System;
using System.Collections.Generic;
using System.IO;

namespace hdsdump.flv {
    public class FLV: IDisposable {
        public string outFile    = "hdsdump.flv";
        public bool   play       = false;
        public bool   usePipe    = false;
        public uint   Filesize      = 0;
        public uint   LastTimestamp = 0;
        public uint   SizeAudio     = 0;
        public uint   SizeVideo     = 0;

        public bool FLVHeaderWritten = false;
        public bool hasAudio = false;
        public bool hasVideo = false;
        public int  Frames   = 0;

        public FLVTagScriptBody onMetaData;

        private FileStream   Stream = null;
        private BinaryWriter Writer = null;
        private Microsoft.Win32.SafeHandles.SafeFileHandle pipeHandle = null;

        private DecoderLastState DecoderState = new DecoderLastState();
        private object lock4write = new object();

        private bool AAC_HeaderWritten = false;
        private bool AVC_HeaderWritten = false;

        public void Write(FLVTag tag) {
            if (tag == null || tag.Data == null || tag.Data.Length == 0) return;

            if (tag.Filter)
                    throw new InvalidOperationException("This media encrypted with Adobe Access DRM. Not supported. Sorry. :`(");

            lock (lock4write) {
                if (!FLVHeaderWritten) {
                    WriteFlvHeader();
                }

                if (tag is FLVTagAudio) {
                    var tagAudio = tag as FLVTagAudio;
                    if (!AAC_HeaderWritten && tagAudio.isAACSequenceHeader)
                        AAC_HeaderWritten = true;
                    else if (AAC_HeaderWritten && tagAudio.isAACSequenceHeader)
                        return;

                    SizeAudio += tag.DataSize;

                } else if (tag is FLVTagVideo) {
                    var tagVideo = tag as FLVTagVideo;
                    if (!AVC_HeaderWritten && tagVideo.codecID == FLVTagVideo.CodecID.AVC && tagVideo.avcPacketType == FLVTagVideo.AVCPacketType.SEQUENCE_HEADER)
                        AVC_HeaderWritten = true;
                    else if (AVC_HeaderWritten && tagVideo.codecID == FLVTagVideo.CodecID.AVC && tagVideo.avcPacketType == FLVTagVideo.AVCPacketType.SEQUENCE_HEADER)
                        return;

                    SizeVideo += tag.DataSize;
                }

                HDSDumper.FixTimestamp(DecoderState, tag);

                if (LastTimestamp < tag.Timestamp)
                    LastTimestamp = tag.Timestamp;

                WriteData(tag.GetBytes());

                Frames++;
            }
        }

        private void WriteData(byte[] data, FileMode fileMode = FileMode.Append) {
            try {
                if (Program.redir2Prog != null) {

                    if (Program.redir2Prog.HasExited)
                        throw new InvalidOperationException("Redirected process was exited");

                    Stream stream = Program.redir2Prog.StandardInput.BaseStream;
                    stream.Write(data, 0, data.Length);
                    stream.Flush();

                } else if (play || Program.isRedirected) {
                    Stream stdout = Console.OpenStandardOutput();
                    stdout.Write(data, 0, data.Length);
                    stdout.Flush();

                } else {
                    if (Writer == null) {
                        if (usePipe) {
                            if (Stream != null) Stream.Close();
                            if (pipeHandle != null) pipeHandle.Close();
                            pipeHandle = NativeMethods.CreateFile(outFile, NativeMethods.GENERIC_WRITE, 0, IntPtr.Zero, NativeMethods.OPEN_EXISTING, NativeMethods.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                            if (pipeHandle.IsInvalid)
                                throw new InvalidOperationException("Cannot create pipe for writting.");
                            Stream = new FileStream(pipeHandle, FileAccess.Write, 4096, true);
                            Writer = new BinaryWriter(Stream);
                        } else {
                            if (Stream != null) this.Stream.Close();
                            if (pipeHandle != null) pipeHandle.Close();
                            Stream = new FileStream(outFile, fileMode);
                            Writer = new BinaryWriter(Stream);
                        }
                    }
                    Writer.Write(data, 0, data.Length);
                    Writer.Flush();
                }
                Filesize += (uint)data.Length;

            } catch (Exception e) {
                Program.DebugLog("Error while writing to file! Message: " + e.Message);
                Program.DebugLog("Exception: " + e.ToString());
                throw;
            }
        }

        private void WriteFlvHeader() {
            Filesize  = 0;
            Frames    = 0;
            SizeAudio = 0;
            SizeVideo = 0;
            FLVHeader flvHeader = new FLVHeader() {
                HasAudio = hasAudio,
                HasVideo = hasVideo
            };
            WriteData(flvHeader.Data, FileMode.Create);
            WriteMetadata();
            FLVHeaderWritten = true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        private void WriteMetadata() {
            if (onMetaData == null) return;

            byte[] data = onMetaData.ToByteArray();

            uint mediaMetadataSize = (uint)data.Length;

            using (var stream = new MemoryStream()) {
                using (HDSBinaryWriter bw = new HDSBinaryWriter(stream)) {
                    bw.WriteByte(Constants.SCRIPT_DATA);
                    bw.WriteUInt24(mediaMetadataSize);
                    bw.WriteUInt24(0);
                    bw.WriteUInt32(0);
                    bw.Write(data);
                    bw.WriteUInt32(FLVTag.TAG_HEADER_BYTE_COUNT + mediaMetadataSize);
                    WriteData(stream.ToArray());
                }
            }
        }

        public void FixFileMetadata() {
            if (play || Program.redir2Prog != null || Program.isRedirected || !File.Exists(outFile)) return;
            if (Writer != null) {
                Writer.Flush();
                Writer.Close();
                Writer = null;
            }
            using (var fs = new FileStream(outFile, FileMode.Open, FileAccess.ReadWrite)) {
                if (fs.Length < 20) return;
                fs.Seek(13, SeekOrigin.Begin);
                int b = fs.ReadByte();
                if (b != Constants.SCRIPT_DATA) return;
                uint dataSize = ReadUint24(fs);
                fs.Seek(7, SeekOrigin.Current);
                long posData = fs.Position;
                onMetaData = new FLVTagScriptBody(fs);
                if (onMetaData.Data.ContainsKey("duration")) {
                    onMetaData.Data["duration"] = (double)(LastTimestamp / 1000);
                    byte[] newData = onMetaData.ToByteArray();
                    if (newData.Length <= dataSize) {
                        fs.Position = posData;
                        fs.Write(newData, 0, newData.Length);
                    }
                }
            }
        }

        private uint ReadUint24(Stream stream) {
            return (((uint)stream.ReadByte()) << 16) |
                   (((uint)stream.ReadByte()) << 8 ) |
                    ((uint)stream.ReadByte());
        }

        #region IDisposable Support
        private bool disposedValue = false; // Для определения избыточных вызовов

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (Stream != null) Stream.Dispose();
                    if (Writer != null) Writer.Dispose();
                    if (pipeHandle != null) pipeHandle.Dispose();
                }
                Stream = null;
                Writer = null;
                pipeHandle = null;
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion

    }

    public class QueueTags : Queue<FLVTag> {
        public uint MaxTS = 0;

        /// <summary>Add FLVTag to the end of queue</summary>
        public new void Enqueue(FLVTag tag) {
            base.Enqueue(tag);
            if (MaxTS < tag.Timestamp)
                MaxTS = tag.Timestamp;
        }

    }
}
