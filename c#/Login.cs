using Safety;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace Spoofer
{
    public partial class Form2 : Form
    {
        private const string BASE_URL = "https://safetyapi.squareweb.app/";
        private readonly SafetyAPI _api;

        public Form2()
        {
            InitializeComponent();
            _api = new SafetyAPI(BASE_URL);
        }
        private async Task InitializeSafetyAPI()
        {
            try
            {
                var init = await _api.InitAsync();

                if (!init.Success)
                {
                    SafetyAPI.Fatal("api offline -> " + (init.Message ?? "no response"));
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                SafetyAPI.Fatal("critical init error -> " + ex.Message);
                Application.Exit();
            }
        }
        internal static class RememberMeStore
        {
            private static readonly string Folder = @"C:\Safety";
            private static readonly string FilePath = Path.Combine(Folder, "user.dat");

            public static void Save(string discordId, string license)
            {
                Directory.CreateDirectory(Folder);

                // Formato simples: discord|license
                var plain = $"{discordId}|{license}";
                var bytes = System.Text.Encoding.UTF8.GetBytes(plain);

                var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

                File.WriteAllBytes(FilePath, protectedBytes);
            }

            public static bool TryLoad(out string discordId, out string license)
            {
                discordId = "";
                license = "";

                if (!File.Exists(FilePath))
                    return false;

                try
                {
                    var protectedBytes = File.ReadAllBytes(FilePath);
                    var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
                    var plain = System.Text.Encoding.UTF8.GetString(bytes);

                    var parts = plain.Split(new[] { '|' }, 2);
                    if (parts.Length != 2) return false;

                    discordId = parts[0];
                    license = parts[1];
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public static void Delete()
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
        }

        private async void Form2_Load(object sender, EventArgs e)
        {
           await InitializeSafetyAPI();
            if (RememberMeStore.TryLoad(out var discordSaved, out var keySaved))
            {
                guna2TextBox1.Text = discordSaved;
                user.Text = keySaved;
                animCheckBox1.Checked = true;
            }
        }

        private async void guna2Button1_Click(object sender, EventArgs e)
        {
            string discord = guna2TextBox1.Text.Trim();
            string key = user.Text.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("validation error -> license key required", "Tropa",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(discord))
            {
                MessageBox.Show("validation error -> discord id required", "Tropa",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {

                var result = await _api.LoginAsync(discord, key);
                if (!result.Success)
                {
                    MessageBox.Show("login failed -> " + (result.Message ?? "unknown error"), "LuxStore",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                var session = SessionInfo.Create(discord, key, result.ExpiresAt, token: "", userName: "");
                if (animCheckBox1.Checked)
                    RememberMeStore.Save(discord, key);
                else
                    RememberMeStore.Delete();
                Form1 Main = new Form1(session);
                Main.StartPosition = FormStartPosition.Manual;
                Main.Location = this.Location;
                Main.Show();
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show("internal server error -> " + ex.Message, "LuxStore",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
