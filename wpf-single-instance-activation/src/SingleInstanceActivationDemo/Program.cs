using System.IO.Pipes;
using System.Text;

const string MutexName = "Demo.SingleInstanceActivation";
const string PipeName = "Demo.SingleInstanceActivation.Pipe";

string queueDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SingleInstanceActivationDemo",
    "activation-queue");

using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirstInstance);

if (!isFirstInstance)
{
    string payload = args.Length > 0 ? string.Join("|", args) : "ACTIVATE";

    if (!await TrySendThroughPipeAsync(payload))
    {
        EnqueueActivationRequest(queueDirectory, payload);
    }

    Console.WriteLine("Activation request forwarded.");
    return;
}

Directory.CreateDirectory(queueDirectory);
using var watcher = StartQueueWatcher(queueDirectory, RouteActivationMessage);

_ = Task.Run(() => RunPipeServerAsync(RouteActivationMessage));

Console.WriteLine("Main instance is running.");
Console.WriteLine("Start another process with an argument to forward it here.");
Console.WriteLine("Press Enter to exit.");
Console.ReadLine();

static async Task<bool> TrySendThroughPipeAsync(string payload)
{
    try
    {
        await using var pipeClient = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await pipeClient.ConnectAsync(timeout.Token);

        await using var writer = new StreamWriter(pipeClient, Encoding.UTF8);
        await writer.WriteLineAsync(payload);
        await writer.FlushAsync();
        return true;
    }
    catch
    {
        return false;
    }
}

static async Task RunPipeServerAsync(Action<string> route)
{
    while (true)
    {
        await using var server = new NamedPipeServerStream(
            PipeName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        await server.WaitForConnectionAsync();

        using var reader = new StreamReader(server, Encoding.UTF8);
        string? payload = await reader.ReadLineAsync();

        if (!string.IsNullOrWhiteSpace(payload))
        {
            route(payload);
        }
    }
}

static void EnqueueActivationRequest(string queueDirectory, string payload)
{
    Directory.CreateDirectory(queueDirectory);

    string id = $"{DateTime.UtcNow:yyyyMMddHHmmssfffffff}_{Environment.ProcessId}_{Guid.NewGuid():N}";
    string tempFile = Path.Combine(queueDirectory, $"{id}.tmp");
    string requestFile = Path.Combine(queueDirectory, $"{id}.request");

    File.WriteAllText(tempFile, payload, Encoding.UTF8);
    File.Move(tempFile, requestFile);
}

static FileSystemWatcher StartQueueWatcher(string queueDirectory, Action<string> route)
{
    var watcher = new FileSystemWatcher(queueDirectory, "*.request")
    {
        EnableRaisingEvents = true,
    };

    watcher.Created += (_, eventArgs) =>
    {
        string payload = File.ReadAllText(eventArgs.FullPath, Encoding.UTF8);
        route(payload);
        File.Delete(eventArgs.FullPath);
    };

    return watcher;
}

static void RouteActivationMessage(string payload)
{
    if (payload == "ACTIVATE")
    {
        Console.WriteLine("Bring main window to front.");
        return;
    }

    Console.WriteLine($"Handle activation payload: {payload}");
}
