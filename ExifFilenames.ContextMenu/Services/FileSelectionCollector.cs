using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExifFilenames.ContextMenu.Services;

/// <summary>
/// Windows Verkenner start voor elk geselecteerd bestand een apart proces.
/// Deze klasse bundelt die aanroepen: het eerste proces wordt de "verzamelaar" en
/// ontvangt via een named pipe de paden van de andere processen, die daarna direct afsluiten.
/// </summary>
public static class FileSelectionCollector
{
    /// <summary>Hoe lang de verzamelaar minimaal wacht op andere processen.</summary>
    private static readonly TimeSpan InitialWait = TimeSpan.FromMilliseconds(1200);

    /// <summary>Hoe lang er na het laatst ontvangen pad nog gewacht wordt.</summary>
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(600);

    private static readonly TimeSpan ClientConnectTimeout = TimeSpan.FromSeconds(3);

    private static string InstanceName =>
        $"ExifFilenames.ContextMenu.Files.{Environment.UserName}.{Process.GetCurrentProcess().SessionId}";

    /// <summary>
    /// Geeft alle paden van deze selectie terug als dit proces de verzamelaar is,
    /// of <c>null</c> als de paden zijn doorgegeven aan een ander proces (dit proces moet dan afsluiten).
    /// Moet op de UI-thread worden aangeroepen (de mutex is thread-gebonden).
    /// </summary>
    public static async Task<IReadOnlyList<string>?> CollectAsync(IReadOnlyList<string> ownPaths)
    {
        using var mutex = new Mutex(initiallyOwned: true, $@"Local\{InstanceName}", out bool isCollector);

        if (!isCollector)
        {
            if (await TrySendToCollectorAsync(ownPaths))
            {
                return null;
            }

            // De verzamelaar is al klaar met verzamelen: verwerk deze bestanden zelfstandig.
            return ownPaths;
        }

        try
        {
            var paths = new List<string>(ownPaths);
            await ReceiveFromOtherInstancesAsync(paths);
            return paths;
        }
        finally
        {
            // Vrijgeven zodat een latere, losse selectie een nieuwe verzamelaar kan starten.
            mutex.ReleaseMutex();
        }
    }

    private static async Task<bool> TrySendToCollectorAsync(IReadOnlyList<string> paths)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", InstanceName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync((int)ClientConnectTimeout.TotalMilliseconds);

            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            foreach (var path in paths)
            {
                await writer.WriteLineAsync(path);
            }
            await writer.FlushAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task ReceiveFromOtherInstancesAsync(List<string> paths)
    {
        var elapsed = Stopwatch.StartNew();
        using var quietCts = new CancellationTokenSource(InitialWait);

        while (!quietCts.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                InstanceName,
                PipeDirection.In,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(quietCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        paths.Add(line);
                    }
                }
            }
            catch (IOException)
            {
                // Afgebroken verbinding: negeer dit ene proces.
            }

            // Verleng het wachten, maar nooit korter dan de initiële wachttijd.
            var remainingInitial = InitialWait - elapsed.Elapsed;
            quietCts.CancelAfter(remainingInitial > QuietPeriod ? remainingInitial : QuietPeriod);
        }
    }
}
