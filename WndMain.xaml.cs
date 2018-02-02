using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace Meow.FR.TidyTinyPics
{
    /// <summary>
    /// Interaction logic for WndMain.xaml
    /// </summary>
    public partial class WndMain : Window
    {
        TidyTinyManager _viewModel = new TidyTinyManager();

        public WndMain()
        {
            InitializeComponent();
            dckMain.DataContext = _viewModel;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Start();
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])(e.Data.GetData(DataFormats.FileDrop));
                foreach (string file in droppedFiles)
                {
                    FileInfo fInfo = new FileInfo(file);
                    if (fInfo.Exists)
                        _viewModel.AddFile(fInfo);
                    else
                    {
                        DirectoryInfo dInfo = new DirectoryInfo(file);
                        if (dInfo.Exists)
                        {
                            FileInfo[] files = dInfo.GetFiles("*.*", SearchOption.AllDirectories);
                            foreach (FileInfo currentf in files)
                                _viewModel.AddFile(currentf);
                        }
                    }
                }
            }
        }

        private void ListView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Reset();
        }

        private void lstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
                _viewModel.CurrentImage = BitmapFrame.Create(new Uri(((FileInfo)e.AddedItems[0]).FullName), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
    }
}
