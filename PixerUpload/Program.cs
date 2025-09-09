using Microsoft.Extensions.Logging;
using PixerUpload;
using PixerUpload.Pixer;
using System.Linq;

// Handle command line arguments
var argsList = args.ToList();
var debugMode = argsList.Remove("--debug");

// Configure logging
using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddConsole()
        .SetMinimumLevel(debugMode ? LogLevel.Debug : LogLevel.Information)
);

var logger = loggerFactory.CreateLogger<Program>();

try
{
    logger.LogInformation("Starting Pixer Upload C# Version");

    // Set logger for FirmwareUpgrade
    FirmwareUpgrade.SetLogger(logger);

    // Firmware upgrade check (equivalent to Python's firmware_upgrade_before_check)
    await FirmwareUpgrade.FirmwareUpgradeBeforeCheckAsync(
        host: "192.168.1.1",
        port: 6000,
        mBleVerNo: 14,
        mIteVerNo: 35,
        mBspVerNo: 1702061,
        bleBinPath: "ble.bin",
        iteBinPath: "ite.bin",
        bspBinPath: "pixer.bin"
    );

    logger.LogInformation("Firmware upgrade check completed.");

    // Get image path from command line arguments
    var imagePath = argsList.FirstOrDefault() ?? "image.png";

    // check image path
    if (!File.Exists(imagePath))
    {
        logger.LogError($"Image file not found: {imagePath}");
        Environment.Exit(1);
    }

    var activity = new MainActivity(loggerFactory.CreateLogger<MainActivity>());

    // Check device status
    await activity.CheckAsync();

    // If image path is provided as command line argument, convert and upload

    var converter = new ImgConverter(imagePath, loggerFactory.CreateLogger<ImgConverter>());
    var data = converter.Convert();

    if (data != null)
    {
        await activity.UploadAsync(data);
        logger.LogInformation($"Successfully processed and uploaded image: {imagePath}");
    }
    else
    {
        logger.LogError($"Failed to convert image: {imagePath}");
        Environment.Exit(1);
    }

    // Uncomment the following line if you want to reset the device
    // await activity.ResetAsync();
}
catch (Exception ex)
{
    logger.LogError($"Application error: {ex.Message}");
    Environment.Exit(1);
}