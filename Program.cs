// Grok-generated
//
// My Prompt:
// C# console multi thread program to log to C:\Temp\time.txt:
// - main thread (each 10 sec) log DateTime of getting last correct answer from NTP-server;
// - auxiliary thread (each 60 sec) request (time.windows.com) and log current UTC DateTime (or error message).
// Including restart of auxiliary thread in case of crash (log the crash to log)

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static readonly string LogFilePath = @"C:\Temp\time.txt";
    private static DateTime? LastNtpTime = null;
    private static readonly object FileLock = new object();

    static async Task Main(string[] args)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));

        // Start auxiliary thread for NTP requests
        _ = Task.Run(() => ManageNtpThread());

        // Main thread: log last NTP time every 10 seconds
        while (true)
        {
            LogMessage($"Main thread: Last NTP time: {(LastNtpTime.HasValue ? LastNtpTime.Value.ToString("u") : "None")}");
            await Task.Delay(10000);
        }
    }

    static void ManageNtpThread()
    {
        while (true)
        {
            try
            {
                Thread ntpThread = new Thread(NtpRequestLoop)
                {
                    IsBackground = true
                };
                ntpThread.Start();
                ntpThread.Join(); // Wait for thread to exit
            }
            catch (Exception ex)
            {
                LogMessage($"NTP thread crashed: {ex.Message}");
            }
            Thread.Sleep(5000); // Wait before restarting
        }
    }

    static void NtpRequestLoop()
    {
        while (true)
        {
            try
            {
                DateTime ntpTime = GetNtpTime("time.windows.com");
                LastNtpTime = ntpTime;
                LogMessage($"NTP thread: UTC time from server: {ntpTime:u}");
            }
            catch (Exception ex)
            {
                LogMessage($"NTP thread error: {ex.Message}");
            }
            Thread.Sleep(60000); // Wait 60 seconds
        }
    }

    static DateTime GetNtpTime(string ntpServer)
    {
        byte[] ntpData = new byte[48];
        ntpData[0] = 0x1B; // NTP request header

        using (var client = new UdpClient(ntpServer, 123))
        {
            client.Send(ntpData, ntpData.Length);
            // ntpData = client.Receive(ref client.Client.RemoteEndPoint);
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0); // Initialize endpoint
            ntpData = client.Receive(ref remoteEndPoint); // Pass the IPEndPoint

            // Parse NTP response (seconds since 1900)
            ulong intPart = BitConverter.ToUInt32(ntpData, 40);
            intPart = (uint)((intPart >> 24) | ((intPart >> 8) & 0xFF00) | ((intPart << 8) & 0xFF0000) | (intPart << 24));
            double seconds = intPart;

            // Convert to DateTime
            DateTime ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return ntpEpoch.AddSeconds(seconds);
        }
    }

    static void LogMessage(string message)
    {
        lock (FileLock)
        {
            string logEntry = $"{DateTime.UtcNow:u}: {message}{Environment.NewLine}";
            File.AppendAllText(LogFilePath, logEntry);
        }
    }
}