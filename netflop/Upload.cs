﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace netflop
{
    public partial class Upload : Form
    {
        class uploadFilmInfo { 
            public string filmName { get; set; }
            //public string path { get; set; }
            public string poster { get; set; }
            //public string releaseDate { get; set; }
            public string description { get; set; }
        }
        string uuid = "";
        string token;
        public Upload(string tk)
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
            token = tk;
        }

        private void uploadBtn_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(new ThreadStart(uploading));
            t.IsBackground = true;
            t.Start();
        }
        private void uploading() {

            MessageBox.Show("Please Wait", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            try
            {
                uploadFilmInfo data = new uploadFilmInfo
                {
                    filmName = nameTxt.Text,
                    //path = videoPathTxt.Text,
                    poster = "poster" + Path.GetExtension(posterPathTxt.Text),
                    description = ovwTxt.Text
                };
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri("https://sv.netflop.site/admin/uploadFilmInfo");
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = client.PostAsync("https://sv.netflop.site/admin/uploadFilmInfo", content).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;
                uuid = responseString.Substring(1, responseString.Length - 2);

                //upload poster file
                var posterContent = new MultipartFormDataContent();
                posterContent.Add(new ByteArrayContent(System.IO.File.ReadAllBytes(posterPathTxt.Text)), "file", "poster" + Path.GetExtension(posterPathTxt.Text));
                //client.BaseAddress = new Uri("https://sv.netflop.site/admin/uploadPoster/" + uuid);
                var posterResponse = client.PostAsync("https://sv.netflop.site/admin/uploadPoster/" + uuid, posterContent).Result;

                //upload video file
                var videoContent = new MultipartFormDataContent();
                videoContent.Add(new ByteArrayContent(System.IO.File.ReadAllBytes(videoPathTxt.Text)), "file", uuid + Path.GetExtension(videoPathTxt.Text));
                //client.BaseAddress = new Uri("https://sv.netflop.site/admin/upload");
                var videoResponse = client.PostAsync("https://sv.netflop.site/admin/upload", videoContent).Result;

                //client.BaseAddress = new Uri("https://sv.netflop.site/hls/generate/" + uuid);
                var hlsResponse = client.PostAsync("https://sv.netflop.site/hls/generate/" + uuid, null).Result;

                MessageBox.Show("Upload successful");

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void videoBrowseBtn_Click(object sender, EventArgs e)
        {
            if(openVideoDialog.ShowDialog() == DialogResult.OK)
            {
                videoPathTxt.Text = openVideoDialog.FileName;
            }
        }

        private void posterBrowseBtn_Click(object sender, EventArgs e)
        {
            if(openPosterDialog.ShowDialog() == DialogResult.OK)
            {
                posterPathTxt.Text = openPosterDialog.FileName;
            }
        }

       
    }
}
