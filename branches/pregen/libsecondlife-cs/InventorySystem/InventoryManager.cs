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
using libsecondlife.AssetSystem;
using libsecondlife.Packets;


namespace libsecondlife.InventorySystem
{
    /// <summary>
    /// Summary description for Inventory.
    /// </summary>
    public class InventoryManager
    {
        public const int FETCH_INVENTORY_SORT_NAME = 0;
        public const int FETCH_INVENTORY_SORT_TIME = 1;

        // Reference to the Client Library
        private SecondLife Client;

        // Reference to the Asset Manager
        private static AssetManager assetManager;
        internal AssetManager AssetManager
        {
            get { return assetManager; }
        }

        // UUID of Root Inventory Folder
        private LLUUID uuidRootFolder;

        // Setup a hashtable to easily lookup folders by UUID
        private Hashtable htFoldersByUUID = new Hashtable();

        // Setup a Hashtable to track download progress
        private Hashtable htFolderDownloadStatus;
        private ArrayList alFolderRequestQueue;

        private int LastPacketRecieved;

        private class DescendentRequest
        {
            public LLUUID FolderID;

            public int Expected = int.MaxValue;
            public int Received = 0;
            public int LastReceived = 0;

            public bool FetchFolders = true;
            public bool FetchItems = true;

            public DescendentRequest(LLUUID folderID)
            {
                FolderID = folderID;
                LastReceived = Environment.TickCount;
            }

            public DescendentRequest(LLUUID folderID, bool fetchFolders, bool fetchItems)
            {
                FolderID = folderID;
                FetchFolders = fetchFolders;
                FetchItems = fetchItems;
                LastReceived = Environment.TickCount;
            }

        }

        // Each InventorySystem needs to be initialized with a client (for network access to SL)
        // and root folder.  The root folder can be the root folder of an object OR an agent.
        public InventoryManager(SecondLife client, LLUUID rootFolder)
        {
            Client = client;
            if (assetManager == null)
            {
                assetManager = new AssetManager(Client);
            }

            uuidRootFolder = rootFolder;

            resetFoldersByUUID();

            // Setup the callback
            PacketCallback InventoryDescendentsCallback = new PacketCallback(InventoryDescendentsHandler);
            Client.Network.RegisterCallback(PacketType.InventoryDescendents, InventoryDescendentsCallback);
        }

        public AssetManager getAssetManager()
        {
            Console.WriteLine("It is not recommended that you access the asset manager directly");
            return AssetManager;
        }

        private void resetFoldersByUUID()
        {
            // Init folder structure with root
            htFoldersByUUID = new Hashtable();

            InventoryFolder ifRootFolder = new InventoryFolder(this, "My Inventory", uuidRootFolder, null);
            htFoldersByUUID[uuidRootFolder] = ifRootFolder;
        }

        public InventoryFolder getRootFolder()
        {
            return (InventoryFolder)htFoldersByUUID[uuidRootFolder];
        }

        public InventoryFolder getFolder(LLUUID folderID)
        {
            return (InventoryFolder)htFoldersByUUID[folderID];
        }

        public InventoryFolder getFolder(String sFolderPath)
        {
            string sSecretConst = "+@#%$#$%^%^%$^$%SV$#%FR$G";
            sFolderPath = sFolderPath.Replace("//", sSecretConst);

            char[] seperators = new char[1];
            seperators[0] = '/';
            string[] sFolderPathParts = sFolderPath.Split(seperators);
            for (int i = 0; i < sFolderPathParts.Length; i++)
            {
                sFolderPathParts[i] = sFolderPathParts[i].Replace(sSecretConst, "/");
            }

            return getFolder(new Queue(sFolderPathParts));
        }
        private InventoryFolder getFolder(Queue qFolderPath)
        {
            return getFolder(qFolderPath, getRootFolder());
        }

        private InventoryFolder getFolder(Queue qFolderPath, InventoryFolder ifRoot)
        {
            string sCurFolder = (string)qFolderPath.Dequeue();

            foreach (InventoryBase ibFolder in ifRoot.alContents)
            {
                if (ibFolder is libsecondlife.InventorySystem.InventoryFolder)
                {
                    if (((InventoryFolder)ibFolder).Name.Equals(sCurFolder))
                    {
                        if (qFolderPath.Count == 0)
                        {
                            return (InventoryFolder)ibFolder;
                        }
                        else
                        {
                            return getFolder(qFolderPath, (InventoryFolder)ibFolder);
                        }
                    }
                }
            }

            return null;
        }

