using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using libsecondlife.AssetSystem;

namespace libsecondlife.TestClient
{
    public class SetAppearanceCommand : Command
    {
        SecondLife Client;
        AppearanceManager aManager;

		public SetAppearanceCommand(TestClient testClient)
        {
            TestClient = testClient;
            Client = (SecondLife)TestClient;

            Name = "setapp";
            Description = "Set appearance to what's stored in the DB.";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (aManager == null)
                aManager = new AppearanceManager(Client);

            aManager.SendAgentSetAppearance();
            return "Done.";
        }
    }
}