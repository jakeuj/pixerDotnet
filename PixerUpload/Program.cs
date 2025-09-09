using Microsoft.Extensions.Logging;
using PixerUpload;
using PixerUpload.Pixer;

// Configure logging
using var loggerFactory = LoggerFactory.Create(builder =>
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug)
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
    
    var activity = new MainActivity(loggerFactory.CreateLogger<MainActivity>());

    // Check device status
    await activity.CheckAsync();

    // Get image path from command line arguments
    var imagePath = args.FirstOrDefault();

    if (imagePath == null)
    {
        logger.LogInformation("No image path provided. Exiting.");
        Environment.Exit(1);
    }

    // check image path
    if (!File.Exists(imagePath))
    {
        logger.LogError($"Image file not found: {imagePath}");
        Environment.Exit(1);
    }
    
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