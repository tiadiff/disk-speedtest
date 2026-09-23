# DiskTester

A simple C# console application to test the read and write speeds of SSDs, HDDs, or USB drives.

## Features
- Test sequential write speed
- Test sequential read speed
- Customizable test file size
- Uses unbuffered writes (WriteThrough) and sequential scan optimization for reads
- Cleans up the test file automatically after the test

## Usage
Run the compiled executable and follow the prompts:

1. **Path:** Enter the drive letter (e.g., `C:\`) or a folder path where you want to perform the test. Ensure you have write permissions to this location.
2. **Size:** Enter the size of the test file in Megabytes (MB). A larger size (e.g., 1024 for 1GB or more) will provide more accurate results by minimizing the effect of caching.

Example:
```
Disk Speed Tester
=================
Enter the path to test (e.g., C:\, D:\, or a folder path): D:\
Enter test file size in MB (e.g., 1024 for 1GB): 1024

Testing Write Speed... (1024 MB)
Write Speed: 450.00 MB/s (Time: 2.28 s)

Testing Read Speed... (1024 MB)
Read Speed: 520.00 MB/s (Time: 1.97 s)

Cleaning up test file...

Press any key to exit.
```

## Build
Compile the project in Release mode using the .NET CLI:
```bash
dotnet build -c Release
```
