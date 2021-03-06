﻿using PakReader.Parsers.Objects;
using PakReader.Parsers.PropertyTagData;
using System;
using System.IO;
using System.Linq;

namespace PakReader.Parsers.Class
{
    public sealed class USoundWave : UObject
    {
        public bool bCooked;
        /** Whether this sound can be streamed to avoid increased memory usage. If using Stream Caching, use Loading Behavior instead to control memory usage. */
        public bool bStreaming = false;
        /** Uncompressed wav data 16 bit in mono or stereo - stereo not allowed for multichannel data */
        public FByteBulkData RawData;
        /** GUID used to uniquely identify this node so it can be found in the DDC */
        public FGuid CompressedDataGuid;
        /** Set to true for programmatically generated audio. */
        public FFormatContainer[] CompressedFormatData;
        /** Format in which audio chunks are stored. */
        public FName AudioFormat;
        /** audio data. */
        public FStreamedAudioChunk[] Chunks;
        /** Cached sample rate for displaying in the tools */
        //public int SampleRate;
        /** Number of channels of multichannel data; 1 or 2 for regular mono and stereo files */
        //public int NumChannels;

        byte[] sound;
        public byte[] Sound
        {
            get
            {
                if (sound == null)
                {
                    if (!this.bStreaming)
                    {
                        if (this.bCooked && this.CompressedFormatData.Length > 0)
                            sound = this.CompressedFormatData[0].Data.Data;
                        else if (this.RawData.Data != null)
                            sound = this.RawData.Data;
                    }
                    else if (this.bStreaming && this.Chunks != null)
                    {
                        sound = new byte[this.Chunks.Sum(x => x.AudioDataSize)];
                        int offset = 0;
                        for (int i = 0; i < this.Chunks.Length; i++)
                        {
                            Buffer.BlockCopy(this.Chunks[i].BulkData.Data, 0, sound, offset, this.Chunks[i].AudioDataSize);
                            offset += this.Chunks[i].AudioDataSize;
                        }
                    }
                }
                return sound;
            }
        }

        internal USoundWave(PackageReader reader, Stream ubulk, long ubulkOffset) : base(reader)
        {
            bCooked = reader.ReadInt32() != 0;
            if (this.TryGetValue("bStreaming", out var v) && v is BoolProperty b)
                bStreaming = b.Value;

            if (!bStreaming)
            {
                if (bCooked)
                {
                    CompressedFormatData = new FFormatContainer[reader.ReadInt32()];
                    for (int i = 0; i < CompressedFormatData.Length; i++)
                    {
                        CompressedFormatData[i] = new FFormatContainer(reader, ubulk, ubulkOffset);
                    }
                    AudioFormat = CompressedFormatData[^1].FormatName;
                    CompressedDataGuid = new FGuid(reader);
                }
                else
                {
                    RawData = new FByteBulkData(reader, ubulk, ubulkOffset);
                    CompressedDataGuid = new FGuid(reader);
                }
            }
            else
            {
                CompressedDataGuid = new FGuid(reader);
                Chunks = new FStreamedAudioChunk[reader.ReadInt32()];
                AudioFormat = reader.ReadFName();
                for (int i = 0; i < Chunks.Length; i++)
                {
                    Chunks[i] = new FStreamedAudioChunk(reader, ubulk, ubulkOffset);
                }
            }
        }
    }
}
