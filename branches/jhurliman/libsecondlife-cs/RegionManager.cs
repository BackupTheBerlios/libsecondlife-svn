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
using System.IO;
using System.Collections.Generic;
using libsecondlife.Packets;

namespace libsecondlife
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="region"></param>
    public delegate void ParcelCompleteCallback(Region region);

    public enum LayerType
    {
        // FIXME:
        Land = 76,
        Wind = 0,
        Unknown1 = 55,
        Unknown2 = 56
    }

    public class LayerBlock
    {
        public byte[] CompressedData;
        public float[,] Pixels;

        public LayerBlock(byte[] data)
        {
            CompressedData = data;
            Pixels = new float[16,16];
        }

        public void CompressedToFile(string filename)
        {
            File.WriteAllBytes(filename + ".ter", CompressedData);
        }

        public void BlockToFile(string filename)
        {
            // FIXME:
            //File.WriteAllBytes(filename + ".raw", Pixels);
        }
    }

    /// <summary>
    /// Represents a region (also known as a sim) in Second Life.
    /// </summary>
    public class Region
    {
        /// <summary></summary>
        public event ParcelCompleteCallback OnParcelCompletion;

        // FIXME: This whole setup is fscked in a really bad way. We can't be 
        // locking on a publically accessible container, and we shouldn't have
        // publically accessible containers anyways because external programs 
        // might be iterating through them or modifying them when internally 
        // we are doing the opposite. The best way to fix this will be 
        // privatizing and adding helper functions to access the dictionary
        public Dictionary<int, Parcel> Parcels;

        /// <summary></summary>
        public LLUUID ID;
        /// <summary></summary>
        public ulong Handle;
        /// <summary></summary>
        public string Name;
        /// <summary></summary>
        public byte[] ParcelOverlay;
        /// <summary></summary>
        public int ParcelOverlaysReceived;
        /// <summary>64x64 Array of parcels which have been successfully downloaded 
        /// (and their LocalID's, 0 = Null)</summary>
        public int[,] ParcelMarked;
        /// <summary>Flag to indicate whether we are downloading a sim's parcels</summary>
        public bool ParcelDownloading;
        /// <summary>Flag to indicate whether to get Dwell values automatically (NOT USED YET). Call Parcel.GetDwell() instead</summary>
        public bool ParcelDwell;
        /// <summary></summary>
        public float TerrainHeightRange00;
        /// <summary></summary>
        public float TerrainHeightRange01;
        /// <summary></summary>
        public float TerrainHeightRange10;
        /// <summary></summary>
        public float TerrainHeightRange11;
        /// <summary></summary>
        public float TerrainStartHeight00;
        /// <summary></summary>
        public float TerrainStartHeight01;
        /// <summary></summary>
        public float TerrainStartHeight10;
        /// <summary></summary>
        public float TerrainStartHeight11;
        /// <summary></summary>
        public float WaterHeight;
        /// <summary></summary>
        public LLUUID SimOwner;
        /// <summary></summary>
        public LLUUID TerrainBase0;
        /// <summary></summary>
        public LLUUID TerrainBase1;
        /// <summary></summary>
        public LLUUID TerrainBase2;
        /// <summary></summary>
        public LLUUID TerrainBase3;
        /// <summary></summary>
        public LLUUID TerrainDetail0;
        /// <summary></summary>
        public LLUUID TerrainDetail1;
        /// <summary></summary>
        public LLUUID TerrainDetail2;
        /// <summary></summary>
        public LLUUID TerrainDetail3;
        /// <summary></summary>
        public bool IsEstateManager;
        /// <summary></summary>
        public EstateTools Estate;
        /// <summary>The terrain blocks for this region</summary>
        public LayerBlock[,] Heightmap;
        /// <summary>The wind data for this region</summary>
        public LayerBlock[,] Windmap;

        private SecondLife Client;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public Region(SecondLife client)
        {
            Estate = new EstateTools(client);
            Client = client;
            ID = new LLUUID();
            ParcelOverlay = new byte[4096];
            ParcelMarked = new int[64, 64];

            Parcels = new Dictionary<int, Parcel>();

            // The array is sized, but all of the values stay null. This is used
            // later to detect if we've received a block at a specific coordinate
            Heightmap = new LayerBlock[16, 16];
            Windmap = new LayerBlock[16, 16];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="id"></param>
        /// <param name="handle"></param>
        /// <param name="name"></param>
        /// <param name="heightList"></param>
        /// <param name="simOwner"></param>
        /// <param name="terrainImages"></param>
        /// <param name="isEstateManager"></param>
        public Region(SecondLife client, LLUUID id, ulong handle, string name, float[] heightList,
                LLUUID simOwner, LLUUID[] terrainImages, bool isEstateManager)
        {
            Client = client;
            Estate = new EstateTools(client);
            ID = id;
            Handle = handle;
            Name = name;
            ParcelOverlay = new byte[4096];
            ParcelMarked = new int[64, 64];
            ParcelDownloading = false;
            ParcelDwell = false;

            TerrainHeightRange00 = heightList[0];
            TerrainHeightRange01 = heightList[1];
            TerrainHeightRange10 = heightList[2];
            TerrainHeightRange11 = heightList[3];
            TerrainStartHeight00 = heightList[4];
            TerrainStartHeight01 = heightList[5];
            TerrainStartHeight10 = heightList[6];
            TerrainStartHeight11 = heightList[7];
            WaterHeight = heightList[8];

            SimOwner = simOwner;

            TerrainBase0 = terrainImages[0];
            TerrainBase1 = terrainImages[1];
            TerrainBase2 = terrainImages[2];
            TerrainBase3 = terrainImages[3];
            TerrainDetail0 = terrainImages[4];
            TerrainDetail1 = terrainImages[5];
            TerrainDetail2 = terrainImages[6];
            TerrainDetail3 = terrainImages[7];

            IsEstateManager = isEstateManager;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="west"></param>
        /// <param name="south"></param>
        /// <param name="east"></param>
        /// <param name="north"></param>
        public void ParcelSubdivide(float west, float south, float east, float north)
        {
            ParcelDividePacket divide = new ParcelDividePacket();
            divide.AgentData.AgentID = Client.Network.AgentID;
            divide.AgentData.SessionID = Client.Network.SessionID;
            divide.ParcelData.East = east;
            divide.ParcelData.North = north;
            divide.ParcelData.South = south;
            divide.ParcelData.West = west;

            // FIXME: Region needs a reference to it's parent Simulator
            //Client.Network.SendPacket((Packet)divide, this.Simulator);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="west"></param>
        /// <param name="south"></param>
        /// <param name="east"></param>
        /// <param name="north"></param>
        public void ParcelJoin(float west, float south, float east, float north)
        {
            ParcelJoinPacket join = new ParcelJoinPacket();
            join.AgentData.AgentID = Client.Network.AgentID;
            join.AgentData.SessionID = Client.Network.SessionID;
            join.ParcelData.East = east;
            join.ParcelData.North = north;
            join.ParcelData.South = south;
            join.ParcelData.West = west;

            // FIXME: Region needs a reference to it's parent Simulator
            //Client.Network.SendPacket((Packet)join, this.Simulator);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prim"></param>
        /// <param name="position"></param>
        /// <param name="avatarPosition"></param>
        public void RezObject(PrimObject prim, LLVector3 position, LLVector3 avatarPosition)
        {
            // FIXME:
            //byte[] textureEntry = new byte[40];
            //Array.Copy(prim.Texture.Data, textureEntry, 16);
            //textureEntry[35] = 0xe0; // No clue

            //Packet objectAdd = libsecondlife.Packets.Object.ObjectAdd(Client.Protocol, Client.Network.AgentID,
            //        LLUUID.GenerateUUID(), avatarPosition,
            //        position, prim, textureEntry);
            //Client.Network.SendPacket(objectAdd);
        }

        /// <summary>
        /// 
        /// </summary>
        public void FillParcels()
        {
            // Begins filling parcels
            ParcelDownloading = true;

            ParcelPropertiesRequestPacket tPacket = new ParcelPropertiesRequestPacket();
            tPacket.AgentData.AgentID = Client.Self.ID;
            tPacket.AgentData.SessionID = Client.Network.SessionID;
            tPacket.ParcelData.SequenceID = -10000;
            tPacket.ParcelData.West = 0.0f;
            tPacket.ParcelData.South = 0.0f;
            tPacket.ParcelData.East = 0.0f;
            tPacket.ParcelData.North = 0.0f;

            Client.Network.SendPacket((Packet)tPacket);
        }

        /// <summary>
        /// 
        /// </summary>
        public void ResetParcelDownload()
        {
            Parcels = new Dictionary<int, Parcel>();
            ParcelMarked = new int[64, 64];
        }

        /// <summary>
        /// 
        /// </summary>
        public void FilledParcels()
        {
            if (OnParcelCompletion != null)
            {
                OnParcelCompletion(this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public override bool Equals(object o)
        {
            if (!(o is Region))
            {
                return false;
            }

            Region region = (Region)o;

            return (region.ID == ID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static bool operator ==(Region lhs, Region rhs)
        {
            try
            {
                return (lhs.ID == rhs.ID);
            }
            catch (NullReferenceException)
            {
                bool lhsnull = false;
                bool rhsnull = false;

                if (lhs == null || lhs.ID == null || lhs.ID.Data == null || lhs.ID.Data.Length == 0)
                {
                    lhsnull = true;
                }

                if (rhs == null || rhs.ID == null || rhs.ID.Data == null || rhs.ID.Data.Length == 0)
                {
                    rhsnull = true;
                }

                return (lhsnull == rhsnull);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
        /// <returns></returns>
        public static bool operator !=(Region lhs, Region rhs)
        {
            return !(lhs == rhs);
        }
    }

    public class RegionManager
    {
        private SecondLife Client;

        // FIXME: Temporary hack because we don't know how to figure out what LayerData packet goes where
        private int x = 0;

        public RegionManager(SecondLife client)
        {
            Client = client;

            Client.Network.RegisterCallback(PacketType.RegionHandshake, new PacketCallback(RegionHandshakeHandler));
            Client.Network.RegisterCallback(PacketType.ParcelOverlay, new PacketCallback(ParcelOverlayHandler));
            Client.Network.RegisterCallback(PacketType.LayerData, new PacketCallback(LayerDataHandler));
        }

        private void RegionHandshakeHandler(Packet packet, Simulator simulator)
        {
            RegionHandshakePacket handshake = (RegionHandshakePacket)packet;

            // Send a RegionHandshakeReply
            RegionHandshakeReplyPacket reply = new RegionHandshakeReplyPacket();
            reply.AgentData.AgentID = Client.Network.AgentID;
            reply.AgentData.SessionID = Client.Network.SessionID;
            reply.RegionInfo.Flags = 0;
            Client.Network.SendPacket(reply, simulator);

            simulator.Region.ID = handshake.RegionInfo.CacheID;
            // FIXME:
            //handshake.RegionInfo.BillableFactor;
            //handshake.RegionInfo.RegionFlags;
            //handshake.RegionInfo.SimAccess;
            simulator.Region.IsEstateManager = handshake.RegionInfo.IsEstateManager;
            simulator.Region.Name = Helpers.FieldToString(handshake.RegionInfo.SimName);
            simulator.Region.SimOwner = handshake.RegionInfo.SimOwner;
            simulator.Region.TerrainBase0 = handshake.RegionInfo.TerrainBase0;
            simulator.Region.TerrainBase1 = handshake.RegionInfo.TerrainBase1;
            simulator.Region.TerrainBase2 = handshake.RegionInfo.TerrainBase2;
            simulator.Region.TerrainBase3 = handshake.RegionInfo.TerrainBase3;
            simulator.Region.TerrainDetail0 = handshake.RegionInfo.TerrainDetail0;
            simulator.Region.TerrainDetail1 = handshake.RegionInfo.TerrainDetail1;
            simulator.Region.TerrainDetail2 = handshake.RegionInfo.TerrainDetail2;
            simulator.Region.TerrainDetail3 = handshake.RegionInfo.TerrainDetail3;
            simulator.Region.TerrainHeightRange00 = handshake.RegionInfo.TerrainHeightRange00;
            simulator.Region.TerrainHeightRange01 = handshake.RegionInfo.TerrainHeightRange01;
            simulator.Region.TerrainHeightRange10 = handshake.RegionInfo.TerrainHeightRange10;
            simulator.Region.TerrainHeightRange11 = handshake.RegionInfo.TerrainHeightRange11;
            simulator.Region.TerrainStartHeight00 = handshake.RegionInfo.TerrainStartHeight00;
            simulator.Region.TerrainStartHeight01 = handshake.RegionInfo.TerrainStartHeight01;
            simulator.Region.TerrainStartHeight10 = handshake.RegionInfo.TerrainStartHeight10;
            simulator.Region.TerrainStartHeight11 = handshake.RegionInfo.TerrainStartHeight11;
            simulator.Region.WaterHeight = handshake.RegionInfo.WaterHeight;

            Client.Log("Received a region handshake for " + simulator.Region.Name, LogLevel.Info);
        }

        private void ParcelOverlayHandler(Packet packet, Simulator simulator)
        {
            ParcelOverlayPacket overlay = (ParcelOverlayPacket)packet;

            if (overlay.ParcelData.SequenceID >= 0 && overlay.ParcelData.SequenceID <= 3)
            {
                Array.Copy(overlay.ParcelData.Data, 0, simulator.Region.ParcelOverlay,
                    overlay.ParcelData.SequenceID * 1024, 1024);
                simulator.Region.ParcelOverlaysReceived++;

                if (simulator.Region.ParcelOverlaysReceived > 3)
                {
                    Client.Log("Finished building the " + simulator.Region.Name + " parcel overlay",
                        LogLevel.Info);
                }
            }
            else
            {
                Client.Log("Parcel overlay with a strange sequence ID of " + overlay.ParcelData.SequenceID +
                    " received from " + simulator.Region.Name, LogLevel.Warning);
            }
        }

        private void LayerDataHandler(Packet packet, Simulator simulator)
        {
            LayerDataPacket layer = (LayerDataPacket)packet;

            lock (this)
            {
                // FIXME: Parse the data in to blocks (should be simple enough, eh!)
                string filename = simulator.Region.Name + x.ToString("00") + ".ter";

                if (layer.LayerID.Type == (byte)LayerType.Land)
                {
                    File.WriteAllBytes(filename, layer.LayerData.Data);
                    x++;
                }
                else if (layer.LayerID.Type == (byte)LayerType.Wind)
                {
                    ;
                }
                else
                {
                    Client.Log("Received a " + layer.LayerData.Data.Length + " byte LayerData with unknown type " +
                        layer.LayerID.Type, LogLevel.Warning);
                }
            }
        }
    }
}
