/*
 * Copyright (c) 2006, the libsecondlife development team
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the Second Life Reverse Engineering Team nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using libsecondlife;

namespace libsecondlife.UDP
{
    public class MalformedDataException : ApplicationException
    {
        public MalformedDataException() { }

        public MalformedDataException(string Message)
            : base(Message)
        {
            this.Source = "Packet decoding";
        }
    }
    
    public abstract class Header
    {
        public byte[] Data;
        public byte Flags
        {
            get { return Data[0]; }
            set { Data[0] = value; }
        }
        public bool Reliable
        {
            get { return (Data[0] & Helpers.MSG_RELIABLE) != 0; }
            set { if (value) { Data[0] += (byte)Helpers.MSG_RELIABLE; } else { Data[0] -= (byte)Helpers.MSG_RELIABLE; } }
        }
        public bool Resent
        {
            get { return (Data[0] & Helpers.MSG_RESENT) != 0; }
            set { if (value) { Data[0] += (byte)Helpers.MSG_RESENT; } else { Data[0] -= (byte)Helpers.MSG_RESENT; } }
        }
        public bool Zerocoded
        {
            get { return (Data[0] & Helpers.MSG_ZEROCODED) != 0; }
            set { if (value) { Data[0] += (byte)Helpers.MSG_ZEROCODED; } else { Data[0] -= (byte)Helpers.MSG_ZEROCODED; } }
        }
        public bool AppendedAcks
        {
            get { return (Data[0] & Helpers.MSG_APPENDED_ACKS) != 0; }
            set { if (value) { Data[0] += (byte)Helpers.MSG_APPENDED_ACKS; } else { Data[0] -= (byte)Helpers.MSG_APPENDED_ACKS; } }
        }
        public ushort Sequence
        {
            get { return (ushort)((Data[2] << 8) + Data[3]); }
            set { Data[2] = (byte)(value % 256); Data[3] = (byte)(value >> 8); }
        }
        public abstract ushort ID { get; set; }
        public abstract PacketFrequency Frequency { get; }
        public uint[] AckList;
        
        protected void CreateAckList(byte[] bytes, ref int packetEnd)
        {
            if (AppendedAcks)
            {
                try
                {
                    int count = bytes[packetEnd];
                    AckList = new uint[count];
                    
                    for (int i = 0; i < count; i++)
                    {
                        AckList[i] = (ushort)(bytes[(packetEnd - i * 4) - 1] | (bytes[packetEnd - i * 4] << 8));
                    }
                }
                catch (Exception)
                {
                    AckList = new uint[0];
                    throw new MalformedDataException();
                }
            }
            else
            {
                AckList = new uint[0];
            }
        }

        public static Header BuildHeader(byte[] bytes, ref int pos, ref int packetEnd)
        {
            if (bytes[4] == 0xFF)
            {
                if (bytes[5] == 0xFF)
                {
                    return new LowHeader(bytes, ref pos, ref packetEnd);
                }
                else
                {
                    return new MediumHeader(bytes, ref pos, ref packetEnd);
                }
            }
            else
            {
                return new HighHeader(bytes, ref pos, ref packetEnd);
            }
        }
    }

    public class LowHeader : Header
    {
        public override ushort ID
        {
            get { return (ushort)((Data[6] << 8) + Data[7]); }
            set { Data[6] = (byte)(value >> 8); Data[7] = (byte)(value % 256); }
        }
        public override PacketFrequency Frequency { get { return PacketFrequency.Low; } }

        public LowHeader()
        {
            Data = new byte[8];
            Data[4] = Data[5] = 0xFF;
            AckList = new uint[0];
        }

        public LowHeader(byte[] bytes, ref int pos, ref int packetEnd)
        {
            if (bytes.Length < 8) { throw new MalformedDataException(); }
            Data = new byte[8];
            Array.Copy(bytes, Data, 8);

            if ((bytes[0] & Helpers.MSG_ZEROCODED) != 0 && bytes[6] == 0)
            {
                if (bytes[7] == 1)
                {
                    Data[7] = bytes[8];
                }
                else
                {
                    throw new MalformedDataException();
                }
            }

            pos = 8;
            CreateAckList(bytes, ref packetEnd);
        }
    }

    public class MediumHeader : Header
    {
        public override ushort ID
        {
            get { return (ushort)Data[5]; }
            set { Data[5] = (byte)value; }
        }
        public override PacketFrequency Frequency { get { return PacketFrequency.Medium; } }

        public MediumHeader()
        {
            Data = new byte[6];
            Data[4] = 0xFF;
            AckList = new uint[0];
        }

        public MediumHeader(byte[] bytes, ref int pos, ref int packetEnd)
        {
            if (bytes.Length < 6) { throw new MalformedDataException(); }
            Data = new byte[6];
            Array.Copy(bytes, Data, 6);
            pos = 6;
            CreateAckList(bytes, ref packetEnd);
        }
    }

    public class HighHeader : Header
    {
        public override ushort ID
        {
            get { return (ushort)Data[4]; }
            set { Data[4] = (byte)value; }
        }
        public override PacketFrequency Frequency { get { return PacketFrequency.High; } }

        public HighHeader()
        {
            Data = new byte[5];
            AckList = new uint[0];
        }

        public HighHeader(byte[] bytes, ref int pos, ref int packetEnd)
        {
            if (bytes.Length < 5) { throw new MalformedDataException(); }
            Data = new byte[5];
            Array.Copy(bytes, Data, 5);
            pos = 5;
            CreateAckList(bytes, ref packetEnd);
        }
    }
