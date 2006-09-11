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

namespace libsecondlife
{
	public class PrimObject
	{
		public float PathTwistBegin = 0;
		public float PathEnd = 0;
		public float ProfileBegin = 0;
		public float PathRadiusOffset = 0;
		public float PathSkew = 0;
		public LLVector3 Position = new LLVector3();
		public uint ProfileCurve = 0;
		public float PathScaleX = 0;
		public float PathScaleY = 0;
		public LLUUID ID = new LLUUID();
		public uint LocalID = 0;
		public LLUUID GroupID = new LLUUID();
		public uint Material = 0;
		public string Name = "";
		public string Description;
		public float PathShearX = 0;
		public float PathShearY = 0;
		public float PathTaperX = 0;
		public float PathTaperY = 0;
		public float ProfileEnd = 0;
		public float PathBegin = 0;
		public uint PathCurve = 0;
		public LLVector3 Scale = new LLVector3();
		public float PathTwist = 0;
		public LLUUID Texture = new LLUUID(); // TODO: Add multi-texture support
		public uint ProfileHollow = 0;
		public float PathRevolutions = 0;
		public LLQuaternion Rotation = new LLQuaternion();
		public uint State;
		
		public PrimObject(LLUUID texture)
		{
			Texture = texture;
		}

        public PrimObject()
        {
            Texture = new LLUUID();
        }

		public static byte PathScaleByte(float pathScale)
		{
			// Y = 100 + 100X
			return (byte)(100 + Convert.ToInt16(100.0F * pathScale));
		}

        public static float PathScaleFloat(byte pathScale)
        {
            // Y = 1 - (X - 100) / 100
            return 1.0F - (((float)pathScale - 100.0F) / 100.0F);
        }

		public static byte PathTwistByte(float pathTwist)
		{
			// Y = 256 + ceil (X / 1.8)
			ushort temp = Convert.ToUInt16(256 + Math.Ceiling(pathTwist / 1.8F));
			return (byte)(temp % 256);
		}

        public static float PathTwistFloat(sbyte pathTwist)
        {
            // Y = 0.5556X
            return (float)pathTwist * 0.5556F;
        }

		public static byte PathShearByte(float pathShear)
		{
			// Y = 256 + 100X
			ushort temp = Convert.ToUInt16(100.0F * pathShear);
			temp += 256;
			return (byte)(temp % 256);
		}

        public static float PathShearFloat(byte pathShear)
        {
            // Y = (X - 256) / 100
            if (pathShear > 150)
            {
                return ((float)pathShear - 256.0F) / 100.0F;
            }
            else
            {
                return (float)pathShear / 100.0F;
            }        }

		public static byte ProfileBeginByte(float profileBegin)
		{
			// Y = ceil (200X)
			return (byte)Convert.ToInt16(200.0F * profileBegin);
		}

        public static float ProfileBeginFloat(byte profileBegin)
        {
            // Y = 0.005X
            return (float)profileBegin * 0.005F;
        }

		public static byte ProfileEndByte(float profileEnd)
		{
			// Y = 200 - ceil (200X)
			return (byte)(200 - (200.0F * profileEnd));
		}

        public static float ProfileEndFloat(byte profileEnd)
        {
            // Y = 1 - 0.005X
            return 1.0F - (float)profileEnd * 0.005F;
        }

		public static byte PathBeginByte(float pathBegin)
		{
			// Y = 100X
			return (byte)Convert.ToInt16(100.0F * pathBegin);
		}

        public static float PathBeginFloat(byte pathBegin)
        {
            // Y = X / 100
            return (float)pathBegin / 100.0F;
        }

		public static byte PathEndByte(float pathEnd)
		{
			// Y = 100 - 100X
			return (byte)(100 - Convert.ToInt16(100.0F * pathEnd));
		}

        public static float PathEndFloat(byte pathEnd)
        {
            // Y = 1 - X / 100
            return 1.0F - (float)pathEnd / 100;
        }

		public static byte PathRadiusOffsetByte(float pathRadiusOffset)
		{
			// Y = 256 + 100X
			return PathShearByte(pathRadiusOffset);
		}

        public static float PathRadiusOffsetFloat(sbyte pathRadiusOffset)
        {
            // Y = X / 100
            return (float)pathRadiusOffset / 100.0F;
        }

		public static byte PathRevolutionsByte(float pathRevolutions)
		{
			// Y = ceil (66X) - 66
			return (byte)(Convert.ToInt16(Math.Ceiling(66.0F * pathRevolutions)) - 66);
		}

        public static float PathRevolutionsFloat(byte pathRevolutions)
        {
            // Y = 1 + 0.015X
            return 1.0F + (float)pathRevolutions * 0.015F;
        }

		/*public static byte PathScaleByte(float pathSkew)
		{
			// Y = 256 + 100X
			return PathShearByte(pathSkew);
		}

        public static float PathScaleFloat(byte pathSkew)
        {
            // Y = -1 + 0.01X
            return -1.0F + (float)pathSkew + 0.01F;
        }

		public static byte PathTaperByte(float pathTaper)
		{
			// Y = 256 + 100X
			return PathShearByte(pathTaper);
		}

        public static float PathTaperFloat(byte pathTaper)
        {
            // Y = -1 + 0.01X
        }*/
	}
}