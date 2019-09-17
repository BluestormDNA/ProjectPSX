using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectPSX
{
    public partial class Launch : Form
    {
        public Launch()
        {
            InitializeComponent();
            UpdateForm();
        }


        private void BtnSelectBios_Click(object sender, EventArgs e)
        {
            var file = OpenFileDialog();
            if (file != null)
            {
                SetUserSetting("BiosLocation", file);
            }
            UpdateForm();
        }

        private void BtnSelectCD_Click(object sender, EventArgs e)
        {
            var file = OpenFileDialog();
            if (file != null)
            {
                SetUserSetting("CDLocation", file);
            }
            UpdateForm();
        }

        private void UpdateForm()
        {
            var bios = GetUserSetting<string>("BiosLocation");
            var cd = GetUserSetting<string>("CDLocation");
            lblBiosLocation.Text = bios;
            lblCDLocation.Text = cd;

            var allowStart = true;
            if (File.Exists(bios))
            {
                lblBiosLocation.ForeColor = Color.DarkGreen;
            }
            else
            {
                lblBiosLocation.ForeColor = Color.Red;
                allowStart = false;
            }

            if (File.Exists(cd))
            {
                lblCDLocation.ForeColor = Color.DarkGreen;
            }
            else
            {
                lblCDLocation.ForeColor = Color.Red;
                allowStart = false;
            }

            btnStart.Enabled = allowStart;

        }

        private string OpenFileDialog()
        {
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    return fileDialog.FileName;
                }
                catch (SecurityException ex)
                {
                    MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}");
                }
            }

            return null;
        }

        private void SetUserSetting<T>(string key, T value)
        {

            if (Properties.Settings.Default[key] == null)
            {
                var setting = new System.Configuration.SettingsProperty(key)
                {
                    DefaultValue = value,
                    IsReadOnly = false,
                    PropertyType = typeof(T),
                    Provider = Properties.Settings.Default.Providers["LocalFileSettingsProvider"]
                };
                Properties.Settings.Default.Properties.Add(setting);
            }


            Properties.Settings.Default[key] = value;
            Properties.Settings.Default.Save(); // Saves settings in application configuration file
        }

        private T GetUserSetting<T>(string key)
        {
            return (T)Properties.Settings.Default[key];
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            var bios = GetUserSetting<string>("BiosLocation");
            var game = GetUserSetting<string>("CDLocation");
            Window form = new Window(bios, game);
            form.Show();
        }
    }
}
