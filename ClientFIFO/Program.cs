using System.Runtime.InteropServices;
using System.Text;

public static class Program
{
    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo(string pathname, uint mode);
    public static int myPid = 0;
    public static string baseDir = "";
    public static string inFifo = "";
    public static string outFifo = "";

    public static List<string> messages = new();

    public static async Task Main(string[] args)
    {
        myPid = Environment.ProcessId;
        baseDir = $"/tmp/chat/proc_{myPid}";

        Directory.CreateDirectory(baseDir);

        inFifo = Path.Combine(baseDir, "in.fifo");
        outFifo = Path.Combine(baseDir, "out.fifo");

        mkfifo(inFifo, 438);
        mkfifo(outFifo, 438);

        Console.WriteLine($"Process initialized with PID: {myPid}");
        Console.WriteLine($"Listening in {inFifo}");

        var tcs = new TaskCompletionSource();

        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
            tcs.SetResult();
        };
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
            tcs.SetResult();
            e.Cancel = false;
        };

        _ = Task.Run(() => Read());
        _ = Task.Run(() => Write());

        await tcs.Task;
    }

    public static void ShowMessages()
    {
        Console.Clear();
        foreach (var message in messages)
        {
            Console.WriteLine(message);
        }
        Console.Write($"Me ({myPid}): {CustomRead.stringBuilder.ToString()}");
    }

    public static async Task Read()
    {
        while (true)
        {
            var msg = await File.ReadAllTextAsync(inFifo);
            messages.Add(msg.Trim());
            ShowMessages();
        }
    }

    public static async Task Write()
    {
        Console.Write($"Me ({myPid}): ");
        while (true)
        {
            var msg = CustomRead.CustomReadLine($"Me ({myPid}): "); 
            messages.Add($"Me ({myPid}): {msg}");
            await File.WriteAllTextAsync(outFifo, msg);
            ShowMessages();
        }
    }
}