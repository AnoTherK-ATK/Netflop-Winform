using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace netflop
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        class LoginPostData
        {
            public string email { get; set; }
            public string password { get; set; }
        }

        class LoginResponseData
        {
            public string token { get; set; }
            public string role { get; set; }
        }

        class publicKey
        {
            public BigInteger e { get; set; }
            public BigInteger n { get; set; }
        }

        private string encrypt(string plain, BigInteger e, BigInteger n)
        {
            string encrypted = "";
            byte[] plainByte = Encoding.UTF8.GetBytes(plain);
            BigInteger plainInt = new BigInteger(plainByte);
            if (plainInt < 0)
            {
                plainInt = new BigInteger(new byte[] { 0 }.Concat(plainByte).ToArray());
            }
            BigInteger encryptedInt = BigInteger.ModPow(plainInt, e, n);
            encrypted = Convert.ToBase64String(encryptedInt.ToByteArray());
            return encrypted;
        }


        private void loginBtn_Click(object sender, EventArgs e)
        {   
            try
            {
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri("https://sv.netflop.site/auth/generate");
                var response = client.GetAsync("https://sv.netflop.site/auth/generate").Result;
                var sessionID = response.Content.ReadAsStringAsync().Result;
                response = client.GetAsync("https://sv.netflop.site/auth/publicKey/" + sessionID).Result;
                var responseStr = response.Content.ReadAsStringAsync().Result;
                var resonseData = Newtonsoft.Json.JsonConvert.DeserializeObject<publicKey>(responseStr);
                var loginData = new LoginPostData
                {
                    email = encrypt(loginEmailBox.Text, resonseData.e, resonseData.n),
                    password = encrypt(loginPasswordBox.Text, resonseData.e, resonseData.n)

                };
                loginEmailBox.Clear();
                loginPasswordBox.Clear();
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(loginData);
                var data = new StringContent(json, Encoding.UTF8, "application/json");
                response = client.PostAsync("https://sv.netflop.site/auth/signin", data).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;
                var responseData = Newtonsoft.Json.JsonConvert.DeserializeObject<LoginResponseData>(responseString);
                if (responseData.token == null)
                {
                    MessageBox.Show("Login failed", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                MessageBox.Show("Login successfully", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Hide();
                var DiscoverForm = new Discover(responseData.token, (responseData.role == "admin"));
                DiscoverForm.Closed += (s, args) => this.Close();
                DiscoverForm.Show();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        class RegisterPostData
        {
            public string name { get; set; }
            public string email { get; set; }
            public string password { get; set; }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            var registerData = new RegisterPostData
            {
                name = registerNameBox.Text,
                email = registerEmailBox.Text,
                password = registerPasswordBox.Text
            };
            try
            {
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri("https://sv.netflop.site/auth/signup");
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(registerData);
                var data = new StringContent(json, Encoding.UTF8, "application/json");
                var response = client.PostAsync("https://sv.netflop.site/auth/signup", data).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;
                MessageBox.Show("Register successfully", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
