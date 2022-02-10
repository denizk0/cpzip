using System.Text.RegularExpressions;
using CommandLine;
using cpzip;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

const string zipSeparator = "/";

#region Logging

bool verbose;

void Log(object message)
{
    if (verbose)
    {
        Console.WriteLine(message?.ToString());
    }
}

void LogError(object message) => Console.Error.WriteLine($"ERROR: {message}");

#endregion

#region Command line processing

FileInfo sourceFile, targetFile;
bool noOverwrite;

var parser = new Parser(with => with.HelpWriter = null);
var parserResult = parser.ParseArguments<Options>(args);

return parserResult.MapResult(o =>
{
    try
    {
        sourceFile = new FileInfo(o.SourceFile);
        targetFile = new FileInfo(o.TargetFile);

        if (!sourceFile.Exists)
        {
            throw new FileNotFoundException($"Source file not found: {sourceFile.FullName}.", o.SourceFile);
        }

        if (!targetFile.Exists)
        {
            throw new FileNotFoundException($"Target file not found: {targetFile.FullName}.", o.TargetFile);
        }

        noOverwrite = o.NoOverwrite;
        verbose = o.Verbose;

        Process(targetFile.FullName, o.TargetFilePath);
        return 0;
    }
    catch (Exception e)
    {
        LogError(e.Message);
        return e.HResult != 0 ? 1 : e.HResult;
    }
}, _ =>
{
    Options.DisplayHelp(parserResult);
    return -1;
});

#endregion

void Process(string zipFilePath, string targetPath)
{
    using var zipArchive = new ZipFile(zipFilePath);

    var zipPath = string.Empty;

    ZipEntry zipEntry = null;

    foreach (var path in targetPath.Split(zipSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        zipPath += path;
        zipEntry = zipArchive.GetEntry(zipPath);

        // not a file
        if (zipEntry == null)
        {
            zipPath += zipSeparator;
            zipEntry = zipArchive.GetEntry(zipPath);

            // not a directory
            if (zipEntry == null)
            {
                throw new FileNotFoundException(
                    $"Entry {zipFilePath}{zipSeparator}{zipPath} not found. Check target path.");
            }
        }
        // it's a file
        else
        {
            var tempFileName = Path.GetTempFileName();
            try
            {
                Log($"Processing nested zip {zipPath}:");
                Log($"Extracting nested zip {zipFilePath}{zipSeparator}{zipPath} to {tempFileName}...");
                ExtractToFile(zipArchive, zipEntry, tempFileName);

                var remainingPath = new Regex(Regex.Escape(zipPath)).Replace(targetPath, string.Empty, 1);
                Process(tempFileName, remainingPath);

                Log($"Deleting existing nested entry {zipFilePath}{zipSeparator}{zipEntry}...");

                var compressionMethod = zipEntry.CompressionMethod;
                zipArchive.BeginUpdate();

                zipArchive.Delete(zipEntry);
                Log($"Creating nested entry {zipFilePath}{zipSeparator}{zipPath} from {tempFileName}...");

                zipArchive.NameTransform = new SingleNameTransform(zipPath);
                zipArchive.Add(tempFileName, compressionMethod);
                zipArchive.CommitUpdate();

                return;
            }
            finally
            {
                Log($"Deleting {tempFileName}...");
                File.Delete(tempFileName);
            }
        }
    }

    var entryName = (zipEntry?.Name ?? string.Empty) + sourceFile.Name;
    var entry = zipArchive.GetEntry(entryName);

    CompressionMethod? method = null;

    if (entry != null)
    {
        if (noOverwrite)
        {
            throw new ApplicationException($"Entry {entryName} already exists.");
        }

        method = entry.CompressionMethod;
        zipArchive.BeginUpdate();
        zipArchive.Delete(entry);

        Log($"Deleting existing entry {zipFilePath}{zipSeparator}{entry}...");
    }

    Log($"Creating entry {zipFilePath}{zipSeparator}{entryName}...");

    if (method == null)
    {
        try
        {
            using var testArchive = new ZipFile(sourceFile.FullName);
            Log("Zip file detected, adding without compression.");
            method = CompressionMethod.Stored;
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
        zipArchive.Add(sourceFile.FullName);
    }
    else
    {
        zipArchive.Add(sourceFile.FullName, method.Value);
    }

    zipArchive.CommitUpdate();
    Log("Done.");
}

#region Helpers

static void ExtractToFile(ZipFile zipFile, ZipEntry entry, string fileName)
{
    using var inputStream = zipFile.GetInputStream(entry);
    using var outputStream = File.OpenWrite(fileName);
    inputStream.CopyTo(outputStream);
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