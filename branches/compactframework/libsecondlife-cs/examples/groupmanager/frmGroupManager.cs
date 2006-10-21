using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using libsecondlife;
using libsecondlife.Packets;

namespace groupmanager
{
    public partial class frmGroupManager : Form
    {
        SecondLife client;

        public frmGroupManager()
        {
            client = new SecondLife();
            client.Groups.OnGroupsUpdated += new GroupManager.GroupsUpdatedCallback(GroupsUpdatedHandler);

            InitializeComponent();
        }

        void GroupsUpdatedHandler()
        {
            Invoke(new MethodInvoker(UpdateGroups));
        }

        void UpdateGroups()
        {
            lstGroups.Items.Clear();

            foreach (Group group in client.Groups.Groups)
            {
                lstGroups.Items.Add(group);
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            frmGroupManager frm = new frmGroupManager();
            frm.ShowDialog();
        }

        private void cmdConnect_Click(object sender, EventArgs e)
        {
            if (cmdConnect.Text == "Connect")
            {
                cmdConnect.Text = "Disconnect";
                txtFirstName.Enabled = txtLastName.Enabled = txtPassword.Enabled = false;

                Hashtable loginParams = NetworkManager.DefaultLoginValues(txtFirstName.Text,
                    txtLastName.Text, txtPassword.Text, "00:00:00:00:00:00", "last", 
                    "Win", "0", "groupmanager", "jhurliman@wsu.edu");

                if (client.Network.Login(loginParams))
                {
                    groupBox.Enabled = true;
                }
                else
                {
                    MessageBox.Show(this, "Error logging in: " + client.Network.LoginError);
                    cmdConnect.Text = "Connect";
                    txtFirstName.Enabled = txtLastName.Enabled = txtPassword.Enabled = true;
                    groupBox.Enabled = false;
                    lstGroups.Items.Clear();
                }
            }
			else
			{
				client.Network.Logout();
				cmdConnect.Text = "Connect";
				txtFirstName.Enabled = txtLastName.Enabled = txtPassword.Enabled = true;
                groupBox.Enabled = false;
                lstGroups.Items.Clear();
			}
        }

        private void lstGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstGroups.SelectedIndex >= 0)
            {
                cmdActivate.Enabled = cmdInfo.Enabled = cmdLeave.Enabled = true;
            }
            else
            {
                cmdActivate.Enabled = cmdInfo.Enabled = cmdLeave.Enabled = false;
            }
        }

        private void cmdInfo_Click(object sender, EventArgs e)
        {
            if (lstGroups.Items[lstGroups.SelectedIndex].ToString() != "none")
            {
                Group group = (Group)lstGroups.Items[lstGroups.SelectedIndex];

                frmGroupInfo frm = new frmGroupInfo(group);
                frm.ShowDialog();
            }
        }

        private void frmGroupManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (client.Network.Connected)
            {
                client.Network.Logout();
            }
        }
    }
}