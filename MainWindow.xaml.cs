using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace DiskTester
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            TargetPathTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            string targetPath = TargetPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
            {
                MessageBox.Show("Invalid target path. Please enter a valid drive or folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(FileSizeTextBox.Text, out int sizeMb) || sizeMb <= 0)
            {
                MessageBox.Show("Invalid file size. Please enter a positive integer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StartButton.IsEnabled = false;
            TargetPathTextBox.IsEnabled = false;
            FileSizeTextBox.IsEnabled = false;
            WriteSpeedTextBlock.Text = "Testing...";
            ReadSpeedTextBlock.Text = "--- MB/s";
            
            try
            {
                await RunBenchmarkAsync(targetPath, sizeMb);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during the test:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Error occurred.";
                TestProgressBar.Value = 0;
            }
            finally
            {
                StartButton.IsEnabled = true;
                TargetPathTextBox.IsEnabled = true;
                FileSizeTextBox.IsEnabled = true;
            }
        }

        private async Task RunBenchmarkAsync(string targetPath, int sizeMb)
        {
            long fileSizeBytes = (long)sizeMb * 1024 * 1024;
            int bufferSize = 1024 * 1024; // 1 MB buffer
            string testFilePath = Path.Combine(targetPath, "disk_speed_test_gui.tmp");
            
            try
            {
                byte[] buffer = new byte[bufferSize];
                new Random().NextBytes(buffer);

                // WRITE TEST
                StatusTextBlock.Text = "Testing Write Speed...";
                TestProgressBar.Value = 0;
                
                Stopwatch sw = Stopwatch.StartNew();
                
                await Task.Run(() =>
                {
                    using (FileStream fs = new FileStream(testFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough))
                    {
                        long bytesWritten = 0;
                        while (bytesWritten < fileSizeBytes)
                        {
                            int toWrite = (int)Math.Min(bufferSize, fileSizeBytes - bytesWritten);
                            fs.Write(buffer, 0, toWrite);
                            bytesWritten += toWrite;
                            
                            // Update progress
                            int progress = (int)((bytesWritten * 100) / fileSizeBytes);
                            Dispatcher.Invoke(() => TestProgressBar.Value = progress);
                        }
                    }
                });

                sw.Stop();
                double writeSpeed = sizeMb / sw.Elapsed.TotalSeconds;
                WriteSpeedTextBlock.Text = $"{writeSpeed:F2} MB/s";

                // READ TEST
                StatusTextBlock.Text = "Testing Read Speed...";
                TestProgressBar.Value = 0;
                sw.Restart();

                await Task.Run(() =>
                {
                    using (FileStream fs = new FileStream(testFilePath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, FileOptions.SequentialScan))
                    {
                        long bytesReadTotal = 0;
                        int bytesRead;
                        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            bytesReadTotal += bytesRead;
                            
                            // Update progress
                            int progress = (int)((bytesReadTotal * 100) / fileSizeBytes);
                            Dispatcher.Invoke(() => TestProgressBar.Value = progress);
                        }
                    }
                });

                sw.Stop();
                double readSpeed = sizeMb / sw.Elapsed.TotalSeconds;
                ReadSpeedTextBlock.Text = $"{readSpeed:F2} MB/s";

                StatusTextBlock.Text = "Test Completed";
                TestProgressBar.Value = 100;
            }
            finally
            {
                // CLEANUP
                if (File.Exists(testFilePath))
                {
                    StatusTextBlock.Text = "Cleaning up...";
                    await Task.Run(() => 
                    {
                        try { File.Delete(testFilePath); } catch { /* Ignore if it fails */ }
                    });
                    if (StatusTextBlock.Text == "Cleaning up...") 
                    {
                        StatusTextBlock.Text = "Ready";
                        TestProgressBar.Value = 0;
                    }
                }
            }
        }
    }
}
