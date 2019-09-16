using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;
using System.Linq;

namespace Extractor2
{
    /// <summary>
    /// Interação lógica para MainWindow.xam
    /// </summary>
    public partial class MainWindow : Window
    {
        private PXCMSenseManager sm;
        private Thread processingThread;

        private string output_folder = null;
        private string output_file = null;
        private string input_folder = null;
        List<string> dirs;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            processingThread.Abort();
            sm.Dispose();
        }

        private void ProcessingThread()
        {
            string nameColor, nameDepth, nameIr, file,folder;
            int width = 640;
            int height = 480;
            //int lostFrames = 0;
            int frameIndex = 0;
            int nframes = 0;
            PXCMImage color;
            PXCMImage depth;
            PXCMImage ir;
            PXCMImage.ImageData imageColor;
            PXCMImage.ImageData imageDepth;
            PXCMImage.ImageData imageIr;
            WriteableBitmap wbm1, wbm2, wbm3;

            foreach (var dir in dirs)
            {
                if (Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    List<string> fileList = new List<string>(Directory.GetFiles(dir, "*.rssdk"));
                    foreach (var input_file in fileList)
                    {
                        //lostFrames = 0;
                        // Create a SenseManager instance
                        sm = PXCMSenseManager.CreateInstance();
                        // Recording mode: true
                        // Playback mode: false
                        // Settings for playback mode (read rssdk files and extract frames)
                        sm.captureManager.SetFileName(input_file, false);
                        sm.captureManager.SetRealtime(false);

                        nframes = sm.captureManager.QueryNumberOfFrames();
                        // Select the color stream
                        sm.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, width, height, 0);
                        sm.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_DEPTH, width, height);
                        sm.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_IR, width, height);

                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            textBox3.Text = input_file;
                            textBox4.Text = nframes.ToString();
                        }));

                        sm.Init();

                        //pxcmStatus status = sm.AcquireFrame(true);
                        while (sm.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
                        {
                            // Retrieve the sample
                            PXCMCapture.Sample sample = sm.QuerySample();
                            // Work on the images
                            color = sample.color;
                            depth = sample.depth;
                            ir = sample.ir;

                            frameIndex = sm.captureManager.QueryFrameIndex();

                            color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out imageColor);
                            depth.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_DEPTH_RAW, out imageDepth);
                            ir.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB24, out imageIr);
                            //convert it to Bitmap
                            wbm1 = imageColor.ToWritableBitmap(0, color.info.width, color.info.height, 100.0, 100.0);
                            wbm2 = imageDepth.ToWritableBitmap(0, depth.info.width, depth.info.height, 100.0, 100.0);
                            wbm3 = imageIr.ToWritableBitmap(0, ir.info.width, ir.info.height, 100.0, 100.0);

                            //Update current frame
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                textBox5.Text = frameIndex.ToString();
                            }));

                            color.ReleaseAccess(imageColor);
                            depth.ReleaseAccess(imageDepth);
                            ir.ReleaseAccess(imageIr);
                            sm.ReleaseFrame();
                            //sm.Close();
                            //sm.Dispose();
                            file = Path.GetFileNameWithoutExtension(input_file);
                            folder = Path.GetFileName(Path.GetDirectoryName(input_file));
                            nameColor = file + "_color_" + frameIndex + ".png";
                            nameDepth = file + "_depth_" + frameIndex + ".png";
                            nameIr =    file + "_ir_" + frameIndex + ".png";
                            CreateThumbnail(folder, nameColor, wbm1);
                            CreateThumbnail(folder, nameDepth, wbm2);
                            CreateThumbnail(folder, nameIr, wbm3);
                        }
                        sm.Dispose();
                    }
                }
            }
        }
        
        void CreateThumbnail(string folderName, string filename, BitmapSource image)
        {
            string currentDir = output_folder + "\\" + folderName;
            Directory.CreateDirectory(currentDir);
            output_file = currentDir + "\\" + filename;

            if (filename != string.Empty)
            {
                using (FileStream stream = new FileStream(output_file, FileMode.Create))
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(stream);
                }
            }            
        }

        private void WriteSummary()
        {
            string filename = output_folder + "\\summary2.csv";
            string header = "Record" + ";" + "Video Index" + ";" + "Total Frames" + "\n";
            string summary = null;
            int nframes = 0;
            
            //PXCMSizeI32 sizeColor;
            // Recording mode: true, Playback mode: false
            // Settings for playback mode (read rssdk files and extract frames)
            File.WriteAllText(filename, header);
            
            foreach (var dir in dirs)
            {
                if (Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    List<string> fileList = new List<string>(Directory.GetFiles(dir, "*.rssdk"));
                    //fileList.OrderBy(item => int.Parse(item));
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        summaryLabel.Content = "Generating Summary...";
                        textBox3.Text = dir.Split('\\').Last();
                    }));
                    foreach (var input_file in fileList)
                    {
                        sm = PXCMSenseManager.CreateInstance();
                        sm.captureManager.SetFileName(input_file, false);
                        //sm.captureManager.SetRealtime(false);
                        nframes = sm.captureManager.QueryNumberOfFrames();
                        //sizeColor = sm.captureManager.QueryImageSize(PXCMCapture.StreamType.STREAM_TYPE_COLOR);
                        summary += dir + ";" + input_file + ";" + nframes + '\n';
                        sm.Dispose();                       
                    }
                    using (System.IO.StreamWriter fs = File.AppendText(filename))
                    {
                        fs.Write(summary);
                        summary = null;
                    }                    
                }
            }
            Dispatcher.BeginInvoke(new Action(() =>
            {
                summaryLabel.Content = "Summary Generated";
            }));
            
        }

        private void sourceButton_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog1 = new WinForms.FolderBrowserDialog();
            // Show the FolderBrowserDialog.
            WinForms.DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == WinForms.DialogResult.OK)
            {
                string folderName = folderBrowserDialog1.SelectedPath;
                textBox1.Text = folderName;
                input_folder = folderName;
                dirs = new List<string>(System.IO.Directory.EnumerateDirectories(folderName));
            }
        }

        private void OutputButton_Click(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog1 = new WinForms.FolderBrowserDialog();
            // Show the FolderBrowserDialog.
            WinForms.DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == WinForms.DialogResult.OK)
            {
                string folderName = folderBrowserDialog1.SelectedPath;
                textBox2.Text = folderName;
                output_folder = folderName;
            }
        }

        private void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            if (input_folder == null || output_folder == null)
            {
                string message = "Please, select the Source and Output directories!";
                string caption = "Missing root folder";
                WinForms.MessageBoxButtons buttons = WinForms.MessageBoxButtons.OK;
                WinForms.DialogResult result;
                // Displays the MessageBox.
                result = WinForms.MessageBox.Show(message, caption, buttons);
            }
            else
            {
                processingThread = new Thread(new ThreadStart(ProcessingThread));
                processingThread.Start();
            }
        }

        private void SummaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (input_folder == null || output_folder == null)
            {
                string message = "Please, select the Source and Output directories!";
                string caption = "Missing root folder";
                WinForms.MessageBoxButtons buttons = WinForms.MessageBoxButtons.OK;
                WinForms.DialogResult result;
                // Displays the MessageBox.
                result = WinForms.MessageBox.Show(message, caption, buttons);
            }
            else
            {
                processingThread = new Thread(new ThreadStart(WriteSummary));
                processingThread.Start();
            }
        }
    }
}