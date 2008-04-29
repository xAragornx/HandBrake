/*  frmReadDVD.cs $
 	
 	   This file is part of the HandBrake source code.
 	   Homepage: <http://handbrake.fr>.
 	   It may be used under the terms of the GNU General Public License. */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections;


namespace Handbrake
{
    public partial class frmReadDVD : Form
    {
        private string inputFile;
        private frmMain mainWindow;
        private frmDvdInfo dvdInfo;
        private Parsing.DVD thisDvd;
        private delegate void UpdateUIHandler();

        public frmReadDVD(string inputFile, frmMain parent, frmDvdInfo dvdInfoWindow)
        {
            InitializeComponent();
            this.inputFile = inputFile;
            this.mainWindow = parent;
            this.dvdInfo = dvdInfoWindow;
            startScan();

        }

        private void startScan()
        {
            try
            {
                lbl_status.Visible = true;
                ThreadPool.QueueUserWorkItem(startProc);
            }
            catch (Exception exc)
            {
                MessageBox.Show("frmReadDVD.cs - startScan " + exc.ToString());
            }
        }

        private void updateUIElements()
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new UpdateUIHandler(updateUIElements));
                    return;
                }
                // Now pass this streamreader to frmMain so that it can be used there.
                mainWindow.setStreamReader(thisDvd);

                mainWindow.drp_dvdtitle.Items.Clear();
                mainWindow.drp_dvdtitle.Items.AddRange(thisDvd.Titles.ToArray());
                mainWindow.drp_dvdtitle.Text = "Automatic";
                mainWindow.drop_chapterFinish.Text = "Auto";
                mainWindow.drop_chapterStart.Text = "Auto";

                // Now select the longest title
                int current_largest = 0;
                Handbrake.Parsing.Title title2Select = thisDvd.Titles[0];

                foreach (Handbrake.Parsing.Title x in thisDvd.Titles)
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


                    mainWindow.drp_dvdtitle.SelectedItem = title2Select;

                this.Close();
            }
            catch (Exception exc)
            {
                MessageBox.Show("frmReadDVD.cs - updateUIElements " + exc.ToString());
                this.Close();
            }
        }

        Functions.CLI process = new Functions.CLI();

        private void startProc(object state)
        {
            try
            {
                string handbrakeCLIPath = Path.Combine(Application.StartupPath, "HandBrakeCLI.exe");
                string dvdInfoPath = Path.Combine(Path.GetTempPath(), "dvdinfo.dat");

                // Make we don't pick up a stale dvdinfo.dat (and that we have rights to the file)
                if (File.Exists(dvdInfoPath))
                    File.Delete(dvdInfoPath);

                string strCmdLine = String.Format(@"cmd /c """"{0}"" -i ""{1}"" -t0 -v >""{2}"" 2>&1""", handbrakeCLIPath, inputFile, dvdInfoPath);

                ProcessStartInfo hbParseDvd = new ProcessStartInfo("CMD.exe", strCmdLine);
                hbParseDvd.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process hbproc = Process.Start(hbParseDvd))
                {
                    hbproc.WaitForExit();
                    // TODO: Verify exit code if the CLI supports it properly
                } 

                if (!File.Exists(dvdInfoPath))
                {
                    throw new Exception("Unable to retrieve the DVD Info. dvdinfo.dat is missing.");
                }

                using (StreamReader sr = new StreamReader(dvdInfoPath))
                {
                    thisDvd = Parsing.DVD.Parse(sr);
                }

                updateUIElements();
            }
            catch (Exception exc)
            {
                MessageBox.Show("frmReadDVD.cs - startProc " + exc.ToString());
                this.Close();
            }

        }
    }
}