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
using System.Timers;
using System.Net;
using System.Collections;
using libsecondlife.Packets;

namespace libsecondlife
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="Message"></param>
    /// <param name="Audible"></param>
    /// <param name="Type"></param>
    /// <param name="Sourcetype"></param>
    /// <param name="FromName"></param>
    /// <param name="ID"></param>
    public delegate void ChatCallback(string Message, byte Audible, byte Type, byte Sourcetype,
        string FromName, LLUUID ID);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="FromAgentID"></param>
    /// <param name="FromAgentName"></param>
    /// <param name="ToAgentID"></param>
    /// <param name="ParentEstateID"></param>
    /// <param name="RegionID"></param>
    /// <param name="Position"></param>
    /// <param name="Dialog"></param>
    /// <param name="GroupIM"></param>
    /// <param name="IMSessionID"></param>
    /// <param name="Timestamp"></param>
    /// <param name="Message"></param>
    public delegate void InstantMessageCallback(LLUUID FromAgentID, string FromAgentName, 
        LLUUID ToAgentID, uint ParentEstateID, LLUUID RegionID, LLVector3 Position, 
        bool Dialog, bool GroupIM, LLUUID IMSessionID, DateTime Timestamp, string Message, byte Offline, byte[] BinaryBucket);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="AgentID"></param>
    /// <param name="Online"></param>
    public delegate void FriendNotificationCallback(LLUUID AgentID, bool Online);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    public delegate void TeleportCallback(string message);

    /// <summary>
    /// 
    /// </summary>
    public class Avatar
    {
        /// <summary></summary>
        public LLUUID ID;
        /// <summary></summary>
        public uint LocalID;
        /// <summary></summary>
        public string Name;
        /// <summary></summary>
        public string GroupName;
        /// <summary></summary>
        public bool Online;
        /// <summary></summary>
        public LLVector3 Position;
        /// <summary></summary>
        public LLQuaternion Rotation;
        /// <summary></summary>
        public Region CurrentRegion;
    }

    /// <summary>
    /// 
    /// </summary>
    public class MainAvatar
    {
        /// <summary></summary>
        public event ChatCallback OnChat;
        /// <summary></summary>
        public event InstantMessageCallback OnInstantMessage;
        /// <summary></summary>
        public event FriendNotificationCallback OnFriendNotification;
        /// <summary></summary>
        public event TeleportCallback OnTeleport;

        /// <summary></summary>
        public LLUUID ID;
        /// <summary></summary>
        public uint LocalID;
        /// <summary></summary>
        public string FirstName;
        /// <summary></summary>
        public string LastName;
        /// <summary></summary>
        public string TeleportMessage;
        /// <summary></summary>
        public LLVector3 Position;
        /// <summary></summary>
        public LLQuaternion Rotation;
        /// <summary></summary>
        public LLVector3d LookAt; // Should we even keep LookAt around? It's 
                                  // just for setting the initial rotation 
                                  // after login AFAIK
        /// <summary></summary>
        public LLVector3d HomePosition;
        /// <summary></summary>
        public LLVector3d HomeLookAt;

        private SecondLife Client;
        private int TeleportStatus;
        private Timer TeleportTimer;
        private bool TeleportTimeout;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public MainAvatar(SecondLife client)
        {
            Client = client;
            TeleportMessage = "";

            // Create emtpy vectors for now
            HomeLookAt = HomePosition = LookAt = new LLVector3d();
            Position = new LLVector3();
            Rotation = new LLQuaternion();

            // Coarse location callback
            PacketCallback callback = new PacketCallback(CoarseLocationHandler);
            Client.Network.RegisterCallback(PacketType.CoarseLocationUpdate, callback);

            // Teleport callbacks
            callback = new PacketCallback(TeleportHandler);
            Client.Network.RegisterCallback(PacketType.TeleportStart, callback);
            Client.Network.RegisterCallback(PacketType.TeleportProgress, callback);
            Client.Network.RegisterCallback(PacketType.TeleportFailed, callback);
            Client.Network.RegisterCallback(PacketType.TeleportFinish, callback);

            // Instant Message callback
            callback = new PacketCallback(InstantMessageHandler);
            Client.Network.RegisterCallback(PacketType.ImprovedInstantMessage, callback);

            // Chat callback
            callback = new PacketCallback(ChatHandler);
            Client.Network.RegisterCallback(PacketType.ChatFromSimulator, callback);

            // Friend notification callback
            callback = new PacketCallback(FriendNotificationHandler);
            Client.Network.RegisterCallback(PacketType.OnlineNotification, callback);
            Client.Network.RegisterCallback(PacketType.OfflineNotification, callback);

            TeleportTimer = new Timer(8000);
            TeleportTimer.Elapsed += new ElapsedEventHandler(TeleportTimerEvent);
            TeleportTimeout = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="target"></param>
        /// <param name="message"></param>
        public void InstantMessage(LLUUID target, string message)
        {
            InstantMessage(FirstName + " " + LastName, LLUUID.GenerateUUID(), target, message, null, LLUUID.GenerateUUID());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="target"></param>
        /// <param name="message"></param>
        /// <param name="ID"></param>
        public void InstantMessage(LLUUID target, string message, LLUUID IMSessionID)
        {
            InstantMessage(FirstName + " " + LastName, LLUUID.GenerateUUID(), target, message, null, IMSessionID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fromName"></param>
        /// <param name="sessionID"></param>
        /// <param name="target"></param>
        /// <param name="message"></param>
        /// <param name="conferenceIDs"></param>
        /// <param name="ID"></param>
        public void InstantMessage(string fromName, LLUUID sessionID, LLUUID target, string message, LLUUID[] conferenceIDs)
        {
            InstantMessage(fromName, sessionID, target, message, conferenceIDs, LLUUID.GenerateUUID());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fromName"></param>
        /// <param name="sessionID"></param>
        /// <param name="target"></param>
        /// <param name="message"></param>
        /// <param name="conferenceIDs"></param>
        /// <param name="ID"></param>
        public void InstantMessage(string fromName, LLUUID sessionID, LLUUID target, string message, LLUUID[] conferenceIDs, LLUUID IMSessionID)
        {
            ImprovedInstantMessagePacket im = new ImprovedInstantMessagePacket();
            im.AgentData.AgentID = this.ID;
            im.AgentData.SessionID = Client.Network.SessionID;
            im.MessageBlock.Dialog = 0;
            im.MessageBlock.FromAgentName = Helpers.StringToField(fromName);
            im.MessageBlock.FromGroup = false;
            im.MessageBlock.ID = IMSessionID;
            im.MessageBlock.Message = Helpers.StringToField(message);
            im.MessageBlock.Offline = 1;
            im.MessageBlock.ToAgentID = target;
            if (conferenceIDs != null && conferenceIDs.Length > 0)
            {
                im.MessageBlock.BinaryBucket = new byte[16 * conferenceIDs.Length];

                for (int i = 0; i < conferenceIDs.Length; ++i)
                {
                    Array.Copy(conferenceIDs[i].Data, 0, im.MessageBlock.BinaryBucket, i * 16, 16);
                }
            }
            else
            {
                im.MessageBlock.BinaryBucket = new byte[0];
            }

            // These fields are mandatory, even if we don't have valid values for them
            im.MessageBlock.Position = new LLVector3();
            im.MessageBlock.RegionID = new LLUUID(); //TODO: Allow region id to be correctly set by caller or fetched from Client.*


            // Send the message
            Client.Network.SendPacket((Packet)im);
        }

        /// <summary>
        /// 
        /// </summary>
        public enum ChatType
        {
            /// <summary></summary>
            Whisper = 0,
            /// <summary></summary>
            Normal = 1,
            /// <summary></summary>
            Shout = 2,
            /// <summary></summary>
            Say = 3,
            /// <summary></summary>
            StartTyping = 4,
            /// <summary></summary>
            StopTyping = 5
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <param name="type"></param>
        public void Chat(string message, int channel, ChatType type)
        {
            ChatFromViewerPacket chat = new ChatFromViewerPacket();
            chat.AgentData.AgentID = this.ID;
            chat.AgentData.SessionID = Client.Network.SessionID;
            chat.ChatData.Channel = channel;
            chat.ChatData.Message = Helpers.StringToField(message);
            chat.ChatData.Type = (byte)type;

            Client.Network.SendPacket((Packet)chat);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="target"></param>
        /// <param name="amount"></param>
        /// <param name="description"></param>
        public void GiveMoney(LLUUID target, int amount, string description)
        {
            // 5001 - transaction type for av to av money transfers
            GiveMoney(target, amount, description, 5001);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="target"></param>
        /// <param name="amount"></param>
        /// <param name="description"></param>
        /// <param name="transactiontype"></param>
        public void GiveMoney(LLUUID target, int amount, string description, int transactiontype)
        {
            MoneyTransferRequestPacket money = new MoneyTransferRequestPacket();
            money.AgentData.AgentID = this.ID;
            money.AgentData.SessionID = Client.Network.SessionID;
            money.MoneyData.Description = Helpers.StringToField(description);
            money.MoneyData.DestID = target;
            money.MoneyData.SourceID = this.ID;
            money.MoneyData.TransactionType = transactiontype;

            Client.Network.SendPacket((Packet)money);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool Teleport(ulong regionHandle, LLVector3 position)
        {
            return Teleport(regionHandle, position, new LLVector3(position.X + 1.0F, position.Y, position.Z));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <returns></returns>
        public bool Teleport(ulong regionHandle, LLVector3 position, LLVector3 lookAt)
        {
            TeleportStatus = 0;

            TeleportLocationRequestPacket teleport = new TeleportLocationRequestPacket();
            teleport.AgentData.AgentID = Client.Network.AgentID;
            teleport.AgentData.SessionID = Client.Network.SessionID;
            teleport.Info.LookAt = lookAt;
            teleport.Info.Position = position;
            // FIXME: Uncomment me
            //teleport.Info.RegionHandle = regionHandle;
            teleport.Header.Reliable = true;

            Client.Log("Teleporting to region " + regionHandle.ToString(), Helpers.LogLevel.Info);

            // Start the timeout check
            TeleportTimeout = false;
            TeleportTimer.Start();

            Client.Network.SendPacket((Packet)teleport);

            while (TeleportStatus == 0 && !TeleportTimeout)
            {
                Client.Tick();
            }

            TeleportTimer.Stop();

            if (TeleportTimeout)
            {
                if (OnTeleport != null) { OnTeleport("Teleport timed out."); }
            }
            else
            {
                if (OnTeleport != null) { OnTeleport(TeleportMessage); }
            }

            return (TeleportStatus == 1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="simName"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool Teleport(string simName, LLVector3 position)
        {
            return Teleport(simName, position, new LLVector3(position.X + 1.0F, position.Y, position.Z));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="simName"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <returns></returns>
        public bool Teleport(string simName, LLVector3 position, LLVector3 lookAt)
        {
            Client.Grid.AddSim(simName);
            int attempts = 0;

            while (attempts++ < 5)
            {
                if (Client.Grid.Regions.ContainsKey(simName))
                {
                    return Teleport(((GridRegion)Client.Grid.Regions[simName]).RegionHandle, position, lookAt);
                }
                else
                {
                    System.Threading.Thread.Sleep(1000);
                    Client.Grid.AddSim(simName);
                    Client.Tick();
                }
            }
            if (OnTeleport != null)
            {
                OnTeleport("Unable to resolve name: " + simName);
            }
            return false;
        }

        private void FriendNotificationHandler(Packet packet, Simulator simulator)
        {
            // If the agent is online...
            if (packet.Type == PacketType.OnlineNotification)
            {
                foreach (OnlineNotificationPacket.AgentBlockBlock block in ((OnlineNotificationPacket)packet).AgentBlock)
                {
                    Client.AddAvatar(block.AgentID);
                    #region AvatarsMutex
                    Client.AvatarsMutex.WaitOne();
                    ((Avatar)Client.Avatars[block.AgentID]).Online = true;
                    Client.AvatarsMutex.ReleaseMutex();
                    #endregion AvatarsMutex

                    if (OnFriendNotification != null)
                    {
                        OnFriendNotification(block.AgentID, true);
                    }
                }
            }

            // If the agent is Offline...
            if (packet.Type == PacketType.OfflineNotification)
            {
                foreach (OfflineNotificationPacket.AgentBlockBlock block in ((OfflineNotificationPacket)packet).AgentBlock)
                {
                    Client.AddAvatar(block.AgentID);
                    #region AvatarsMutex
                    Client.AvatarsMutex.WaitOne();
                    ((Avatar)Client.Avatars[block.AgentID]).Online = false;
                    Client.AvatarsMutex.ReleaseMutex();
                    #endregion AvatarsMutex

                    if (OnFriendNotification != null)
                    {
                        OnFriendNotification(block.AgentID, true);
                    }
                }
            }
        }

        private void CoarseLocationHandler(Packet packet, Simulator simulator)
        {
            // Check if the avatar position hasn't been updated
            if (Position.X == 0 && Position.Y == 0 && Position.Z == 0)
            {
                CoarseLocationUpdatePacket coarsePacket = (CoarseLocationUpdatePacket)packet;

                Position.X = (float)coarsePacket.Location[0].X;
                Position.Y = (float)coarsePacket.Location[0].Y;
                Position.Z = (float)coarsePacket.Location[0].Z * 4; // Z is in meters / 4

                // Send an AgentUpdate packet with the new camera location
                AgentUpdatePacket updatePacket = new AgentUpdatePacket();
                updatePacket.AgentData.AgentID = this.ID;
                updatePacket.AgentData.SessionID = Client.Network.SessionID;
                updatePacket.AgentData.BodyRotation = new LLQuaternion();
                updatePacket.AgentData.CameraAtAxis = new LLVector3();
                updatePacket.AgentData.CameraCenter = new LLVector3();
                updatePacket.AgentData.CameraLeftAxis = new LLVector3();
                updatePacket.AgentData.CameraUpAxis = new LLVector3();
                updatePacket.AgentData.ControlFlags = 0;
                updatePacket.AgentData.Far = 320.0F;
                updatePacket.AgentData.Flags = 0;
                updatePacket.AgentData.HeadRotation = new LLQuaternion();
                updatePacket.AgentData.State = 0;
                updatePacket.Header.Reliable = true;
                Client.Network.SendPacket((Packet)updatePacket);

                // Send an AgentFOV packet widening our field of vision
                AgentFOVPacket fovPacket = new AgentFOVPacket();
                fovPacket.AgentData.AgentID = this.ID;
                fovPacket.AgentData.SessionID = Client.Network.SessionID;
                fovPacket.AgentData.CircuitCode = simulator.CircuitCode;
                fovPacket.FOVBlock.GenCounter = 0;
                fovPacket.FOVBlock.VerticalAngle = 6.28318531f;
                fovPacket.Header.Reliable = true;
                Client.Network.SendPacket((Packet)fovPacket);
            }
        }

        private void InstantMessageHandler(Packet packet, Simulator simulator)
        {
            if (packet.Type == PacketType.ImprovedInstantMessage)
            {
                ImprovedInstantMessagePacket im = (ImprovedInstantMessagePacket)packet;
                
                if (OnInstantMessage != null)
                {
                    OnInstantMessage(
                        im.AgentData.AgentID
                        , Helpers.FieldToString(im.MessageBlock.FromAgentName),
                        im.MessageBlock.ToAgentID
                        , im.MessageBlock.ParentEstateID
                        , im.MessageBlock.RegionID
                        , im.MessageBlock.Position
                        , Convert.ToBoolean(im.MessageBlock.Dialog)
                        , im.MessageBlock.FromGroup
                        , im.MessageBlock.ID
                        , new DateTime(im.MessageBlock.Timestamp)
                        , Helpers.FieldToString(im.MessageBlock.Message)
                        , im.MessageBlock.Offline
                        , im.MessageBlock.BinaryBucket
                        );
                }
            }
        }

        private void ChatHandler(Packet packet, Simulator simulator)
        {
            if (packet.Type == PacketType.ChatFromSimulator)
            {
                ChatFromSimulatorPacket chat = (ChatFromSimulatorPacket)packet;

                if (OnChat != null)
                {
                    OnChat(Helpers.FieldToString(chat.ChatData.Message), chat.ChatData.Audible, 
                        chat.ChatData.ChatType, chat.ChatData.SourceType, 
                        Helpers.FieldToString(chat.ChatData.FromName), chat.ChatData.SourceID);
                }
            }
        }

        private void TeleportHandler(Packet packet, Simulator simulator)
        {
            if (packet.Type == PacketType.TeleportStart)
            {
                TeleportMessage = "Teleport started";
            }
            else if (packet.Type == PacketType.TeleportProgress)
            {
                TeleportMessage = Helpers.FieldToString(((TeleportProgressPacket)packet).Info.Message);
            }
            else if (packet.Type == PacketType.TeleportFailed)
            {
                TeleportMessage = Helpers.FieldToString(((TeleportFailedPacket)packet).Info.Reason);
                TeleportStatus = -1;
            }
            else if (packet.Type == PacketType.TeleportFinish)
            {
                TeleportFinishPacket finish = (TeleportFinishPacket)packet;
                TeleportMessage = "Teleport finished";

                if (Client.Network.Connect(new IPAddress((long)finish.Info.SimIP), finish.Info.SimPort, 
                    simulator.CircuitCode, true) != null)
                {
                    // Sync the current region and current simulator
                    Client.CurrentRegion = Client.Network.CurrentSim.Region;

                    // Move the avatar in to this sim
                    CompleteAgentMovementPacket move = new CompleteAgentMovementPacket();
                    move.AgentData.AgentID = this.ID;
                    move.AgentData.SessionID = Client.Network.SessionID;
                    move.AgentData.CircuitCode = simulator.CircuitCode;
                    Client.Network.SendPacket((Packet)move);

                    Client.Log("Moved to new sim " + Client.Network.CurrentSim.Region.Name + "(" + 
                        Client.Network.CurrentSim.IPEndPoint.ToString() + ")",
                        Helpers.LogLevel.Info);

                    // Sleep a little while so we can collect parcel information
                    System.Threading.Thread.Sleep(1000);

                    TeleportStatus = 1;
                }
                else
                {
                    TeleportStatus = -1;
                }
            }
        }

        private void TeleportTimerEvent(object source, System.Timers.ElapsedEventArgs ea)
        {
            TeleportTimeout = true;
        }
    }
}
