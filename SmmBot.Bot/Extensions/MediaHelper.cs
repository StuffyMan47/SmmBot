using System.Text.RegularExpressions;
using Telegram.Bot.Types;

namespace SmmBot.Bot.Extensions;

public static class MediaHelper
{
    private static readonly Regex DataUriRegex = new Regex(@"^data:(?<mime>[\w/\-\.]+);(?<encoding>\w+),(?<data>.*)", RegexOptions.Compiled);

    public static InputFile GetInputFile(string filePathOrBase64, string fileName = "image.png")
    {
        if (string.IsNullOrEmpty(filePathOrBase64))
        {
            throw new ArgumentException("Path or base64 string cannot be null or empty", nameof(filePathOrBase64));
        }

        if (filePathOrBase64.StartsWith("data:image"))
        {
            var match = DataUriRegex.Match(filePathOrBase64);
            if (match.Success)
            {
                var base64Data = match.Groups["data"].Value;
                var bytes = Convert.FromBase64String(base64Data);
                var stream = new MemoryStream(bytes);
                return InputFile.FromStream(stream, fileName);
            }
        }
        else if (filePathOrBase64.StartsWith("http://") || filePathOrBase64.StartsWith("https://"))
        {
            return InputFile.FromUri(filePathOrBase64);
        }

        throw new ArgumentException("Invalid file format", nameof(filePathOrBase64));
    }
}
