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

using System.Collections;
using System;
using System.Xml;

using libsecondlife;
using libsecondlife.AssetSystem;

namespace libsecondlife.InventorySystem
{
    /// <summary>
    /// Summary description for InventoryFolder.
    /// </summary>
    public class InventoryItem : InventoryBase
    {
        private const uint FULL_MASK_PERMISSIONS = 2147483647;

        public string Name
        {
            get { return base.name; }
            set
            {
                name = value;
                UpdateItem();
            }
        }

        internal LLUUID folderID = new LLUUID();
        public LLUUID FolderID
        {
            get { return folderID; }
            set
            {
                InventoryFolder iTargetFolder = base.IManager.getFolder(value);
                if (iTargetFolder == null)
                {
                    throw new Exception("Target Folder [" + value + "] does not exist.");
                }

                base.IManager.getFolder(this.FolderID).alContents.Remove(this);
                iTargetFolder.alContents.Add(this);

                folderID = value;
                UpdateItem();
            }
        }

        internal LLUUID itemID = new LLUUID();
        public LLUUID ItemID
        {
            get { return itemID; }
        }

        internal sbyte invType = 0;
        public sbyte InvType
        {
            get { return invType; }
        }

        internal sbyte type = 0;
        public sbyte Type
        {
            get { return type; }
            set
            {
                type = value;
                UpdateItem();
            }
        }

        internal string description = "";
        public string Description
        {
            get { return description; }
            set
            {
                description = value;
                UpdateItem();
            }
        }

        internal uint crc = 0;
        public uint CRC
        {
            get { return crc; }
            set
            {
                crc = value;
            }
        }

        internal LLUUID ownerID = new LLUUID();
        public LLUUID OwnerID
        {
            get { return ownerID; }
        }

        internal LLUUID creatorID = new LLUUID();
        public LLUUID CreatorID
        {
            get { return creatorID; }
        }

        internal Asset asset;
        public Asset Asset
        {
            get
            {
                if (asset != null)
                {
                    return asset;
                }
                else
                {
                    if ((AssetID != null) && (AssetID != new LLUUID()))
                    {
                        base.IManager.AssetManager.GetInventoryAsset(this);
                        return Asset;
                    }
                }
                return null;
            }
        }

        internal LLUUID assetID = new LLUUID();
        public LLUUID AssetID
        {
            get { return assetID; }
            set
            {
                assetID = value;
                UpdateItem();
            }
        }

        internal LLUUID groupID = new LLUUID();
        public LLUUID GroupID
        {
            get { return groupID; }
            set
            {
                groupID = value;
                UpdateItem();
            }
        }

        internal bool groupOwned = false;
        public bool GroupOwned
        {
            get { return groupOwned; }
            set
            {
                groupOwned = value;
                UpdateItem();
            }
        }

        internal int creationDate = (int)((TimeSpan)(DateTime.UtcNow - new DateTime(1970, 1, 1))).TotalSeconds;
        public int CreationDate
        {
            get { return creationDate; }
        }

        internal byte saleType = 0;
        public byte SaleType
        {
            get { return saleType; }
            set
            {
                saleType = value;
                UpdateItem();
            }
        }

        internal uint baseMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask
        {
            get { return baseMask; }
        }

        internal int salePrice = 0;
        public int SalePrice
        {
            get { return salePrice; }
            set
            {
                salePrice = value;
                UpdateItem();
            }
        }

        internal uint everyoneMask = 0;
        public uint EveryoneMask
        {
            get { return everyoneMask; }
            set
            {
                everyoneMask = value;
                UpdateItem();
            }
        }

        internal uint flags = 0;
        public uint Flags
        {
            get { return flags; }
            set
            {
                flags = value;
                UpdateItem();
            }
        }

        internal uint nextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask
        {
            get { return nextOwnerMask; }
            set
            {
                nextOwnerMask = value;
                UpdateItem();
            }
        }

        internal uint groupMask = 0;
        public uint GroupMask
        {
            get { return groupMask; }
            set
            {
                groupMask = value;
                UpdateItem();
            }
        }

        internal uint ownerMask = FULL_MASK_PERMISSIONS;
        public uint OwnerMask
        {
            get { return ownerMask; }
        }

        internal InventoryItem(InventoryManager manager)
            : base(manager)
        {
        }

