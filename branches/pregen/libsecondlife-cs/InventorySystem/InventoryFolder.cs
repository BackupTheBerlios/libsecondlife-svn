using System.Collections;
using System;
using libsecondlife;

namespace libsecondlife.InventorySystem
{
    /// <summary>
    /// Summary description for InventoryFolder.
    /// </summary>
    public class InventoryFolder : InventoryBase
    {
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                base.IManager.FolderRename(this);
            }
        }

        private LLUUID folderID;
        public LLUUID FolderID
        {
            get { return folderID; }
        }

        private LLUUID parentID;
        public LLUUID ParentID
        {
            get { return parentID; }
            set
            {
                InventoryFolder ifParent = IManager.getFolder(this.ParentID);
                ifParent.alContents.Remove(this);

                ifParent = IManager.getFolder(value);
                ifParent.alContents.Add(this);

                this.parentID = value;

                base.IManager.FolderMove(this, value);
            }
        }

        internal sbyte type;
        public sbyte Type
        {
            get { return type; }
        }

        public ArrayList alContents = new ArrayList();

        internal InventoryFolder(InventoryManager manager)
            : base(manager)
        {
            name = "";
            folderID = new LLUUID();
            parentID = new LLUUID();
            type = -1;
        }

        internal InventoryFolder(InventoryManager manager, String name, LLUUID folderID, LLUUID parentID)
            : base(manager)
        {
            this.name = name;
            this.folderID = folderID;
            this.parentID = parentID;
            this.type = 0;
        }

        internal InventoryFolder(InventoryManager manager, String name, LLUUID folderID, LLUUID parentID, sbyte Type)
            : base(manager)
        {
            this.name = name;
            this.folderID = folderID;
            this.parentID = parentID;
            this.type = Type;
        }

        internal InventoryFolder(InventoryManager manager, Hashtable htData)
            : base(manager)
        {
            this.name = (string)htData["name"];
            this.folderID = new LLUUID((string)htData["folder_id"]);
            this.parentID = new LLUUID((string)htData["parent_id"]);
            this.type = sbyte.Parse(htData["type_default"].ToString());
        }


        public InventoryFolder CreateFolder(string name)
        {
            return base.IManager.FolderCreate(name, FolderID);
        }

        public void Delete()
        {
            IManager.getFolder(this.ParentID).alContents.Remove(this);
            IManager.FolderRemove(this);
        }

        public void MoveTo(InventoryFolder newParent)
        {
            MoveTo(newParent.FolderID);
        }

        public void MoveTo(LLUUID newParentID)
        {
            this.ParentID = newParentID;
        }

        public InventoryNotecard NewNotecard(string name, string description, string body)
        {
            return base.IManager.NewNotecard(name, description, body, this.FolderID);
        }

        public InventoryImage NewImage(string name, string description, byte[] j2cdata)
        {
            return base.IManager.NewImage(name, description, j2cdata, this.FolderID);
        }

        public ArrayList GetItemByName(string name)
        {
            ArrayList items = new ArrayList();
            foreach (InventoryBase ib in alContents)
            {
                if (ib is InventoryFolder)
                {
                    items.AddRange(((InventoryFolder)ib).GetItemByName(name));
                }
                else if (ib is InventoryItem)
                {
                    if (((InventoryItem)ib).Name.Equals(name))
                    {
                        items.Add(ib);
                    }
                }
            }

            return items;
        }

        override public string ToXML(bool outputAssets)
        {
            string output = "<folder ";

            output += "name = '" + xmlSafe(Name) + "' ";
            output += "uuid = '" + FolderID + "' ";
            output += "parent = '" + ParentID + "' ";
            output += "Type = '" + Type + "' ";
            output += ">\n";

            foreach (Object oContent in alContents)
            {
                output += ((InventoryBase)oContent).ToXML(outputAssets);
            }

            output += "</folder>\n";

            return output;
        }
    }
}
