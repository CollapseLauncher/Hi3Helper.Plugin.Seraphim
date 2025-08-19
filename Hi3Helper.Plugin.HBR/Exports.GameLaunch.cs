using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Management;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Plugin.HBR;

public partial class Seraphim
{
    /// <inheritdoc/>
    public override async Task<bool> LaunchGameFromGameManagerCoreAsync(GameManagerExtension.RunGameFromGameManagerContext context, string? startArgument, bool isRunBoosted, ProcessPriorityClass processPriority, CancellationToken token)
    {
        if (!TryGetGameProcessFromContext(context, startArgument, out Process? process))
        {
            return false;
        }

        using (process)
        {
            process.Start();
            process.PriorityBoostEnabled = isRunBoosted;
            process.PriorityClass        = processPriority;

            CancellationTokenSource gameLogReaderCts = new CancellationTokenSource();
            CancellationTokenSource coopCts          = CancellationTokenSource.CreateLinkedTokenSource(token, gameLogReaderCts.Token);

            // Run game log reader (Create a new thread)
            _ = ReadGameLog(context, coopCts.Token);

            // ReSharper disable once PossiblyMistakenUseOfCancellationToken
            await process.WaitForExitAsync(token);
            await gameLogReaderCts.CancelAsync();

            return true;
        }
    }

    /// <inheritdoc/>
    public override bool IsGameRunningCore(GameManagerExtension.RunGameFromGameManagerContext context, out bool isGameRunning)
    {
        isGameRunning = false;
        if (!TryGetGameExecutablePath(context, out string? gameExecutablePath))
        {
            return false;
        }

        using Process? process = FindExecutableProcess(gameExecutablePath);
        isGameRunning = process != null;

        return true;
    }

    /// <inheritdoc/>
    public override async Task<bool> WaitRunningGameCoreAsync(GameManagerExtension.RunGameFromGameManagerContext context, CancellationToken token)
    {
        if (!TryGetGameExecutablePath(context, out string? gameExecutablePath))
        {
            return false;
        }

        using Process? process = FindExecutableProcess(gameExecutablePath);
        if (process == null)
        {
            return true;
        }

        await process.WaitForExitAsync(token);
        return true;
    }

    /// <inheritdoc/>
    public override bool KillRunningGameCore(GameManagerExtension.RunGameFromGameManagerContext context, out bool wasGameRunning)
    {
        wasGameRunning = false;
        if (!TryGetGameExecutablePath(context, out string? gameExecutablePath))
        {
            return false;
        }

        using Process? process = FindExecutableProcess(gameExecutablePath);
        if (process == null)
        {
            return true;
        }

        wasGameRunning = true;
        process.Kill();
        return true;
    }

    private static Process? FindExecutableProcess(string executablePath)
    {
        ReadOnlySpan<char> executableDirPath = Path.GetDirectoryName(executablePath.AsSpan());
        string             executableName    = Path.GetFileNameWithoutExtension(executablePath);

        Process[] processes     = Process.GetProcessesByName(executableName);
        Process?  returnProcess = null;

        foreach (Process process in processes)
        {
            if (process.MainModule?.FileName.StartsWith(executableDirPath, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                returnProcess = process;
                break;
            }
        }

        try
        {
            return returnProcess;
        }
        finally
        {
            foreach (var process in processes.Where(x => x != returnProcess))
            {
                process.Dispose();
            }
        }
    }

    private static bool TryGetGameExecutablePath(GameManagerExtension.RunGameFromGameManagerContext context, [NotNullWhen(true)] out string? gameExecutablePath)
    {
        gameExecutablePath = null;
        if (context is not { GameManager: HBRGameManager hbrGameManager, PresetConfig: PluginPresetConfigBase presetConfig })
        {
            return false;
        }

        hbrGameManager.GetGamePath(out string? gamePath);
        presetConfig.comGet_GameExecutableName(out string executablePath);

        gamePath?.NormalizePathInplace();
        executablePath.NormalizePathInplace();

        if (string.IsNullOrEmpty(gamePath))
        {
            return false;
        }

        gameExecutablePath = Path.Combine(gamePath, executablePath);
        return File.Exists(gameExecutablePath);
    }

    private static bool TryGetGameProcessFromContext(GameManagerExtension.RunGameFromGameManagerContext context, string? startArgument, [NotNullWhen(true)] out Process? process)
    {
        process = null;
        if (!TryGetGameExecutablePath(context, out string? gameExecutablePath))
        {
            return false;
        }

        ProcessStartInfo startInfo = string.IsNullOrEmpty(startArgument) ?
            new ProcessStartInfo(gameExecutablePath) :
            new ProcessStartInfo(gameExecutablePath, startArgument);

        process = new Process
        {
            StartInfo = startInfo
        };
        return true;
    }

    private static async Task ReadGameLog(GameManagerExtension.RunGameFromGameManagerContext context, CancellationToken token)
    {
        if (context is not { PresetConfig: PluginPresetConfigBase presetConfig })
        {
            return;
        }

        presetConfig.comGet_GameAppDataPath(out string gameAppDataPath);
        presetConfig.comGet_GameLogFileName(out string gameLogFileName);

        if (string.IsNullOrEmpty(gameAppDataPath) ||
            string.IsNullOrEmpty(gameLogFileName))
        {
            return;
        }

        string gameLogPath = Path.Combine(gameAppDataPath, gameLogFileName);

        int retry = 5;
        while (!File.Exists(gameLogPath) && retry >= 0)
        {
            // Delays for 5 seconds to wait the game log existence
            await Task.Delay(1000, token);
            --retry;
        }

        if (retry <= 0)
        {
            return;
        }

        var printCallback = context.PrintGameLogCallback;

        await using FileStream fileStream = File.Open(gameLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new StreamReader(fileStream);

        while (!token.IsCancellationRequested)
        {
            while (await reader.ReadLineAsync(token) is { } line)
            {
                PassStringLineToCallback(printCallback, line);
            }

            await Task.Delay(250, token);
        }

        return;

        static unsafe void PassStringLineToCallback(GameManagerExtension.PrintGameLog? invoke, string line)
        {
            char* lineP   = line.GetPinnableStringPointer();
            int   lineLen = line.Length;

            invoke?.Invoke(lineP, lineLen, 0);
        }
    }
}
