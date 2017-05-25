/* Ported to .NET from AdobeHDS.php by K-S-V https://github.com/K-S-V/Scripts/blob/master/AdobeHDS.php
 * GNU General Public License v3.0
 * Big snx 2 K-S-V
 */
using System;
using System.Security.Cryptography;
using System.IO;
using System.Diagnostics.CodeAnalysis;

namespace hdsdump {
    public sealed class AkamaiDecryptor: IDisposable {
        int  ecmID;
        uint ecmTimestamp;
        int  ecmVersion;
        int  kdfVersion;
        int  dccAccReserved;
        int? prevEcmID;

        public bool debug;
        uint   decryptBytes;
        string lastKeyUrl;
        public string sessionID;
        public byte[] sessionKey;
        string sessionKeyUrl;

        byte[] packetIV;
        byte[] packetKey;
        byte[] packetSalt;
        byte[] saltAesKey;
        // KDF constants
        byte[] hmacKey   = Unhexlify("3b27bdc9e00fd5995d60a1ee0aa057a9f1416ed085b21762110f1c2204ddf80ec8caab003070fd43baafdde27aeb3194ece5c1adff406a51185eb5dd7300c058");
        byte[] hmacData1 = Unhexlify("d1ba6371c56ce6b498f1718228b0aa112f24a47bcad757a1d0b3f4c2b8bd637cb8080d9c8e7855b36a85722a60552a6c00");
        byte[] hmacData2 = Unhexlify("d1ba6371c56ce6b498f1718228b0aa112f24a47bcad757a1d0b3f4c2b8bd637cb8080d9c8e7855b36a85722a60552a6c01");
        HMACSHA1 sha1;
        HMACSHA1 shaSalt;
        HMACSHA1 finalsha;
        AesCryptoServiceProvider aesProvider;

        public AkamaiDecryptor() {
            lastKeyUrl    = "";
            sessionID     = "";
            sessionKeyUrl = "";
            sessionKey    = null;
            sha1     = new HMACSHA1(hmacKey);
            shaSalt  = new HMACSHA1();
            finalsha = new HMACSHA1();
            aesProvider = new AesCryptoServiceProvider() {
                Mode    = CipherMode.CBC,
                Padding = PaddingMode.Zeros
            };
            InitDecryptor();
        }

