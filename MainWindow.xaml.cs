using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DiskTester
{
    public class TestResult
    {
        public string Date { get; set; }
        public string Drive { get; set; }
        public int SizeMB { get; set; }
        public string WriteSpeed { get; set; }
        public string ReadSpeed { get; set; }
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<TestResult> ResultsHistory { get; set; }
        private CancellationTokenSource? _cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            
            ResultsHistory = new ObservableCollection<TestResult>();
            ResultsDataGrid.ItemsSource = ResultsHistory;

            LoadDrives();
            LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string csvFile = Path.Combine(desktopPath, "DiskSpeedResults.csv");
                if (File.Exists(csvFile))
                {
                    var lines = File.ReadAllLines(csvFile);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var parts = lines[i].Split(',');
                        if (parts.Length == 5)
                        {
                            ResultsHistory.Insert(0, new TestResult
                            {
                                Date = parts[0],
                                Drive = parts[1],
                                SizeMB = int.TryParse(parts[2], out int s) ? s : 0,
                                WriteSpeed = parts[3] + " MB/s",
                                ReadSpeed = parts[4] + " MB/s"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load history: {ex.Message}");
            }
        }

        private void LoadDrives()
        {
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                DriveComboBox.ItemsSource = drives.Select(d => new 
                { 
                    DisplayName = $"{d.Name} ({d.DriveFormat}) - {(d.TotalSize / (1024*1024*1024))} GB", 
                    Path = d.Name 
                }).ToList();
                
                DriveComboBox.DisplayMemberPath = "DisplayName";
                DriveComboBox.SelectedValuePath = "Path";
                
                if (DriveComboBox.Items.Count > 0)
                    DriveComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load drives: {ex.Message}");
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (DriveComboBox.SelectedValue == null)
            {
                MessageBox.Show("Please select a valid drive.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string targetPath = DriveComboBox.SelectedValue.ToString();

            if (!int.TryParse(FileSizeTextBox.Text, out int sizeMb) || sizeMb <= 0)
            {
                MessageBox.Show("Invalid file size. Please enter a positive integer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StartButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            DriveComboBox.IsEnabled = false;
            FileSizeTextBox.IsEnabled = false;
            WriteSpeedTextBlock.Text = "Testing...";
            ReadSpeedTextBlock.Text = "--- MB/s";
            
            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                await RunBenchmarkAsync(targetPath, sizeMb, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                StatusTextBlock.Text = "Test Cancelled.";
                TestProgressBar.Value = 0;
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
                CancelButton.IsEnabled = false;
                DriveComboBox.IsEnabled = true;
                FileSizeTextBox.IsEnabled = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            CancelButton.IsEnabled = false;
            StatusTextBlock.Text = "Cancelling...";
        }

        private async Task RunBenchmarkAsync(string targetPath, int sizeMb, CancellationToken token)
        {
            long fileSizeBytes = (long)sizeMb * 1024 * 1024;
            int bufferSize = 1024 * 1024; // 1 MB buffer
            string testFilePath = Path.Combine(targetPath, "disk_speed_test_gui.tmp");
            
            double totalWriteSpeed = 0;
            double totalReadSpeed = 0;
            int passes = 5;

            try
            {
                byte[] buffer = new byte[bufferSize];
                new Random().NextBytes(buffer);

                for (int pass = 1; pass <= passes; pass++)
                {
                    // WRITE TEST
                    StatusTextBlock.Text = $"Testing Write Speed (Pass {pass}/{passes})...";
                    TestProgressBar.Value = 0;
                    
                    Stopwatch sw = Stopwatch.StartNew();
                    
                    await Task.Run(() =>
                    {
                        using (FileStream fs = new FileStream(testFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough))
                        {
                            long bytesWritten = 0;
                            while (bytesWritten < fileSizeBytes)
                            {
                                token.ThrowIfCancellationRequested();
                                int toWrite = (int)Math.Min(bufferSize, fileSizeBytes - bytesWritten);
                                fs.Write(buffer, 0, toWrite);
                                bytesWritten += toWrite;
                                
                                // Update progress
                                int progress = (int)((bytesWritten * 100) / fileSizeBytes);
                                Dispatcher.Invoke(() => TestProgressBar.Value = progress);
                            }
                        }
                    }, token);

                    sw.Stop();
                    double writeSpeed = sizeMb / sw.Elapsed.TotalSeconds;
                    totalWriteSpeed += writeSpeed;
                    WriteSpeedTextBlock.Text = $"{(totalWriteSpeed / pass):F2} MB/s";

                    // READ TEST
                    StatusTextBlock.Text = $"Testing Read Speed (Pass {pass}/{passes})...";
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
                                token.ThrowIfCancellationRequested();
                                bytesReadTotal += bytesRead;
                                
                                // Update progress
                                int progress = (int)((bytesReadTotal * 100) / fileSizeBytes);
                                Dispatcher.Invoke(() => TestProgressBar.Value = progress);
                            }
                        }
                    }, token);

                    sw.Stop();
                    double readSpeed = sizeMb / sw.Elapsed.TotalSeconds;
                    totalReadSpeed += readSpeed;
                    ReadSpeedTextBlock.Text = $"{(totalReadSpeed / pass):F2} MB/s";
                    
                    // Delete the file between passes to ensure fresh allocation and prevent cache hits
                    await Task.Run(() => 
                    {
                        try { File.Delete(testFilePath); } catch { }
                    });
                }

                double avgWriteSpeed = totalWriteSpeed / passes;
                double avgReadSpeed = totalReadSpeed / passes;

                WriteSpeedTextBlock.Text = $"{avgWriteSpeed:F2} MB/s";
                ReadSpeedTextBlock.Text = $"{avgReadSpeed:F2} MB/s";

                StatusTextBlock.Text = "Test Completed (Averaged over 5 passes)";
                TestProgressBar.Value = 100;

                // Log Result
                LogResult(targetPath, sizeMb, avgWriteSpeed, avgReadSpeed);
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

        private void LogResult(string drive, int sizeMb, double writeSpeed, double readSpeed)
        {
            var result = new TestResult
            {
                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Drive = drive,
                SizeMB = sizeMb,
                WriteSpeed = $"{writeSpeed:F2} MB/s",
                ReadSpeed = $"{readSpeed:F2} MB/s"
            };

            // Update UI
            ResultsHistory.Insert(0, result);

            // Save to CSV on Desktop
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string csvFile = Path.Combine(desktopPath, "DiskSpeedResults.csv");
                
                bool fileExists = File.Exists(csvFile);
                using (StreamWriter sw = new StreamWriter(csvFile, true))
                {
                    if (!fileExists)
                    {
                        sw.WriteLine("Date,Drive,Size(MB),Write Speed(MB/s),Read Speed(MB/s)");
                    }
                    sw.WriteLine($"{result.Date},{result.Drive},{result.SizeMB},{writeSpeed:F2},{readSpeed:F2}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save results to CSV: {ex.Message}");
            }
        }
    }
}
