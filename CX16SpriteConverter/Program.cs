using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.RegularExpressions;

var paletteFile = args[0];
var fileToConvert = args[1];
var outputFile = args[2];
var paletteOutputFile = args[3];

int extraImageColors = 0;

// sprite dimensions
int spriteSize = 32;

List<Rgb24> paletteColors = GetPaletteColorsFromFile(paletteFile);

if (paletteColors.Count > 16)
{
    Console.WriteLine($"Warning: {paletteColors.Count} colors found in palette.  Max is 16 for 4bpp.");
}

byte[] paletteOutputBytes = new byte[paletteColors.Count * 2];
int paletteByteIndex = 0;
foreach (var paletteColor in paletteColors)
{
    int green4Bit = paletteColor.G >> 4;
    int blue4Bit = paletteColor.B >> 4;
    int red4Bit = paletteColor.R >> 4;

    byte greenBlueByte = OneBitIntToByte(green4Bit << 4 | blue4Bit);
    byte redByte = OneBitIntToByte(red4Bit);

    paletteOutputBytes[paletteByteIndex] = greenBlueByte;
    paletteOutputBytes[paletteByteIndex + 1] = redByte;

    paletteByteIndex += 2;
}

WriteBytesToFile(paletteOutputFile, paletteOutputBytes);

using (Image<Rgba32> inputFile = Image.Load<Rgba32>(fileToConvert))
{
    if ((inputFile.Width % spriteSize != 0) || (inputFile.Height % spriteSize != 0))
    {
        Console.WriteLine("Warning: image does not conform to sprite size.");
    }

    int numCols = inputFile.Width / spriteSize;
    int numRows = inputFile.Height / spriteSize;
    int numSpritesInSheet = (numCols * numRows);

    // 4bpp = 1 byte per 2 pixels
    byte[] outputBytes = new byte[(inputFile.Width * inputFile.Height / 2)];
    int byteIndex = 0;

    for (int currentSprite = 0; currentSprite < numSpritesInSheet; currentSprite++)
    {
        for (int currentRow = 0; currentRow < numRows; currentRow++)
        {
            for (int currentcol = 0; currentcol < numCols; currentcol++)
            {
                for (int y = currentRow * spriteSize; y < (currentRow + 1) * spriteSize; y++)
                {
                    for (int x = currentcol * spriteSize; x < (currentcol + 1) * spriteSize; x++)
                    {
                        Rgba32 highNibbleColor = inputFile[x, y];
                        Rgba32 lowNibbleColor = inputFile[x + 1, y];

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
            }
        }
    }

    if (extraImageColors > 0)
    {
        Console.WriteLine($"Warning: {extraImageColors} were found that weren't in the original palette.");
    }

    WriteBytesToFile(outputFile, outputBytes);
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

List<Rgb24> GetPaletteColorsFromFile(string paletteFile)
{
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

    return paletteColors;
}

static void WriteBytesToFile(string outputFile, byte[] outputBytes)
{
    outputFile = outputFile.ToUpper();
    FileStream outputFileStream = new FileStream(outputFile, FileMode.Create);
    BinaryWriter outputWriter = new BinaryWriter(outputFileStream);

    outputWriter.Write(outputBytes);

    outputFileStream.Close();
    outputWriter.Close();
}