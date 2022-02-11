using System.Text.RegularExpressions;
using CommandLine;
using cpzip;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

const string zipEntrySeparator =  "/";
var indent = 0;
Options options;

#region Logging


void Log(object message)
{
    if (options.Verbose)
    {
        Console.WriteLine(new string('\t', indent) + message);
    }
}

void LogError(object message) => Console.Error.WriteLine($"ERROR: {message}");

#endregion

#region Command line processing

var parser = new Parser(with => with.HelpWriter = null);
var parserResult = parser.ParseArguments<Options>(args);

return parserResult.MapResult(o =>
{
    options = o;
    try
    {
        foreach (var targetFile in ResolveFileNames(options.TargetFile))
        {
            foreach (var sourceFile in ResolveFileNames(options.SourceFile))
            {
                try
                {
                    Process(sourceFile, targetFile, options.TargetFilePath);
                }
                catch (Exception e)
                {
                    LogError($"{Path.GetFileName(sourceFile)} -> {Path.GetFileName(targetFile)}{zipEntrySeparator}{options.TargetFilePath}: {e.Message}");
                }
            }
        }
        
        return 0;
    }
    catch (Exception e)
    {
        LogError(e.Message);
        return e.HResult != 0 ? 1 : e.HResult;
    }
    finally
    {
        indent = 0;        
        Log("Done.");
    }
}, _ =>
{
    Options.DisplayHelp(parserResult);
    return -1;
});

#endregion

void Process(string sourceFileName, string zipFileName, string targetZipPath)
{
    Log($"{Path.GetFileName(sourceFileName)} -> {Path.GetFileName(zipFileName)}{zipEntrySeparator}{targetZipPath}:");
    
    indent++;
    
    using var zipArchive = new ZipFile(zipFileName);

    var zipPath = string.Empty;

    ZipEntry zipEntry = null;

    foreach (var path in targetZipPath.Split(zipEntrySeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        zipPath += path;
        zipEntry = zipArchive.GetEntry(zipPath);

        // not a file
        if (zipEntry == null)
        {
            zipPath += zipEntrySeparator;
            zipEntry = zipArchive.GetEntry(zipPath);

            // not a directory
            if (zipEntry == null)
            {
                throw new FileNotFoundException(
                    $"Entry {zipFileName}{zipEntrySeparator}{zipPath} not found. Check target path.");
            }
        }
        // it's a file
        else
        {
            var tempFileName = Path.GetTempFileName();
            try
            {
                Log($"Processing nested zip {zipPath}:");
                Log($"Extracting nested zip {zipFileName}{zipEntrySeparator}{zipPath} to {tempFileName}...");
                ExtractToFile(zipArchive, zipEntry, tempFileName);

                var remainingPath = new Regex(Regex.Escape(zipPath)).Replace(targetZipPath, string.Empty, 1);
                Process(sourceFileName, tempFileName, remainingPath);

                Log($"Deleting existing nested entry {zipFileName}{zipEntrySeparator}{zipEntry}...");

                var compressionMethod = zipEntry.CompressionMethod;
                zipArchive.BeginUpdate();

                zipArchive.Delete(zipEntry);
                Log($"Creating nested entry {zipFileName}{zipEntrySeparator}{zipPath} from {tempFileName}...");

                zipArchive.NameTransform = new SingleNameTransform(zipPath);
                zipArchive.Add(tempFileName, compressionMethod);
                zipArchive.CommitUpdate();

                return;
            }
            finally
            {
                indent--;
                Log($"Deleting {tempFileName}...");
                File.Delete(tempFileName);
            }
        }
    }

    var entryName = (zipEntry?.Name ?? string.Empty) + Path.GetFileName(sourceFileName);
    var entry = zipArchive.GetEntry(entryName);

    CompressionMethod? method = null;

    if (entry != null)
    {
        if (options.NoOverwrite)
        {
            throw new ApplicationException($"Entry {entryName} already exists.");
        }

        method = entry.CompressionMethod;
        zipArchive.BeginUpdate();
        zipArchive.Delete(entry);

        Log($"Deleting existing entry {zipFileName}{zipEntrySeparator}{entry}...");
    }

    Log($"Creating entry {zipFileName}{zipEntrySeparator}{entryName}...");

    if (method == null)
    {
        try
        {
            using var testArchive = new ZipFile(sourceFileName);

            method = CompressionMethod.Stored;
            Log($"Zip file detected, adding with compression method {method}.");
        }
        catch (Exception)
        {
            // set stored method for archived files
        }
    }

    if (!zipArchive.IsUpdating)
    {
        zipArchive.BeginUpdate();
    }

    zipArchive.NameTransform = new SingleNameTransform(entryName);

    if (method == null)
    {
        zipArchive.Add(sourceFileName);
    }
    else
    {
        zipArchive.Add(sourceFileName, method.Value);
    }

    zipArchive.CommitUpdate();

    indent--;
}

#region Helpers

static void ExtractToFile(ZipFile zipFile, ZipEntry entry, string fileName)
{
    using var inputStream = zipFile.GetInputStream(entry);
    using var outputStream = File.OpenWrite(fileName);
    inputStream.CopyTo(outputStream);
}

static IEnumerable<string> ResolveFileNames(string filePattern)
{
    var directory = Path.GetDirectoryName(filePattern);

    if (string.IsNullOrEmpty(directory))
    {
        directory = Directory.GetCurrentDirectory();
    }
    
    var directoryInfo = new DirectoryInfo(directory);
    if (!directoryInfo.Exists)
    {
        throw new DirectoryNotFoundException($"File directory not found: {filePattern}.");
    }

    var pattern = Path.GetFileName(filePattern);
    var files = directoryInfo.GetFiles(pattern);

    if (!files.Any())
    {
        throw new FileNotFoundException($"File not found: {filePattern}.");
    }

    return files.Select(f => f.FullName);
}

internal class SingleNameTransform : INameTransform
{
    private readonly string _name;

    public SingleNameTransform(string name)
    {
        _name = name;
    }

    public string TransformFile(string name) => _name;
    
    public string TransformDirectory(string name) => _name;
}
#endregion