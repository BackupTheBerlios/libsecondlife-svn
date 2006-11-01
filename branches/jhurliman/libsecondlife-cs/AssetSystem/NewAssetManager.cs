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
using System.Security.Cryptography;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife
{
    public enum AssetType
    {
        Image = 0,
        Notecard = 7
    }

    public class Asset
    {
        public LLUUID ID;
        public byte[] Data;
        public AssetType Type;
        public bool Tempfile;
    }

    public class ImageAsset : Asset
    {
        public float DownloadPriority;
    }

    public class AssetTransferStatus
    {
        public Asset Asset;
        public LLUUID TransactionID;
        public bool Success;
        public bool Complete;
        public uint Transferred;

        public AssetTransferStatus(Asset asset, LLUUID transactionID)
        {
            Asset = asset;
            TransactionID = transactionID;
            Success = false;
            Complete = false;
        }
    }

    public class AssetManager
	{
        public delegate void UploadCompleteCallback(AssetTransferStatus status);
        public delegate void DownloadCompleteCallback(AssetTransferStatus status);
        public delegate void ImageDownloadCompleteCallback(AssetTransferStatus status);
        public delegate void ImageDataReceivedCallback(AssetTransferStatus status);

        private SecondLife Client;
        private Dictionary<LLUUID, AssetTransferStatus> DownloadRequests;
        private AssetTransferStatus UploadRequest;
        private UploadCompleteCallback OnUploadComplete;
        private ImageDownloadCompleteCallback OnImageDownloadComplete;

        public AssetManager(SecondLife client)
        {
            Client = client;

            DownloadRequests = new Dictionary<LLUUID, AssetTransferStatus>();

            // Used to upload small assets, or as an initial packet for large transfers
            Client.Network.RegisterCallback(PacketType.AssetUploadComplete, new PacketCallback(AssetUploadCompleteHandler));

            // Transfer Packets for downloading large assets
            Client.Network.RegisterCallback(PacketType.TransferInfo, new PacketCallback(TransferInfoHandler));
            Client.Network.RegisterCallback(PacketType.TransferPacket, new PacketCallback(TransferPacketHandler));

            // XFer packets for uploading large assets
            Client.Network.RegisterCallback(PacketType.ConfirmXferPacket, new PacketCallback(ConfirmXferPacketHandler));
            Client.Network.RegisterCallback(PacketType.RequestXfer, new PacketCallback(RequestXferHandler));
        }

        public void BeginDownloadImage(LLUUID imageID, float priority, ImageDownloadCompleteCallback idcc)
        {
            Dictionary<LLUUID, float> images = new Dictionary<LLUUID, float>();
            images[imageID] = priority;

            BeginDownloadImages(images, idcc);
        }

        public void BeginDownloadImages(Dictionary<LLUUID, float> imageAndPriority, ImageDownloadCompleteCallback idcc)
        {
            OnImageDownloadComplete = idcc;

            int i = 0;
            RequestImagePacket request = new RequestImagePacket();

            request.AgentData.AgentID = Client.Network.AgentID;
            request.AgentData.SessionID = Client.Network.SessionID;
            request.RequestImage = new RequestImagePacket.RequestImageBlock[imageAndPriority.Count];

            foreach (KeyValuePair<LLUUID, float> iap in imageAndPriority)
            {
                request.RequestImage[i] = new RequestImagePacket.RequestImageBlock();
                request.RequestImage[i].DownloadPriority = iap.Value;
                // TODO: What is DiscardLevel?
                request.RequestImage[i].DiscardLevel = 0;
                request.RequestImage[i].Packet = 0;
                request.RequestImage[i].Image = iap.Key;
                // TODO: What is Type?
                request.RequestImage[i].Type = 0;

                if (!DownloadRequests.ContainsKey(iap.Key) || DownloadRequests[iap.Key].Complete)
                {
                    // Starting a transfer for an image that hasn't been downloaded this 
                    // session, or has already completed downloading and we are requesting
                    // again. Add
                    ImageAsset image = new ImageAsset();
                    image.Type = AssetType.Image;
                    image.ID = iap.Key;
                    image.DownloadPriority = iap.Value;

                    AssetTransferStatus imageDownload = new AssetTransferStatus(image, LLUUID.GenerateUUID());

                    lock (DownloadRequests)
                    {
                        // Add the new image download to our dictionary
                        DownloadRequests[iap.Key] = imageDownload;
                    }

                    i++;
                }
            }

            Client.Network.SendPacket(request);
        }

        public void BeginUploadAsset(Asset asset, UploadCompleteCallback ucc)
        {
            if (UploadRequest == null || UploadRequest.Complete)
            {
                OnUploadComplete = ucc;
                UploadRequest = new AssetTransferStatus(asset, LLUUID.GenerateUUID());

                AssetUploadRequestPacket upload = new AssetUploadRequestPacket();
                upload.AssetBlock.TransactionID = UploadRequest.TransactionID;
                upload.AssetBlock.Type = (sbyte)asset.Type;
                upload.AssetBlock.Tempfile = asset.Tempfile;
                upload.AssetBlock.StoreLocal = false;
                
                if (asset.Data.Length > 500)
                {
                    upload.AssetBlock.AssetData = new byte[0];
                }
                else
                {
                    upload.AssetBlock.AssetData = asset.Data;
                }

                Client.Network.SendPacket(upload);

                Client.Log("Beginning upload of " + asset.Data.Length + " byte asset, type " + asset.Type.ToString(), 
                    LogLevel.Info);
            }
            else
            {
                throw new Exception("Attempted to upload an asset when a current transfer is in progress");
            }
        }

        private void AssetUploadCompleteHandler(Packet packet, Simulator simulator)
        {
            AssetUploadCompletePacket complete = (AssetUploadCompletePacket)packet;

            UploadRequest.Asset.ID = complete.AssetBlock.UUID;
            UploadRequest.Complete = true;
            UploadRequest.Success = complete.AssetBlock.Success;

            if (OnUploadComplete != null)
            {
                OnUploadComplete(UploadRequest);
            }
        }

        private void TransferInfoHandler(Packet packet, Simulator simulator)
        {
            ;
        }

        private void TransferPacketHandler(Packet packet, Simulator simulator)
        {
            ;
        }

        private void ConfirmXferPacketHandler(Packet packet, Simulator simulator)
        {
            ;
        }

        private void RequestXferHandler(Packet packet, Simulator simulator)
        {
            ;
        }
    }
}
