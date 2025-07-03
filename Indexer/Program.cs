using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.ABI;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
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
    private static readonly SearchValues<string> AllowedPluginExtSearchValues = SearchValues.Create(AllowedPluginExt, StringComparison.OrdinalIgnoreCase);

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

            FileInfo? fileInfo = FindPluginLibraryAndGetAssets(path, out List<SelfUpdateAssetInfo> assetInfo, out SelfUpdateReferenceInfo? reference);
            if (fileInfo == null || reference == null || string.IsNullOrEmpty(reference.MainLibraryName))
            {
                Console.Error.WriteLine("No valid plugin library was found.");
                return 1;
            }

            string referenceFilePath = Path.Combine(path, "manifest.json");
            return WriteToJson(reference, referenceFilePath, assetInfo);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"An unknown error has occurred! {ex}");
            return int.MinValue;
        }
    }

    private static int WriteToJson(SelfUpdateReferenceInfo reference, string referenceFilePath, List<SelfUpdateAssetInfo> assetInfo)
    {
        DateTimeOffset creationDate = reference.PluginCreationDate.ToOffset(reference.PluginCreationDate.Offset);

        Console.WriteLine("Plugin has been found!");
        Console.WriteLine($"  Main Library Path Name: {reference.MainLibraryName}");
        Console.WriteLine($"  Main Plugin Name: {reference.MainPluginName}");
        Console.WriteLine($"  Creation Date: {creationDate}");
        Console.WriteLine($"  Version: {reference.PluginVersion}");
        Console.Write("Writing metadata info...");

        using FileStream referenceFileStream = File.Create(referenceFilePath);
        using Utf8JsonWriter writer = new Utf8JsonWriter(referenceFileStream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n"
        });

        writer.WriteStartObject();

        writer.WriteString(nameof(SelfUpdateReferenceInfo.MainLibraryName), reference.MainLibraryName);
        writer.WriteString(nameof(SelfUpdateReferenceInfo.MainPluginName), reference.MainPluginName);
        writer.WriteString(nameof(SelfUpdateReferenceInfo.MainPluginAuthor), reference.MainPluginAuthor);
        writer.WriteString(nameof(SelfUpdateReferenceInfo.MainPluginDescription), reference.MainPluginDescription);
        writer.WriteString(nameof(SelfUpdateReferenceInfo.PluginStandardVersion), reference.PluginStandardVersion.ToString());
        writer.WriteString(nameof(SelfUpdateReferenceInfo.PluginVersion), reference.PluginVersion.ToString());
        writer.WriteString(nameof(SelfUpdateReferenceInfo.PluginCreationDate), creationDate);
        writer.WriteString(nameof(SelfUpdateReferenceInfo.ManifestDate), DateTimeOffset.Now);

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

    private static FileInfo? FindPluginLibraryAndGetAssets(string dirPath, out List<SelfUpdateAssetInfo> fileList, out SelfUpdateReferenceInfo? referenceInfo)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(dirPath);
        List<SelfUpdateAssetInfo> fileListRef = [];
        fileList = fileListRef;
        referenceInfo = null;

        FileInfo? mainLibraryFileInfo = null;
        SelfUpdateReferenceInfo? referenceInfoResult = null;

        Parallel.ForEach(directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => !x.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)), Impl);
        referenceInfo = referenceInfoResult;

        return mainLibraryFileInfo;

        void Impl(FileInfo fileInfo)
        {
            string fileName = fileInfo.FullName.AsSpan(directoryInfo.FullName.Length).TrimStart("\\/").ToString();

            if (mainLibraryFileInfo == null &&
                IsPluginLibrary(fileInfo, fileName, out SelfUpdateReferenceInfo? referenceInfoInner))
            {
                Interlocked.Exchange(ref mainLibraryFileInfo, fileInfo);
                Interlocked.Exchange(ref referenceInfoResult, referenceInfoInner);
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

    private unsafe delegate void* GetPlugin();
    private unsafe delegate GameVersion* GetVersion();

    private static unsafe bool IsPluginLibrary(FileInfo fileInfo, string fileName, [NotNullWhen(true)] out SelfUpdateReferenceInfo? referenceInfo)
    {
        nint handle = nint.Zero;
        referenceInfo = null;

        if (fileInfo.Name.IndexOfAny(AllowedPluginExtSearchValues) < 0)
        {
            return false;
        }
        char* getPluginNameP = (char*)Utf16StringMarshaller.ConvertToUnmanaged("GetPlugin");
        char* getPluginVersionNameP = (char*)Utf16StringMarshaller.ConvertToUnmanaged("GetPluginVersion");
        char* getGetPluginStandardVersionNameP = (char*)Utf16StringMarshaller.ConvertToUnmanaged("GetPluginStandardVersion");

        try
        {
            if (!NativeLibrary.TryLoad(fileInfo.FullName, out handle) ||
                !NativeLibrary.TryGetExport(handle, "TryGetApiExport", out nint exportAddress) ||
                exportAddress == nint.Zero)
            {
                return false;
            }

            delegate* unmanaged[Cdecl]<char*, void**, int> tryGetApiExportCallback = (delegate* unmanaged[Cdecl]<char*, void**, int>)exportAddress;

            nint getPluginP = nint.Zero;
            int tryResult = tryGetApiExportCallback(getPluginNameP, (void**)&getPluginP);

            if (tryResult != 0 ||
                getPluginP == nint.Zero)
            {
                return false;
            }

            void* pluginP = Marshal.GetDelegateForFunctionPointer<GetPlugin>(getPluginP)();
            if (pluginP == null)
            {
                return false;
            }

            IPlugin? plugin = ComInterfaceMarshaller<IPlugin>.ConvertToManaged(pluginP);
            if (plugin == null)
            {
                return false;
            }

            tryResult = tryGetApiExportCallback(getPluginVersionNameP, (void**)&getPluginP);

            if (tryResult != 0 ||
                getPluginP == nint.Zero)
            {
                return false;
            }

            GameVersion pluginVersion = *Marshal.GetDelegateForFunctionPointer<GetVersion>(getPluginP)();

            tryResult = tryGetApiExportCallback(getGetPluginStandardVersionNameP, (void**)&getPluginP);

            if (tryResult != 0 ||
                getPluginP == nint.Zero)
            {
                return false;
            }

            GameVersion pluginStandardVersion = *Marshal.GetDelegateForFunctionPointer<GetVersion>(getPluginP)();

            plugin.GetPluginName(out string? pluginName);
            plugin.GetPluginAuthor(out string? pluginAuthor);
            plugin.GetPluginDescription(out string? pluginDescription);
            plugin.GetPluginCreationDate(out DateTime* pluginCreationDate);

            referenceInfo = new SelfUpdateReferenceInfo
            {
                Assets = [],
                MainPluginName = pluginName,
                MainPluginAuthor = pluginAuthor,
                MainPluginDescription = pluginDescription,
                PluginCreationDate = *pluginCreationDate,
                PluginVersion = pluginVersion,
                PluginStandardVersion = pluginStandardVersion,
                MainLibraryName = fileName
            };
            return true;
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