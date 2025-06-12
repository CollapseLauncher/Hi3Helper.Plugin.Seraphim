using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.HBR.Management.Api;
using Hi3Helper.Plugin.HBR.Utility;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.HBR.Management;

// ReSharper disable once InconsistentNaming
public partial class HBRGameInstaller
{
    private async ValueTask StartInstallOrUpdateAsync(
        string gamePath,
        InstallProgressDelegate? progressDelegate,
        InstallProgressStateDelegate? progressStateDelegate,
        CancellationToken token)
    {
        if (_currentGameAssetManifest?.GameAssets == null)
        {
            throw new NullReferenceException("_currentGameAssetManifest is null!");
        }

        TimeSpan timedOutCancelTokenSpan = TimeSpan.FromSeconds(10);
        InstallProgress installProgress = new InstallProgress
        {
            StateCount           = 1,
            TotalStateToComplete = 1,
            DownloadedCount      = 0,
            TotalCountToDownload = _currentGameAssetManifest.GameAssets.Count,
            DownloadedBytes      = 0,
            TotalBytesToDownload = _currentGameAssetManifest.GameAssets.Sum(x => x.AssetSize)
        };

        string baseUrl = GameAssetBaseUrl.CombineUrlFromString(_currentGameAssetManifest.RootSuffixPath);
        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new NullReferenceException("Base URL cannot be retrieved as it's null-ed!");
        }

        // Perform write execution
        await Parallel.ForEachAsync(_currentGameAssetManifest.GameAssets, new ParallelOptions
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

            string   filePath = Path.Combine(gamePath, asset.AssetPath);
            FileInfo fileInfo = new FileInfo(filePath);
            fileInfo.Directory?.Create();

            if (fileInfo.Exists)
            {
                fileInfo.IsReadOnly = false;
            }

            await using FileStream fileStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            Interlocked.Increment(ref installProgress.DownloadedCount);
            progressDelegate?.Invoke(in installProgress);
            progressStateDelegate?.Invoke(InstallProgressState.Download);

            if (fileInfo.Exists &&
                fileInfo.Length == asset.AssetPath.Length &&
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

            // Reset the length of an existing file (if any)
            fileStream.SetLength(0);

            using HttpResponseMessage responseMessage = await _downloadHttpClient.GetAsync(baseUrl, HttpCompletionOption.ResponseHeadersRead, innerToken);
            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Cannot retrieve file from URL: {baseUrl} (Status Code: {responseMessage.StatusCode} ({(int)responseMessage.StatusCode})",
                    null,
                    responseMessage.StatusCode);
            }

            await using Stream networkStream = await responseMessage.Content.ReadAsStreamAsync(innerToken);

            byte[]                  buffer                  = ArrayPool<byte>.Shared.Rent(4 << 10);
            CancellationTokenSource timedOutReadCancelToken = new CancellationTokenSource(timedOutCancelTokenSpan);
            CancellationTokenSource cooperatedCancelToken   = CancellationTokenSource.CreateLinkedTokenSource(timedOutReadCancelToken.Token, innerToken);
            try
            {
                int read;
                while ((read = await networkStream.ReadAsync(new Memory<byte>(buffer, 0, buffer.Length), cooperatedCancelToken.Token)) > 0)
                {
                    // Dispose, free resources before updating and writing to local stream.
                    timedOutReadCancelToken.Dispose();
                    cooperatedCancelToken.Dispose();

                    // The reason to silent the warning is that, for writing to local stream, the completion is always
                    // guaranteed (unless the user had a bad drive due to bad sector, etc.). The OS won't throw a timed-out exception
                    // even at the slowest storage possible (unless, again, the user had a bad drive).

                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    await fileStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, read), innerToken);
                    Interlocked.Add(ref installProgress.DownloadedBytes, read);
                    progressDelegate?.Invoke(in installProgress);

                    // Refresh da token
                    timedOutReadCancelToken = new CancellationTokenSource(timedOutCancelTokenSpan);
                    cooperatedCancelToken   = CancellationTokenSource.CreateLinkedTokenSource(timedOutReadCancelToken.Token, innerToken);
                }

                SharedStatic.InstanceLogger?.LogInformation("Download for file: {FilePath} is completed!", fileStream.Name);
            }
            finally
            {
                timedOutReadCancelToken.Dispose();
                cooperatedCancelToken.Dispose();
                ArrayPool<byte>.Shared.Return(buffer);
            }
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