        private void RequestFolder(DescendentRequest dr)
        {
            FetchInventoryDescendentsPacket fetch = new FetchInventoryDescendentsPacket();
            htFolderDownloadStatus[dr.FolderID] = dr;

            fetch.AgentData.AgentID = Client.Network.AgentID;
            fetch.InventoryData.FetchFolders = dr.FetchFolders;
            fetch.InventoryData.FetchItems = dr.FetchItems;
            fetch.InventoryData.FolderID = dr.FolderID;
            fetch.InventoryData.OwnerID = Client.Network.AgentID;
            fetch.InventoryData.SortOrder = FETCH_INVENTORY_SORT_NAME;

            Client.Network.SendPacket((Packet)fetch);
        }

        internal InventoryFolder FolderCreate(String name, LLUUID parentid)
        {
            InventoryFolder ifolder = new InventoryFolder(this, name, LLUUID.GenerateUUID(), parentid);
            ifolder.type = -1;

            if (htFoldersByUUID.Contains(ifolder.ParentID))
            {
                if (((InventoryFolder)htFoldersByUUID[ifolder.ParentID]).alContents.Contains(ifolder) == false)
                {
                    // Add new folder to the contents of the parent folder
                    ((InventoryFolder)htFoldersByUUID[ifolder.ParentID]).alContents.Add(ifolder);
                }
            }
            else
            {
                throw new Exception("Parent Folder " + ifolder.ParentID + " does not exist in this Inventory Manager");
            }

            if (htFoldersByUUID.Contains(ifolder.FolderID) == false)
            {
                htFoldersByUUID[ifolder.FolderID] = ifolder;
            }

            CreateInventoryFolderPacket create = new CreateInventoryFolderPacket();
            create.AgentData.AgentID = Client.Network.AgentID;
            create.FolderData.FolderID = ifolder.FolderID;
            create.FolderData.Name = Helpers.StringToField(ifolder.Name);
            create.FolderData.ParentID = ifolder.ParentID;

            Client.Network.SendPacket((Packet)create);

            return ifolder;
        }

        internal void FolderRemove(InventoryFolder ifolder)
        {
            FolderRemove(ifolder.FolderID);
        }

        internal void FolderRemove(LLUUID folderID)
        {
            htFoldersByUUID.Remove(folderID);

            RemoveInventoryFolderPacket remove = new RemoveInventoryFolderPacket();
            remove.AgentData.AgentID = Client.Network.AgentID;
            remove.FolderData = new RemoveInventoryFolderPacket.FolderDataBlock[1];
            remove.FolderData[0].FolderID = folderID;

            Client.Network.SendPacket((Packet)remove);
        }

        internal void FolderMove(InventoryFolder iFolder, LLUUID newParentID)
        {
            MoveInventoryFolderPacket move = new MoveInventoryFolderPacket();
            move.AgentData.AgentID = Client.Network.AgentID;
            move.AgentData.Stamp = true;
            move.InventoryData = new MoveInventoryFolderPacket.InventoryDataBlock[1];
            move.InventoryData[0].FolderID = iFolder.FolderID;
            move.InventoryData[0].ParentID = newParentID;

            Client.Network.SendPacket((Packet)move);
        }

        internal void FolderRename(InventoryFolder ifolder)
        {
            UpdateInventoryFolderPacket update = new UpdateInventoryFolderPacket();
            update.AgentData.AgentID = Client.Network.AgentID;
            update.FolderData = new UpdateInventoryFolderPacket.FolderDataBlock[1];
            update.FolderData[0].FolderID = ifolder.FolderID;
            update.FolderData[0].Name = Helpers.StringToField(ifolder.Name);
            update.FolderData[0].ParentID = ifolder.ParentID;
            update.FolderData[0].Type = ifolder.Type;

            Client.Network.SendPacket((Packet)update);
        }

