using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.CommandLine;
using System.Text.RegularExpressions;

var paletteFileOption = new Option<string>(
    name: "--paletteFile",
    description: "The Gimp-formatted palette file (RGB values as plain text) to convert to CX16 12-bit color"
) { IsRequired = true };

var spriteSheetFileOption = new Option<string>(
    name: "--spriteSheetFile",
    description: "A PNG indexed color sprite sheet file to convert to CX16 4bpp indexed sprite."
) { IsRequired = true };

var spriteSizeOption = new Option<int>(
    name: "--spriteSize",
    description: "Size of one square side of a square sprite.",
    getDefaultValue: () => 32
);

var spriteOutputFileOption = new Option<string>(
    name: "--spriteOutputFile",
    description: "The CX16 sprite sheet, ready to directly load into memory."
) { IsRequired = true };

var paletteOutputFileOption = new Option<string?>(
    name: "--paletteOutputFile",
    description: "The C16 palette file, ready to be loaded into memory at a 16-color offset."
);

var rootCommand = new RootCommand("Convert indexed color PNG sprite sheets for use as sprites in the Commander X16.");
rootCommand.AddOption(paletteFileOption);
rootCommand.AddOption(spriteSheetFileOption);
rootCommand.AddOption(spriteSizeOption);
rootCommand.AddOption(spriteOutputFileOption);
rootCommand.AddOption(paletteOutputFileOption);

rootCommand.SetHandler((paletteFile, spriteSheetFile, spriteSize, spriteOutputFile, paletteOutputFile) =>
{
    ConvertToCx16Sprites(paletteFile, spriteSheetFile, spriteSize, spriteOutputFile, paletteOutputFile);
}, paletteFileOption, spriteSheetFileOption, spriteSizeOption, spriteOutputFileOption, paletteOutputFileOption);


return rootCommand.Invoke(args);

void ConvertToCx16Sprites(string paletteFile, string spriteSheetFile, int spriteSize, string spriteOutputFile, string? paletteOutputFile)
{
    int extraImageColors = 0;

    List<Rgb24> paletteColors = GetPaletteColorsFromFile(paletteFile);
    if (paletteOutputFile != null)
    {
        WritePaletteColorsToFile(paletteOutputFile, paletteColors);
    }

    using (Image<Rgba32> inputFile = Image.Load<Rgba32>(spriteSheetFile))
    {
        if ((inputFile.Width % spriteSize != 0) || (inputFile.Height % spriteSize != 0))
        {
            Console.WriteLine("Warning: image does not conform to sprite size.");
        }

        int numCols = inputFile.Width / spriteSize;
        int numRows = inputFile.Height / spriteSize;

        // 4bpp = 1 byte per 2 pixels
        byte[] outputBytes = new byte[(inputFile.Width * inputFile.Height / 2)];
        int byteIndex = 0;

        for (int currentRow = 0; currentRow < numRows; currentRow++)
        {
            for (int currentcol = 0; currentcol < numCols; currentcol++)
            {
                for (int y = currentRow * spriteSize; y < (currentRow + 1) * spriteSize; y++)
                {
                    for (int x = currentcol * spriteSize; x < (currentcol + 1) * spriteSize; x+=2)
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
        

        if (extraImageColors > 0)
        {
            Console.WriteLine($"Warning: {extraImageColors} were found that weren't in the original palette.");
        }

        WriteBytesToFile(spriteOutputFile, outputBytes);
    }

    void WritePaletteColorsToFile(string paletteOutputFile, List<Rgb24> paletteColors)
    {
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
    }
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