using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PluginIndexer;

public class SelfUpdateAssetInfo
{
    public required string FilePath { get; set; }

    public long Size { get; set; }

    public required byte[] FileHash { get; set; }
}

public class Program
{
    private static readonly string[]             AllowedPluginExt             = [".dll", ".exe", ".so", ".dylib"];
#if !BFLAT
    private static readonly SearchValues<string> AllowedPluginExtSearchValues = SearchValues.Create(AllowedPluginExt, StringComparison.OrdinalIgnoreCase);
#endif

    public static int Main(params string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return int.MaxValue;
        }

        try
        {
            string path = args[0];
            if (!Directory.Exists(path))
            {
                Console.Error.WriteLine("Path is not a directory or it doesn't exist!");
                return 2;
            }

            FileInfo? fileInfo = FindPluginLibraryAndGetAssets(path, out List<SelfUpdateAssetInfo> assetInfo, out string? mainLibraryName);
            if (fileInfo == null || string.IsNullOrEmpty(mainLibraryName))
            {
                Console.Error.WriteLine("No valid plugin library was found.");
                return 1;
            }

            string referenceFilePath = Path.Combine(path, "manifest.json");
            return WriteToJson(fileInfo, mainLibraryName, referenceFilePath, assetInfo);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"An unknown error has occurred! {ex}");
            return int.MinValue;
        }
    }

    private static int WriteToJson(FileInfo fileInfo, string mainLibraryName, string referenceFilePath, List<SelfUpdateAssetInfo> assetInfo)
    {
        DateTimeOffset creationDate = fileInfo.CreationTime;
        FileVersionInfo pluginFileVersionInfo = FileVersionInfo.GetVersionInfo(fileInfo.FullName);
        if (!Version.TryParse(pluginFileVersionInfo.FileVersion, out Version? pluginFileVersion))
        {
            Console.Error.WriteLine($"Cannot parse plugin's {fileInfo.Name} version: {pluginFileVersionInfo.FileVersion}");
            return 3;
        }

        Console.WriteLine("Plugin has been found!");
        Console.WriteLine($"  Main Library Path Name: {mainLibraryName}");
        Console.WriteLine($"  Creation Date: {creationDate}");
        Console.WriteLine($"  Version: {pluginFileVersion}");
        Console.Write("Writing metadata info...");

        using FileStream referenceFileStream = File.Create(referenceFilePath);
        using Utf8JsonWriter writer = new Utf8JsonWriter(referenceFileStream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = true,
#if !BFLAT
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n"
#endif
        });

        writer.WriteStartObject();

        writer.WriteString("MainLibraryName", mainLibraryName);
        writer.WriteString("PluginVersion", pluginFileVersion.ToString());
        writer.WriteString("PluginCreationDate", creationDate);
        writer.WriteString("ManifestDate", DateTimeOffset.Now);

        writer.WriteStartArray("Assets");
        foreach (var asset in assetInfo)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(asset.FilePath), asset.FilePath);
            writer.WriteNumber(nameof(asset.Size), asset.Size);
            writer.WriteBase64String(nameof(asset.FileHash), asset.FileHash);

            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.Flush();

        Console.WriteLine(" Done!");
        return 0;
    }

    private static FileInfo? FindPluginLibraryAndGetAssets(string dirPath, out List<SelfUpdateAssetInfo> fileList, out string? mainLibraryName)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(dirPath);
        List<SelfUpdateAssetInfo> fileListRef = [];
        fileList = fileListRef;

        FileInfo? mainLibraryFileInfo = null;
        mainLibraryName = null;

        string? outMainLibraryName = null;
        Parallel.ForEach(directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => !x.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)), Impl);

        mainLibraryName = outMainLibraryName;
        return mainLibraryFileInfo;

        void Impl(FileInfo fileInfo)
        {
            string fileName = fileInfo.FullName.AsSpan(directoryInfo.FullName.Length).TrimStart("\\/").ToString();

            if (mainLibraryFileInfo == null &&
                IsPluginLibrary(fileInfo))
            {
                Interlocked.Exchange(ref mainLibraryFileInfo, fileInfo);
                Interlocked.Exchange(ref outMainLibraryName, fileName);
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                MD5 hash = MD5.Create();
                using FileStream fileStream = fileInfo.OpenRead();

                int read;
                while ((read = fileStream.Read(buffer)) > 0)
                {
                    hash.TransformBlock(buffer, 0, read, buffer, 0);
                }

                hash.TransformFinalBlock(buffer, 0, read);

                byte[] hashBytes = hash.Hash ?? [];
                SelfUpdateAssetInfo assetInfo = new SelfUpdateAssetInfo
                {
                    FileHash = hashBytes,
                    FilePath = fileName,
                    Size = fileInfo.Length
                };

                lock (fileListRef)
                {
                    fileListRef.Add(assetInfo);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private static bool IsPluginLibrary(FileInfo fileInfo)
    {
        nint handle = nint.Zero;

#if !BFLAT
        if (fileInfo.Name.IndexOfAny(AllowedPluginExtSearchValues) < 0)
        {
            return false;
        }
#else
        if (!AllowedPluginExt.Any(x => fileInfo.Name.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
#endif

        try
        {
            return NativeLibrary.TryLoad(fileInfo.FullName, out handle) &&
                   NativeLibrary.TryGetExport(handle, "TryGetApiExport", out _);
        }
        finally
        {
            if (handle != nint.Zero)
            {
                NativeLibrary.Free(handle);
            }
        }
    }

    private static void PrintHelp()
    {
        string? execPath = Path.GetFileName(Environment.ProcessPath);
        Console.WriteLine($"Usage: {execPath} [plugin_dll_directory_path]");
    }
}