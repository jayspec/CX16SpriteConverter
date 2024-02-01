using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;

var paletteFile = args[0];
var fileToConvert = args[1];
var outputFile = args[2];

int extraImageColors = 0;

var paletteColors = new List<Rgb24>();

// Read the Gimp Palette File, which is just a text file
// Filled with RGB values
using (StreamReader reader = new StreamReader(paletteFile))
{
    string? line;
    Regex rgbRegex = new Regex(@"\s*(\d+)\s+(\d+)\s+(\d+)\s+");
    while ((line = reader.ReadLine()) != null)
    {
        MatchCollection matches = rgbRegex.Matches(line);
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                int red = int.Parse(match.Groups[1].Value);
                int green = int.Parse(match.Groups[2].Value);
                int blue = int.Parse(match.Groups[3].Value);

                byte redByte = OneBitIntToByte(red);
                byte greenByte = OneBitIntToByte(green);
                byte blueByte = OneBitIntToByte(blue);

                Rgb24 rgb = new Rgb24(redByte, greenByte, blueByte);
                paletteColors.Add(rgb);

            }
        }
    }
}

if (paletteColors.Count > 16)
{
    Console.WriteLine($"Warning: {paletteColors.Count} colors found in palette.  Max is 16 for 4bpp.");
}

using (Image<Rgba32> inputFile = Image.Load<Rgba32>(fileToConvert))
{
    // commented out code is to read palette from separate PNG file
    //for (int row = 0; row < palette.Width; row++)
    //{
    //    for (int col = 0; col < palette.Height; col++)
    //    {
    //        var color = palette[row, col];
    //        if (!paletteColors.Contains(color))
    //        {
    //            // 4bpp
    //            if (paletteColors.Count >= 16)
    //            {
    //                extraPaletteColors++;
    //            }
    //            else
    //            {
    //                paletteColors.Add(color);
    //            }
    //        }
    //    }

    //}

    //if (extraPaletteColors > 0)
    //{
    //    Console.WriteLine($"Warning: {paletteColors.Count} palette colors found, but only 16 (4bpp) are supported.");
    //}

    // 4bpp = 1 byte per 2 pixels
    byte[] outputBytes = new byte[(inputFile.Width * inputFile.Height / 2)];
    int byteIndex = 0;

    for (int y = 0; y < inputFile.Height; y++)
    {
        for (int x = 0; x < inputFile.Width; x+=2)
        {
            Rgba32 highNibbleColor = inputFile[x, y];
            Rgba32 lowNibbleColor = inputFile[x+1, y];

            // We don't have the alpha value in the palette file, so strip the alpha
            Rgb24 highNibbleNoAlpha = new Rgb24(highNibbleColor.R, highNibbleColor.G, highNibbleColor.B);
            Rgb24 lowNibbleNoAlpha = new Rgb24(lowNibbleColor.R, lowNibbleColor.G, lowNibbleColor.B);

            if (!paletteColors.Contains(highNibbleNoAlpha))
            {
                extraImageColors++;
            }

            if (!paletteColors.Contains(lowNibbleNoAlpha))
            {
                extraImageColors++;
            }

            int highNibblePaletteIndex = paletteColors.IndexOf(highNibbleNoAlpha);
            int lowNibblePaletteIndex = paletteColors.IndexOf(lowNibbleNoAlpha);

            int colorIndex = (highNibblePaletteIndex << 4) | lowNibblePaletteIndex;

            byte colorIndexByte = OneBitIntToByte(colorIndex);

            outputBytes[byteIndex] = colorIndexByte;
            byteIndex++;
        }
    }

    if (extraImageColors > 0)
    {
        Console.WriteLine($"Warning: {extraImageColors} were found that weren't in the original palette.");
    }

    outputFile = outputFile.ToUpper();
    FileStream outputFileStream = new FileStream(outputFile, FileMode.Create);
    BinaryWriter outputWriter = new BinaryWriter(outputFileStream);

    outputWriter.Write(outputBytes);

    outputFileStream.Close();
    outputWriter.Close();
}

byte OneBitIntToByte(int value)
{
    if (value > 255 || value < 0)
    {
        throw new Exception($"Value {value} out of one byte range.");
    }

    byte[] fullBytes = BitConverter.GetBytes(value);

    if (!BitConverter.IsLittleEndian)
    {
        Array.Reverse(fullBytes);
    }

    return fullBytes[0];

}
