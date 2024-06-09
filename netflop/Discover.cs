using HLS_GUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace netflop
{
    public partial class Discover : Form
    {
        public int index_num;
        public int ts_num;
        public string source_video_path;
        public string m3u8_ts_playlist;
        private string m3u8_master_url;
        string token;

        class StreamInfo
        {
            public List<string> Uri_ts { get; set; }

            public string Uri { get; set; }
            public int Bandwidth { get; set; }
            public string Codecs { get; set; }
            public int Resolution { get; set; }
            public string FrameRate { get; set; }
        }
        class FilmData
        {
            public string uuid { get; set; }
            public string filmName { get; set; }
            public string path { get; set; }
            public string poster { get; set; }
            public string releaseDate { get; set; }
            public string description { get; set; }
        }
        private void load()
        {
            dataGridView1.Rows.Clear();
           
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("https://sv.netflop.site/user/getAllFilmsInfo");
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            var response = client.GetAsync("https://sv.netflop.site/user/getAllFilmsInfo").Result;
            var responseString = response.Content.ReadAsStringAsync().Result;
            var responseData = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FilmData>>(responseString);
            foreach (var film in responseData)
            {
                dataGridView1.Rows.Add(film.filmName, film.description, "View", film.uuid);
            }
        }
        public Discover(string token, bool isAdmin = false)
        {
            InitializeComponent();
            dataGridView1.Columns.Add("Name", "Name");
            dataGridView1.Columns.Add("Description", "Description");
            dataGridView1.Columns.Add("View", "View");
            dataGridView1.Columns.Add("uuid", "uuid");

            dataGridView1.Columns[3].Visible = false;
            //change column width
            dataGridView1.Columns[0].Width = 200;
            dataGridView1.Columns[1].Width = 400;
            dataGridView1.Columns[2].Width = 80;
            this.token = token;
            if(isAdmin )
            {
                uploadBtn.Visible = true;
            }
            //grid view columns
            load();
        }
        //row click event
        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 2)
            {
                DataGridViewRow row = this.dataGridView1.Rows[e.RowIndex];
                string uuid = row.Cells["uuid"].Value.ToString();
                string link_movie = "https://sv.netflop.site/public/media/" + uuid + "/master.m3u8";
               // playing(link_movie);
                player player_hls = new player(link_movie,uuid);
                player_hls.Show();

            }
        }

        private void dataGridView1_CellContentClick_1(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 2)
            {
                DataGridViewRow row = this.dataGridView1.Rows[e.RowIndex];
                string uuid = row.Cells["uuid"].Value.ToString();
                string link_movie = "https://sv.netflop.site/public/media/" + uuid + "/master.m3u8";
                //playing(link_movie);
                player player_hls = new player(link_movie,uuid);
                player_hls.Show();
                

            }
        }

        private void uploadBtn_Click(object sender, EventArgs e)
        {
            new Upload(token).Show();
        }

        private void refreshBtn_Click(object sender, EventArgs e)
        {
            load();
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

        private double check_speed(string url)
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
        private StreamInfo get_best_stream(List<StreamInfo> list_stream, double network_speed)
        {
            StreamInfo best_stream = null;
            double networkSpeedInBits = network_speed * 8;
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
                    while (i < ts_num)
                    {
                        double speed = check_speed(streams[0].Uri_ts[i]);
                        StreamInfo best_stream = get_best_stream(streams, speed);
                        if (best_stream != null)
                        {
                            string ts_link = best_stream.Uri_ts[i];
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
                     //   player player_ts = new player(source_video_path);

                      //  player_ts.Show();
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
        private static void CloseAllPlayerForms()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                if (form is player)
                {
                    form.Close();
                }
            }
        }
        private void playing(string link)
        {   
            CloseAllPlayerForms();
            MessageBox.Show("Please Wait", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
            m3u8_master_url = link;
            string path_source = Create_folder("source_video_HLS");
            download_ts(path_source);
        }
    }
}
