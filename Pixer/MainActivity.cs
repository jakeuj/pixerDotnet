using System.Text;
using Microsoft.Extensions.Logging;

namespace PixerUpload;

public class MainActivity
{
    private readonly ILogger<MainActivity> _logger;
    private int _pixerBattery = 0;
    //private readonly int _validUpdateBatteryLevel = 20;

    public int PixerBattery => _pixerBattery;

    public MainActivity(ILogger<MainActivity>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MainActivity>.Instance;
    }

    public async Task CheckAsync()
    {
        await Task.Run(async () =>
        {
            using var client = new SocketClient(logger: _logger as ILogger<SocketClient>);
            string? bleVersion = null;
            string? iteVersion = null;
            string? mcuVersion = null;
            string? batteryLevel = null;

            try
            {
                if (await client.ConnectAsync())
                {
                    var response = await client.SendAsync(Encoding.UTF8.GetBytes("#TEST#"));
                    if (response == "Hello PC!")
                    {
                        bleVersion = await client.SendAsync(Encoding.UTF8.GetBytes("bleVersion"));
                        iteVersion = await client.SendAsync(Encoding.UTF8.GetBytes("iteVersion"));
                        mcuVersion = await client.SendAsync(Encoding.UTF8.GetBytes("mcuVersion"));
                        batteryLevel = await client.SendAsync(Encoding.UTF8.GetBytes("batteryLevel"));

                        if (int.TryParse(batteryLevel, out int battery))
                        {
                            _pixerBattery = battery;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in checkPixerBattery: {ex.Message}");
            }

            _logger.LogDebug($"Battery={_pixerBattery}");
            _logger.LogDebug($"BLE Version={bleVersion}");
            _logger.LogDebug($"MCU Version={mcuVersion}");
            _logger.LogDebug($"ITE Version={iteVersion}");
        });
    }

    public async Task ResetAsync()
    {
        await Task.Run(async () =>
        {
            using var client = new SocketClient(logger: _logger as ILogger<SocketClient>);

            try
            {
                if (await client.ConnectAsync())
                {
                    var response = await client.SendAsync(Encoding.UTF8.GetBytes("#TEST#"));
                    if (response == "Hello PC!")
                    {
                        await client.SendAsync(Encoding.UTF8.GetBytes("reset"));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in reset: {ex.Message}");
            }

            _logger.LogDebug("reset sent");
        });
    }

    public async Task UploadAsync(byte[] data)
    {
        await Task.Run(async () =>
        {
            using var client = new SocketClient(logger: _logger as ILogger<SocketClient>);

            try
            {
                if (await client.ConnectAsync())
                {
                    await client.UploadAsync(data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in upload: {ex.Message}");
            }

            _logger.LogDebug("uploaded");
        });
    }
}
