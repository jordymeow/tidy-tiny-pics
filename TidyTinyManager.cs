using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.IO;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Threading;

namespace Meow.FR.TidyTinyPics
{
    class TidyTinyManager : DispatcherObject, INotifyPropertyChanged
    {
        BackgroundWorker bWorker = new BackgroundWorker();

        #region Properties
        private ObservableCollection<FileInfo> _Files;
        public ObservableCollection<FileInfo> Files
        {
            get { return _Files; }
            set
            {
                _Files = value;
                OnPropertyChanged(o => o.Files);
            }
        }

        public int Count { get { return Files.Count; } }

        private bool _RenameFiles;
        public bool RenameFiles
        {
            get { return _RenameFiles; }
            set
            {
                _RenameFiles = value;
                OnPropertyChanged(o => o.RenameFiles);
            }
        }

        private int _Processed;
        public int Processed
        {
            get { return _Processed; }
            set
            {
                _Processed = value;
                OnPropertyChanged(o => o.Processed);
            }
        }

        private bool _IsRunning;
        public bool IsRunning
        {
            get { return _IsRunning; }
            set
            {
                _IsRunning = value;
                OnPropertyChanged(o => o.IsRunning);
            }
        }

        private string _MaxEdgeSize;
        public string MaxEdgeSize
        {
            get { return _MaxEdgeSize; }
            set
            {
                _MaxEdgeSize = value;
                OnPropertyChanged(o => o.MaxEdgeSize);
            }
        }

        private string _Quality;
        public string Quality
        {
            get { return _Quality; }
            set
            {
                _Quality = value;
                OnPropertyChanged(o => o.Quality);
            }
        }

        private BitmapSource _CurrentImage;
        public BitmapSource CurrentImage
        {
            get { return _CurrentImage; }
            set
            {
                _CurrentImage = value;
                OnPropertyChanged(o => o.CurrentImage);
            }
        }
        #endregion

        #region OnPropertyChanged
        protected virtual void OnPropertyChanged<R>(Expression<Func<TidyTinyManager, R>> propertyExpr)
        {
            Dispatcher.Invoke(DispatcherPriority.DataBind, new Action(() =>
            {
                OnPropertyChanged(this.GetPropertySymbol(propertyExpr));
            }));
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            Dispatcher.Invoke(DispatcherPriority.DataBind, new Action(() =>
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }));
        }

        public event PropertyChangedEventHandler PropertyChanged = (a, b) => { };
        #endregion

        #region Image resizing algorithms
        private bool ResizeImage(FileInfo file, int maximumEdgeSize, long quality)
        {
            FileStream memoryStream = null;
            int newWidth, newHeight;
            try
            {
                // Read the properties and the image
                Stack<PropertyItem> properties = new Stack<PropertyItem>();
                memoryStream = file.OpenRead();
                System.Drawing.Image image = System.Drawing.Image.FromStream(memoryStream);
                if (image.PropertyItems != null)
                    foreach (PropertyItem item in image.PropertyItems)
                        properties.Push(item);
                memoryStream.Close();
                memoryStream.Dispose();

                double ratio = (double)image.Width / (double)image.Height;
                if (ratio < 1)
                {
                    // Portrait
                    newHeight = maximumEdgeSize;
                    newWidth = Convert.ToInt32((double)maximumEdgeSize * ratio);
                }
                else
                {
                    // Lanscape
                    newWidth = maximumEdgeSize;
                    newHeight = Convert.ToInt32((double)maximumEdgeSize / ratio);
                }
                if (newWidth > image.Width || newHeight > image.Height)
                    return false;


                System.Drawing.Image thumbnail = new Bitmap(newWidth, newHeight);
                System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(thumbnail);

                // Resize, delete the old image, create the new one and add the properties
                graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphic.SmoothingMode = SmoothingMode.HighQuality;
                graphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphic.CompositingQuality = CompositingQuality.HighQuality;
                graphic.DrawImage(image, 0, 0, newWidth, newHeight);
                ImageCodecInfo[] info = ImageCodecInfo.GetImageEncoders();
                EncoderParameters encoderParameters;
                encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                File.Delete(file.FullName);
                memoryStream = file.OpenWrite();
                while (properties.Count > 0)
                    thumbnail.SetPropertyItem(properties.Pop());
                thumbnail.Save(memoryStream, info[1], encoderParameters);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (memoryStream != null)
                    memoryStream.Close();
            }

        }
        #endregion

