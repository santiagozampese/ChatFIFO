using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

public static class Program
{
    const string BaseDir = "/tmp/chat";

    private static readonly ConcurrentDictionary<int, CancellationTokenSource> ActiveClients = new();

    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo(string pathname, uint mode);
    public static async Task Main(string[] args)
    {
        Directory.CreateDirectory(BaseDir);
        Console.WriteLine($"[SERVER] Initialized. Monitoring '{BaseDir}' for new clients...");

        var discoveryTask = Task.Run(DiscoveryLoop);

        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.SetResult();
        };

        await tcs.Task;
        Console.WriteLine("\n[SERVER] Ending Server...");
    }

    private static async Task DiscoveryLoop()
    {
        while (true)
        {
            try
            {
                var dirs = Directory.GetDirectories(BaseDir, "proc_*");

                foreach (var dir in dirs)
                {
                    string folderName = Path.GetFileName(dir);
                    string pidStr = folderName.Replace("proc_", "");

                    if (int.TryParse(pidStr, out int pid))
                    {
                        if (!ActiveClients.ContainsKey(pid))
                        {
                            if (IsProcessAlive(pid))
                            {                    
                                RegisterNewClient(pid, dir);
                            }
                            else
                            {
                                TryCleanupDirectory(dir);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[SERVER] Invalid directory name: {folderName}");
                    }       
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER ERROR]: {ex.Message}");
            }

            await Task.Delay(1000);
        }
    }

    private static void RegisterNewClient(int pid, string clientDir)
    {
        var cts = new CancellationTokenSource();

        if (ActiveClients.TryAdd(pid, cts))
        {
            Console.WriteLine($"[SERVER] New client detected: PID {pid}");
            Task.Run(() => ListenToClientAsync(pid, clientDir, cts.Token));
        }
    }

    private static async Task ListenToClientAsync(int pid, string clientDir, CancellationToken cancellationToken)
    {
        string outFifoPath = Path.Combine(clientDir, "out.fifo");

        while (!cancellationToken.IsCancellationRequested && File.Exists(outFifoPath))
        {
            try
            {
                using var fileStream = new FileStream(outFifoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
                using var reader = new StreamReader(fileStream, Encoding.UTF8);

                string? message;

                while ((message = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(message)) continue;

                    string formattedMsg = $"{pid}: {message}";
                    Console.WriteLine($"[BROADCAST] {formattedMsg}");

                    await BroadcastMessageAsync(senderPid: pid, message: formattedMsg);
                }
            }
            catch (Exception)
            {
                if (!IsProcessAlive(pid))
                {
                    Console.WriteLine($"[SERVER] Client PID {pid} disconnected");
                    UnregisterClient(pid, clientDir);
                    break;            
                }

                await Task.Delay(500, cancellationToken);
            }
        }
    }

    private static async Task BroadcastMessageAsync(int senderPid, string message)
    {
        var targetPids = ActiveClients.Keys.Where(pid => pid != senderPid).ToList();

        foreach (var targetPid in targetPids)
        {
            string targetInFifo = Path.Combine(BaseDir, $"proc_{targetPid}", "in.fifo");

            if (File.Exists(targetInFifo))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var stream = new FileStream(targetInFifo, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 4096, true);
                        using var writer = new StreamWriter(stream, Encoding.UTF8) {AutoFlush = true};
                        await writer.WriteLineAsync(message);
                    }
                    catch
                    {
                        
                    }
                });
            }
        }
    }

    private static void UnregisterClient(int pid, string clientDir)
    {
        if (ActiveClients.TryRemove(pid, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        TryCleanupDirectory(clientDir);
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            Process.GetProcessById(pid);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryCleanupDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch
        {
            
        }
    }
}