        internal InventoryItem(InventoryManager Manager, string Name, LLUUID ID, LLUUID FolderID, sbyte InvType, sbyte Type, LLUUID UUIDOwnerCreater)
            : base(Manager)
        {
            name = Name;
            itemID = ID;
            folderID = FolderID;
            invType = InvType;
            type = Type;
            ownerID = UUIDOwnerCreater;
            creatorID = UUIDOwnerCreater;

            UpdateCRC();
        }

        internal InventoryItem(InventoryManager Manager, string Name, string Description, LLUUID ID, LLUUID FolderID, sbyte InvType, sbyte Type, LLUUID UUIDOwnerCreater)
            : base(Manager)
        {
            name = Name;
            description = Description;
            itemID = ID;
            folderID = FolderID;
            invType = InvType;
            type = Type;
            ownerID = UUIDOwnerCreater;
            creatorID = UUIDOwnerCreater;

            UpdateCRC();
        }

        public override bool Equals(object o)
        {
            if ((o is InventoryItem) == false)
            {
                return false;
            }

            return this.itemID == ((InventoryItem)o).itemID;
        }

        public override int GetHashCode()
        {
            return this.itemID.GetHashCode();
        }

        public int CompareTo(object obj)
        {
            if (obj is InventoryBase)
            {
                InventoryBase temp = (InventoryBase)obj;
                return this.name.CompareTo(temp.name);
            }
            throw new ArgumentException("object is not an InventoryItem");
        }

        private void UpdateItem()
        {
            UpdateCRC();
            base.IManager.ItemUpdate(this);
        }

        private void UpdateCRC()
        {
            crc = Helpers.InventoryCRC(creationDate, saleType, invType, type, assetID, groupID, salePrice, ownerID,
                creatorID, itemID, folderID, everyoneMask, flags, nextOwnerMask, groupMask, ownerMask);
        }

        public void MoveTo(InventoryFolder targetFolder)
        {
            this.FolderID = targetFolder.FolderID;
        }
        public void MoveTo(LLUUID targetFolderID)
        {
            this.FolderID = targetFolderID;
        }

        public void CopyTo(LLUUID targetFolder)
        {
            base.IManager.ItemCopy(this.ItemID, targetFolder);
        }

        public void GiveTo(LLUUID ToAgentID)
        {
            base.IManager.ItemGiveTo(this, ToAgentID);
        }

        public void Delete()
        {
            base.IManager.getFolder(this.FolderID).alContents.Remove(this);
            base.IManager.ItemRemove(this);

        }

        public void ClearAssetTest()
        {
            asset = null;
        }

        virtual internal void SetAssetData(byte[] assetData)
        {
            if (asset == null)
            {
                if (AssetID != null)
                {
                    asset = new Asset(AssetID, Type, assetData);
                }
                else
                {
                    asset = new Asset(LLUUID.GenerateUUID(), Type, assetData);
                    AssetID = asset.AssetID;
                }
            }
            else
            {
                asset.AssetData = assetData;
            }
        }

        override public string ToXML(bool outputAssets)
        {
            string output = "<item ";

            output += "name = '" + xmlSafe(Name) + "' ";
            output += "uuid = '" + ItemID + "' ";
            output += "invtype = '" + InvType + "' ";
            output += "type = '" + Type + "' ";

            output += "description = '" + xmlSafe(Description) + "' ";
            output += "crc = '" + CRC + "' ";
            output += "ownerid = '" + OwnerID + "' ";
            output += "creatorid = '" + CreatorID + "' ";

            output += "assetid = '" + AssetID + "' ";
            output += "groupid = '" + GroupID + "' ";

            output += "groupowned = '" + GroupOwned + "' ";
            output += "creationdate = '" + CreationDate + "' ";
            output += "flags = '" + Flags + "' ";

            output += "saletype = '" + SaleType + "' ";
            output += "saleprice = '" + SalePrice + "' ";
            output += "basemask = '" + BaseMask + "' ";
            output += "everyonemask = '" + EveryoneMask + "' ";
            output += "nextownermask = '" + NextOwnerMask + "' ";
            output += "groupmask = '" + GroupMask + "' ";
            output += "ownermask = '" + OwnerMask + "' ";

            output += "/>\n";

            return output;
        }
    }
}