        public TidyTinyManager()
        {
            bWorker.DoWork += new DoWorkEventHandler(bWorker_DoWork);
            bWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bWorker_RunWorkerCompleted);
            _Files = new ObservableCollection<FileInfo>();
            _Processed = 0;
            MaxEdgeSize = "2048";
            Quality = "90";
            OnPropertyChanged(o => o.Count);
        }

        void bWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            IsRunning = false;
            Processed = 0;
            CurrentImage = null;
            GC.Collect();
        }

        void bWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (CurrentImage != null)
            {
                CurrentImage = null;
                GC.Collect();
            }
            IsRunning = true;
            var newList = new ObservableCollection<FileInfo>();
            int fileNumber = 0;
            if (RenameFiles)
            {
                foreach (var f in Files)
                {
                    while (File.Exists(f.DirectoryName + @"\" + fileNumber + f.Extension))
                        fileNumber++;
                    f.MoveTo(f.DirectoryName + @"\" + fileNumber + f.Extension);
                }
            }
            for (int c = Files.Count - 1; c >= 0; c--)
            {
                if (!ResizeImage(Files[c], int.Parse(MaxEdgeSize), long.Parse(Quality)))
                    newList.Add(Files[c]);
                var img = new Bitmap(Files[c].FullName);
                var exifData = new Exifacto.ExifData(img);
                img.Dispose();
                img = null;
                if (exifData != null && exifData.DateTimeDigitized.HasValue)
                {
                    string newFilename = Files[c].DirectoryName + @"\" + exifData.DateTimeDigitized.Value.Year + "." +
                        ((exifData.DateTimeDigitized.Value.Month < 10) ? "0" + exifData.DateTimeDigitized.Value.Month : exifData.DateTimeDigitized.Value.Month.ToString()) + "." +
                        ((exifData.DateTimeDigitized.Value.Day < 10) ? "0" + exifData.DateTimeDigitized.Value.Day : exifData.DateTimeDigitized.Value.Day.ToString()) + "_" +
                        ((exifData.DateTimeDigitized.Value.Hour < 10) ? "0" + exifData.DateTimeDigitized.Value.Hour : exifData.DateTimeDigitized.Value.Hour.ToString()) + "h" +
                        ((exifData.DateTimeDigitized.Value.Minute < 10) ? "0" + exifData.DateTimeDigitized.Value.Minute : exifData.DateTimeDigitized.Value.Minute.ToString()) + Files[c].Extension;
                    int number = 0;
                    while (File.Exists(newFilename))
                    {
                        newFilename = Files[c].DirectoryName + @"\" + exifData.DateTimeDigitized.Value.Year + "." +
                        ((exifData.DateTimeDigitized.Value.Month < 10) ? "0" + exifData.DateTimeDigitized.Value.Month : exifData.DateTimeDigitized.Value.Month.ToString()) + "." +
                        ((exifData.DateTimeDigitized.Value.Day < 10) ? "0" + exifData.DateTimeDigitized.Value.Day : exifData.DateTimeDigitized.Value.Day.ToString()) + "_" +
                        ((exifData.DateTimeDigitized.Value.Hour < 10) ? "0" + exifData.DateTimeDigitized.Value.Hour : exifData.DateTimeDigitized.Value.Hour.ToString()) + "h" +
                        ((exifData.DateTimeDigitized.Value.Minute < 10) ? "0" + exifData.DateTimeDigitized.Value.Minute : exifData.DateTimeDigitized.Value.Minute.ToString()) + "_" + ++number + Files[c].Extension;
                    }
                    exifData = null;
                    if (RenameFiles)
                        Files[c].MoveTo(newFilename);
                }
                else
                    newList.Add(Files[c]);
                Dispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { CurrentImage = new BitmapImage(new Uri(Files[c].FullName)); }));
                Processed++;
            }
            Files = newList;
            CurrentImage = null;
        }

        public void Start()
        {
            if (IsRunning)
                bWorker.CancelAsync();
            else
                bWorker.RunWorkerAsync();
        }

        public void Reset()
        {
            Files.Clear();
            CurrentImage = null;
            OnPropertyChanged(o => o.Files);
            OnPropertyChanged(o => o.Count);
        }

        public void AddFile(FileInfo file)
        {
            if (file.Extension.ToLower() != ".jpg" && file.Extension.ToLower() != ".jpeg")
                return;
            foreach (FileInfo f in Files)
                if (f.FullName == file.FullName)
                    return;
            Files.Add(file);
            OnPropertyChanged(o => o.Files);
            OnPropertyChanged(o => o.Count);
        }
    }

    public static class SymbolExtensions
    {
        public static string GetPropertySymbol<T, R>(this T obj, Expression<Func<T, R>> expr)
        {
            return ((MemberExpression)expr.Body).Member.Name;
        }
    }
}
