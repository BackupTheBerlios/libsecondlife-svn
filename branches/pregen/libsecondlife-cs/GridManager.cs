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
using System.Collections;
using libsecondlife.Packets;

namespace libsecondlife
{
	/// <summary>
	/// Class for regions on the world map
	/// </summary>
	public class GridRegion
	{
		public int X;
		public int Y;
		public string Name;
		public byte Access;
		public uint RegionFlags;
		public byte WaterHeight;
		public byte Agents;
		public LLUUID MapImageID;
		public ulong RegionHandle; // Used for teleporting

		public GridRegion() 
		{

		}
	}

	/// <summary>
	/// Manages grid-wide tasks such as the world map
	/// </summary>
	public class GridManager
	{
		public Hashtable Regions;
		SecondLife Client;

		public GridManager(SecondLife client)
		{
			Client = client;
			Regions = new Hashtable();
			PacketCallback callback = new PacketCallback(MapBlockReplyHandler);
			Client.Network.RegisterCallback(PacketType.MapBlockReply, callback);
		}

		public void AddSim(string name) 
		{
			if(!Regions.ContainsKey(name)) 
			{
                MapNameRequestPacket map = new MapNameRequestPacket();
                map.AgentData.AgentID = Client.Network.AgentID;
                map.NameData.Name = Helpers.StringToField(name);

                Client.Network.SendPacket((Packet)map);
			}
		}

		public void AddAllSims() 
		{
            MapBlockRequestPacket request = new MapBlockRequestPacket();
            request.AgentData.AgentID = Client.Network.AgentID;
            request.PositionData.MaxX = 65535;
            request.PositionData.MaxY = 65535;
            request.PositionData.MinX = 0;
            request.PositionData.MinY = 0;

            Client.Network.SendPacket((Packet)request);
		}

		public GridRegion GetSim(string name) 
		{
			if(Regions.ContainsKey(name)) 
				return (GridRegion)Regions[name];

			AddSim(name);
			System.Threading.Thread.Sleep(1000);

			if(Regions.ContainsKey(name)) 
				return (GridRegion)Regions[name];
			else 
			{
				//TODO: Put some better handling inplace here with some retry code
				Client.Log("GetSim(): Returned a sim that we aren't tracking",Helpers.LogLevel.Warning);
				return new GridRegion();
			}
		}

		private void MapBlockReplyHandler(Packet packet, Simulator simulator) 
		{
			GridRegion region;
            MapBlockReplyPacket map = (MapBlockReplyPacket)packet;

            foreach (MapBlockReplyPacket.DataBlock block in map.Data)
            {
                region = new GridRegion();

                region.X = block.X;
                region.Y = block.Y;
                region.Name = Helpers.FieldToString(block.Name);
                region.RegionFlags = block.RegionFlags;
                region.WaterHeight = block.WaterHeight;
                region.Agents = block.Agents;
                region.Access = block.Access;
                region.MapImageID = block.MapImageID;
                // FIXME: We need Helpers.UIntsToLong()
                //region.RegionHandle = new U64(region.X * 256,region.Y * 256);

                if (region.Name != "" && region.X != 0 && region.Y != 0)
                {
                    Regions[region.Name] = region;
                }
            }
		}
	}
}