        internal void ItemUpdate(InventoryItem iitem)
        {
            UpdateInventoryItemPacket update = new UpdateInventoryItemPacket();
            update.AgentData.AgentID = Client.Network.AgentID;
            update.AgentData.SessionID = Client.Network.SessionID;
            update.InventoryData = new UpdateInventoryItemPacket.InventoryDataBlock[1];
            update.InventoryData[0].AssetID = iitem.AssetID;
            update.InventoryData[0].BaseMask = iitem.BaseMask;
            update.InventoryData[0].CRC = iitem.CRC;
            update.InventoryData[0].CreationDate = iitem.CreationDate;
            update.InventoryData[0].CreatorID = iitem.CreatorID;
            update.InventoryData[0].Description = Helpers.StringToField(iitem.Description);
            update.InventoryData[0].EveryoneMask = iitem.EveryoneMask;
            update.InventoryData[0].Flags = iitem.Flags;
            update.InventoryData[0].FolderID = iitem.FolderID;
            update.InventoryData[0].GroupID = iitem.GroupID;
            update.InventoryData[0].GroupMask = iitem.GroupMask;
            update.InventoryData[0].GroupOwned = iitem.GroupOwned;
            update.InventoryData[0].InvType = iitem.InvType;
            update.InventoryData[0].ItemID = iitem.ItemID;
            update.InventoryData[0].Name = Helpers.StringToField(iitem.Name);
            update.InventoryData[0].NextOwnerMask = iitem.NextOwnerMask;
            update.InventoryData[0].OwnerID = iitem.OwnerID;
            update.InventoryData[0].OwnerMask = iitem.OwnerMask;
            update.InventoryData[0].SalePrice = iitem.SalePrice;
            update.InventoryData[0].SaleType = iitem.SaleType;
            update.InventoryData[0].Type = iitem.Type;
            
            Client.Network.SendPacket((Packet)update);
        }

        internal void ItemCopy(LLUUID ItemID, LLUUID TargetFolderID)
        {
            CopyInventoryItemPacket copy = new CopyInventoryItemPacket();
            copy.AgentData.AgentID = Client.Network.AgentID;
            copy.InventoryData = new CopyInventoryItemPacket.InventoryDataBlock[1];
            copy.InventoryData[0].NewFolderID = TargetFolderID;
            copy.InventoryData[0].OldItemID = ItemID;

            Client.Network.SendPacket((Packet)copy);
        }

        internal void ItemGiveTo(InventoryItem iitem, LLUUID ToAgentID)
        {
            ImprovedInstantMessagePacket im = new ImprovedInstantMessagePacket();
            im.AgentData.AgentID = Client.Network.AgentID;
            im.AgentData.SessionID = Client.Network.SessionID;

            byte[] BinaryBucket = new byte[17];
			BinaryBucket[0] = (byte)iitem.Type;
			Array.Copy(iitem.ItemID.Data, 0, BinaryBucket, 1, 16);
            im.MessageBlock.BinaryBucket = BinaryBucket;

            im.MessageBlock.Dialog = 4;
            im.MessageBlock.FromAgentName = Helpers.StringToField(Client.Avatar.FirstName + " " + Client.Avatar.LastName);
            im.MessageBlock.FromGroup = false;
            im.MessageBlock.ID = LLUUID.GenerateUUID();
            im.MessageBlock.Message = Helpers.StringToField(iitem.Name);
            im.MessageBlock.Offline = 0;
            im.MessageBlock.ParentEstateID = 0;
            im.MessageBlock.Position = Client.Avatar.Position;
            im.MessageBlock.RegionID = new LLUUID();
            im.MessageBlock.Timestamp = 0;
            im.MessageBlock.ToAgentID = ToAgentID;

            Client.Network.SendPacket((Packet)im);
        }

        internal void ItemRemove(InventoryItem iitem)
        {
            RemoveInventoryItemPacket remove = new RemoveInventoryItemPacket();
            remove.AgentData.AgentID = Client.Network.AgentID;
            remove.InventoryData = new RemoveInventoryItemPacket.InventoryDataBlock[1];
            remove.InventoryData[0].ItemID = iitem.ItemID;

            InventoryFolder ifolder = getFolder(iitem.FolderID);
            ifolder.alContents.Remove(iitem);

            Client.Network.SendPacket((Packet)remove);
        }

        internal InventoryNotecard NewNotecard(string Name, string Description, string Body, LLUUID FolderID)
        {
            LLUUID ItemID = LLUUID.GenerateUUID();
            InventoryNotecard iNotecard = new InventoryNotecard(this, Name, Description, ItemID, FolderID, Client.Network.AgentID);

            // Create this notecard on the server.
            ItemUpdate(iNotecard);

            if ((Body != null) && (Body.Equals("") != true))
            {
                iNotecard.Body = Body;
            }

            return iNotecard;
        }

        internal InventoryImage NewImage(string Name, string Description, byte[] j2cdata, LLUUID FolderID)
        {
            LLUUID ItemID = LLUUID.GenerateUUID();
            InventoryImage iImage = new InventoryImage(this, Name, Description, ItemID, FolderID, Client.Network.AgentID);

            // Create this notecard on the server.
            ItemUpdate(iImage);

            if ((j2cdata != null) && (j2cdata.Length != 0))
            {
                iImage.J2CData = j2cdata;
            }

            return iImage;
        }

