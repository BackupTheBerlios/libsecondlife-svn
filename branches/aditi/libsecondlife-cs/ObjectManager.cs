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
using libsecondlife.Packets;

namespace libsecondlife
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="simulator"></param>
    /// <param name="prim"></param>
    /// <param name="regionHandle"></param>
    /// <param name="timeDilation"></param>
    public delegate void NewPrimCallback(Simulator simulator, PrimObject prim, ulong regionHandle, ushort timeDilation);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="simulator"></param>
    /// <param name="avatar"></param>
    /// <param name="regionHandle"></param>
    /// <param name="timeDilation"></param>
    public delegate void NewAvatarCallback(Simulator simulator, Avatar avatar, ulong regionHandle, ushort timeDilation);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="simulator"></param>
    /// <param name="prim"></param>
    /// <param name="regionHandle"></param>
    /// <param name="timeDilation"></param>
    public delegate void PrimMovedCallback(Simulator simulator, PrimUpdate prim, ulong regionHandle, ushort timeDilation);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="simulator"></param>
    /// <param name="avatar"></param>
    /// <param name="regionHandle"></param>
    /// <param name="timeDilation"></param>
    public delegate void AvatarMovedCallback(Simulator simulator, AvatarUpdate avatar, ulong regionHandle, ushort timeDilation);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="simulator"></param>
    /// <param name="objectID"></param>
    public delegate void KillObjectCallback(Simulator simulator, uint objectID);

    /// <summary>
    /// Contains all of the variables sent in an object update packet for a 
    /// prim object. Used to track position and movement of prims.
    /// </summary>
    public struct PrimUpdate
    {
        /// <summary></summary>
        public uint LocalID;
        /// <summary></summary>
        public byte State;
        /// <summary></summary>
        public LLVector3 Position;
        /// <summary></summary>
        public LLVector3 Velocity;
        /// <summary></summary>
        public LLVector3 Acceleration;
        /// <summary></summary>
        public LLQuaternion Rotation;
        /// <summary></summary>
        public LLVector3 RotationVelocity;
    }

    /// <summary>
    /// Contains all of the variables sent in an object update packet for an 
    /// avatar. Used to track position and movement of avatars.
    /// </summary>
    public struct AvatarUpdate
    {
        /// <summary></summary>
        public uint LocalID;
        /// <summary></summary>
        public byte State;
        /// <summary></summary>
        public LLVector4 CollisionPlane;
        /// <summary></summary>
        public LLVector3 Position;
        /// <summary></summary>
        public LLVector3 Velocity;
        /// <summary></summary>
        public LLVector3 Acceleration;
        /// <summary></summary>
        public LLQuaternion Rotation;
        /// <summary></summary>
        public LLVector3 RotationVelocity;
    }

	/// <summary>
	/// Handles all network traffic related to prims and avatar positions and 
    /// movement.
	/// </summary>
	public class ObjectManager
    {
        /// <summary>
        /// This event will be raised for every ObjectUpdate block that 
        /// contains a new prim.
        /// <remarks>Depending on the circumstances a client could 
        /// receive two or more of these events for the same object, if you 
        /// or the object left the current sim and returned for example. Client
        /// applications are responsible for tracking and storing objects.
        /// </remarks>
        /// </summary>
        public event NewPrimCallback OnNewPrim;
        /// <summary>
        /// This event will be raised for every ObjectUpdate block that 
        /// contains a new avatar.
        /// <remarks>Depending on the circumstances a client 
        /// could receive two or more of these events for the same avatar, if 
        /// you or the other avatar left the current sim and returned for 
        /// example. Client applications are responsible for tracking and 
        /// storing objects.</remarks>
        /// </summary>
        public event NewAvatarCallback OnNewAvatar;
        /// <summary>
        /// This event will be raised when a prim movement packet is received, 
        /// containing the updated position, rotation, and movement-related 
        /// vectors.
        /// </summary>
        public event PrimMovedCallback OnPrimMoved;
        /// <summary>
        /// This event will be raised when an avatar movement packet is 
        /// received, containing the updated position, rotation, and 
        /// movement-related vectors.
        /// </summary>
        public event AvatarMovedCallback OnAvatarMoved;
        /// <summary>
        /// This event will be raised when an object is removed from a 
        /// simulator.
        /// </summary>
        public event KillObjectCallback OnObjectKilled;

        private SecondLife Client;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public ObjectManager(SecondLife client)
        {
            Client = client;

            Client.Network.RegisterCallback(PacketType.ObjectUpdate, new PacketCallback(UpdateHandler));
            Client.Network.RegisterCallback(PacketType.ImprovedTerseObjectUpdate, new PacketCallback(TerseUpdateHandler));
            Client.Network.RegisterCallback(PacketType.ObjectUpdateCompressed, new PacketCallback(CompressedUpdateHandler));
            Client.Network.RegisterCallback(PacketType.ObjectUpdateCached, new PacketCallback(CachedUpdateHandler));
            Client.Network.RegisterCallback(PacketType.KillObject, new PacketCallback(KillObjectHandler));
        }

        public void RequestObject(Simulator simulator, uint localID)
        {
            RequestMultipleObjectsPacket request = new RequestMultipleObjectsPacket();
            request.AgentData.AgentID = Client.Network.AgentID;
            request.AgentData.SessionID = Client.Network.SessionID;
            request.ObjectData = new RequestMultipleObjectsPacket.ObjectDataBlock[1];
            request.ObjectData[0].ID = localID;
            request.ObjectData[0].CacheMissType = 0;

            Client.Network.SendPacket(request, simulator);
        }

        private void ParseAvName(string name, ref string firstName, ref string lastName, ref string groupName)
        {
            string[] lines = name.Split('\n');

            foreach (string line in lines)
            {
                if (line.Substring(0, 19) == "Title STRING RW SV ")
                {
                    groupName = line.Substring(19);
                }
                else if (line.Substring(0, 23) == "FirstName STRING RW SV ")
                {
                    firstName = line.Substring(23);
                }
                else if (line.Substring(0, 22) == "LastName STRING RW SV ")
                {
                    lastName = line.Substring(22);
                }
                else
                {
                    Client.Log("Unhandled line in an avatar name: " + line, Helpers.LogLevel.Warning);
                }
            }
        }

        private void UpdateHandler(Packet packet, Simulator simulator)
        {
            ObjectUpdatePacket update = (ObjectUpdatePacket)packet;

            foreach (ObjectUpdatePacket.ObjectDataBlock block in update.ObjectData)
            {
                if (block.ObjectData.Length == 60)
                {
                    // New prim spotted
                    PrimObject prim = new PrimObject();

                    prim.Position = new LLVector3(block.ObjectData, 0);
                    prim.Rotation = new LLQuaternion(block.ObjectData, 36, true);

                    // TODO: Parse the rest of the ObjectData byte array fields

                    prim.LocalID = block.ID;
                    prim.State = block.State;
                    prim.ID = block.FullID;
                    prim.ParentID = block.ParentID;
                    //block.OwnerID Sound-related
                    prim.Material = block.Material;
                    prim.PathCurve = block.PathCurve;
                    prim.ProfileCurve = block.ProfileCurve;
                    prim.PathBegin = PrimObject.PathBeginFloat(block.PathBegin);
                    prim.PathEnd = PrimObject.PathEndFloat(block.PathEnd);
                    prim.PathScaleX = PrimObject.PathScaleFloat(block.PathScaleX);
                    prim.PathScaleY = PrimObject.PathScaleFloat(block.PathScaleY);
                    prim.PathShearX = PrimObject.PathShearFloat(block.PathShearX);
                    prim.PathShearY = PrimObject.PathShearFloat(block.PathShearY);
                    prim.PathTwist = block.PathTwist; //PrimObject.PathTwistFloat(block.PathTwist);
                    prim.PathTwistBegin = block.PathTwistBegin; //PrimObject.PathTwistFloat(block.PathTwistBegin);
                    prim.PathRadiusOffset = PrimObject.PathRadiusOffsetFloat(block.PathRadiusOffset);
                    prim.PathTaperX = PrimObject.PathTaperFloat((byte)block.PathTaperX);
                    prim.PathTaperY = PrimObject.PathTaperFloat((byte)block.PathTaperY);
                    prim.PathRevolutions = PrimObject.PathRevolutionsFloat(block.PathRevolutions);
                    prim.PathSkew = PrimObject.PathSkewFloat((byte)block.PathSkew);
                    prim.ProfileBegin = PrimObject.ProfileBeginFloat(block.ProfileBegin);
                    prim.ProfileEnd = PrimObject.ProfileEndFloat(block.ProfileEnd);
                    prim.ProfileHollow = block.ProfileHollow;
                    prim.Name = Helpers.FieldToString(block.NameValue);
                    //block.Data ?
                    //block.Text Hovering text
                    //block.TextColor LLColor4U of the hovering text
                    //block.MediaURL Quicktime stream
                    // TODO: Multi-texture support
                    if (block.TextureEntry.Length >= 16)
                    {
                        prim.Texture = new LLUUID(block.TextureEntry, 0);
                    }
                    else
                    {
                        prim.Texture = new LLUUID();
                    }
                    //block.TextureAnim ?
                    //block.JointType ?
                    //block.JointPivot ?
                    //block.JointAxisOrAnchor ?
                    //block.PCode ?
                    //block.PSBlock Particle system related
                    //block.ExtraParams ?
                    prim.Scale = block.Scale;
                    //block.Flags ?
                    //block.UpdateFlags ?
                    //block.ClickAction ?
                    //block.Gain Sound-related
                    //block.Sound Sound-related
                    //block.Radius Sound-related

                    if (OnNewPrim != null)
                    {
                        OnNewPrim(simulator, prim, update.RegionData.RegionHandle, update.RegionData.TimeDilation);
                    }
                }
                else if (block.ObjectData.Length == 76)
                {
                    // New avatar spotted
                    Avatar avatar = new Avatar();
                    string FirstName = "";
                    string LastName = "";
                    string GroupName = "";

                    //avatar.CollisionPlane = new LLQuaternion(block.ObjectData, 0);
                    avatar.Position = new LLVector3(block.ObjectData, 16);
                    avatar.Rotation = new LLQuaternion(block.ObjectData, 52, true);

                    // TODO: Parse the rest of the ObjectData byte array fields

                    ParseAvName(Helpers.FieldToString(block.NameValue), ref FirstName, ref LastName, ref GroupName);

                    avatar.ID = block.FullID;
                    avatar.LocalID = block.ID;
                    avatar.Name = FirstName + " " + LastName;
                    avatar.GroupName = GroupName;
                    avatar.Online = true;
                    avatar.CurrentRegion = simulator.Region;

                    if (FirstName == Client.Avatar.FirstName && LastName == Client.Avatar.LastName)
                    {
                        // Update our avatar
                        Client.Avatar.LocalID = avatar.LocalID;
                        Client.Avatar.Position = avatar.Position;
                        Client.Avatar.Rotation = avatar.Rotation;
                    }
                    else
                    {
                        Client.AddAvatar(avatar);

                        if (OnNewAvatar != null)
                        {
                            OnNewAvatar(simulator, avatar, update.RegionData.RegionHandle, update.RegionData.TimeDilation);
                        }
                    }
                }
                else
                {
                    // Unknown
                    Client.Log("Unhandled ObjectData.ObjectData length:\n" + block.ObjectData.Length, 
                        Helpers.LogLevel.Warning);
                }
            }
        }

        private void TerseUpdateHandler(Packet packet, Simulator simulator)
        {
            float x, y, z, w;
            uint localid;
            LLVector4 CollisionPlane = null;
            LLVector3 Position;
            LLVector3 Velocity;
            LLVector3 Acceleration;
            LLQuaternion Rotation;
            LLVector3 RotationVelocity;

            ImprovedTerseObjectUpdatePacket update = (ImprovedTerseObjectUpdatePacket)packet;

            foreach (ImprovedTerseObjectUpdatePacket.ObjectDataBlock block in update.ObjectData)
            {
                int i = 0;
                bool avatar;

                localid = (uint)(block.Data[i++] + (block.Data[i++] << 8) + 
                    (block.Data[i++] << 16) + (block.Data[i++] << 24));

                byte state = block.Data[i++];

                avatar = Convert.ToBoolean(block.Data[i++]);

                if (avatar)
                {
                    CollisionPlane = new LLVector4(block.Data, i);
                    i += 16;
                }

                // Position
                Position = new LLVector3(block.Data, i);
                i += 12;
                // Velocity
                x = Dequantize(block.Data, i, -128.0F, 128.0F);
                i += 2;
                y = Dequantize(block.Data, i, -128.0F, 128.0F);
                i += 2;
                z = Dequantize(block.Data, i, -128.0F, 128.0F);
                i += 2;
                Velocity = new LLVector3(x, y, z);
                // Acceleration
                x = Dequantize(block.Data, i, -64.0F, 64.0F);
                i += 2;
                y = Dequantize(block.Data, i, -64.0F, 64.0F);
                i += 2;
                z = Dequantize(block.Data, i, -64.0F, 64.0F);
                i += 2;
                Acceleration = new LLVector3(x, y, z);
                // Rotation
                x = Dequantize(block.Data, i, -1.0F, 1.0F);
                i += 2;
                y = Dequantize(block.Data, i, -1.0F, 1.0F);
                i += 2;
                z = Dequantize(block.Data, i, -1.0F, 1.0F);
                i += 2;
                w = Dequantize(block.Data, i, -1.0F, 1.0F);
                i += 2;
                Rotation = new LLQuaternion(x, y, z, w);
                // Rotation velocity
                x = Dequantize(block.Data, i, -64.0F, 64.0F);
                i += 2;
                y = Dequantize(block.Data, i, -64.0F, 64.0F);
                i += 2;
                z = Dequantize(block.Data, i, -64.0F, 64.0F);
                i += 2;
                RotationVelocity = new LLVector3(x, y, z);

                if (avatar)
                {
                    if (localid == Client.Avatar.LocalID)
                    {
                        Client.Avatar.Position = Position;
                        Client.Avatar.Rotation = Rotation;
                    }

                    AvatarUpdate avupdate = new AvatarUpdate();
                    avupdate.LocalID = localid;
                    avupdate.State = state;
                    avupdate.Position = Position;
                    avupdate.CollisionPlane = CollisionPlane;
                    avupdate.Velocity = Velocity;
                    avupdate.Acceleration = Acceleration;
                    avupdate.Rotation = Rotation;
                    avupdate.RotationVelocity = RotationVelocity;

                    if (OnAvatarMoved != null)
                    {
                        OnAvatarMoved(simulator, avupdate, update.RegionData.RegionHandle, update.RegionData.TimeDilation);
                    }
                }
                else
                {
                    PrimUpdate primupdate = new PrimUpdate();
                    primupdate.LocalID = localid;
                    primupdate.State = state;
                    primupdate.Position = Position;
                    primupdate.Velocity = Velocity;
                    primupdate.Acceleration = Acceleration;
                    primupdate.Rotation = Rotation;
                    primupdate.RotationVelocity = RotationVelocity;

                    if (OnPrimMoved != null)
                    {
                        OnPrimMoved(simulator, primupdate, update.RegionData.RegionHandle, update.RegionData.TimeDilation);
                    }
                }
            }
        }

        private void CompressedUpdateHandler(Packet packet, Simulator simulator)
        {
            ObjectUpdateCompressedPacket update = (ObjectUpdateCompressedPacket)packet;
            PrimObject prim;

            foreach (ObjectUpdateCompressedPacket.ObjectDataBlock block in update.ObjectData)
            {
                int i = 0;
                prim = new PrimObject();

                prim.ID = new LLUUID(block.Data, 0);
                i += 16;
                prim.LocalID = (uint)(block.Data[i++] + (block.Data[i++] << 8) +
                    (block.Data[i++] << 16) + (block.Data[i++] << 24));
                prim.Scale = new LLVector3(block.Data, i);
                i += 12;
                prim.Position = new LLVector3(block.Data, i);
                i += 12;
                prim.Rotation = new LLQuaternion(block.Data, i, true);
                i += 12;

                // FIXME: Fill in the rest of these fields
                prim.PathCurve = (uint)block.Data[69];
                prim.ProfileCurve = (uint)block.Data[83];

                if (OnNewPrim != null)
                {
                    OnNewPrim(simulator, prim, update.RegionData.RegionHandle, update.RegionData.TimeDilation);
                }
            }
        }

        private void CachedUpdateHandler(Packet packet, Simulator simulator)
        {
            int i = 0;

            ObjectUpdateCachedPacket update = (ObjectUpdateCachedPacket)packet;

            // Assume clients aren't caching objects for now, so request updates for all of these objects
            RequestMultipleObjectsPacket request = new RequestMultipleObjectsPacket();
            request.AgentData.AgentID = Client.Network.AgentID;
            request.AgentData.SessionID = Client.Network.AgentID;
            request.ObjectData = new RequestMultipleObjectsPacket.ObjectDataBlock[update.ObjectData.Length];

            foreach (ObjectUpdateCachedPacket.ObjectDataBlock block in update.ObjectData)
            {
                request.ObjectData[i] = new RequestMultipleObjectsPacket.ObjectDataBlock();
                request.ObjectData[i].ID = block.ID;
                i++;

                //Client.Log("CachedData ID=" + block.ID + ", CRC=" + block.CRC + ", UpdateFlags=" + block.UpdateFlags, 
                //Helpers.LogLevel.Info);
            }

            Client.Network.SendPacket(request);
        }

        private void KillObjectHandler(Packet packet, Simulator simulator)
        {
            if (OnObjectKilled != null)
            {
                foreach (KillObjectPacket.ObjectDataBlock block in ((KillObjectPacket)packet).ObjectData)
                {
                    OnObjectKilled(simulator, block.ID);
                }
            }
        }

        /// <summary>
        /// Takes a quantized 16-bit value from a byte array and its range and returns 
        /// a float representation of the continuous value. For example, a value of 
        /// 32767 and a range of -128.0 to 128.0 would return 0.0. The endian conversion 
        /// from the 16-bit little endian to the native platform will also be handled.
        /// </summary>
        /// <param name="byteArray">The byte array containing the short value</param>
        /// <param name="pos">The beginning position of the short (quantized) value</param>
        /// <param name="lower">The lower quantization range</param>
        /// <param name="upper">The upper quantization range</param>
        /// <returns>A 32-bit floating point representation of the dequantized value</returns>
        private float Dequantize(byte[] byteArray, int pos, float lower, float upper)
        {
            ushort value = (ushort)(byteArray[pos] + (byteArray[pos + 1] << 8));
            float QV = (float)value;
            float range = upper - lower;
            float QF = range / 65536.0F;
            return (float)((QV * QF - (0.5F * range)) + QF);
        }
    }
}
