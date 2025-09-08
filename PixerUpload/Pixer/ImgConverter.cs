using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Logging;
using System.Text;

namespace PixerUpload;

public class ImgConverter
{
    private readonly string _sourcePath;
    private readonly ILogger<ImgConverter> _logger;

    public ImgConverter(string sourcePath, ILogger<ImgConverter>? logger = null)
    {
        _sourcePath = sourcePath;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ImgConverter>.Instance;
    }

    public byte[]? Convert(int width = 1872, int height = 1404)
    {
        try
        {
            if (!File.Exists(_sourcePath))
            {
                _logger.LogError($"file '{_sourcePath}' not found");
                return null;
            }

            using var image = Image.Load<Rgba32>(_sourcePath);

            // Rotate if height > width (portrait orientation)
            if (image.Height > image.Width)
            {
                image.Mutate(x => x.Rotate(90));
            }

            // Calculate aspect ratios
            float imgRatio = (float)image.Width / image.Height;
            float targetRatio = (float)width / height;

            int newWidth, newHeight;

            // Resize logic matching Python implementation
            if (imgRatio >= targetRatio)
            {
                newHeight = height;
                newWidth = (int)(image.Width * ((float)newHeight / image.Height));
            }
            else
            {
                newWidth = width;
                newHeight = (int)(image.Height * ((float)newWidth / image.Width));
            }

            // Resize with Lanczos resampling (equivalent to PIL's LANCZOS)
            image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));

            // Calculate crop coordinates
            float left = (newWidth - width) / 2.0f;
            float top = (newHeight - height) / 2.0f;
            float right = left + width;
            float bottom = top + height;

            // Crop to exact dimensions
            var cropRect = new Rectangle((int)left, (int)top, width, height);
            image.Mutate(x => x.Crop(cropRect));

            // Convert to grayscale
            using var grayscaleImage = image.Clone();
            grayscaleImage.Mutate(x => x.Grayscale());

            // Extract pixel data
            var pixels = new byte[width * height];
            grayscaleImage.ProcessPixelRows(accessor =>
            {
                int pixelIndex = 0;
                for (int y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        // Convert RGBA to grayscale value (0-255)
                        var pixel = pixelRow[x];
                        // Use standard grayscale conversion formula
                        byte grayscaleValue = (byte)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                        pixels[pixelIndex++] = grayscaleValue;
                    }
                }
            });

            // Pack pixels into 4-bit format (2 pixels per byte)
            var packedData = new List<byte>();
            for (int i = 0; i < pixels.Length; i += 2)
            {
                byte pixel1 = (byte)(pixels[i] >> 4);  // First pixel (upper 4 bits)
                byte pixel2 = 0;

                if (i + 1 < pixels.Length)
                {
                    pixel2 = (byte)(pixels[i + 1] >> 4);  // Second pixel (lower 4 bits)
                }

                // Pack: pixel2 in upper 4 bits, pixel1 in lower 4 bits
                // This matches the Python implementation: (pixel2 << 4) | pixel1
                byte packedByte = (byte)((pixel2 << 4) | pixel1);
                packedData.Add(packedByte);
            }

            // Create header and combine with packed data
            string headerString = "#file#000801314144imagebin";
            byte[] headerBytes = Encoding.UTF8.GetBytes(headerString);

            // Combine header and packed data
            var combinedData = new byte[headerBytes.Length + packedData.Count];
            Array.Copy(headerBytes, 0, combinedData, 0, headerBytes.Length);
            Array.Copy(packedData.ToArray(), 0, combinedData, headerBytes.Length, packedData.Count);

            _logger.LogDebug($"successfully converted image '{_sourcePath}'");
            return combinedData;
        }
        catch (FileNotFoundException)
        {
            _logger.LogError($"file '{_sourcePath}' not found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"error handling the image: {ex.Message}");
            return null;
        }
    }
}
