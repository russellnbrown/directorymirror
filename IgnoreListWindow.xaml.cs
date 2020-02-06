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
        }
    }

  

}
