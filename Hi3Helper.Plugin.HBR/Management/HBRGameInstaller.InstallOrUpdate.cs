using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Management.Api;
using Hi3Helper.Plugin.HBR.Utility;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Plugin.HBR.Management;

// ReSharper disable once InconsistentNaming
public partial class HBRGameInstaller
{
    private async ValueTask StartInstallAsyncInner(
        string gamePath,
        InstallProgress installProgress,
        List<GameInstallAsset> assets,
        string assetRootSuffix,
        InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate,
        bool isDownloadThroughMode = false,
        CancellationToken token = default)
    {
        if (assets.Count == 0)
        {
            return;
        }

        installProgress.DownloadedCount = 0;
        installProgress.TotalCountToDownload = assets.Count;
        installProgress.DownloadedBytes = 0;
        installProgress.TotalBytesToDownload = assets.Sum(x => x.AssetSize);

        string baseUrl = GameAssetBaseUrl.CombineUrlFromString(assetRootSuffix);
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new NullReferenceException("Base URL cannot be retrieved as it's null-ed!");
        }

        // Perform write execution
        await Parallel.ForEachAsync(assets, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken      = token
        }, Impl);

        return;

        async ValueTask Impl(GameInstallAsset asset, CancellationToken innerToken)
        {
            if (string.IsNullOrEmpty(asset.AssetPath))
            {
                throw new NullReferenceException("AssetPath is null!");
            }

            string   filePath = Path.Combine(gamePath, asset.AssetPath.TrimStart("/\\").ToString());
            FileInfo fileInfo = new FileInfo(filePath);
            fileInfo.Directory?.Create();

            if (fileInfo.Exists)
            {
                fileInfo.IsReadOnly = false;
            }

#if DEBUG
            if (!fileInfo.Exists)
            {
                SharedStatic.InstanceLogger?.LogTrace("File: {FilePath} doesn't exist! Creating new...", fileInfo.FullName);
            }
#endif

            await using FileStream fileStream = fileInfo.Open(
                isDownloadThroughMode ? FileMode.Create : FileMode.OpenOrCreate,
                isDownloadThroughMode ? FileAccess.Write : FileAccess.ReadWrite,
                isDownloadThroughMode ? FileShare.Write : FileShare.ReadWrite);
            Interlocked.Increment(ref installProgress.DownloadedCount);
            progressDelegate?.Invoke(in installProgress);
            progressStateDelegate?.Invoke(InstallProgressState.Download);

            if (!isDownloadThroughMode)
            {
                if (fileInfo.Exists &&
                    fileInfo.Length == asset.AssetSize &&
                    await IsHashMatchedAsync(
                        fileStream,
                        asset,
                        read =>
                        {
                            Interlocked.Add(ref installProgress.DownloadedBytes, read);
                            progressDelegate?.Invoke(in installProgress);
                        },
                        innerToken))
                {
                    SharedStatic.InstanceLogger?.LogInformation("Download for file: {FilePath} is completed!", fileStream.Name);
                    return;
                }
            }

            // Reset the length of an existing file (if any)
            fileStream.SetLength(0);
            string assetDownloadUrl = baseUrl.CombineUrlFromString(asset.AssetPath);

#if DEBUG
            SharedStatic.InstanceLogger?.LogTrace("Trying to download the asset from URL: {AssetUrl}", assetDownloadUrl);
#endif

            // Use Retry-able Copy-To Stream task to start the download
            await using RetryableCopyToStreamTask downloadTask = RetryableCopyToStreamTask
                .CreateTask(
                    async (pos, thisCtx) => await BridgedNetworkStream.CreateStream(_downloadHttpClient, assetDownloadUrl, pos, null, thisCtx),
                    fileStream,
                    new RetryableCopyToStreamTaskOptions
                    {
                        IsDisposeTargetStream = true,
                        MaxBufferSize = 4 << 10,
                        MaxRetryCount = 5,
                        MaxTimeoutSeconds = 5d,
                        RetryDelaySeconds = 0d
                    });

            // Start download task
            await downloadTask.StartTaskAsync(read =>
            {
                Interlocked.Add(ref installProgress.DownloadedBytes, read);
                progressDelegate?.Invoke(in installProgress);
            }, innerToken);
        }
    }

    private static async Task<List<GameInstallAsset>> StartGetMismatchedAssets(
        string gamePath,
        InstallProgress installProgress,
        List<GameInstallAsset> assets,
        InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate,
        CancellationToken token = default)
    {
        ConcurrentQueue<GameInstallAsset> queue = [];
        installProgress.DownloadedCount = 0;
        installProgress.TotalCountToDownload = assets.Count;
        installProgress.DownloadedBytes = 0;
        installProgress.TotalBytesToDownload = assets.Sum(x => x.AssetSize);

        progressDelegate?.Invoke(in installProgress);
        progressStateDelegate?.Invoke(InstallProgressState.Verify);

        // Perform read-verify execution
        await Parallel.ForEachAsync(assets, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = token
        }, Impl);

        return queue.ToList();

        async ValueTask Impl(GameInstallAsset asset, CancellationToken innerToken)
        {
            string filePath = Path.Combine(gamePath, asset.AssetPath.TrimStart("/\\").ToString());
            FileInfo fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists || fileInfo.Length != asset.AssetSize)
            {
                queue.Enqueue(asset);
                Interlocked.Add(ref installProgress.DownloadedBytes, asset.AssetSize);
                progressDelegate?.Invoke(in installProgress);
                return;
            }

            await using FileStream readOnlyStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            if (await IsHashMatchedAsync(
                    readOnlyStream,
                    asset,
                    read =>
                    {
                        Interlocked.Add(ref installProgress.DownloadedBytes, read);
                        progressDelegate?.Invoke(in installProgress);
                    },
                    innerToken))
            {
                return;
            }

            queue.Enqueue(asset);
            Interlocked.Add(ref installProgress.DownloadedBytes, asset.AssetSize);
            progressDelegate?.Invoke(in installProgress);
        }
    }

    private static async ValueTask<bool> IsHashMatchedAsync(
        FileStream fileStream,
        GameInstallAsset asset,
        Action<long> readStatusCallback,
        CancellationToken token)
    {
        Crc64Ecma crc64Ecma = new Crc64Ecma();
        const int bufferSize = 256 << 10;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize); // 256 KB of buffer

        long totalRead = 0;
        try
        {
            int read;
            while ((read = await fileStream.ReadAtLeastAsync(new Memory<byte>(buffer, 0, bufferSize), bufferSize, false, token)) > 0)
            {
                crc64Ecma.Append(new ReadOnlySpan<byte>(buffer, 0, read));
                totalRead += read;
                readStatusCallback(read);
            }

            byte[] hashResult = crc64Ecma.GetHashAndReset();
            if (hashResult.SequenceEqual(asset.AssetHash ?? throw new NullReferenceException("AssetHash of the asset cannot be null!")))
            {
                return true;
            }

            // If it's still invalid, try reversing the byte order (assume it's LE->BE)
            Array.Reverse(hashResult);
            if (hashResult.SequenceEqual(asset.AssetHash))
            {
                return true;
            }

            // Otherwise if it keeps failing, then return false and restore the order.
            Array.Reverse(hashResult);
            SharedStatic.InstanceLogger?.LogError(
                "Hash for: {FilePath} isn't match! {HashRemote} Remote != {HashLocal} Local. Download will be restarted!",
                fileStream.Name,
                Convert.ToHexStringLower(asset.AssetHash),
                Convert.ToHexStringLower(hashResult));

            // Reset the read count if the hash isn't match.
            readStatusCallback(-totalRead);
            return false;
        }
        catch (Exception)
        {
            // Also reset the read count if an exception occur.
            readStatusCallback(-totalRead);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
