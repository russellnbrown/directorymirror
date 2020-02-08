﻿/*
 * Copyright (C) 2020 russell brown
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


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Data;
using System.Globalization;

namespace DirectoryMirror
{
    /// <summary>
    /// Interaction logic for IgnoreListWindow.xaml
    /// </summary>
    public partial class IgnoreListWindow : Window
    {

        
    public IgnoreListWindow()
        {
            
            InitializeComponent();

            includes.ItemsSource = MainWindow.Get.includes;
            excludes.ItemsSource = MainWindow.Get.excludes;
            excludedirs.ItemsSource = MainWindow.Get.excludedirs;
        }
    }

  

}
