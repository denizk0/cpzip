using System.Diagnostics.CodeAnalysis;
using CommandLine;
using CommandLine.Text;

namespace cpzip;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
internal class Options
{
    [Value(0, Required = true, HelpText = "Source file(s) to copy. Can be a wildcard.", MetaName = "source_file")]
    public string SourceFile { get; set; }

    [Value(1, Required = true, HelpText = "Target file(s) to copy to. Can be a wildcard.", MetaName = "target_file")]
    public string TargetFile { get; set; }

    [Value(2, Required = true, HelpText = "Path within the target file. Use '/' as a separator as a separator or as a root path.", MetaName = "target_path")]
    public string TargetFilePath { get; set; }

    [Option('n', "no-overwrite", Required = false, HelpText = "Do not overwrite existing files.")]
    public bool NoOverwrite { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }

    
    internal static void DisplayHelp<T>(ParserResult<T> result)
    {  
        var helpText = HelpText.AutoBuild(result, h =>
        {
            h.AdditionalNewLineAfterOption = false;
            h.Copyright = string.Empty;
            h.Heading =
                "Copies file to the target zip archive, with nested archives support.";
            h.AddPreOptionsLine("Usage: cpzip source_file target_file target_path [options]");
            h.AddPostOptionsLine("Example:");
            h.AddPostOptionsLine("cpzip my_photo.png my_photos.zip christmas/this_year.zip/new");
            h.AddPostOptionsLine("will copy 'my_photo.png' to the folder 'new' of the nested file 'this_year.zip' updating 'my_photos.zip' accordingly");
            
            return HelpText.DefaultParsingErrorsHandler(result, h);
        }, e => e);
        Console.WriteLine(helpText);
    }
}