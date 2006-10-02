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

using libsecondlife;

using libsecondlife.InventorySystem;

using libsecondlife.Packets;

namespace libsecondlife.AssetSystem
{
	/// <summary>
	/// Summary description for AssetManager.
	/// </summary>
	public class ImageManager
	{
		private SecondLife Client;

		private Hashtable htDownloadRequests = new Hashtable();

		private class TransferRequest
		{
			public bool Completed;
			public bool Status;
			public string StatusMsg;

			public uint Size;
			public uint Received;
			public int LastPacket;
			public byte[] AssetData;

			public TransferRequest()
			{
				Completed = false;

				Status		= false;
				StatusMsg	= "";

				AssetData	= null;
			}
		}

		public ImageManager( SecondLife client )
		{
			Client = client;

			// Used to upload small assets, or as an initial start packet for large transfers
			PacketCallback ImageDataCallback = new PacketCallback(ImageDataCallbackHandler);
			Client.Network.RegisterCallback(PacketType.ImageData, ImageDataCallback);

			// Transfer Packets for downloading large assets		
			PacketCallback ImagePacketCallback = new PacketCallback(ImagePacketCallbackHandler);
            Client.Network.RegisterCallback(PacketType.ImagePacket, ImagePacketCallback);
		}

		public byte[] RequestImage( LLUUID ImageID )
		{
			TransferRequest tr = new TransferRequest();
			tr.Completed  = false;
			tr.Size		  = int.MaxValue; // Number of bytes expected
			tr.Received   = 0; // Number of bytes received
			tr.LastPacket = getUnixtime(); // last time we recevied a packet for this request

			htDownloadRequests[ImageID] = tr;

            RequestImagePacket request = new RequestImagePacket();
            request.RequestImage = new RequestImagePacket.RequestImageBlock[1];
            request.RequestImage[0].DiscardLevel = 0;
            request.RequestImage[0].DownloadPriority = 1215000.0F;
            request.RequestImage[0].Image = ImageID;
            request.RequestImage[0].Packet = 0;

			Client.Network.SendPacket((Packet)request);

			while( tr.Completed == false )
			{
				Client.Tick();
			}

			if( tr.Status == true )
			{
				return tr.AssetData;
			} 
			else 
			{
				throw new Exception( "RequestImage: " + tr.StatusMsg );
			}

		}

		public void ImageDataCallbackHandler(Packet packet, Simulator simulator)
		{
            ImageDataPacket image = (ImageDataPacket)packet;
			TransferRequest tr = (TransferRequest)htDownloadRequests[image.ImageID.ID];

            if (tr != null)
            {
                tr.Size = image.ImageID.Size;
                tr.AssetData = new byte[tr.Size];

                Array.Copy(image.ImageData.Data, 0, tr.AssetData, tr.Received, image.ImageData.Data.Length);
                tr.Received += (uint)image.ImageData.Data.Length;

                // If we've gotten all the data, mark it completed.
                if (tr.Received >= tr.Size)
                {
                    tr.Completed = true;
                    tr.Status = true;
                }
            }
            else
            {
                Client.Log("Received an ImageData packet with an unknown ID field " + 
                    image.ImageID.ID, Helpers.LogLevel.Warning);
            }
		}

		public void ImagePacketCallbackHandler(Packet packet, Simulator simulator)
		{
            ImagePacketPacket image = (ImagePacketPacket)packet;
			TransferRequest tr = (TransferRequest)htDownloadRequests[image.ImageID.ID];

            if (tr != null)
            {
                Array.Copy(image.ImageData.Data, 0, tr.AssetData, tr.Received, image.ImageData.Data.Length);
                tr.Received += (uint)image.ImageData.Data.Length;

                // If we've gotten all the data, mark it completed.
                if (tr.Received >= tr.Size)
                {
                    tr.Completed = true;
                    tr.Status = true;
                }
            }
            else
            {
                Client.Log("Received an ImagePacket packet with an unknown ID field " +
                    image.ImageID.ID, Helpers.LogLevel.Warning);
            }
		}


		public static int getUnixtime()
		{
			TimeSpan ts = (DateTime.UtcNow - new DateTime(1970,1,1,0,0,0));
			return (int)ts.TotalSeconds;
		}
	}
}
