using System;

using libsecondlife;
using libsecondlife.AssetSystem;

namespace libsecondlife.InventorySystem
{
    /// <summary>
    /// Summary description for InventoryNotecard.
    /// </summary>
    public class InventoryImage : InventoryItem
    {

        public byte[] J2CData
        {
            get
            {
                if (Asset != null)
                {
                    return ((AssetImage)Asset).J2CData;
                }
                else
                {
                    if ((AssetID != null) && (AssetID != new LLUUID()))
                    {
                        base.IManager.AssetManager.GetInventoryAsset(this);
                        return ((AssetImage)Asset).J2CData;
                    }
                }

                return null;
            }

            set
            {
                base.asset = new AssetImage(LLUUID.GenerateUUID(), value);
                base.IManager.AssetManager.UploadAsset(Asset);
                this.AssetID = Asset.AssetID;
            }

        }

        internal InventoryImage(InventoryManager manager, string name, string description, LLUUID id, LLUUID folderID, LLUUID uuidOwnerCreater)
            : base(manager, name, description, id, folderID, 0, 0, uuidOwnerCreater)
        {

        }

        internal InventoryImage(InventoryManager manager, InventoryItem ii)
            : base(manager, ii.name, ii.description, ii.itemID, ii.folderID, ii.invType, ii.type, ii.creatorID)
        {
            if ((ii.InvType != 0) || (ii.Type != Asset.ASSET_TYPE_IMAGE))
            {
                throw new Exception("The InventoryItem cannot be converted to a Image/Texture, wrong InvType/Type.");
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
            if (Asset == null)
            {
                if (AssetID != null)
                {
                    asset = new AssetImage(AssetID, assetData);
                }
                else
                {
                    asset = new AssetImage(LLUUID.GenerateUUID(), assetData);
                    AssetID = asset.AssetID;
                }
            }
            else
            {
                Asset.AssetData = assetData;
            }

        }

        override public string ToXML(bool outputAssets)
        {
            string output = "<image ";

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

            output += ">\n";

            if (outputAssets)
            {
                output += xmlSafe(Helpers.FieldToString(base.Asset.AssetData));
            }

            output += "</image>";

            return output;
        }
    }
}
