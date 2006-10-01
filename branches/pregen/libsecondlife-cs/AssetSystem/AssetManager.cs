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
    public class AssetManager
    {
        public const int SINK_FEE_IMAGE = 1;

        private SecondLife Client;

        private Hashtable htUploadRequests = new Hashtable();
        private Hashtable htDownloadRequests = new Hashtable();

        private class TransferRequest
        {
            public bool Completed;
            public bool Status;
            public string StatusMsg;

            public int Size;
            public int Received;
            public int LastPacket;
            public byte[] AssetData;
        }

        internal AssetManager(SecondLife client)
        {
            Client = client;

            // Used to upload small assets, or as an initial start packet for large transfers
            Client.Network.RegisterCallback(PacketType.AssetUploadComplete, new PacketCallback(AssetUploadCompleteCallbackHandler));

            // Transfer Packets for downloading large assets
            Client.Network.RegisterCallback(PacketType.TransferInfo, new PacketCallback(TransferInfoCallbackHandler));
            Client.Network.RegisterCallback(PacketType.TransferPacket, new PacketCallback(TransferPacketCallbackHandler));

            // XFer packets for uploading large assets
            Client.Network.RegisterCallback(PacketType.ConfirmXferPacket, new PacketCallback(ConfirmXferPacketCallbackHandler));
            Client.Network.RegisterCallback(PacketType.RequestXfer, new PacketCallback(RequestXferCallbackHandler));
        }

        public void SinkFee(int sinkType)
        {
            switch (sinkType)
            {
                case SINK_FEE_IMAGE:
                    Client.Avatar.GiveMoney(new LLUUID(), 10, "Image Upload");
                    break;
                default:
                    throw new Exception("AssetManager: Unknown sinktype (" + sinkType + ")");
            }
        }

        public void UploadAsset(Asset asset)
        {
            AssetUploadRequestPacket request = new AssetUploadRequestPacket();
            TransferRequest tr = new TransferRequest();
            tr.Completed = false;
            htUploadRequests[asset.AssetID] = tr;

            request.AssetBlock.StoreLocal = false;
            request.AssetBlock.Tempfile = asset.Tempfile;
            request.AssetBlock.UUID = asset.AssetID;

            if (asset.AssetData.Length > 500)
            {
                request.AssetBlock.AssetData = asset.AssetData;
                
                Client.Network.SendPacket((Packet)request);

                tr.AssetData = asset.AssetData;
            }
            else
            {
                request.AssetBlock.AssetData = new byte[0];

                Client.Network.SendPacket((Packet)request);
            }

            while (tr.Completed == false)
            {
                Client.Tick();
            }

            if (tr.Status == false)
            {
                throw new Exception(tr.StatusMsg);
            }
            else
            {
                if (asset.Type == Asset.ASSET_TYPE_IMAGE)
                {
                    SinkFee(SINK_FEE_IMAGE);
                }
            }
        }

        public void GetInventoryAsset(InventoryItem item)
        {
            LLUUID TransferID = LLUUID.GenerateUUID();

            TransferRequest tr = new TransferRequest();
            tr.Completed = false;
            tr.Size = int.MaxValue; // Number of bytes expected
            tr.Received = 0; // Number of bytes received
            tr.LastPacket = getUnixtime(); // last time we recevied a packet for this request

            htDownloadRequests[TransferID] = tr;

            //FIXME:
            TransferRequestPacket request = new TransferRequestPacket();
            //request.TransferInfo.ChannelType = ?;
            //Packet packet = AssetPackets.TransferRequest(Client.Network.SessionID, Client.Network.AgentID, TransferID, item);
            //Client.Network.SendPacket(packet);

            while (tr.Completed == false)
            {
                Client.Tick();
            }

            item.SetAssetData(tr.AssetData);
        }


        public void AssetUploadCompleteCallbackHandler(Packet packet, Simulator simulator)
        {
            AssetUploadCompletePacket complete = (AssetUploadCompletePacket)packet;
            
            TransferRequest tr = (TransferRequest)htUploadRequests[complete.AssetBlock.UUID];
            if (complete.AssetBlock.Success)
            {
                tr.Completed = true;
                tr.Status = true;
                tr.StatusMsg = "Success";
            }
            else
            {
                tr.Completed = true;
                tr.Status = false;
                tr.StatusMsg = "Server returned failed";
            }
        }

        public void TransferInfoCallbackHandler(Packet packet, Simulator simulator)
        {
            TransferInfoPacket info = (TransferInfoPacket)packet;
            TransferRequest tr = (TransferRequest)htDownloadRequests[info.TransferInfo.TransferID];

            if (tr == null) { return; }

            if (info.TransferInfo.Status == -2)
            {
                tr.Completed = true;
                tr.Status = false;
                tr.StatusMsg = "Asset Status -2 :: Likely Status Not Found";

                tr.Size = 1;
                tr.AssetData = new byte[1];

            }
            else
            {
                tr.Size = info.TransferInfo.Size;
                tr.AssetData = new byte[info.TransferInfo.Size];
            }
        }

        public void TransferPacketCallbackHandler(Packet packet, Simulator simulator)
        {
            TransferPacketPacket transfer = (TransferPacketPacket)packet;

            // Append data to data received.
            TransferRequest tr = (TransferRequest)htDownloadRequests[transfer.TransferData.TransferID];

            if (tr == null) { return; }

            Array.Copy(transfer.TransferData.Data, 0, tr.AssetData, tr.Received, transfer.TransferData.Data.Length);
            tr.Received += transfer.TransferData.Data.Length;

            // If we've gotten all the data, mark it completed.
            if (tr.Received >= tr.Size)
            {
                tr.Completed = true;
            }

        }

        public void ConfirmXferPacketCallbackHandler(Packet packet, Simulator simulator)
        {
            ConfirmXferPacketPacket confirm = (ConfirmXferPacketPacket)packet;
            TransferRequest tr = (TransferRequest)htUploadRequests[confirm.XferID.ID];

            if (tr != null)
            {
                SendXferPacketPacket xfer = new SendXferPacketPacket();
                xfer.DataPacket.Data = new byte[1000];

                xfer.XferID.ID = confirm.XferID.ID;
                // Set the last packet so we know where we're at in the transfer
                xfer.XferID.Packet = (uint)++tr.LastPacket;

                if ((tr.LastPacket + 1) * 1000 <= tr.AssetData.Length)
                {
                    Array.Copy(tr.AssetData, tr.LastPacket * 1000, xfer.DataPacket.Data, 0, 1000);
                }
                else
                {
                    Array.Copy(tr.AssetData, tr.LastPacket * 1000, xfer.DataPacket.Data, 0, 
                        tr.AssetData.Length - tr.LastPacket * 1000);
                }

                Client.Network.SendPacket((Packet)xfer);
            }
            else
            {
                Client.Log("Received a ConfirmXferPacket with an unknown ID " + confirm.XferID.ID, Helpers.LogLevel.Warning);
            }
        }

        public void RequestXferCallbackHandler(Packet packet, Simulator simulator)
        {
            RequestXferPacket request = (RequestXferPacket)packet;
            TransferRequest tr = (TransferRequest)htUploadRequests[request.XferID.VFileID];
            byte[] packetData = new byte[1004];

            // First four bytes of the data are the total asset length
            packetData[0] = (byte)(tr.AssetData.Length % 256);
            packetData[1] = (byte)((tr.AssetData.Length >> 8) % 256);
            packetData[2] = (byte)((tr.AssetData.Length >> 16) % 256);
            packetData[3] = (byte)((tr.AssetData.Length >> 24) % 256);

            // Copy the first 1000 bytes of the asset in to the packet
            Array.Copy(tr.AssetData, 0, packetData, 4, 1000);
            
            // Set the last packet so we know where we're at in the transfer
            tr.LastPacket = 0;

            SendXferPacketPacket xfer = new SendXferPacketPacket();
            xfer.XferID.ID = request.XferID.ID;
            xfer.XferID.Packet = 0;
            xfer.DataPacket.Data = packetData;

            // Send the first packet of the transfer out
            Client.Network.SendPacket((Packet)xfer);
        }

        public static int getUnixtime()
        {
            TimeSpan ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            return (int)ts.TotalSeconds;
        }
    }
}