        public void DownloadInventory()
        {
            resetFoldersByUUID();

            if (htFolderDownloadStatus == null)
            {
                // Create status table
                htFolderDownloadStatus = new Hashtable();
            }
            else
            {
                if (htFolderDownloadStatus.Count != 0)
                {
                    throw new Exception("Inventory Download requested while previous download in progress.");
                }
            }

            if (alFolderRequestQueue == null)
            {
                alFolderRequestQueue = new ArrayList();
            }

            // Set last packet received to now, just so out time-out timer works
            LastPacketRecieved = Environment.TickCount;

            // Send Packet requesting the root Folder, 
            // this should recurse through all folders
            RequestFolder(new DescendentRequest(uuidRootFolder));

            while ((htFolderDownloadStatus.Count > 0) || (alFolderRequestQueue.Count > 0))
            {
                if (htFolderDownloadStatus.Count == 0)
                {
                    DescendentRequest dr = (DescendentRequest)alFolderRequestQueue[0];
                    alFolderRequestQueue.RemoveAt(0);
                    RequestFolder(dr);
                }

                if ((Environment.TickCount - LastPacketRecieved) > 5000)
                {
                    Client.Log("Time-out while waiting for packets", Helpers.LogLevel.Warning);
                    
                    // TODO: We shouldn't be interacting directly with the console from libsecondlife
                    Console.WriteLine("Current Status:");

                    // have to make a seperate list otherwise we run into modifying the original array
                    // while still enumerating it.
                    ArrayList alRestartList = new ArrayList();

                    if (htFolderDownloadStatus[0] != null)
                    {
                        Console.WriteLine(htFolderDownloadStatus[0].GetType());
                    }

                    foreach (DescendentRequest dr in htFolderDownloadStatus)
                    {
                        Console.WriteLine(dr.FolderID + " " + dr.Expected + " / " + dr.Received + " / " + dr.LastReceived);

                        alRestartList.Add(dr);
                    }

                    LastPacketRecieved = Environment.TickCount;
                    foreach (DescendentRequest dr in alRestartList)
                    {
                        RequestFolder(dr);
                    }

                }
                Client.Tick();

            }
        }





