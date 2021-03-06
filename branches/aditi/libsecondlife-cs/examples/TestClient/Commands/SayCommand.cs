using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class SayCommand: Command
    {
        SecondLife Client;

        public SayCommand(TestClient testClient)
		{
            TestClient = testClient;
            Client = (SecondLife)TestClient;

			Name = "say";
			Description = "Say something.  (usage: say (optional channel) whatever)";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
            int channel = 0;
            int startIndex = 0;
            string message = String.Empty;
            if (args.Length < 1)
            {
                return "usage: say (optional channel) whatever";
            }
            else if (args.Length > 1)
            {
                if (Int32.TryParse(args[0], out channel))
					startIndex = 1;
            }
            
			for (int i = startIndex; i < args.Length; i++) {
				message += args[i] + " ";
            }

			Client.Self.Chat(message, channel, MainAvatar.ChatType.Normal);

            return "Said " + message;
		}
    }
}
