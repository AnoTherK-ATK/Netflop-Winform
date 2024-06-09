﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System.Threading;
namespace HLS_GUI
{
    public partial class player : Form
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private string _url;
        private string _uuid;

        // attribute for download
        public int index_num;
        public int ts_num;
        public string source_video_path;
        public string folder_video_path;
        public string m3u8_ts_playlist;
        public int resolution_index;
        private string m3u8_master_url;

        public object Int { get; private set; }

        class StreamInfo
        {
            public List<string> Uri_ts { get; set; }

            public string Uri { get; set; }
            public int Bandwidth { get; set; }
            public string Codecs { get; set; }
            public int Resolution { get; set; }
            public string FrameRate { get; set; }
        }

        public player(string url, string uuid)
        {

            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
            Core.Initialize();

            _url = url;
            _uuid = uuid;
            string vlcLibDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc", "win-x64"); // Adjust path as needed
            _libVLC = new LibVLC("--no-xlib", $"--plugin-path={vlcLibDirectory}");

            _mediaPlayer = new MediaPlayer(_libVLC);

            var vlcControl = new VideoView
            {
                MediaPlayer = _mediaPlayer,
                Dock = DockStyle.Fill
            };

            panel1.Controls.Add(vlcControl);
            
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            Thread t = new Thread(new ThreadStart(playing));
            t.IsBackground = false;
            t.Start();
            t.Join();
           //  if (!string.IsNullOrEmpty(source_video_path))
           // {
           //    var media = new Media(_libVLC, new Uri(source_video_path));
             //  _mediaPlayer.Play(media);
           // }
            //else
            //{
              //  MessageBox.Show("URL is empty. Please provide a valid URL.");
            //}
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                _mediaPlayer?.Dispose();
                _libVLC?.Dispose();
            }
            base.Dispose(disposing);
            Delete_folder(folder_video_path);
        }

        
        private void pause_btn_Click_1(object sender, EventArgs e)
        {
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
            }
        }

        private void play_btn_Click_1(object sender, EventArgs e)
        {
            if (!_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Play();
            }
        }

        private void replay_btn_Click_1(object sender, EventArgs e)
        {
            _mediaPlayer.Stop();
            var media = new Media(_libVLC, new Uri(_url));
            _mediaPlayer.Play(media);
        }



        private bool check_url(string url)
        {
            bool result = Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult);
            return result;
        }
        private async Task<string> get_m3u8_content(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    return await client.GetStringAsync(url);
                }
                catch
                {

                    return null;
                }
            }
        }
        private async Task<List<StreamInfo>> parse_m3u8_content(string content)
        {
            index_num = 0;
            List<StreamInfo> stream_info = new List<StreamInfo>();
            using (StringReader reader = new StringReader(content))
            {
                string line;
                StreamInfo current_stream = null;

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#EXT-X-STREAM-INF"))
                    {
                        current_stream = new StreamInfo();
                        var attributes = line.Substring(18).Split(',');

                        foreach (var attribute in attributes)
                        {
                            var key_value = attribute.Split('=');
                            if (key_value.Length == 2)
                            {
                                string key = key_value[0].Trim();
                                string value = key_value[1].Trim().Trim('"');

                                switch (key)
                                {
                                    case "BANDWIDTH":
                                        current_stream.Bandwidth = int.Parse(value);
                                        break;
                                    case "CODECS":
                                        current_stream.Codecs = value;
                                        break;
                                    case "RESOLUTION":
                                        current_stream.Resolution = int.Parse(value.Split('x').First());
                                        break;
                                    case "FRAME-RATE":
                                        current_stream.FrameRate = value;
                                        break;
                                }
                            }
                        }
                    }
                    else if (!line.StartsWith("#") && current_stream != null)
                    {
                        current_stream.Uri = line.Trim();
                        string name_m3u8 = current_stream.Uri.ToString();
                        string master_m3u8_url = m3u8_master_url;
                        string name_masterm3u8 = master_m3u8_url.Split('/').Last();
                        string variant_m3u8_url = master_m3u8_url.Replace(name_masterm3u8, name_m3u8);
                        current_stream.Uri = variant_m3u8_url;
                        current_stream.Uri_ts = await get_ts_url(current_stream.Uri);
                        stream_info.Add(current_stream);
                        current_stream = null;
                        index_num++;
                    }
                }
            }
            m3u8_ts_playlist = await get_m3u8_content(stream_info[0].Uri);
            return stream_info;
        }
        private async Task<List<string>> get_ts_url(string url)

        {
            ts_num = 0;
            List<string> url_ts = new List<string>();
            using (HttpClient client = new HttpClient())
            {
                string m3u8Content = await client.GetStringAsync(url);


                using (StringReader reader = new StringReader(m3u8Content))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!line.StartsWith("#") && line.EndsWith(".ts"))
                        {
                            url_ts.Add(new Uri(new Uri(url), line).ToString());
                            ts_num++;
                        }
                    }
                }
            }
            return url_ts;
        }

        private double check_speed()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            WebClient web = new WebClient();
            byte[] bytes = web.DownloadData("https://www.google.com");
            watch.Stop();
            double sec = watch.Elapsed.TotalSeconds;
            double speed = bytes.Count() / sec;
            return speed;
        }
        private StreamInfo get_best_stream(List<StreamInfo> list_stream)
        {
            StreamInfo best_stream = null;
            double networkSpeedInBits = check_speed() * 8;
            foreach (StreamInfo stream in list_stream)
            {
                if (networkSpeedInBits >= stream.Bandwidth)
                {

                    if (best_stream == null)
                    {
                        best_stream = stream;
                    }
                    else
                    {
                        if (best_stream.Bandwidth <= stream.Bandwidth && best_stream.Resolution <= stream.Resolution)
                        {
                            best_stream = stream;
                        }
                    }
                }
            }
            return best_stream;
        }
        private string Create_folder(string namefolder)
        {
            string path_folder = Directory.GetCurrentDirectory();
            path_folder = Path.Combine(path_folder, namefolder);
            if (!Directory.Exists(path_folder))
            {
                Directory.CreateDirectory(path_folder);
            }
            else
            {
                try
                {
                    string[] path_file = Directory.GetFiles(path_folder);
                    foreach (string path in path_file)
                    {
                        System.IO.File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Alert", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
            return path_folder;
        }
        private void Delete_folder(string path_folder)
        {
            try
            {
                if (Directory.Exists(path_folder))
                {
                    Directory.Delete(path_folder, true);
                }
                else
                {
                    Console.WriteLine("Folder does not exist.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
            }
        }
        static async Task DownloadTsFile(string tsUrl, string outputDirectory)
        {
            using (HttpClient client = new HttpClient())
            {
                string fileName = Path.GetFileName(new Uri(tsUrl).LocalPath);
                string outputPath = Path.Combine(outputDirectory, fileName);

                byte[] tsContent = await client.GetByteArrayAsync(tsUrl);
                System.IO.File.WriteAllBytes(outputPath, tsContent);


            }
        }
        private string Create_m3u8_file(List<string> ts_name, string path_folder)
        {
            string m3u8_content = m3u8_ts_playlist;
            var lines = m3u8_content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int tsIndex = 0;
            List<string> outputLines = new List<string>();

            foreach (var line in lines)
            {
                if (line.EndsWith(".ts") && tsIndex < ts_name.Count)
                {

                    outputLines.Add(ts_name[tsIndex]);
                    tsIndex++;
                }
                else
                {

                    outputLines.Add(line);
                }
            }


            string outputContent = string.Join(Environment.NewLine, outputLines);
            string outputpath = Path.Combine(path_folder, "playlist.m3u8");
            System.IO.File.WriteAllText(outputpath, outputContent);
            return outputpath;
        }
        private async void download_ts(string path_source)
        {
            if (check_url(m3u8_master_url))
            {
                string m3u8_master_content = await get_m3u8_content(m3u8_master_url);
                if (m3u8_master_content != null)
                {
                    List<StreamInfo> streams = await parse_m3u8_content(m3u8_master_content);

                    int i = 0;
                    List<string> ts_name = new List<string>();
                    MessageBox.Show("Chose resolution before view", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    while (set_quality.SelectedItem==null )
                    {
                        if(set_quality.SelectedItem!= null)
                        {
                            break;
                        }
                    }
                    while (i < ts_num)
                    {
                        if (resolution_index == 0) {

                            StreamInfo best_stream = get_best_stream(streams);
                            if (best_stream != null)
                            {
                                string ts_link = best_stream.Uri_ts[i];
                                ts_name.Add(ts_link.Split('/').Last());
                                if (ts_link != "")
                                {
                                    await DownloadTsFile(ts_link, path_source);
                                }

                            }
                        }
                        if(resolution_index !=0)
                        {
                            int index = resolution_index-1;
                            string ts_link = streams[index].Uri_ts[i];
                            ts_name.Add(ts_link.Split('/').Last());
                            if (ts_link != "")
                            {
                                await DownloadTsFile(ts_link, path_source);
                            }
                        }
                        
                        i++;
                    }


                    if (i == ts_num)
                    {
                        source_video_path = Create_m3u8_file(ts_name, path_source);

                        MessageBox.Show("Ready to view", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        var media = new Media(_libVLC, new Uri(source_video_path));
                        _mediaPlayer.Play(media);
                    }
                }
                else
                {
                    MessageBox.Show("Cannot view", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
            else
            {
                MessageBox.Show("Invalid URL", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }



        }
       
        private void playing()
        {   
            
            m3u8_master_url = _url;
            folder_video_path = Create_folder(_uuid);
            download_ts(folder_video_path);
        }

        private void set_quality_SelectedIndexChanged(object sender, EventArgs e)
        {
            resolution_index =Int32.Parse(set_quality.SelectedIndex.ToString());
        }
    }
}
