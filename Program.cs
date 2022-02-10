using System.IO.Compression;
using System.Text.RegularExpressions;
using CommandLine;
using cpzip;

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
    using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Update);

    var zipPath = string.Empty;
    ZipArchiveEntry zipEntry = null;

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
                throw new FileNotFoundException($"Entry {zipFilePath}{zipSeparator}{zipPath} not found. Check target path.");
            }
        }
        // it's a file
        else
        {
            var tempFileName = Path.GetTempFileName();
            try
            {
                Log($"Processing nested zip {zipPath}...");
                Log($"Extracting nested zip {zipFilePath}{zipSeparator}{zipPath} to {tempFileName}...");
                zipEntry.ExtractToFile(tempFileName, true);

                // trim processed path
                var remainingPath = new Regex(Regex.Escape(zipPath)).Replace(targetPath, string.Empty, 1);

                Process(tempFileName, remainingPath);

                Log($"Deleting existing nested entry {zipFilePath}{zipSeparator}{zipEntry}...");
                zipEntry.Delete();

                Log($"Creating nested entry {zipFilePath}{zipSeparator}{zipPath} from {tempFileName}...");
                zipArchive.CreateEntryFromFile(tempFileName, zipPath, CompressionLevel.NoCompression);

                return;
            }
            finally
            {
                Log($"Deleting {tempFileName}...");
                File.Delete(tempFileName);
            }
        }
    }

    var entryName = (zipEntry?.FullName ?? string.Empty) + sourceFile.Name;
    var entry = zipArchive.GetEntry(entryName);
    if (entry != null)
    {
        if (noOverwrite)
        {
            throw new ApplicationException($"Entry {entryName} already exists.");
        }

        entry.Delete();
        Log($"Deleting existing entry {zipFilePath}{zipSeparator}{entry}...");
    }

    Log($"Creating entry {zipFilePath}{zipSeparator}{entryName}...");

    try
    {
        using var testArchive = ZipFile.Open(sourceFile.FullName, ZipArchiveMode.Read);
    }
    catch (Exception)
    {
        zipArchive.CreateEntryFromFile(sourceFile.FullName, entryName);
    }
    
    Log("Zip file detected, adding without compression.");
    zipArchive.CreateEntryFromFile(sourceFile.FullName, entryName, CompressionLevel.NoCompression);
}