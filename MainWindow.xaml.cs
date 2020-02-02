/*
 * Copyright (C) 2020 Russell Brown
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using CSLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace DirectoryMirror
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private Copier copier = null;
        private const string startText = "Start";
        private const string abortText = "Abort";

        public MainWindow()
        {
            // initialize logging and read settings

            Settings.Load("DirectoryMirror");
            l.To("mirrorbackup.log");

            // get debugging level from settings. make sure it is written
            // back to ensure it exists in the file to make editing possible
            bool debuglevel = Settings.Get("Debug", false);
            if (debuglevel)
            {
                l.MinConsoleLogLevel = l.Level.Debug;
                l.MinLogLevel = l.Level.Debug;
                Settings.Set("Debug", true);
            }
            else
            {
                l.MinConsoleLogLevel = l.Level.Info;
                l.MinLogLevel = l.Level.Info;
                Settings.Set("Debug", false);
            }

            InitializeComponent();
            try
            {
                DryRunCB.IsChecked = Settings.Get("DryRun", false);
                CheckTimestampsCB.IsChecked = Settings.Get("CheckTimestamps", false);
                CheckContentCB.IsChecked = Settings.Get("CheckContent", false);
                RemInDestCB.IsChecked = Settings.Get("RemIfNotInSrc", false);
                SourceTB.Text = Settings.Get("SourceDir", "");
                DestinationTB.Text = Settings.Get("DestDir", "");
                DryRunCB.IsChecked = Settings.Get("DryRun", true);
                CheckSizeCB.IsChecked = Settings.Get("CheckSize", false);
                CheckSizeBiggerCB.IsChecked = Settings.Get("CheckSizeBigger", false);
                CheckContentQuickCB.IsChecked = Settings.Get("CheckContentQuick", false);
                TimeBufferCB.IsChecked = Settings.Get("TimeBuffer", false);
            }
            catch
            {

            }

            // start timer for checking copier progress and GUI updates
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += checkCopierProgressAndUpdateGUI;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            dispatcherTimer.Start();

        }

        //
        // checkCopierProgressAndUpdateGUI
        //
        // get copier status and update GUI. Add any messages to console list,
        // if copier is finished, reset the 'Start' button to show Start
        //
        private void checkCopierProgressAndUpdateGUI(object sender, EventArgs e)
        {
            if (copier != null)
            {
                Status.Content = copier.GetStatus();
                if (!copier.IsRunning && (string)StartBtn.Content != "Start")
                    StartBtn.Content = "Start";
                List<String> messages = copier.GetMessages();
                foreach(String s in messages)
                {
                    console.Items.Add(s);
                }
            }
            else
                Status.Content = "...";
        }

        //
        // SelectDestBtn_Click
        //
        // called by select destination button. Show dialog & update deatination text box
        //
        private void SelectDestBtn_Click(object sender, RoutedEventArgs e)
        {
            // NOTE - ookii dialog is used as WPF still dosn't have an inbuilt browser and 
            // we don't want to package up all of System.Windows.Forms to use that one
            var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dlg.ShowNewFolderButton = false;
            dlg.SelectedPath = DestinationTB.Text;
            bool res = (bool)dlg.ShowDialog();
            if (res)
                DestinationTB.Text = dlg.SelectedPath;
        }

        //
        // SelectSourceBtn_Click
        //
        // called by select source button. Show dialog & update source text box
        //
        private void SelectSourceBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dlg.ShowNewFolderButton = false;
            dlg.SelectedPath = SourceTB.Text;
            bool res = (bool)dlg.ShowDialog();
            if (res)
                SourceTB.Text = dlg.SelectedPath;
        }

        //
        // StartBtn_Click
        //
        // called by 'Start' button. This may be displaying 'Start' to start a scan or
        // 'Abort' to stop one.
        //
        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            // if abort, stop the copier, update status and rename button to 'Start'
            if ((string)StartBtn.Content == abortText && copier != null)
            {
                copier.Stop();
                StartBtn.Content = startText;
                Status.Content = copier.GetStatus();
                return;
            }

            // if 'Start' check source exists and create dest if nesessary
            if (!Directory.Exists(SourceTB.Text))
            {
                MessageBox.Show(SourceTB.Text + " does not exist");
                return;
            }
            if (!Directory.Exists(DestinationTB.Text))
            {
                Directory.CreateDirectory(DestinationTB.Text);
            }

            // create copier process and run it
            copier = new Copier(SourceTB.Text, DestinationTB.Text, 
                                (bool)CheckTimestampsCB.IsChecked,(bool)TimeBufferCB.IsChecked,
                                (bool)CheckContentCB.IsChecked,(bool)CheckContentQuickCB.IsChecked, 
                                (bool)CheckSizeCB.IsChecked, (bool)CheckSizeBiggerCB.IsChecked,
                                (bool)RemInDestCB.IsChecked, (bool)DryRunCB.IsChecked);
            console.Items.Clear();
            copier.Start();
            // change function of start button to 'Abort'
            StartBtn.Content = abortText;
        }

        //
        // Window_Closing
        //
        // when app closes, save settings
        //
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Set("RemIfNotInSrc",(bool)RemInDestCB.IsChecked);
            Settings.Set("CheckTimestamps", (bool)CheckTimestampsCB.IsChecked);
            Settings.Set("CheckContent", (bool)CheckContentCB.IsChecked);
            Settings.Set("SourceDir", SourceTB.Text);
            Settings.Set("DestDir", DestinationTB.Text);
            Settings.Set("DryRun", (bool)DryRunCB.IsChecked);
            Settings.Set("CheckSize", (bool)CheckSizeCB.IsChecked);
            Settings.Set("CheckSizeBigger", (bool)CheckSizeBiggerCB.IsChecked);
            Settings.Set("CheckContentQuick", (bool)CheckContentQuickCB.IsChecked );
            Settings.Set("TimeBuffer", (bool)TimeBufferCB.IsChecked);
            Settings.Save();
        }

        //
        // LogBtn_Click
        //
        // 'View Log' button pressed. Open log file with the default application
        //
        private void LogBtn_Click(object sender, RoutedEventArgs e)
        {
            l.Pause();
            string path = l.GetPath();
            ProcessStartInfo psi = new ProcessStartInfo(path);
            psi.UseShellExecute = true;
            Process.Start(psi); 
            l.Resume();
        }

        //
        // CheckSizeBigger_Click
        //
        // if user clicks the 'copy if size is bigger' checkbox, make sure the copy on
        // size checkbox is checked
        //
        private void CheckSizeBigger_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)CheckSizeBiggerCB.IsChecked)
                CheckSizeCB.IsChecked = true;
        }

        //
        // CheckContentQuickCB_Click
        //
        // if user clicks the 'quick content check' checkbox, make sure the copy on
        // content changed checkbox is checked
        //
        private void CheckContentQuickCB_Click(object sender, RoutedEventArgs e)
        {
            if ( (bool)CheckContentQuickCB.IsChecked )
                CheckContentCB.IsChecked = true;
        }

        //
        // TimeBufferCB_Click
        //
        // if user clicks the 'allow buffer' checkbox, make sure the copy on
        // newer checkbox is checked
        //
        private void TimeBufferCB_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)TimeBufferCB.IsChecked)
                CheckTimestampsCB.IsChecked = true;
        }
    }
}