        /*
        Low 00333 - InventoryDescendents - Untrusted - Unencoded
            1044 ItemData (Variable)
                0047 GroupOwned (BOOL / 1)
                0149 CRC (U32 / 1)
                0159 CreationDate (S32 / 1)
                0345 SaleType (U8 / 1)
                0395 BaseMask (U32 / 1)
                0506 Name (Variable / 1)
                0562 InvType (S8 / 1)
                0630 Type (S8 / 1)
                0680 AssetID (LLUUID / 1)
                0699 GroupID (LLUUID / 1)
                0716 SalePrice (S32 / 1)
                0719 OwnerID (LLUUID / 1)
                0736 CreatorID (LLUUID / 1)
                0968 ItemID (LLUUID / 1)
                1025 FolderID (LLUUID / 1)
                1084 EveryoneMask (U32 / 1)
                1101 Description (Variable / 1)
                1189 Flags (U32 / 1)
                1348 NextOwnerMask (U32 / 1)
                1452 GroupMask (U32 / 1)
                1505 OwnerMask (U32 / 1)
            1297 AgentData (01)
                0219 AgentID (LLUUID / 1)
                0366 Descendents (S32 / 1)
                0418 Version (S32 / 1)
                0719 OwnerID (LLUUID / 1)
                1025 FolderID (LLUUID / 1)
            1298 FolderData (Variable)
                0506 Name (Variable / 1)
                0558 ParentID (LLUUID / 1)
                0630 Type (S8 / 1)
                1025 FolderID (LLUUID / 1)
        */
        public void InventoryDescendentsHandler(Packet packet, Simulator simulator)
        {
            //			Console.WriteLine("Status|Queue :: " + htFolderDownloadStatus.Count + "/" + qFolderRequestQueue.Count);
            
            LastPacketRecieved = Environment.TickCount;

            InventoryDescendentsPacket descendents = (InventoryDescendentsPacket)packet;
            InventoryItem invItem;
            InventoryFolder invFolder;

            int iDescendentsExpected = descendents.AgentData.Descendents;
            int iDescendentsReceivedThisBlock = 0;

            foreach (InventoryDescendentsPacket.ItemDataBlock block in descendents.ItemData)
            {
                invItem = new InventoryItem(this);

                invItem.Name = Helpers.FieldToString(block.Name);
                invItem.Description = Helpers.FieldToString(block.Description);
                invItem.invType = block.InvType;
                invItem.type = block.Type;
                invItem.saleType = block.SaleType;
                invItem.groupOwned = block.GroupOwned;
                invItem.folderID = block.FolderID;
                invItem.itemID = block.ItemID;
                invItem.assetID = block.AssetID;
                invItem.groupID = block.GroupID;
                invItem.ownerID = block.OwnerID;
                invItem.creatorID = block.CreatorID;
                invItem.crc = block.CRC;
                invItem.flags = block.Flags;
                invItem.baseMask = block.BaseMask;
                invItem.everyoneMask = block.EveryoneMask;
                invItem.nextOwnerMask = block.NextOwnerMask;
                invItem.groupMask = block.GroupMask;
                invItem.ownerMask = block.OwnerMask;
                invItem.creationDate = block.CreationDate;
                invItem.salePrice = block.SalePrice;

                if (invItem.Name != "")
                {
                    iDescendentsReceivedThisBlock++;

                    InventoryFolder ifolder = (InventoryFolder)htFoldersByUUID[invItem.FolderID];

                    if (ifolder != null)
                    {
                        if (ifolder.alContents.Contains(invItem) == false)
                        {
                            if ((invItem.InvType == 7) && (invItem.Type == Asset.ASSET_TYPE_NOTECARD))
                            {
                                invItem = new InventoryNotecard(this, invItem);
                            }
                            else if ((invItem.InvType == 0) && (invItem.Type == Asset.ASSET_TYPE_IMAGE))
                            {
                                invItem = new InventoryImage(this, invItem);
                            }

                            ifolder.alContents.Add(invItem);
                        }
                    }
                    else
                    {
                        Client.Log("Received an InventoryDescendents packet with an unknown FolderID " + 
                            invItem.FolderID.ToString(), Helpers.LogLevel.Warning);
                    }
                }
            }

            foreach (InventoryDescendentsPacket.FolderDataBlock block in descendents.FolderData)
            {
                invFolder = new InventoryFolder(this, Helpers.FieldToString(block.Name), block.FolderID, block.ParentID);

                // There is always a folder block, even if there aren't any folders.
                // The "filler" block will have an empty name
                if (invFolder.Name != "")
                {
                    iDescendentsReceivedThisBlock++;

                    // Add folder to Parent
                    InventoryFolder ifolder = (InventoryFolder)htFoldersByUUID[invFolder.ParentID];

                    if (!ifolder.alContents.Contains(invFolder))
                    {
                        ifolder.alContents.Add(invFolder);
                    }

                    // Add folder to UUID Lookup
                    htFoldersByUUID[invFolder.FolderID] = invFolder;

                    // If it's not the root, should be safe to "recurse"
                    if (!invFolder.FolderID.Equals(uuidRootFolder))
                    {
                        bool alreadyQueued = false;

                        foreach (DescendentRequest dr in alFolderRequestQueue)
                        {
                            if (dr.FolderID == invFolder.FolderID)
                            {
                                alreadyQueued = true;
                                break;
                            }
                        }

                        if (!alreadyQueued)
                        {
                            alFolderRequestQueue.Add(new DescendentRequest(invFolder.FolderID));
                        }
                    }
                }
            }

            // Update download status for this folder
            if (iDescendentsReceivedThisBlock >= iDescendentsExpected)
            {
                // We received all the descendents we're expecting for this folder
                // in this packet, so go ahead and remove folder from status list.
                htFolderDownloadStatus.Remove(descendents.AgentData.FolderID);
            }
            else
            {
                // This one packet didn't have all the descendents we're expecting
                // so update the total we're expecting, and update the total downloaded
                DescendentRequest dr = (DescendentRequest)htFolderDownloadStatus[descendents.AgentData.FolderID];
                if (dr != null)
                {
                    dr.Expected = iDescendentsExpected;
                    dr.Received += iDescendentsReceivedThisBlock;
                    dr.LastReceived = Environment.TickCount;

                    if (dr.Received >= dr.Expected)
                    {
                        // Looks like after updating, we have all the descendents, 
                        // remove from folder status.
                        htFolderDownloadStatus.Remove(descendents.AgentData.FolderID);
                    }
                    else
                    {
                        htFolderDownloadStatus[descendents.AgentData.FolderID] = dr;
                    }
                }
                else
                {
                    Client.Log("Received an InventoryDescendents packet with an unknown FolderID " + 
                        descendents.AgentData.FolderID, Helpers.LogLevel.Warning);
                }
            }
        }
    }
}
