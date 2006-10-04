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
using System.Threading;
using libsecondlife.Packets;

namespace libsecondlife
{
    /// <summary>
    /// Main class to expose Second Life functionality to clients. All of the
    /// classes needed for sending and receiving data are accessible through 
    /// this class.
    /// </summary>
    public class SecondLife
    {
        /// <summary></summary>
        public NetworkManager Network;
        /// <summary></summary>
        public ParcelManager Parcels;
        /// <summary></summary>
        public MainAvatar Avatar;
        /// <summary></summary>
        public Hashtable Avatars;
        /// <summary></summary>
        public Mutex AvatarsMutex;
        /// <summary></summary>
        public Inventory Inventory;
        /// <summary></summary>
        public Region CurrentRegion;
        /// <summary></summary>
        public GridManager Grid;
        /// <summary></summary>
        public ObjectManager Objects;
        /// <summary></summary>
        public bool Debug;

        /// <summary>
        /// 
        /// </summary>
        public SecondLife()
        {
            Network = new NetworkManager(this);
            Parcels = new ParcelManager(this);
            Avatar = new MainAvatar(this);
            Avatars = new Hashtable();
            AvatarsMutex = new Mutex(false, "AvatarsMutex");
            Inventory = new Inventory(this);
            Grid = new GridManager(this);
            Objects = new ObjectManager(this);
            CurrentRegion = null;
            Debug = true;

            Network.RegisterCallback(PacketType.UUIDNameReply, new PacketCallback(GetAgentNameHandler));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Avatar.FirstName + " " + Avatar.LastName;
        }

        /// <summary>
        /// A simple sleep function that will allow pending threads to run
        /// </summary>
        public void Tick()
        {
            System.Threading.Thread.Sleep(0);
        }