        public void InitDecryptor() {
            decryptBytes   = 0;
            dccAccReserved = 0;
            ecmID          = 0;
            ecmTimestamp   = 0;
            ecmVersion     = 0;
            kdfVersion     = 0;
            packetIV       = null;
            prevEcmID      = null;
            saltAesKey     = null;
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        private byte[] AesDecrypt(byte[] encrypted, byte[] Key, byte[] IV) {
            byte[] plain;
            int count;
            using (MemoryStream mStream = new MemoryStream(encrypted)) {
                using (CryptoStream cryptoStream = new CryptoStream(mStream, aesProvider.CreateDecryptor(Key, IV), CryptoStreamMode.Read)) {
                    plain = new byte[encrypted.Length];
                    count = cryptoStream.Read(plain, 0, plain.Length);
                }
            }
            byte[] returnval = new byte[count];
            Buffer.BlockCopy(plain, 0, returnval, 0, count);
            return returnval;
        }

        public void KDF() {
            // Decrypt packet salt
            if (ecmID != prevEcmID) {
                byte[] saltHmacKey = sha1.ComputeHash(F4FOldMethod.AppendBuf(sessionKey, packetIV));
                if (debug) Program.DebugLog("SaltHmacKey  : " + Hexlify(saltHmacKey));
                shaSalt.Key = saltHmacKey;
                saltAesKey  = F4FOldMethod.BlockCopy(shaSalt.ComputeHash(hmacData1), 0, 16);
                if (debug) Program.DebugLog("SaltAesKey   : " + Hexlify(saltAesKey));
                prevEcmID = ecmID;
            }
            if (debug) Program.DebugLog("EncryptedSalt: " + Hexlify(packetSalt));

            byte[] decryptedSalt = AesDecrypt(packetSalt, saltAesKey, packetIV);
            if (decryptedSalt==null) {
                Program.Quit("<c:Red>Error ocurred while decription salt of fagment.");
            }
            if (debug) Program.DebugLog("DecryptedSalt: " + Hexlify(decryptedSalt));
            decryptBytes = F4FOldMethod.ReadInt32(ref decryptedSalt, 0);
            if (debug) Program.DebugLog("DecryptBytes : " + decryptBytes);
            byte[] decryptedSalt2 = F4FOldMethod.BlockCopy(decryptedSalt, 4, 16);
            if (debug) Program.DebugLog("DecryptedSalt: " + Hexlify(decryptedSalt2));
            // Generate final packet decryption key
            byte[] finalHmacKey = sha1.ComputeHash(decryptedSalt2);
            if (debug) Program.DebugLog("FinalHmacKey : " + Hexlify(finalHmacKey));
            finalsha.Key = finalHmacKey;
            packetKey = F4FOldMethod.BlockCopy(finalsha.ComputeHash(hmacData2), 0, 16);
            if (debug) Program.DebugLog("PacketKey    : " + Hexlify(packetKey));
        }

        public void DecryptFLVTag(flv.FLVTag tag, string baseUrl, string auth) {
            if      (tag.Type == flv.FLVTag.TagType.AKAMAI_ENC_AUDIO) tag.Type = flv.FLVTag.TagType.AUDIO;
            else if (tag.Type == flv.FLVTag.TagType.AKAMAI_ENC_VIDEO) tag.Type = flv.FLVTag.TagType.VIDEO;
            else return;
            tag.Data = Decrypt(tag.Data, 0, baseUrl, auth);
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public byte[] Decrypt(byte[] data, long pos, string baseUrl, string auth) {
            if (debug) Program.DebugLog("\n----- Akamai Decryption Start -----");
            byte[] decryptedData = new byte[0];
            // Parse packet header
            using (MemoryStream ms = new MemoryStream(data)) {
                ms.Position = pos;
                using (HDSBinaryReader br = new HDSBinaryReader(ms)) {
                    int b = br.ReadByte();
                    ecmVersion = (b >> 4);
                    if (ecmVersion != 11)
                        ecmVersion = b;
                    ecmID          = br.ReadInt32();
                    ecmTimestamp   = (uint)br.ReadInt32();
                    kdfVersion     = br.ReadInt16();
                    dccAccReserved = br.ReadByte();

                    if (debug) Program.DebugLog("ECM Version  : " + ecmVersion + ", ECM ID: " + ecmID + ", ECM Timestamp: " + ecmTimestamp + ", KDF Version: " + kdfVersion + ", DccAccReserved: " + dccAccReserved);
                    b = br.ReadByte();

                    bool iv  = ((b & 2) > 0);
                    bool key = ((b & 4) > 0);

                    if (iv) {
                        packetIV = br.ReadBytes(16);
                        if (debug) Program.DebugLog("PacketIV     : " + Hexlify(packetIV));
                    }

                    if (key) {
                        sessionKeyUrl = br.ReadString();
                        if (debug) Program.DebugLog("SessionKeyUrl: " + sessionKeyUrl);
                        string keyPath = sessionKeyUrl.Substring(sessionKeyUrl.LastIndexOf('/'));
                        string keyUrl  = HTTP.JoinUrl(baseUrl, keyPath) + auth;

                        // Download key file if required
                        if (sessionKeyUrl != lastKeyUrl) {
                            if ((baseUrl.Length == 0) && (sessionKey.Length == 0)) {
                                if (debug) Program.DebugLog("Unable to download session key without manifest url. you must specify it manually using 'adkey' switch.");
                            } else {
                                if (baseUrl.Length > 0) {
                                    if (debug) Program.DebugLog("Downloading new session key from " + keyUrl);
                                    byte[] downloadedData = HTTP.TryGETData(keyUrl, out int retCode, out string status);
                                    if (retCode == 200) {
                                        sessionID = "_" + keyPath.Substring("/key_".Length);
                                        sessionKey = downloadedData;
                                    } else {
                                        if (debug) Program.DebugLog("Failed to download new session key, Status: " + status + " ("+retCode+")");
                                        sessionID = "";
                                    }
                                }
                            }
                            lastKeyUrl = sessionKeyUrl;
                            if (sessionKey == null || sessionKey.Length == 0) {
                                Program.Quit("Failed to download akamai session decryption key");
                            }
                        }
                    }

                    if (debug) Program.DebugLog("SessionKey   : " + Hexlify(sessionKey));

                    if (sessionKey == null || sessionKey.Length < 1)
                        Program.Quit("ERROR: Fragments can't be decrypted properly without corresponding session key.");

                    byte reserved; byte[] reservedBlock1, reservedBlock2, encryptedData, lastBlockData;

                    reserved       = br.ReadByte();
                    packetSalt     = br.ReadBytes(32);
                    reservedBlock1 = br.ReadBytes(20);
                    reservedBlock2 = br.ReadBytes(20);
                    if (debug) Program.DebugLog("ReservedByte : " + reserved + ", ReservedBlock1: " + Hexlify(reservedBlock1) + ", ReservedBlock2: " + Hexlify(reservedBlock2));

                    // Generate packet decryption key
                    KDF();

                    // Decrypt packet data
                    encryptedData = br.ReadBytes((int)decryptBytes);
                    lastBlockData = br.ReadToEnd();
                    if (decryptBytes > 0)
                        decryptedData = AesDecrypt(encryptedData, packetKey, packetIV);
                    decryptedData = F4FOldMethod.AppendBuf(decryptedData, lastBlockData);
                    if (debug) {
                        Program.DebugLog("EncryptedData: " + Hexlify(encryptedData, 64));
                        Program.DebugLog("DecryptedData: " + Hexlify(decryptedData, 64));
                        Program.DebugLog("----- Akamai Decryption End -----\n");
                    }
                } // using (HDSBinaryReader br = new HDSBinaryReader(ms))
            } // using (MemoryStream ms = new MemoryStream(data))

            return decryptedData;
        }

        public void Dispose() {
            sha1.Dispose();
            aesProvider.Dispose();
            finalsha.Dispose();
            shaSalt.Dispose();
        }

        public static string Hexlify(byte[] ba, int len = 0) {
            if (ba == null || ba.Length == 0) return string.Empty;
            if ((len == 0) || (len > ba.Length)) len = ba.Length;
            System.Text.StringBuilder hex = new System.Text.StringBuilder(len * 2);
            for (int i = 0; i < len; i++)
                hex.AppendFormat("{0:x2}", ba[i]);
            return hex.ToString();
        }

        public static byte[] Unhexlify(string hexString) {
            if (string.IsNullOrEmpty(hexString)) return new byte[0];
            byte[] HexAsBytes = new byte[hexString.Length / 2];
            for (int index = 0; index < HexAsBytes.Length; index++) {
                string byteValue = hexString.Substring(index * 2, 2);
                HexAsBytes[index] = byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber);
            }
            return HexAsBytes;
        }

    }
}
