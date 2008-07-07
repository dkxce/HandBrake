/*  Common.cs $
 	
 	   This file is part of the HandBrake source code.
 	   Homepage: <http://handbrake.fr>.
 	   It may be used under the terms of the GNU General Public License. */

using System;
using System.Collections;
using System.Text;
using System.Windows.Forms;
using System.Globalization;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Handbrake.Functions
{
    class Common
    {
        /// <summary>
        /// Checks for updates and returns true if an update is available.
        /// </summary>
        /// <param name="debug">Turns on debug mode. Don't use on program startup</param>
        /// <returns>Boolean True = Update available</returns>
        public Boolean updateCheck(Boolean debug)
        {
            try
            {
                Functions.RssReader rssRead = new Functions.RssReader();
                string build = rssRead.build();

                int latest = int.Parse(build);
                int current = Properties.Settings.Default.hb_build;
                int skip = Properties.Settings.Default.skipversion;

                if (latest == skip)
                    return false;
                else
                {
                    Boolean update = (latest > current);
                    return update;
                }
            }
            catch (Exception exc)
            {
                if (debug == true)
                    MessageBox.Show("Unable to check for updates, Please try again later. \n" + exc.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Get's HandBrakes version data from the CLI.
        /// </summary>
        /// <returns>Arraylist of Version Data. 0 = hb_version 1 = hb_build</returns>
        public ArrayList getCliVersionData()
        {
            ArrayList cliVersionData = new ArrayList();
            // 0 = SVN Build / Version
            // 1 = Build Date

            Process cliProcess = new Process();
            ProcessStartInfo handBrakeCLI = new ProcessStartInfo("HandBrakeCLI.exe", " -u");
            handBrakeCLI.UseShellExecute = false;
            handBrakeCLI.RedirectStandardError = true;
            handBrakeCLI.RedirectStandardOutput = true;
            handBrakeCLI.CreateNoWindow = true;
            cliProcess.StartInfo = handBrakeCLI;
            cliProcess.Start();

            // Retrieve standard output and report back to parent thread until the process is complete
            String line;
            TextReader stdOutput = cliProcess.StandardError;

            while (!cliProcess.HasExited)
            {
                line = stdOutput.ReadLine();
                Match m = Regex.Match(line, @"HandBrake svn[0-9]*[M]* \([0-9]*\)");
                if (m.Success != false)
                {
                    string data = line.Replace("(", "").Replace(")","").Replace("HandBrake ","");
                    string[] arr = data.Split(' ');
                    cliVersionData.Add(arr[0]);
                    cliVersionData.Add(arr[1]);
                    return cliVersionData;
                }
            }
            return null;
        }

        /// <summary>
        /// Update the presets.dat file with the latest version of HandBrak's presets from the CLI
        /// </summary>
        public void grabCLIPresets()
        {
            string handbrakeCLIPath = Path.Combine(Application.StartupPath, "HandBrakeCLI.exe");
            string presetsPath = Path.Combine(Application.StartupPath, "presets.dat");

            string strCmdLine = String.Format(@"cmd /c """"{0}"" --preset-list >""{1}"" 2>&1""", handbrakeCLIPath, presetsPath);

            ProcessStartInfo hbGetPresets = new ProcessStartInfo("CMD.exe", strCmdLine);
            hbGetPresets.WindowStyle = ProcessWindowStyle.Hidden;

            Process hbproc = Process.Start(hbGetPresets);
            hbproc.WaitForExit();
            hbproc.Dispose();
            hbproc.Close();
        }

        /// <summary>
        /// Function which generates the filename and path automatically based on 
        /// the Source Name, DVD title and DVD Chapters
        /// </summary>
        /// <param name="mainWindow"></param>
        public void autoName(frmMain mainWindow)
        {
            if (Properties.Settings.Default.autoNaming == "Checked")
            {
                if (mainWindow.drp_dvdtitle.Text != "Automatic")
                {
                    string source = mainWindow.text_source.Text;
                    string[] sourceName = source.Split('\\');
                    source = sourceName[sourceName.Length - 1].Replace(".iso", "").Replace(".mpg", "").Replace(".ts", "").Replace(".ps", "");

                    string title = mainWindow.drp_dvdtitle.Text;
                    string[] titlesplit = title.Split(' ');
                    title = titlesplit[0];

                    string cs = mainWindow.drop_chapterStart.Text;
                    string cf = mainWindow.drop_chapterFinish.Text;

                    if (title == "Automatic")
                        title = "";
                    if (cs == "Auto")
                        cs = "";
                    if (cf == "Auto")
                        cf = "";

                    string dash = "";
                    if (cf != "Auto")
                        dash = "-";

                    if (!mainWindow.text_destination.Text.Contains("\\"))
                    {
                        string filePath = "";
                        if (Properties.Settings.Default.autoNamePath.Trim() != "")
                        {
                            if (Properties.Settings.Default.autoNamePath.Trim() != "Click 'Browse' to set the default location")
                                filePath = Properties.Settings.Default.autoNamePath + "\\";
                        }
                        mainWindow.text_destination.Text = filePath + source + "_T" + title + "_C" + cs + dash + cf + ".mp4";
                    }
                    else
                    {
                        string dest = mainWindow.text_destination.Text;

                        string[] destName = dest.Split('\\');


                        string[] extension = dest.Split('.');
                        string ext = extension[extension.Length - 1];

                        destName[destName.Length - 1] = source + "_T" + title + "_C" + cs + dash + cf + "." + ext;

                        string fullDest = "";
                        foreach (string part in destName)
                        {
                            if (fullDest != "")
                                fullDest = fullDest + "\\" + part;
                            else
                                fullDest = fullDest + part;
                        }

                        mainWindow.text_destination.Text = fullDest;
                    }
                }
            }
        }

        /// <summary>
        /// This function takes in a Query which has been parsed by QueryParser and
        /// set's all the GUI widgets correctly.
        /// </summary>
        /// <param name="mainWindow"></param>
        /// <param name="presetQuery">The Parsed CLI Query</param>
        /// <param name="name">Name of the preset</param>
        public void presetLoader(frmMain mainWindow, Functions.QueryParser presetQuery, string name)
        {
            // ---------------------------
            // Setup the GUI
            // ---------------------------

            // Source tab
            #region source
            // Reset some vaules to stock first to prevent errors.
            mainWindow.check_iPodAtom.CheckState = CheckState.Unchecked;

            // Now load all the new settings onto the main window
            if (presetQuery.Source != "")
                mainWindow.text_source.Text = presetQuery.Source;

            selectLongestTitle(mainWindow);
            mainWindow.drop_chapterStart.Text = "Auto";
            mainWindow.drop_chapterFinish.Text = "Auto";

            if (presetQuery.Format != null)
            {
                string destination = mainWindow.text_destination.Text;
                destination = destination.Replace(".mp4", "." + presetQuery.Format);
                destination = destination.Replace(".m4v", "." + presetQuery.Format);
                destination = destination.Replace(".avi", "." + presetQuery.Format);
                destination = destination.Replace(".mkv", "." + presetQuery.Format);
                destination = destination.Replace(".ogm", "." + presetQuery.Format);
                mainWindow.text_destination.Text = destination;
            }

            #endregion

            // Destination tab
            #region destination

            if (presetQuery.Destination != "")
                mainWindow.text_destination.Text = presetQuery.Destination;

            mainWindow.drp_videoEncoder.Text = presetQuery.VideoEncoder;

            if (presetQuery.Format != null)
            {
                if (presetQuery.Format == "mp4")
                    mainWindow.drop_format.SelectedIndex = 0;
                else if (presetQuery.Format == "m4v")
                    mainWindow.drop_format.SelectedIndex = 1;
                else if (presetQuery.Format == "mkv")
                    mainWindow.drop_format.SelectedIndex = 2;
                else if (presetQuery.Format == "avi")
                    mainWindow.drop_format.SelectedIndex = 3;
                else if (presetQuery.Format == "ogm")
                    mainWindow.drop_format.SelectedIndex = 4;
            }

            if (presetQuery.IpodAtom == true)
                mainWindow.check_iPodAtom.CheckState = CheckState.Checked;
            else
                mainWindow.check_iPodAtom.CheckState = CheckState.Unchecked;

            if (presetQuery.OptimizeMP4 == true)
                mainWindow.check_optimiseMP4.CheckState = CheckState.Checked;
            else
                mainWindow.check_optimiseMP4.CheckState = CheckState.Unchecked;

            #endregion



            // Picture Settings Tab
            #region Picture
            mainWindow.drp_crop.SelectedIndex = 1;
            mainWindow.text_top.Text = presetQuery.CropTop;
            mainWindow.text_bottom.Text = presetQuery.CropBottom;
            mainWindow.text_left.Text = presetQuery.CropLeft;
            mainWindow.text_right.Text = presetQuery.CropRight;

            mainWindow.drp_deInterlace_option.Text = presetQuery.DeInterlace;
            mainWindow.drp_deNoise.Text = presetQuery.DeNoise;

            if (presetQuery.DeTelecine == true)
                mainWindow.check_detelecine.CheckState = CheckState.Checked;
            else
                mainWindow.check_detelecine.CheckState = CheckState.Unchecked;


            if (presetQuery.DeBlock == true)
                mainWindow.check_deblock.CheckState = CheckState.Checked;
            else
                mainWindow.check_deblock.CheckState = CheckState.Unchecked;


            if (presetQuery.Anamorphic == true)
                mainWindow.drp_anamorphic.SelectedIndex = 1;
            else
                mainWindow.drp_anamorphic.SelectedIndex = 0;

            if (presetQuery.LooseAnamorphic == true)
                mainWindow.drp_anamorphic.SelectedIndex = 2;
            else
            {
                if (presetQuery.Anamorphic != true)
                    mainWindow.drp_anamorphic.SelectedIndex = 0;
            }


            if (presetQuery.Width != 0)
                mainWindow.text_width.Text = presetQuery.Width.ToString();
            else
            {
                mainWindow.text_width.Text = "";
            }

            if (presetQuery.Height != 0)
                mainWindow.text_height.Text = presetQuery.Height.ToString();
            else
            {
                mainWindow.text_height.Text = "";
            }

            if (presetQuery.VFR == true)
                mainWindow.check_vfr.CheckState = CheckState.Checked;
            else
                mainWindow.check_vfr.CheckState = CheckState.Unchecked;
            #endregion

            // Video Settings Tab
            #region video
            mainWindow.text_bitrate.Text = presetQuery.AverageVideoBitrate;
            mainWindow.text_filesize.Text = presetQuery.VideoTargetSize;
            mainWindow.slider_videoQuality.Value = presetQuery.VideoQuality;
            if (mainWindow.slider_videoQuality.Value != 0)
            {
                int ql = presetQuery.VideoQuality;
                mainWindow.SliderValue.Text = ql.ToString() + "%";
            }

            if (presetQuery.TwoPass == true)
                mainWindow.check_2PassEncode.CheckState = CheckState.Checked;
            else
                mainWindow.check_2PassEncode.CheckState = CheckState.Unchecked;

            if (presetQuery.Grayscale == true)
                mainWindow.check_grayscale.CheckState = CheckState.Checked;
            else
                mainWindow.check_grayscale.CheckState = CheckState.Unchecked;

            mainWindow.drp_videoFramerate.Text = presetQuery.VideoFramerate;

            if (presetQuery.TurboFirstPass == true)
                mainWindow.check_turbo.CheckState = CheckState.Checked;
            else
                mainWindow.check_turbo.CheckState = CheckState.Unchecked;

            if (presetQuery.LargeMP4 == true)
                mainWindow.check_largeFile.CheckState = CheckState.Checked;
            else
            {
                mainWindow.check_largeFile.CheckState = CheckState.Unchecked;
                mainWindow.check_largeFile.BackColor = Color.Transparent;
            }



            #endregion

            // Chapter Markers Tab
            #region Chapter Markers

            if (presetQuery.ChapterMarkers == true)
            {
                mainWindow.Check_ChapterMarkers.CheckState = CheckState.Checked;
                mainWindow.text_destination.Text = mainWindow.text_destination.Text.Replace(".mp4", ".m4v");
            }
            else
                mainWindow.Check_ChapterMarkers.CheckState = CheckState.Unchecked;

            #endregion

            // Audio Settings Tab
            #region Audio

            // Handle Track 1
            if (presetQuery.AudioTrack1 == "")
                mainWindow.drp_track1Audio.Text = "Automatic";
            else
                mainWindow.drp_track1Audio.Text = presetQuery.AudioTrack1;

            // Handle Track 2
            if (presetQuery.AudioEncoder2 != null)  // Fix for loading in built in presets. Where 2 encoders but no tracks in the preset.
            {
                mainWindow.drp_track2Audio.Enabled = true;
                mainWindow.drp_audsr_2.Enabled = true;
                mainWindow.drp_audmix_2.Enabled = true;
                mainWindow.drp_audenc_2.Enabled = true;
                mainWindow.drp_audbit_2.Enabled = true;
                mainWindow.drp_audsr_2.Text = "48";
                if ((presetQuery.AudioTrack2 != null) && (presetQuery.AudioTrack2 != "None"))
                    mainWindow.drp_track2Audio.Text = presetQuery.AudioTrack2;
                else
                    mainWindow.drp_track2Audio.Text = "Automatic";
            }
            else if (presetQuery.AudioTrack2 == "None")
            {
                mainWindow.drp_track2Audio.Text = "None";
                mainWindow.drp_track2Audio.SelectedIndex = 0;
                mainWindow.drp_audsr_2.Enabled = false;
                mainWindow.drp_audmix_2.Enabled = false;
                mainWindow.drp_audenc_2.Enabled = false;
                mainWindow.drp_audbit_2.Enabled = false;
            }
            else
            {
                mainWindow.drp_track2Audio.Text = presetQuery.AudioTrack2;
                mainWindow.drp_audsr_2.Enabled = true;
                mainWindow.drp_audmix_2.Enabled = true;
                mainWindow.drp_audenc_2.Enabled = true;
                mainWindow.drp_audbit_2.Enabled = true;
            }

            // Handle Track 3
            if (presetQuery.AudioTrack3 == "None")
            {
                mainWindow.drp_track3Audio.SelectedIndex = 0;
                mainWindow.drp_audsr_3.Enabled = false;
                mainWindow.drp_audmix_3.Enabled = false;
                mainWindow.drp_audenc_3.Enabled = false;
                mainWindow.drp_audbit_3.Enabled = false;
                mainWindow.trackBar3.Enabled = false;

                mainWindow.drp_track3Audio.Text = "None";
                mainWindow.drp_audsr_3.Text = "";
                mainWindow.drp_audmix_3.Text = "Automatic";
                mainWindow.drp_audenc_3.Text = "";
                mainWindow.drp_audbit_3.Text = "";
                mainWindow.trackBar3.Value = 0;

            }
            else
            {
                mainWindow.drp_track3Audio.Text = presetQuery.AudioTrack3;
                mainWindow.drp_audsr_3.Enabled = true;
                mainWindow.drp_audmix_3.Enabled = true;
                mainWindow.drp_audenc_3.Enabled = true;
                mainWindow.drp_audbit_3.Enabled = true;
                mainWindow.trackBar3.Enabled = true;
            }

            // Handle Track 4
            if (presetQuery.AudioTrack4 == "None")
            {
                mainWindow.drp_track4Audio.SelectedIndex = 0;
                mainWindow.drp_audsr_4.Enabled = false;
                mainWindow.drp_audmix_4.Enabled = false;
                mainWindow.drp_audenc_4.Enabled = false;
                mainWindow.drp_audbit_4.Enabled = false;
                mainWindow.trackBar4.Enabled = false;

                mainWindow.drp_track4Audio.Text = "None";
                mainWindow.drp_audsr_4.Text = "";
                mainWindow.drp_audmix_4.Text = "Automatic";
                mainWindow.drp_audenc_4.Text = "";
                mainWindow.drp_audbit_4.Text = "";
                mainWindow.trackBar4.Value = 0;
            }
            else
            {
                mainWindow.drp_track4Audio.Text = presetQuery.AudioTrack4;
                mainWindow.drp_audsr_4.Enabled = true;
                mainWindow.drp_audmix_4.Enabled = true;
                mainWindow.drp_audenc_4.Enabled = true;
                mainWindow.drp_audbit_4.Enabled = true;
                mainWindow.trackBar4.Enabled = true;
            }

            // Now lets start setting stuff
            if (presetQuery.AudioEncoder1 != null)
                mainWindow.drp_audenc_1.Text = presetQuery.AudioEncoder1;
            mainWindow.drp_audenc_2.Text = presetQuery.AudioEncoder2;
            mainWindow.drp_audenc_3.Text = presetQuery.AudioEncoder3;
            mainWindow.drp_audenc_4.Text = presetQuery.AudioEncoder4;

            if (presetQuery.AudioBitrate1 != null)
                mainWindow.drp_audbit_1.Text = presetQuery.AudioBitrate1;
            mainWindow.drp_audbit_2.Text = presetQuery.AudioBitrate2;
            mainWindow.drp_audbit_3.Text = presetQuery.AudioBitrate4;
            mainWindow.drp_audbit_3.Text = presetQuery.AudioBitrate4;

            if (presetQuery.AudioSamplerate1 != null)
                mainWindow.drp_audsr_1.Text = presetQuery.AudioSamplerate1;
            mainWindow.drp_audsr_2.Text = presetQuery.AudioSamplerate2;
            mainWindow.drp_audsr_3.Text = presetQuery.AudioSamplerate3;
            mainWindow.drp_audsr_4.Text = presetQuery.AudioSamplerate4;

            mainWindow.drp_audmix_1.Text = presetQuery.AudioTrackMix1;
            mainWindow.drp_audmix_2.Text = presetQuery.AudioTrackMix2;
            mainWindow.drp_audmix_3.Text = presetQuery.AudioTrackMix3;
            mainWindow.drp_audmix_4.Text = presetQuery.AudioTrackMix4;


            // Dynamic Range Compression (Should be a float but we use double for ease)
            double value = 0;
            double actualValue = 0;

            value = presetQuery.DRC1;
            if (value > 0)
                value = value - 10;
            mainWindow.trackBar1.Value = int.Parse(value.ToString());
            actualValue = presetQuery.DRC1 / 10;
            mainWindow.lbl_drc1.Text = actualValue.ToString();

            value = presetQuery.DRC2;
            if (value > 0)
                value = value - 10;
            mainWindow.trackBar2.Value = int.Parse(value.ToString());
            actualValue = presetQuery.DRC2 / 10;
            mainWindow.lbl_drc2.Text = actualValue.ToString();

            value = presetQuery.DRC3;
            if (value > 0)
                value = value - 10;
            mainWindow.trackBar3.Value = int.Parse(value.ToString());
            actualValue = presetQuery.DRC3 / 10;
            mainWindow.lbl_drc3.Text = actualValue.ToString();

            value = presetQuery.DRC4;
            if (value > 0)
                value = value - 10;
            mainWindow.trackBar4.Value = int.Parse(value.ToString());
            actualValue = presetQuery.DRC4 / 10;
            mainWindow.lbl_drc4.Text = actualValue.ToString();


            // Subtitle Stuff
            mainWindow.drp_subtitle.Text = presetQuery.Subtitles;

            if (presetQuery.ForcedSubtitles == true)
            {
                mainWindow.check_forced.CheckState = CheckState.Checked;
                mainWindow.check_forced.Enabled = true;
            }
            else
                mainWindow.check_forced.CheckState = CheckState.Unchecked;


            #endregion

            // H264 Tab & Preset Name
            #region other
            mainWindow.rtf_x264Query.Text = presetQuery.H264Query;

            // Set the preset name
            mainWindow.groupBox_output.Text = "Output Settings (Preset: " + name + ")";
            #endregion
        }

        /// <summary>
        /// Generates a CLI query based on the GUI widgets.
        /// </summary>
        /// <param name="mainWindow"></param>
        /// <returns>The CLI String</returns>
        public string GenerateTheQuery(frmMain mainWindow)
        {
            // Source tab
            #region source
            string source = mainWindow.text_source.Text;
            string dvdTitle = mainWindow.drp_dvdtitle.Text;
            string chapterStart = mainWindow.drop_chapterStart.Text;
            string chapterFinish = mainWindow.drop_chapterFinish.Text;
            int totalChapters = mainWindow.drop_chapterFinish.Items.Count - 1;
            string dvdChapter = "";

            if ((source != "") && (source.Trim() != "Click 'Browse' to continue"))
                source = " -i " + '"' + source + '"';
            else
                source = "";

            if (dvdTitle == "Automatic")
                dvdTitle = "";
            else
            {
                string[] titleInfo = dvdTitle.Split(' ');
                dvdTitle = " -t " + titleInfo[0];
            }

            if (chapterFinish.Equals("Auto") && chapterStart.Equals("Auto"))
                dvdChapter = "";
            else if (chapterFinish == chapterStart)
                dvdChapter = " -c " + chapterStart;
            else
                dvdChapter = " -c " + chapterStart + "-" + chapterFinish;

            string querySource = source + dvdTitle + dvdChapter;
            #endregion

            // Destination tab
            #region Destination

            string destination = mainWindow.text_destination.Text;
            string videoEncoder = mainWindow.drp_videoEncoder.Text;
            string width = mainWindow.text_width.Text;
            string height = mainWindow.text_height.Text;

            if (destination != "")
                destination = " -o " + '"' + destination + '"';

            switch (videoEncoder)
            {
                case "MPEG-4 (FFmpeg)":
                    videoEncoder = " -e ffmpeg";
                    break;
                case "MPEG-4 (XviD)":
                    videoEncoder = " -e xvid";
                    break;
                case "H.264 (x264)":
                    videoEncoder = " -e x264";
                    break;
                case "VP3 (Theora)":
                    videoEncoder = " -e theora";
                    break;
                default:
                    videoEncoder = " -e x264";
                    break;
            }

            if (width != "")
                width = " -w " + width;

            if (height == "Auto")
                height = "";
            else if (height != "")
                height = " -l " + height;

            string queryDestination = destination + videoEncoder + width + height;
            #endregion

            string query = querySource + queryDestination;
            query = query + generateTabbedComponentsQuery(mainWindow, source);
            return query;
        }

        /// <summary>
        /// Generates a CLI query for the preview function.
        /// This basically forces a shortened version of the encdode.
        /// </summary>
        /// <param name="mainWindow"></param>
        /// <returns>Returns a CLI query String.</returns>
        public string GeneratePreview(frmMain mainWindow)
        {
            // Source tab
            #region source
            string source = mainWindow.text_source.Text;
            string dvdTitle = mainWindow.drp_dvdtitle.Text;
            string chapterStart = mainWindow.drop_chapterStart.Text;
            string chapterFinish = mainWindow.drop_chapterFinish.Text;
            int totalChapters = mainWindow.drop_chapterFinish.Items.Count - 1;
            string dvdChapter = "";

            if ((source != "") && (source.Trim() != "Click 'Browse' to continue"))
                source = " -i " + '"' + source + '"';
            else
                source = "";

            if (dvdTitle == "Automatic")
                dvdTitle = "";
            else
            {
                string[] titleInfo = dvdTitle.Split(' ');
                dvdTitle = " -t " + titleInfo[0];
            }

            dvdChapter = " -c 2 ";

            string querySource = source + dvdTitle + dvdChapter;
            #endregion

            // Destination tab
            #region Destination

            string destination = mainWindow.text_destination.Text;
            string videoEncoder = mainWindow.drp_videoEncoder.Text;
            string width = mainWindow.text_width.Text;
            string height = mainWindow.text_height.Text;

            if (destination != "")
                destination = " -o " + '"' + destination.Replace(".m", "_sample.m").Replace(".avi", "_sample.avi").Replace(".ogm", "_sample.ogm") + '"';


            switch (videoEncoder)
            {
                case "MPEG-4 (FFmpeg)":
                    videoEncoder = " -e ffmpeg";
                    break;
                case "MPEG-4 (XviD)":
                    videoEncoder = " -e xvid";
                    break;
                case "H.264 (x264)":
                    videoEncoder = " -e x264";
                    break;
                case "VP3 (Theora)":
                    videoEncoder = " -e theora";
                    break;
                default:
                    videoEncoder = " -e x264";
                    break;
            }

            if (width != "")
                width = " -w " + width;

            if (height == "Auto")
                height = "";
            else if (height != "")
                height = " -l " + height;

            string queryDestination = destination + videoEncoder + width + height;
            #endregion

            string query = querySource + queryDestination;
            query = query + generateTabbedComponentsQuery(mainWindow, source);
            return query;
        }

        /// <summary>
        /// Set's up the DataGridView on the Chapters tab (frmMain)
        /// </summary>
        /// <param name="mainWindow"></param>
        public void chapterNaming(frmMain mainWindow)
        {
            try
            {
                mainWindow.data_chpt.Rows.Clear();
                int i = 0;
                int rowCount = 0;
                int start = 0;
                int finish = 0;
                if (mainWindow.drop_chapterFinish.Text != "Auto")
                    finish = int.Parse(mainWindow.drop_chapterFinish.Text);

                if (mainWindow.drop_chapterStart.Text != "Auto")
                    start = int.Parse(mainWindow.drop_chapterStart.Text);

                rowCount = finish - (start - 1);

                while (i < rowCount)
                {
                    DataGridViewRow row = new DataGridViewRow();

                    mainWindow.data_chpt.Rows.Insert(i, row);
                    mainWindow.data_chpt.Rows[i].Cells[0].Value = (i + 1);
                    mainWindow.data_chpt.Rows[i].Cells[1].Value = "Chapter " + (i + 1);
                    i++;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("chapterNaming() Error has occured: \n" + exc.ToString());
            }
        }

        /// <summary>
        /// Select the longest title in the DVD title dropdown menu on frmMain
        /// </summary>
        public void selectLongestTitle(frmMain mainWindow)
        {
            int current_largest = 0;
            Handbrake.Parsing.Title title2Select;

            // Check if there are titles in the DVD title dropdown menu and make sure, it's not just "Automatic"
            if (mainWindow.drp_dvdtitle.Items[0].ToString() != "Automatic")
                title2Select = (Handbrake.Parsing.Title)mainWindow.drp_dvdtitle.Items[0];
            else
                title2Select = null;

            // So, If there are titles in the DVD Title dropdown menu, lets select the longest.
            if (title2Select != null)
            {
                foreach (Handbrake.Parsing.Title x in mainWindow.drp_dvdtitle.Items)
                {
                    string title = x.ToString();
                    if (title != "Automatic")
                    {
                        string[] y = title.Split(' ');
                        string time = y[1].Replace("(", "").Replace(")", "");
                        string[] z = time.Split(':');

                        int hours = int.Parse(z[0]) * 60 * 60;
                        int minutes = int.Parse(z[1]) * 60;
                        int seconds = int.Parse(z[2]);
                        int total_sec = hours + minutes + seconds;

                        if (current_largest == 0)
                        {
                            current_largest = hours + minutes + seconds;
                            title2Select = x;
                        }
                        else
                        {
                            if (total_sec > current_largest)
                            {
                                current_largest = total_sec;
                                title2Select = x;
                            }
                        }
                    }
                }

                // Now set the longest title in the gui.
                mainWindow.drp_dvdtitle.SelectedItem = title2Select;
            }
        }

        // Generates part of the CLI query, for the tabbed components only.
        private string generateTabbedComponentsQuery(frmMain mainWindow, string source)
        {
            // Picture Settings Tab
            #region Picture Settings Tab

            string cropSetting = mainWindow.drp_crop.Text;
            string cropTop = mainWindow.text_top.Text;
            string cropBottom = mainWindow.text_bottom.Text;
            string cropLeft = mainWindow.text_left.Text;
            string cropRight = mainWindow.text_right.Text;
            string cropOut = "";
            string deInterlace_Option = mainWindow.drp_deInterlace_option.Text;
            string deinterlace = "";
            string grayscale = "";
            string pixelRatio = "";
            string vfr = "";
            string deblock = "";
            string detelecine = "";
            string lanamorphic = "";



            if (cropSetting == "Automatic")
                cropOut = "";
            else if (cropSetting == "No Crop")
                cropOut = " --crop 0:0:0:0 ";
            else
            {
                if ((mainWindow.text_top.Text == "") && (mainWindow.text_bottom.Text == "") && (mainWindow.text_left.Text == "") && (mainWindow.text_right.Text == ""))
                    cropOut = "";
                else
                {
                    if (mainWindow.text_top.Text == "")
                        cropTop = "0";
                    if (mainWindow.text_bottom.Text == "")
                        cropBottom = "0";
                    if (mainWindow.text_left.Text == "")
                        cropLeft = "0";
                    if (mainWindow.text_right.Text == "")
                        cropRight = "0";

                    cropOut = " --crop " + cropTop + ":" + cropBottom + ":" + cropLeft + ":" + cropRight;
                }
            }

            switch (deInterlace_Option)
            {
                case "None":
                    deinterlace = "";
                    break;
                case "Fast":
                    deinterlace = " --deinterlace=\"fast\"";
                    break;
                case "Slow":
                    deinterlace = " --deinterlace=\"slow\"";
                    break;
                case "Slower":
                    deinterlace = " --deinterlace=\"slower\"";
                    break;
                case "Slowest":
                    deinterlace = " --deinterlace=\"slowest\"";
                    break;
                default:
                    deinterlace = "";
                    break;
            }

            if (mainWindow.check_grayscale.Checked)
                grayscale = " -g ";

            if (mainWindow.drp_anamorphic.SelectedIndex == 1)
                pixelRatio = " -p ";
            else if (mainWindow.drp_anamorphic.SelectedIndex == 2)
                pixelRatio = " -P ";
            else
                pixelRatio = " ";


            if (mainWindow.check_deblock.Checked)
                deblock = " --deblock";

            if (mainWindow.check_detelecine.Checked)
                detelecine = " --detelecine";

            if (mainWindow.check_vfr.Checked)
                vfr = " -V ";



            string queryPictureSettings = cropOut + deinterlace + deblock + detelecine + vfr + grayscale + pixelRatio + lanamorphic;
            #endregion

            // Video Settings Tab
            #region Video Settings Tab

            string videoBitrate = mainWindow.text_bitrate.Text;
            string videoFilesize = mainWindow.text_filesize.Text;
            double videoQuality = mainWindow.slider_videoQuality.Value;
            string vidQSetting = "";
            string twoPassEncoding = "";
            string videoFramerate = mainWindow.drp_videoFramerate.Text;
            string vid_frame_rate = "";
            string turboH264 = "";
            string largeFile = "";
            string denoise = "";
            string ipodAtom = "";
            string optimizeMP4 = "";

            if (videoBitrate != "")
                videoBitrate = " -b " + videoBitrate;

            if (videoFilesize != "")
                videoFilesize = " -S " + videoFilesize;

            // Video Quality Setting

            if ((videoQuality == 0))
                vidQSetting = "";
            else
            {
                videoQuality = videoQuality / 100;
                if (videoQuality == 1)
                {
                    vidQSetting = "1.0";
                }
                vidQSetting = " -q " + videoQuality.ToString(new CultureInfo("en-US"));
            }

            if (mainWindow.check_2PassEncode.Checked)
                twoPassEncoding = " -2 ";

            if (videoFramerate == "Same as source")
                vid_frame_rate = "";
            else
            {
                if (!mainWindow.check_vfr.Checked)
                    vid_frame_rate = " -r " + videoFramerate;
            }

            if (mainWindow.check_turbo.Checked)
                turboH264 = " -T ";

            if (mainWindow.check_largeFile.Checked)
                largeFile = " -4 ";


            switch (mainWindow.drp_deNoise.Text)
            {
                case "None":
                    denoise = "";
                    break;
                case "Weak":
                    denoise = " --denoise=\"weak\"";
                    break;
                case "Medium":
                    denoise = " --denoise=\"medium\"";
                    break;
                case "Strong":
                    denoise = " --denoise=\"strong\"";
                    break;
                default:
                    denoise = "";
                    break;
            }

            if (mainWindow.check_iPodAtom.Checked)
                ipodAtom = " -I ";

            if (mainWindow.check_optimiseMP4.Checked)
                optimizeMP4 = " -O ";


            string queryVideoSettings = videoBitrate + videoFilesize + vidQSetting + twoPassEncoding + vid_frame_rate + turboH264 + ipodAtom + optimizeMP4 + largeFile + denoise;
            #endregion

            // Audio Settings Tab
            #region Audio Settings Tab

            // Query
            string tracks = "";
            string aencoder = "";
            string audioBitrate = "";
            string audioSampleRate = "";
            string Mixdown = "";
            string drc = "";
            string subScan = "";
            string forced = "";

            // Track 1
            string track1 = mainWindow.drp_track1Audio.Text;
            string aencoder1 = mainWindow.drp_audenc_1.Text;
            string audioBitrate1 = mainWindow.drp_audbit_1.Text;
            string audioSampleRate1 = mainWindow.drp_audsr_1.Text;
            string Mixdown1 = mainWindow.drp_audmix_1.Text;
            string drc1 = mainWindow.trackBar1.Value.ToString();

            // Track 2
            string track2 = mainWindow.drp_track2Audio.Text;
            string aencoder2 = mainWindow.drp_audenc_2.Text;
            string audioBitrate2 = mainWindow.drp_audbit_2.Text;
            string audioSampleRate2 = mainWindow.drp_audsr_2.Text;
            string Mixdown2 = mainWindow.drp_audmix_2.Text;
            string drc2 = mainWindow.trackBar2.Value.ToString();

            // Track 3
            string track3 = mainWindow.drp_track3Audio.Text;
            string aencoder3 = mainWindow.drp_audenc_3.Text;
            string audioBitrate3 = mainWindow.drp_audbit_3.Text;
            string audioSampleRate3 = mainWindow.drp_audsr_3.Text;
            string Mixdown3 = mainWindow.drp_audmix_3.Text;
            string drc3 = mainWindow.trackBar3.Value.ToString();

            // Track 4
            string track4 = mainWindow.drp_track4Audio.Text;
            string aencoder4 = mainWindow.drp_audenc_4.Text;
            string audioBitrate4 = mainWindow.drp_audbit_4.Text;
            string audioSampleRate4 = mainWindow.drp_audsr_4.Text;
            string Mixdown4 = mainWindow.drp_audmix_4.Text;
            string drc4 = mainWindow.trackBar4.Value.ToString();


            //
            // Audio Track Selections
            //
            if (track1 == "Automatic")
                tracks = " -a 1";
            else if (track1 == "")
                tracks = "";
            else if (track1 == "None")
                tracks = "";
            else
            {
                string[] tempSub = track1.Split(' ');
                tracks = " -a " + tempSub[0];
            }

            if (track2 != "None")
            {
                string[] tempSub;
                tempSub = track2.Split(' ');
                if (tracks == "")
                    tracks = " -a none," + tempSub[0];
                else
                    tracks = tracks + "," + tempSub[0];
            }

            if (track3 != "None")
            {
                string[] tempSub;
                tempSub = track3.Split(' ');
                tracks = tracks + "," + tempSub[0];
            }

            if (track4 != "None")
            {
                string[] tempSub;
                tempSub = track4.Split(' ');
                tracks = tracks + "," + tempSub[0];
            }

            //
            // Audio Encoder
            //
            if (aencoder1 != "")
                aencoder = " -E " + getAudioEncoder(aencoder1);

            if (aencoder2 != "")
            {
                if (aencoder == "")
                    aencoder = " -E faac," + getAudioEncoder(aencoder2);
                else
                    aencoder = aencoder + "," + getAudioEncoder(aencoder2);
            }

            if (aencoder3 != "")
            {
                aencoder = aencoder + "," + getAudioEncoder(aencoder3);
            }

            if (aencoder4 != "")
            {
                aencoder = aencoder + "," + getAudioEncoder(aencoder4);
            }

            //
            // Audio Bitrate Selections
            //
            if (audioBitrate1 != "")
                audioBitrate = " -B " + audioBitrate1;

            if (audioBitrate2 != "")
            {
                if (audioBitrate == "")
                    audioBitrate = " -B 160," + audioBitrate2;
                else
                    audioBitrate = audioBitrate + "," + audioBitrate2;
            }

            if (audioBitrate3 != "")
            {
                audioBitrate = audioBitrate + "," + audioBitrate3;
            }

            if (audioBitrate4 != "")
            {
                audioBitrate = audioBitrate + "," + audioBitrate4;
            }

            //Audio Sample Rate   - audioSampleRate

            if (audioSampleRate1 != "")
                audioSampleRate = " -R " + audioSampleRate1.Replace("Auto", "0");

            if (audioSampleRate2 != "")
            {
                if (audioSampleRate == "")
                    audioSampleRate = " -R 0," + audioSampleRate2.Replace("Auto", "0");
                else
                    audioSampleRate = audioSampleRate + "," + audioSampleRate2.Replace("Auto", "0");
            }
            else
            {
                // All this is a hack, because when AppleTV is selected, there is no sample rate selected. so just add a 48
                // It should probably be setup later so the GUI widget has the value 48 in it.

                if ((track2 != "") && (track2 != "None"))
                {
                    if (audioSampleRate == "")
                        audioSampleRate = " -R 0,0";
                    else
                        audioSampleRate = audioSampleRate + ",0";
                }

            }

            if (audioSampleRate3 != "")
            {
                audioSampleRate = audioSampleRate + "," + audioSampleRate3.Replace("Auto", "0");
            }

            if (audioSampleRate4 != "")
            {
                audioSampleRate = audioSampleRate + "," + audioSampleRate4.Replace("Auto", "0");
            }

            //
            // Audio Mixdown Selections
            //

            if ((Mixdown1 != "") && (Mixdown1 != "Automatic"))
                Mixdown = " -6 " + getMixDown(Mixdown1);

            if ((Mixdown2 != "") && (Mixdown2 != "Automatic"))
            {
                if (Mixdown != "")
                    Mixdown = Mixdown + "," + getMixDown(Mixdown2);
            }

            if ((Mixdown3 != "") && (Mixdown3 != "Automatic"))
            {
                if (Mixdown != "")
                    Mixdown = Mixdown + "," + getMixDown(Mixdown3);
            }

            if ((Mixdown4 != "") && (Mixdown4 != "Automatic"))
            {
                if (Mixdown != "")
                    Mixdown = Mixdown + "," + getMixDown(Mixdown4);
            }


            //
            // DRC
            //
            double value = 0;

            value = mainWindow.trackBar1.Value / 10.0;
            value++;

            if (value > 1.0)
                drc = " -D " + value;

            value = mainWindow.trackBar2.Value / 10.0;
            value++;
            if (drc2 != "0")
            {
                if (drc == "")
                    drc = " -D 1," + value;
                else
                    drc = drc + "," + value;
            }

            value = mainWindow.trackBar3.Value / 10.0;
            value++;
            if (drc3 != "0")
            {
                drc = drc + "," + value;
            }

            value = mainWindow.trackBar4.Value / 10.0;
            value++;
            if (drc4 != "0")
            {
                drc = drc + "," + value;
            }


            // Subtitles
            string subtitles = mainWindow.drp_subtitle.Text;
            if (subtitles == "None")
                subtitles = "";
            else if (subtitles == "")
                subtitles = "";
            else if (subtitles == "Autoselect")
            {
                subScan = " -U ";
                subtitles = "";
            }
            else
            {
                string[] tempSub;
                tempSub = subtitles.Split(' ');
                subtitles = " -s " + tempSub[0];
            }

            if (mainWindow.check_forced.Checked)
                forced = " -F ";


            string queryAudioSettings = tracks + aencoder + audioBitrate + audioSampleRate + Mixdown + drc + subScan + subtitles + forced;

            #endregion

            // Chapter Markers Tab
            #region Chapter Markers

            string ChapterMarkers = "";

            // Attach Source name and dvd title to the start of the chapters.csv filename.
            // This is for the queue. It allows different chapter name files for each title.
            string source_name = mainWindow.text_source.Text;
            string[] sourceName = source.Split('\\');
            source_name = sourceName[sourceName.Length - 1].Replace(".iso", "").Replace(".mpg", "").Replace(".ts", "").Replace(".ps", "");
            source_name = source_name.Replace("\"", "");

            string source_title = mainWindow.drp_dvdtitle.Text;
            string[] titlesplit = source_title.Split(' ');
            source_title = titlesplit[0];

            if (mainWindow.Check_ChapterMarkers.Checked)
            {

                if ((source_name.Trim().Replace("-i ", "") != "Click 'Browse' to continue") && (source_name.Trim().Replace("-i ", "") != ""))
                {
                    if (source_title != "Automatic")
                    {
                        string filename = source_name + "-" + source_title + "-chapters.csv";
                        string path = Path.Combine(Path.GetTempPath(), filename);

                        Boolean saveCSV = chapterCSVSave(mainWindow, path);
                        if (saveCSV == false)
                            ChapterMarkers = " -m ";
                        else
                        {
                            ChapterMarkers = " --markers=" + "\"" + path + "\"";
                        }
                    }
                    else
                    {
                        string filename = source_name + "-chapters.csv";
                        string path = Path.Combine(Path.GetTempPath(), filename);

                        Boolean saveCSV = chapterCSVSave(mainWindow, path);
                        if (saveCSV == false)
                            ChapterMarkers = " -m ";
                        else
                        {
                            ChapterMarkers = " --markers=" + "\"" + path + "\"";
                        }
                    }
                }
                else
                {
                    string path = Path.Combine(Path.GetTempPath(), "chapters.csv");
                    ChapterMarkers = " --markers=" + "\"" + path + "\"";
                }
            }

            string chapter_markers = ChapterMarkers;
            #endregion

            // H264 Tab
            #region  H264 Tab

            string h264Advanced = mainWindow.rtf_x264Query.Text;

            if ((h264Advanced == ""))
                h264Advanced = "";
            else
                h264Advanced = " -x " + h264Advanced;


            string h264Settings = h264Advanced;
            #endregion

            // Other
            #region Processors / Other

            string processors = Properties.Settings.Default.Processors;
            //  Number of Processors Handler

            if (processors == "Automatic")
                processors = "";
            else
                processors = " -C " + processors + " ";


            string queryAdvancedSettings = processors;

            string verbose = " -v ";
            #endregion

            return queryPictureSettings + queryVideoSettings + h264Settings + queryAudioSettings + ChapterMarkers + queryAdvancedSettings + verbose;
        }

        // This function saves the data in the chapters tab, dataGridView into a CSV file called chapters.csv
        // in a directory specified by file_path_name
        private Boolean chapterCSVSave(frmMain mainWindow, string file_path_name)
        {
            try
            {
                StringBuilder csv = new StringBuilder();

                foreach (DataGridViewRow row in mainWindow.data_chpt.Rows)
                {
                    csv.Append(row.Cells[0].Value.ToString());
                    csv.Append(",");
                    csv.Append(row.Cells[1].Value.ToString());
                    csv.Append(Environment.NewLine);
                }
                StreamWriter file = new StreamWriter(file_path_name);
                file.Write(csv.ToString());
                file.Close();
                file.Dispose();
                return true;

            }
            catch (Exception exc)
            {
                MessageBox.Show("Unable to save Chapter Makrers file! \nChapter marker names will NOT be saved in your encode \n\n" + exc.ToString(), "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        // Get the CLI equive of the audio mixdown from the widget name.
        private string getMixDown(string selectedAudio)
        {
            switch (selectedAudio)
            {
                case "Automatic":
                    return "";
                case "Mono":
                    return "mono";
                case "Stereo":
                    return "stereo";
                case "Dolby Surround":
                    return "dpl1";
                case "Dolby Pro Logic II":
                    return "dpl2";
                case "6 Channel Discrete":
                    return "6ch";
                default:
                    return "";
            }
        }

        // Get the CLI equiv of the audio encoder from the widget name.
        private string getAudioEncoder(string selectedEncoder)
        {
            switch (selectedEncoder)
            {
                case "AAC":
                    return "faac";
                case "MP3":
                    return "lame";
                case "Vorbis":
                    return "vorbis";
                case "AC3":
                    return "ac3";
                default:
                    return "";
            }
        }

    }
}
