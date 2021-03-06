﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using XeLib.IO;
using XeLib.Utilities;
using XeLib.Security;
using XeLib.Compression;

namespace XeLib.Bootloaders
{
    public class CXBootloader
    {
        public Stream Stream { get; protected set; }
        public long Origin { get; protected set; }

        public ushort magic; // 0x00
        public ushort version; // 0x02
        public ushort unkWord1; // 0x04
        public ushort unkWord2; // 0x06
        public uint entryPoint; // 0x08
        public uint length; // 0x0c

        public byte[] hmacSalt;

        public CXBootloader(Stream stream) {
            this.Stream = stream;
            this.Origin = stream.Position;

            Read();
        }

        public void Read() {
            var reader = new XeReader(Stream);
            Read(reader);
        }

        public void Read(XeReader reader) {
            reader.Seek(Origin, SeekOrigin.Begin);

            // First read the header
            var header = new byte[0x10];
            reader.Read(header, 0, 0x10);

            // Parse info bits
            magic = BufferUtils.ToUInt16(header, 0x00);
            version = BufferUtils.ToUInt16(header, 0x02);
            unkWord1 = BufferUtils.ToUInt16(header, 0x04);
            unkWord2 = BufferUtils.ToUInt16(header, 0x06);
            entryPoint = BufferUtils.ToUInt32(header, 0x08);
            length = BufferUtils.ToUInt32(header, 0x0c);

            hmacSalt = new byte[0x10];
            reader.Read(hmacSalt, 0, 0x10);

            reader.Seek(Origin + length, SeekOrigin.Begin);
        }

        public static void EncryptDecrypt(Stream input, Stream output, byte[] key, out byte[] digest) {
            byte[] header = new byte[0x20];
            input.Read(header, 0, 0x20);
            output.Write(header, 0, 0x20);

            var length = BufferUtils.ToUInt32(header, 0x0c);

            var data = new byte[length - 0x20];
            input.Read(data, 0, data.Length);

            var hash = XeCrypt.XeCryptHmacSha(key, header, 0x10, 0x10);

            Array.Resize(ref hash, 0x10);

            digest = new byte[0x10];
            Buffer.BlockCopy(hash, 0, digest, 0, 0x10);

            // Decrypt the data
            var rc4 = XeCrypt.XeCryptRc4Key(hash);
            XeCrypt.XeCryptRc4Ecb(rc4, ref data, 0, data.Length);

            output.Write(data, 0, data.Length);
        }

        public void Decrypt(Stream output, byte[] key) {
            byte[] digest;
            Decrypt(output, key, out digest);
        }

        public void Decrypt(Stream output, byte[] key, out byte[] digest) {
            Stream.Seek(Origin, SeekOrigin.Begin);

            byte[] header = new byte[0x20];
            Stream.Read(header, 0, 0x20);
            output.Write(header, 0, 0x20);

            var data = new byte[length - 0x20];
            Stream.Read(data, 0, data.Length);

            var hash = XeCrypt.XeCryptHmacSha(key, header, 0x10, 0x10);

            Array.Resize(ref hash, 0x10);

            digest = new byte[0x10];
            Buffer.BlockCopy(hash, 0, digest, 0, 0x10);

            // Decrypt the data
            var rc4 = XeCrypt.XeCryptRc4Key(hash);
            XeCrypt.XeCryptRc4Ecb(rc4, ref data, 0, data.Length);

            output.Write(data, 0, data.Length);
        }

        public byte[] GetDigestKey(byte[] key) {
            Stream.Seek(Origin + 0x10, SeekOrigin.Begin);

            var hash = XeCrypt.XeCryptHmacSha(key, hmacSalt, 0, 0x10);

            Array.Resize(ref hash, 0x10);

            return hash;
        }

        public string GetName() {
            var buf = new byte[2];
            BufferUtils.FromUInt16(magic, buf);
            return Encoding.ASCII.GetString(buf);
        }

        public string GetFileName() {
            return String.Format("{0}.{1}.bin", GetName(), this.version);
        }
    }
}
