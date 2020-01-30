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
       

        public MainWindow()
        {
            Settings.Load("DirectoryMirror");
            l.To("mirrorbackup.log");

            InitializeComponent();
            try
            {
                DryRunCB.IsChecked = Settings.Get("DryRun",false);
                CheckTimestampsCB.IsChecked = Settings.Get("CheckTimestamps", false);
                CheckContentCB.IsChecked = Settings.Get("CheckContent", false);
                RemInDestCB.IsChecked = Settings.Get("RemIfNotInSrc", false);
                SourceTB.Text = Settings.Get("SourceDir", "");
                DestinationTB.Text = Settings.Get("DestDir", "");
                DryRunCB.IsChecked = Settings.Get("DryRun", true);
                cz.IsChecked = Settings.Get("CheckSize", false);
            }
            catch
            {

            }
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += updateMainList;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            dispatcherTimer.Start();

        }
        private void updateMainList(object sender, EventArgs e)
        {
            if (copier != null)
            {
                Status.Content = copier.GetStatus();
                if (!copier.IsRunning)
                    StartBtn.Content = "Start";
                List<String> messages = copier.getMessages();
                foreach(String s in messages)
                {
                    console.Items.Add(s);
                }
            }
            else
                Status.Content = "...";
        }

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

        private void SelectSourceBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dlg.ShowNewFolderButton = false;
            dlg.SelectedPath = SourceTB.Text;
            bool res = (bool)dlg.ShowDialog();
            if (res)
                SourceTB.Text = dlg.SelectedPath;
        }

        private Copier copier = null;

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((string)StartBtn.Content == "Abort" && copier != null)
            {
                copier.Stop();
                StartBtn.Content = "Start";
                Status.Content = copier.GetStatus();
                return;
            }

            if (!Directory.Exists(SourceTB.Text))
            {
                MessageBox.Show(SourceTB.Text + " does not exist");
                return;
            }
            if (!Directory.Exists(SourceTB.Text))
            {
                MessageBox.Show(DestinationTB.Text + " does not exist");
                return;
            }
            copier = new Copier(SourceTB.Text, DestinationTB.Text, (bool)CheckTimestampsCB.IsChecked,
                                (bool)CheckContentCB.IsChecked, (bool)cz.IsChecked, (bool)RemInDestCB.IsChecked, (bool)DryRunCB.IsChecked);
            console.Items.Clear();
            copier.Start();
            StartBtn.Content = "Abort";


        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Set("RemIfNotInSrc",(bool)RemInDestCB.IsChecked);
            Settings.Set("CheckTimestamps", (bool)CheckTimestampsCB.IsChecked);
            Settings.Set("CheckContent", (bool)CheckContentCB.IsChecked);
            Settings.Set("SourceDir", SourceTB.Text);
            Settings.Set("DestDir", DestinationTB.Text);
            Settings.Set("DryRun", (bool)DryRunCB.IsChecked);
            Settings.Set("CheckSize", (bool)cz.IsChecked);
            Settings.Save();
        }

        private void LogBtn_Click(object sender, RoutedEventArgs e)
        {
            l.Pause();
            string path = l.GetPath();
            ProcessStartInfo psi = new ProcessStartInfo(path);
            psi.UseShellExecute = true;
            Process.Start(psi); 
            l.Resume();
        }
    }
}
