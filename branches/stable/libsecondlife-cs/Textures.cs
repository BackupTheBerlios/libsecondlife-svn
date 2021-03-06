/*
 * Copyright (c) 2006, Second Life Reverse Engineering Team
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
using System.Collections.Generic;
using System.Text;

namespace libsecondlife
{
    /// <summary>
    /// 
    /// </summary>
    public enum Bumpiness
    {
        None = 0,
        Brightness = 1,
        Darkness = 2,
        Woodgrain = 3,
        Bark = 4,
        Bricks = 5,
        Checker = 6,
        Concrete = 7,
        Crustytile = 8,
        Cutstone = 9,
        Discs = 10,
        Gravel = 11,
        Petridish = 12,
        Siding = 13,
        Stonetile = 14,
        Stucco = 15,
        Suction = 16,
        Weave = 17
    }

    /// <summary>
    /// 
    /// </summary>
    public enum Shininess
    {
        None = 0,
        Low = 0x40,
        Medium = 0x80,
        High = 0xC0
    }

    /// <summary>
    /// 
    /// </summary>
    public enum Mapping
    {
        Default = 0,
        Planar = 2
    }

    /// <summary>
    /// 
    /// </summary>
    public class TextureEntry
    {
        /// <summary></summary>
        public TextureEntryFace DefaultTexture;
        /// <summary></summary>
        public Dictionary<uint, TextureEntryFace> FaceTextures;

        /// <summary>
        /// 
        /// </summary>
        public TextureEntry()
        {
            FaceTextures = new Dictionary<uint, TextureEntryFace>();
            DefaultTexture = new TextureEntryFace(null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pos"></param>
        public TextureEntry(byte[] data, int pos, int length)
        {
            FromBytes(data, pos, length);
        }

        public TextureEntryFace GetFace(uint index)
        {
            if (FaceTextures.ContainsKey(index))
                return FaceTextures[index];
            else
                return DefaultTexture;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public TextureEntryFace SetFace(uint index)
        {
            if (!FaceTextures.ContainsKey(index))
                FaceTextures[index] = new TextureEntryFace(this.DefaultTexture);

            return FaceTextures[index];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            byte[] bytes = new byte[0];
            return bytes;
        }

        private bool ReadFaceBitfield(byte[] data, ref int pos, ref uint faceBits, ref uint bitfieldSize)
        {
            faceBits = 0;
            bitfieldSize = 0;

            if (pos >= data.Length)
                return false;

            byte b = 0;
            do
            {
                b = data[pos];
                faceBits = (faceBits << 7) | (uint)(b & 0x7F);
                bitfieldSize += 7;
                pos++;
            }
            while ((b & 0x80) != 0);

            return (faceBits != 0);
        }

        private float DequantizeSigned(byte[] byteArray, int pos, float upper)
        {
            short value = (short)(byteArray[pos] | (byteArray[pos + 1] << 8));
            float QV = (float)value;
            float QF = upper / 32767.0F;
            return (float)(QV * QF);
        }

        private float RepeatFloat(byte[] data, int pos)
        {
            return DequantizeSigned(data, pos, 101.0F) + 1.0F;
        }

        private float OffsetFloat(byte[] data, int pos)
        {
            return DequantizeSigned(data, pos, 1.0F);
        }

        private float RotationFloat(byte[] data, int pos)
        {
            return DequantizeSigned(data, pos, 359.995F);
        }

        private void FromBytes(byte[] data, int pos, int length)
        {
            FaceTextures = new Dictionary<uint, TextureEntryFace>();
            DefaultTexture = new TextureEntryFace(null);

            if (length == 0) 
                return;  // No TextureEntry to process

            uint BitfieldSize = 0;
            uint faceBits = 0;
            int i = pos;

            //Read TextureID ---------------------------------------
            DefaultTexture.TextureID = new LLUUID(data, i);
            i += 16;

            while (ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                LLUUID tmpUUID = new LLUUID(data, i);
                i += 16;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).TextureID = tmpUUID;
            }
            //Read RGBA --------------------------------------------
            DefaultTexture.RGBA = (uint)(data[i] + (data[i + 1] << 8) + (data[i + 2] << 16) + (data[i + 3] << 24));
            i += 4;

            while (ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                uint tmpUint = (uint)(data[i] + (data[i + 1] << 8) + (data[i + 2] << 16) + (data[i + 3] << 24));
                i += 4;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).RGBA = tmpUint;
            }
            //Read RepeatU -----------------------------------------
            DefaultTexture.RepeatU = RepeatFloat(data, i);
            i += 2;

            while (ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                float tmpFloat = RepeatFloat(data, i);
                i += 2;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).RepeatU = tmpFloat;
            }
            //Read RepeatV -----------------------------------------
            DefaultTexture.RepeatV = RepeatFloat(data, i);
            i += 2;

            while (ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                float tmpFloat = RepeatFloat(data, i);
                i += 2;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).RepeatV = tmpFloat;
            }
            //Read OffsetU -----------------------------------------
            DefaultTexture.OffsetU = OffsetFloat(data, i);
            i += 2;

            while (ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                float tmpFloat = OffsetFloat(data, i);
                i += 2;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).OffsetU = tmpFloat;
            }
            //Read OffsetV -----------------------------------------
            DefaultTexture.OffsetV = OffsetFloat(data, i);
            i += 2;

            while (ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                float tmpFloat = OffsetFloat(data, i);
                i += 2;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).OffsetV = tmpFloat;
            }
            //Read Rotation ----------------------------------------
            DefaultTexture.Rotation = RotationFloat(data, i);
            i += 2;

            while (ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                float tmpFloat = RotationFloat(data, i);
                i += 2;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).Rotation = tmpFloat;
            }
            //Read Flags1 ------------------------------------------
            DefaultTexture.Flags1 = data[i];
            i++;

            while (ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                byte tmpByte = data[i];
                i++;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).Flags1 = tmpByte;
            }
            //Read Flags2 ------------------------------------------
            DefaultTexture.Flags2 = data[i];
            i++;

            while (i - pos < length && ReadFaceBitfield(data, ref i, ref faceBits, ref BitfieldSize))
            {
                byte tmpByte = data[i];
                i++;

                for (uint face = 0, bit = 1; face < BitfieldSize; face++, bit <<= 1)
                    if ((faceBits & bit) != 0)
                        SetFace(face).Flags2 = tmpByte;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class TextureEntryFace
    {
        [Flags]
        public enum TextureAttributes : uint
        {
            None      = 0,
            TextureID = 1 << 0,
            RGBA      = 1 << 1,
            RepeatU   = 1 << 2,
            RepeatV   = 1 << 3,
            OffsetU   = 1 << 4,
            OffsetV   = 1 << 5,
            Rotation  = 1 << 6,
            Flags1    = 1 << 7,
            Flags2    = 1 << 8,
            All = 0xFFFFFFFF
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="defaultTexture"></param>
        public TextureEntryFace(TextureEntryFace defaultTexture)
        {
            DefaultTexture = defaultTexture;
            if (DefaultTexture == null)
                hasAttribute = TextureAttributes.All;
            else
                hasAttribute = TextureAttributes.None;
        }

        /// <summary>
        /// 
        /// </summary>
        public LLUUID TextureID
        {
            get
            {
                if ((hasAttribute & TextureAttributes.TextureID) != 0)
                    return _TextureID;
                else
                    return DefaultTexture._TextureID;
            }
            set
            {
                _TextureID = value;
                hasAttribute |= TextureAttributes.TextureID;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public uint RGBA
        {
            get
            {
                if ((hasAttribute & TextureAttributes.RGBA) != 0)
                    return _RGBA;
                else
                    return DefaultTexture._RGBA;
            }
            set
            {
                _RGBA = value;
                hasAttribute |= TextureAttributes.RGBA;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public float RepeatU
        {
            get
            {
                if ((hasAttribute & TextureAttributes.RepeatU) != 0)
                    return _RepeatU;
                else
                    return DefaultTexture._RepeatU;
            }
            set
            {
                _RepeatU = value;
                hasAttribute |= TextureAttributes.RepeatU;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public float RepeatV
        {
            get
            {
                if ((hasAttribute & TextureAttributes.RepeatV) != 0)
                    return _RepeatV;
                else
                    return DefaultTexture._RepeatV;
            }
            set
            {
                _RepeatV = value;
                hasAttribute |= TextureAttributes.RepeatV;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public float OffsetU
        {
            get
            {
                if ((hasAttribute & TextureAttributes.OffsetU) != 0)
                    return _OffsetU;
                else
                    return DefaultTexture._OffsetU;
            }
            set
            {
                _OffsetU = value;
                hasAttribute |= TextureAttributes.OffsetU;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public float OffsetV
        {
            get
            {
                if ((hasAttribute & TextureAttributes.OffsetV) != 0)
                    return _OffsetV;
                else
                    return DefaultTexture._OffsetV;
            }
            set
            {
                _OffsetV = value;
                hasAttribute |= TextureAttributes.OffsetV;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public float Rotation
        {
            get
            {
                if ((hasAttribute & TextureAttributes.Rotation) != 0)
                    return _Rotation;
                else
                    return DefaultTexture._Rotation;
            }
            set
            {
                _Rotation = value;
                hasAttribute |= TextureAttributes.Rotation;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public byte Flags1
        {
            get
            {
                if ((hasAttribute & TextureAttributes.Flags1) != 0)
                    return _Flags1;
                else
                    return DefaultTexture._Flags1;
            }
            set
            {
                _Flags1 = value;
                hasAttribute |= TextureAttributes.Flags1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public byte Flags2
        {
            get
            {
                if ((hasAttribute & TextureAttributes.Flags2) != 0)
                    return _Flags2;
                else
                    return DefaultTexture._Flags2;
            }
            set
            {
                _Flags2 = value;
                hasAttribute |= TextureAttributes.Flags2;
            }
        }

        private TextureAttributes hasAttribute;
        private TextureEntryFace DefaultTexture;
        private LLUUID _TextureID;
        private uint _RGBA;
        private float _RepeatU;
        private float _RepeatV;
        private float _OffsetU;
        private float _OffsetV;
        private float _Rotation;
        private byte _Flags1;
        private byte _Flags2;
    }


    /// <summary>
    /// 
    /// </summary>
    public class TextureAnimation
    {
        /// <summary></summary>
        public uint Flags;
        /// <summary></summary>
        public uint Face;
        /// <summary></summary>
        public uint SizeX;
        /// <summary></summary>
        public uint SizeY;
        /// <summary></summary>
        public float Start;
        /// <summary></summary>
        public float Length;
        /// <summary></summary>
        public float Rate;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pos"></param>
        public TextureAnimation(byte[] data, int pos)
        {
            FromBytes(data, pos);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            byte[] bytes = new byte[0];
            return bytes;
        }

        private void FromBytes(byte[] data, int pos)
        {
            int i = pos;

            if (data.Length == 0)
                return;

            Flags = (uint)data[i++];
            Face = (uint)data[i++];
            SizeX = (uint)data[i++];
            SizeY = (uint)data[i++];

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(data, i, 4);
                Array.Reverse(data, i + 4, 4);
                Array.Reverse(data, i + 8, 4);
            }

            Start = BitConverter.ToSingle(data, i);
            Length = BitConverter.ToSingle(data, i + 4);
            Rate = BitConverter.ToSingle(data, i + 8);
        }
    }
}
