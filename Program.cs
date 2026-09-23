using System;
using System.Diagnostics;
using System.IO;

namespace DiskTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Disk Speed Tester");
            Console.WriteLine("=================");
            
            Console.Write("Enter the path to test (e.g., C:\\, D:\\, or a folder path): ");
            string targetPath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
            {
                Console.WriteLine("Invalid path. Exiting.");
                return;
            }

            Console.Write("Enter test file size in MB (e.g., 1024 for 1GB): ");
            if (!int.TryParse(Console.ReadLine(), out int sizeMb) || sizeMb <= 0)
            {
                Console.WriteLine("Invalid size. Exiting.");
                return;
            }

            long fileSizeBytes = (long)sizeMb * 1024 * 1024;
            int bufferSize = 1024 * 1024; // 1 MB buffer
            string testFilePath = Path.Combine(targetPath, "disk_speed_test.tmp");

            try
            {
                byte[] buffer = new byte[bufferSize];
                // Fill with random data to prevent SSD compression from skewing results
                new Random().NextBytes(buffer);

                Console.WriteLine($"\nTesting Write Speed... ({sizeMb} MB)");
                Stopwatch sw = Stopwatch.StartNew();
                
                // WriteThrough bypasses the OS cache for writes
                using (FileStream fs = new FileStream(testFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough))
                {
                    long bytesWritten = 0;
                    while (bytesWritten < fileSizeBytes)
                    {
                        int toWrite = (int)Math.Min(bufferSize, fileSizeBytes - bytesWritten);
                        fs.Write(buffer, 0, toWrite);
                        bytesWritten += toWrite;
                    }
                }
                
                sw.Stop();
                double writeSpeed = (sizeMb / sw.Elapsed.TotalSeconds);
                Console.WriteLine($"Write Speed: {writeSpeed:F2} MB/s (Time: {sw.Elapsed.TotalSeconds:F2} s)");

                Console.WriteLine($"\nTesting Read Speed... ({sizeMb} MB)");
                sw.Restart();

                // SequentialScan optimizes cache behavior for sequential reads
                using (FileStream fs = new FileStream(testFilePath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, FileOptions.SequentialScan))
                {
                    long bytesReadTotal = 0;
                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        bytesReadTotal += bytesRead;
                    }
                }

                sw.Stop();
                double readSpeed = (sizeMb / sw.Elapsed.TotalSeconds);
                Console.WriteLine($"Read Speed: {readSpeed:F2} MB/s (Time: {sw.Elapsed.TotalSeconds:F2} s)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
            }
            finally
            {
                if (File.Exists(testFilePath))
                {
                    Console.WriteLine("\nCleaning up test file...");
                    File.Delete(testFilePath);
                }
            }

            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey();
        }
    }
}