        /// <summary>
        /// Send a log message to the debugging output system
        /// </summary>
        /// <param name="message">The log message</param>
        /// <param name="level">From the LogLevel enum, either Info, Warning, or Error</param>
        public void Log(string message, Helpers.LogLevel level)
        {
            if (Debug)
            {
                Console.WriteLine(level.ToString() + ": " + message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="AgentID"></param>
        public void AddAvatar(LLUUID AgentID)
        {
            // Quick sanity check
            if (Avatars.ContainsKey(AgentID))
            {
                return;
            }

            GetAgentDetails(AgentID);

            AvatarsMutex.WaitOne();
            Avatars[AgentID] = new Avatar();
            AvatarsMutex.ReleaseMutex();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="avatar"></param>
        public void AddAvatar(Avatar avatar)
        {
            AvatarsMutex.WaitOne();
            Avatars[avatar.ID] = avatar;
            AvatarsMutex.ReleaseMutex();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="AgentID"></param>
        private void GetAgentDetails(LLUUID AgentID)
        {
            //FIXME:
            //Packet packet = Packets.Communication.UUIDNameRequest(Protocol, AgentID);
            //Network.SendPacket(packet);

            //// TODO: Shouldn't this function block?
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="simulator"></param>
        private void GetAgentNameHandler(Packet packet, Simulator simulator)
        {
            if (packet.Type == PacketType.UUIDNameReply)
            {
                UUIDNameReplyPacket reply = (UUIDNameReplyPacket)packet;

                #region AvatarsMutex
                AvatarsMutex.WaitOne();
                foreach (UUIDNameReplyPacket.UUIDNameBlockBlock block in reply.UUIDNameBlock)
                {
                    ((Avatar)Avatars[block.ID]).Name = Helpers.FieldToString(block.FirstName) +
                        " " + Helpers.FieldToString(block.LastName);
                }
                AvatarsMutex.ReleaseMutex();
                #endregion AvatarsMutex
            }
        }
    }

    /// <summary>
    /// Static helper functions and global variables
    /// </summary>
    public class Helpers
    {
        /// <summary></summary>
        public readonly static string VERSION = "libsecondlife 0.0.9";
        /// <summary>This header flag signals that ACKs are appended to the packet</summary>
        public const byte MSG_APPENDED_ACKS = 0x10;
        /// <summary>This header flag signals that this packet has been sent before</summary>
        public const byte MSG_RESENT = 0x20;
        /// <summary>This header flags signals that an ACK is expected for this packet</summary>
        public const byte MSG_RELIABLE = 0x40;
        /// <summary>This header flag signals that the message is compressed using zerocoding</summary>
        public const byte MSG_ZEROCODED = 0x80;

        /// <summary>
        /// 
        /// </summary>
        public enum LogLevel
        {
            /// <summary></summary>
            Info,
            /// <summary></summary>
            Warning,
            /// <summary></summary>
            Error
        };

        /// <summary>
        /// Convert a variable length field (byte array) to a string.
        /// </summary>
        /// <remarks>If the byte array has unprintable characters in it, a 
        /// hex dump will be put in the string instead</remarks>
        /// <param name="bytes">The byte array to convert to a string</param>
        /// <returns>A UTF8 string, minus the null terminator</returns>
        public static string FieldToString(byte[] bytes)
        {
            return FieldToString(bytes, "");
        }

        /// <summary>
        /// Convert a variable length field (byte array) to a string, with a
        /// field name prepended to each line of the output.
        /// </summary>
        /// <remarks>If the byte array has unprintable characters in it, a 
        /// hex dump will be put in the string instead</remarks>
        /// <param name="bytes">The byte array to convert to a string</param>
        /// <param name="fieldName">A field name to prepend to each line of output</param>
        /// <returns>A UTF8 string, minus the null terminator</returns>
        public static string FieldToString(byte[] bytes, string fieldName)
        {
            bool printable = true;

            for (int i = 0; i < bytes.Length; ++i)
            {
                // Check if there are any unprintable characters in the array
                if ((bytes[i] < 0x20 || bytes[i] > 0x7E) && bytes[i] != 0x09
                    && bytes[i] != 0x0D && bytes[i] != 0x0A && bytes[i] != 0x00)
                {
                    printable = false;
                    break;
                }
            }

            if (printable)
            {
                return System.Text.Encoding.UTF8.GetString(bytes).Replace("\0", "");
            }
            else
            {
                string output = "";

                for (int i = 0; i < bytes.Length; i += 16)
                {
                    if (i != 0) { output += "\n"; }
                    if (fieldName != "") { output += fieldName + ": "; }

                    for (int j = 0; j < 16; j++)
                    {
                        if ((i + j) < bytes.Length)
                        {
                            string s = String.Format("{0:X} ", bytes[i + j]);
                            if (s.Length == 2)
                            {
                                s = "0" + s;
                            }

                            output += s;
                        }
                        else
                        {
                            output += "   ";
                        }
                    }

                    for (int j = 0; j < 16 && (i + j) < bytes.Length; j++)
                    {
                        if (bytes[i + j] >= 0x20 && bytes[i + j] < 0x7E)
                        {
                            output += (char)bytes[i + j];
                        }
                        else
                        {
                            output += ".";
                        }
                    }
                }

                return output;
            }
        }

        /// <summary>
        /// Convert a UTF8 string to a byte array
        /// </summary>
        /// <param name="str">The string to convert to a byte array</param>
        /// <returns>A null-terminated byte array</returns>
        public static byte[] StringToField(string str)
        {
            if (!str.EndsWith("\0")) { str += "\0"; }
            return System.Text.UTF8Encoding.UTF8.GetBytes(str);
        }

        public static int GetUnixTime()
        {
            TimeSpan ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            return (int)ts.TotalSeconds;
        }

        /// <summary>
        /// Decode a zerocoded byte array, used to decompress packets marked
        /// with the zerocoded flag
        /// </summary>
        /// <remarks>Any time a zero is encountered, the next byte is a count 
        /// of how many zeroes to expand. One zero is encoded with 0x00 0x01, 
        /// two zeroes is 0x00 0x02, three zeroes is 0x00 0x03, etc. The 
        /// first four bytes are copied directly to the output buffer.
        /// </remarks>
        /// <param name="src">The byte array to decode</param>
        /// <param name="srclen">The length of the byte array to decode</param>
        /// <param name="dest">The output byte array to decode to</param>
        /// <returns>The length of the output buffer</returns>
        public static int ZeroDecode(byte[] src, int srclen, byte[] dest)
        {
            uint zerolen = 0;

            Array.Copy(src, 0, dest, 0, 4);
            zerolen += 4;

            //int bodylen;
            //if ((src[0] & MSG_APPENDED_ACKS) == 0)
            //{
            //    bodylen = srclen;
            //}
            //else
            //{
            //    bodylen = srclen - src[srclen - 1] * 4 - 1;
            //}
            int bodylen = srclen;

            uint i;
            for (i = zerolen; i < bodylen; i++)
            {
                if (src[i] == 0x00)
                {
                    for (byte j = 0; j < src[i + 1]; j++)
                    {
                        dest[zerolen++] = 0x00;
                    }

                    i++;
                }
                else
                {
                    dest[zerolen++] = src[i];
                }
            }

            // HACK: Fix truncated zerocoded messages
            for (uint j = zerolen; j < zerolen + 16; j++)
            {
                dest[j] = 0;
            }
            zerolen += 16;

            // copy appended ACKs
            for (; i < srclen; i++)
            {
                dest[zerolen++] = src[i];
            }

            return (int)zerolen;
        }

        /// <summary>
        /// Decode enough of a byte array to get the packet ID.  Data before and
        /// after the packet ID is undefined.
        /// </summary>
        /// <param name="src">The byte array to decode</param>
        /// <param name="dest">The output byte array to encode to</param>
        public static void ZeroDecodeCommand(byte[] src, byte[] dest)
        {
            for (int srcPos = 4, destPos = 4; destPos < 8; ++srcPos)
            {
                if (src[srcPos] == 0x00)
                {
                    for (byte j = 0; j < src[srcPos + 1]; ++j)
                    {
                        dest[destPos++] = 0x00;
                    }

                    ++srcPos;
                }
                else
                {
                    dest[destPos++] = src[srcPos];
                }
            }
        }

        /// <summary>
        /// Encode a byte array with zerocoding. Used to compress packets marked
        /// with the zerocoded flag. Any zeroes in the array are compressed down
        /// to a single zero byte followed by a count of how many zeroes to expand
        /// out. A single zero becomes 0x00 0x01, two zeroes becomes 0x00 0x02,
        /// three zeroes becomes 0x00 0x03, etc. The first four bytes are copied
        /// directly to the output buffer.
        /// </summary>
        /// <param name="src">The byte array to encode</param>
        /// <param name="srclen">The length of the byte array to encode</param>
        /// <param name="dest">The output byte array to encode to</param>
        /// <returns>The length of the output buffer</returns>
        public static int ZeroEncode(byte[] src, int srclen, byte[] dest)
        {
            uint zerolen = 0;
            byte zerocount = 0;

            Array.Copy(src, 0, dest, 0, 4);
            zerolen += 4;

            int bodylen;
            if ((src[0] & MSG_APPENDED_ACKS) == 0)
            {
                bodylen = srclen;
            }
            else
            {
                bodylen = srclen - src[srclen - 1] * 4 - 1;
            }

            uint i;
            for (i = zerolen; i < bodylen; i++)
            {
                if (src[i] == 0x00)
                {
                    zerocount++;

                    if (zerocount == 0)
                    {
                        dest[zerolen++] = 0x00;
                        dest[zerolen++] = 0xff;
                        zerocount++;
                    }
                }
                else
                {
                    if (zerocount != 0)
                    {
                        dest[zerolen++] = 0x00;
                        dest[zerolen++] = (byte)zerocount;
                        zerocount = 0;
                    }

                    dest[zerolen++] = src[i];
                }
            }

            if (zerocount != 0)
            {
                dest[zerolen++] = 0x00;
                dest[zerolen++] = (byte)zerocount;
            }

            // copy appended ACKs
            for (; i < srclen; i++)
            {
                dest[zerolen++] = src[i];
            }

            return (int)zerolen;
        }

        /// <summary>
        /// Calculates the CRC (cyclic redundancy check) needed to upload inventory.
        /// </summary>
        /// <param name="creationDate">Creation date</param>
        /// <param name="saleType">Sale type</param>
        /// <param name="invType">Inventory type</param>
        /// <param name="type">Type</param>
        /// <param name="assetID">Asset ID</param>
        /// <param name="groupID">Group ID</param>
        /// <param name="salePrice">Sale price</param>
        /// <param name="ownerID">Owner ID</param>
        /// <param name="creatorID">Creator ID</param>
        /// <param name="itemID">Item ID</param>
        /// <param name="folderID">Folder ID</param>
        /// <param name="everyoneMask">Everyone mask (permissions)</param>
        /// <param name="flags">Flags</param>
        /// <param name="nextOwnerMask">Next owner mask (permissions)</param>
        /// <param name="groupMask">Group mask (permissions)</param>
        /// <param name="ownerMask">Owner mask (permisions)</param>
        /// <returns>The calculated CRC</returns>
        public static uint InventoryCRC(int creationDate, byte saleType, sbyte invType, sbyte type,
            LLUUID assetID, LLUUID groupID, int salePrice, LLUUID ownerID, LLUUID creatorID,
            LLUUID itemID, LLUUID folderID, uint everyoneMask, uint flags, uint nextOwnerMask,
            uint groupMask, uint ownerMask)
        {
            uint CRC = 0;

            // IDs
            CRC += assetID.CRC(); // AssetID
            CRC += folderID.CRC(); // FolderID
            CRC += itemID.CRC(); // ItemID

            // Permission stuff
            CRC += creatorID.CRC(); // CreatorID
            CRC += ownerID.CRC(); // OwnerID
            CRC += groupID.CRC(); // GroupID

            // CRC += another 4 words which always seem to be zero -- unclear if this is a LLUUID or what
            CRC += ownerMask;
            CRC += nextOwnerMask;
            CRC += everyoneMask;
            CRC += groupMask;

            // The rest of the CRC fields
            CRC += flags; // Flags
            CRC += (uint)invType; // InvType
            CRC += (uint)type; // Type 
            CRC += (uint)creationDate; // CreationDate
            CRC += (uint)salePrice;    // SalePrice
            CRC += (uint)((uint)saleType * 0x07073096); // SaleType

            return CRC;
        }
    }
}