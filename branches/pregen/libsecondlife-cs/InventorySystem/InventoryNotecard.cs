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

using libsecondlife;
using libsecondlife.AssetSystem;

namespace libsecondlife.InventorySystem
{
    /// <summary>
    /// Summary description for InventoryNotecard.
    /// </summary>
    public class InventoryNotecard : InventoryItem
    {
        public string Body
        {
            get
            {
                if (Asset != null)
                {
                    return ((AssetNotecard)Asset).Body;
                }
                else
                {
                    if ((AssetID != null) && (AssetID != new LLUUID()))
                    {
                        base.IManager.AssetManager.GetInventoryAsset(this);
                        return ((AssetNotecard)Asset).Body;
                    }
                }

                return "";
            }

            set
            {
                base.asset = new AssetNotecard(LLUUID.GenerateUUID(), value);
                base.IManager.AssetManager.UploadAsset(Asset);
                this.AssetID = Asset.AssetID;
            }
        }

        internal InventoryNotecard(InventoryManager manager, string name, string description, LLUUID id, LLUUID folderID, LLUUID uuidOwnerCreater)
            : base(manager, name, description, id, folderID, 7, 7, uuidOwnerCreater)
        {
        }

        internal InventoryNotecard(InventoryManager manager, InventoryItem ii)
            : base(manager, ii.name, ii.description, ii.itemID, ii.folderID, ii.invType, ii.type, ii.creatorID)
        {
            if ((ii.InvType != 7) || (ii.Type != Asset.ASSET_TYPE_NOTECARD))
            {
                throw new Exception("The InventoryItem cannot be converted to a Notecard, wrong InvType/Type.");
            }

            this.IManager = manager;
            this.asset = ii.asset;
            this.assetID = ii.assetID;
            this.baseMask = ii.baseMask;
            this.crc = ii.crc;
            this.creationDate = ii.creationDate;
            this.everyoneMask = ii.everyoneMask;
            this.flags = ii.flags;
            this.groupID = ii.groupID;
            this.groupMask = ii.groupMask;
            this.groupOwned = ii.groupOwned;
            this.invType = ii.invType;
            this.nextOwnerMask = ii.nextOwnerMask;
            this.ownerID = ii.ownerID;
            this.ownerMask = ii.ownerMask;
            this.salePrice = ii.salePrice;
            this.saleType = ii.saleType;
            this.type = ii.type;
        }

        override internal void SetAssetData(byte[] assetData)
        {
            if (asset == null)
            {
                if (AssetID != null)
                {
                    asset = new AssetNotecard(AssetID, assetData);
                }
                else
                {
                    asset = new AssetNotecard(LLUUID.GenerateUUID(), assetData);
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
            string output = "<notecard ";

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

            output += ">";

            if (outputAssets)
            {
                output += xmlSafe(Body);
            }

            output += "</notecard>";

            return output;
        }
    }
}
