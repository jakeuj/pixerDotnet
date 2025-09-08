using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PixerUpload.Pixer;

public static class FirmwareUpgrade
{
    private static ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    private static ILogger Logger => _logger;

    private static async Task<TcpClient> ConnectAsync(string host = "192.168.1.1", int port = 6000, int timeoutMs = 2000)
    {
        Logger.LogDebug($"[FW] ConnectAsync: connecting to {host}:{port} with timeout {timeoutMs}ms");

        var client = new TcpClient();
        client.ReceiveTimeout = timeoutMs;
        client.SendTimeout = timeoutMs;

        Logger.LogDebug($"[FW] ConnectAsync: TcpClient created, calling ConnectAsync...");
        await client.ConnectAsync(host, port);

        Logger.LogDebug($"[FW] ConnectAsync: connection established successfully");
        Logger.LogDebug($"[FW] ConnectAsync: client.Connected={client.Connected}");

        return client;
    }

    private static async Task<string?> SendAsync(TcpClient client, byte[] data, int recvSize = 64, int retries = 5)
    {
        var dataStr = Encoding.UTF8.GetString(data);
        Logger.LogDebug($"[FW] SendAsync: sending '{dataStr}' ({data.Length} bytes), recvSize={recvSize}, retries={retries}");

        try
        {
            var stream = client.GetStream();
            Logger.LogDebug($"[FW] SendAsync: got stream, writing data...");
            await stream.WriteAsync(data);
            Logger.LogDebug($"[FW] SendAsync: data written, starting read loop...");

            for (int i = 0; i < retries; i++)
            {
                Logger.LogDebug($"[FW] SendAsync: retry {i + 1}/{retries}");
                try
                {
                    var buffer = new byte[recvSize];
                    Logger.LogDebug($"[FW] SendAsync: calling ReadAsync...");
                    var bytesRead = await stream.ReadAsync(buffer);
                    Logger.LogDebug($"[FW] SendAsync: read {bytesRead} bytes");

                    if (bytesRead > 0)
                    {
                        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Logger.LogDebug($"[FW] SendAsync: received '{response}'");
                        return response;
                    }
                    else
                    {
                        Logger.LogDebug($"[FW] SendAsync: no data received (0 bytes)");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"[FW] SendAsync: retry {i + 1} failed: {ex.GetType().Name}: {ex.Message}");
                    // Timeout, continue retrying
                }
            }
            Logger.LogWarning($"[FW] SendAsync: all {retries} retries exhausted, returning null");
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FW] SendAsync error: {ex.GetType().Name}: {ex.Message}");
            Logger.LogError($"[FW] SendAsync stack trace: {ex.StackTrace}");
            return null;
        }
    }

    private static async Task<bool> SendFileAsync(TcpClient client, string pathOnDevice, Stream stream)
    {
        // 534KB
        const int MaxSize = 1024 * 1024; // 1MB
        // 與裝置端一致
        const int ChunkSize = 1024;
        // 握手/回覆等待（5秒）
        const int StartTimeoutMs = 5000;
        // 傳輸階段超時（30秒/每次sendall）
        const int TransferTimeoutMs = 30000;
        // 等待 "OK!" 總秒數（每秒輪詢一次）
        const int OkWaitSeconds = 180;

        Logger.LogDebug($"[FW] SendFileAsync: starting file transfer to '{pathOnDevice}'");

        try
        {
            // Calculate size and check limit
            long size = stream.Length;
            Logger.LogDebug($"[FW] SendFileAsync: file size = {size} bytes");

            if (size > MaxSize)
            {
                Logger.LogError($"[FW] file too large: {size} > {MaxSize}");
                return false;
            }

            var networkStream = client.GetStream();
            Logger.LogDebug($"[FW] SendFileAsync: got network stream");

            // 1) Start file transfer + path + file length
            Logger.LogDebug($"[FW] SendFileAsync: setting timeouts to {StartTimeoutMs}ms");
            client.ReceiveTimeout = StartTimeoutMs;
            client.SendTimeout = StartTimeoutMs;

            Logger.LogDebug($"[FW] SendFileAsync: sending 'sendFile' command");
            var resp = await SendAsync(client, Encoding.UTF8.GetBytes("sendFile"));
            Logger.LogDebug($"[FW] sendFile init resp: {resp}");
            if (resp == null)
            {
                Logger.LogError($"[FW] SendFileAsync: no response to 'sendFile' command");
                return false;
            }

            Logger.LogDebug($"[FW] SendFileAsync: sending path '{pathOnDevice}'");
            await networkStream.WriteAsync(Encoding.UTF8.GetBytes(pathOnDevice));

            Logger.LogDebug($"[FW] SendFileAsync: sending size '{size}'");
            var ack1 = await SendAsync(client, Encoding.UTF8.GetBytes(size.ToString()));
            Logger.LogDebug($"[FW] send size resp: {ack1}");
            if (ack1 == null)
            {
                Logger.LogError($"[FW] SendFileAsync: no response to size command");
                return false;
            }

            // 2) Transfer data
            Logger.LogDebug($"[FW] SendFileAsync: starting data transfer, setting timeouts to {TransferTimeoutMs}ms");
            client.ReceiveTimeout = TransferTimeoutMs;
            client.SendTimeout = TransferTimeoutMs;

            long sent = 0;
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[ChunkSize];
            Logger.LogDebug($"[FW] SendFileAsync: stream seeked to beginning, chunk size = {ChunkSize}");

            int chunkCount = 0;
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0)
                {
                    Logger.LogDebug($"[FW] SendFileAsync: end of stream reached");
                    break;
                }

                chunkCount++;
                Logger.LogDebug($"[FW] SendFileAsync: chunk {chunkCount}, read {bytesRead} bytes, writing to network...");
                await networkStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                sent += bytesRead;

                if (chunkCount % 100 == 0) // Log every 100 chunks to avoid spam
                {
                    Logger.LogDebug($"[FW] SendFileAsync: progress - sent {sent}/{size} bytes ({sent * 100 / size}%)");
                }
            }
            Logger.LogDebug($"[FW] SendFileAsync: data transfer complete - sent {sent} bytes in {chunkCount} chunks");

            // 3) Wait for final "OK!"
            Logger.LogDebug($"[FW] SendFileAsync: waiting for 'OK!' response, timeout = 5000ms, max wait = {OkWaitSeconds} seconds");
            client.ReceiveTimeout = 5000;
            for (int i = 0; i < OkWaitSeconds; i++)
            {
                Logger.LogDebug($"[FW] SendFileAsync: waiting for OK, attempt {i + 1}/{OkWaitSeconds}");
                try
                {
                    var responseBuffer = new byte[256];
                    Logger.LogDebug($"[FW] SendFileAsync: calling ReadAsync for OK response...");
                    var bytesRead = await networkStream.ReadAsync(responseBuffer);
                    Logger.LogDebug($"[FW] SendFileAsync: read {bytesRead} bytes for OK response");

                    if (bytesRead > 0)
                    {
                        var msg = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead).Trim();
                        Logger.LogDebug($"[FW] SendFileAsync: received response: '{msg}'");
                        if (msg == "OK!")
                        {
                            Logger.LogDebug($"[FW] SendFileAsync: received 'OK!' - file transfer successful");
                            return true;
                        }
                        else
                        {
                            Logger.LogDebug($"[FW] SendFileAsync: received '{msg}' instead of 'OK!', continuing to wait");
                        }
                    }
                    else
                    {
                        Logger.LogDebug($"[FW] SendFileAsync: no data received while waiting for OK");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"[FW] SendFileAsync: exception while waiting for OK (attempt {i + 1}): {ex.GetType().Name}: {ex.Message}");
                    // Timeout, continue waiting
                }
                Logger.LogDebug($"[FW] SendFileAsync: waiting 1 second before next attempt...");
                await Task.Delay(1000);
            }
            Logger.LogError($"[FW] SendFileAsync: wait OK! timeout after {OkWaitSeconds} seconds");
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FW] send_file error: {ex.Message}");
            return false;
        }
        finally
        {
            Logger.LogDebug($"[FW] SendFileAsync: entering finally block - draining remaining responses");
            // Drain any remaining responses
            // 清殘留，避免下一階段失步
            try
            {
                // 清空殘留回應，避免下一命令失步
                // Store original timeout to restore later
                var originalTimeout = client.ReceiveTimeout;
                client.ReceiveTimeout = 200; // 200ms timeout for draining
                var networkStream = client.GetStream();
                var drainBuffer = new byte[256];
                int drainCount = 0;

                while (true)
                {
                    try
                    {
                        Logger.LogDebug($"[FW] SendFileAsync: draining response {drainCount + 1}");

                        // Use synchronous Read with timeout (like Python implementation)
                        // This respects the ReceiveTimeout property unlike ReadAsync
                        var bytesRead = networkStream.Read(drainBuffer, 0, drainBuffer.Length);
                        if (bytesRead == 0)
                        {
                            Logger.LogDebug($"[FW] SendFileAsync: drain complete - no more data");
                            break;
                        }
                        drainCount++;
                        var drainedData = Encoding.UTF8.GetString(drainBuffer, 0, bytesRead);
                        Logger.LogDebug($"[FW] SendFileAsync: drained {bytesRead} bytes: '{drainedData}'");
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException socketEx &&
                                                 socketEx.SocketErrorCode == SocketError.TimedOut)
                    {
                        // Timeout occurred - this is expected and means no more data to drain
                        Logger.LogDebug($"[FW] SendFileAsync: drain timeout - no more data available");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug($"[FW] SendFileAsync: drain exception: {ex.GetType().Name}: {ex.Message}");
                        break;
                    }
                }
                Logger.LogDebug($"[FW] SendFileAsync: drained {drainCount} responses");

                // Restore original timeout
                try
                {
                    client.ReceiveTimeout = originalTimeout;
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"[FW] SendFileAsync: failed to restore timeout: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug($"[FW] SendFileAsync: drain setup exception: {ex.GetType().Name}: {ex.Message}");
                // Ignore drain errors
            }

            Logger.LogDebug($"[FW] SendFileAsync: adding processing delay");
            await Task.Delay(500); // Give device some processing time
            Logger.LogDebug($"[FW] SendFileAsync: finally block complete");
        }
    }

    public static async Task FirmwareUpgradeBeforeCheckAsync(
        string host = "192.168.1.1",
        int port = 6000,
        int mBleVerNo = 14,
        int mIteVerNo = 35,
        int mBspVerNo = 1702061,
        string bleBinPath = "ble_new.bin",
        string iteBinPath = "ite_new.bin",
        string bspBinPath = "pixer.bin")
    {
        // check firmware file is exists
        if (!File.Exists(bleBinPath) || !File.Exists(iteBinPath) || !File.Exists(bspBinPath))
        {
            Logger.LogInformation("Firmware files not found. Please place ble.bin, ite.bin, and pixer.bin in the same directory as the executable.");
            return;
        }

        Logger.LogInformation("Firmware files found. Proceeding with firmware upgrade.");


        //bool askCharge = false;
        bool reboot = false;
        bool updateOta = false;

        TcpClient? client = null;
        Logger.LogInformation($"[FW] Starting firmware upgrade process");
        Logger.LogDebug($"[FW] Target versions - BLE: {mBleVerNo}, ITE: {mIteVerNo}, BSP: {mBspVerNo}");
        Logger.LogDebug($"[FW] Binary files - BLE: {bleBinPath}, ITE: {iteBinPath}, BSP: {bspBinPath}");

        try
        {
            Logger.LogDebug("[FW] Attempting to connect...");
            client = await ConnectAsync(host, port, 2000);
            Logger.LogInformation("[FW] Connection established successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FW] Connection failed: {ex.GetType().Name}: {ex.Message}");
            Logger.LogError($"[FW] Connection stack trace: {ex.StackTrace}");
            return;
        }

        try
        {
            // Initial handshake
            Logger.LogDebug("[FW] Starting initial handshake with #TEST# command");
            var hello = await SendAsync(client, Encoding.UTF8.GetBytes("#TEST#"));
            Logger.LogDebug($"[FW] Handshake response: '{hello}'");

            if (hello == null)
            {
                Logger.LogError("[FW] No response to #TEST# command - device may not be responding");
                return;
            }
            else if (hello == "Hello PC!")
            {
                Logger.LogDebug("[FW] Received expected 'Hello PC!' response");
            }
            else if (hello.Length > 9)
            {
                // Consistent with App behavior: if response is mixed, continue with subsequent queries
                Logger.LogDebug("[FW] Received mixed/long response, continuing with version queries");
            }
            else
            {
                Logger.LogWarning($"[FW] Unexpected handshake response: '{hello}', continuing anyway");
            }

            // Read versions and battery
            Logger.LogDebug("[FW] Querying device versions and battery level...");
            var bleVersion = await SendAsync(client, Encoding.UTF8.GetBytes("bleVersion")) ?? "0.0.0";
            var iteVersion = await SendAsync(client, Encoding.UTF8.GetBytes("iteVersion")) ?? "0.0.0";
            var mcuVersion = await SendAsync(client, Encoding.UTF8.GetBytes("mcuVersion")) ?? "0_0000-00-00_0";
            var batteryString = await SendAsync(client, Encoding.UTF8.GetBytes("batteryLevel")) ?? "0";

            Logger.LogInformation($"[FW] Device info - BLE: {bleVersion}, ITE: {iteVersion}, MCU: {mcuVersion}, Battery: {batteryString}%");

            // Parse versions
            Logger.LogDebug("[FW] Parsing version numbers...");
            int bleNo = 0, iteNo = 0, bspNo = 0, battery = 0;

            try
            {
                var bleParts = bleVersion.Split('.');
                Logger.LogDebug($"[FW] BLE version parts: [{string.Join(", ", bleParts)}]");
                if (bleParts.Length >= 3)
                {
                    bleNo = int.Parse(bleParts[2]);
                    Logger.LogDebug($"[FW] Parsed BLE version number: {bleNo}");
                }
                else
                {
                    Logger.LogWarning($"[FW] BLE version format unexpected: {bleVersion}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[FW] Failed to parse BLE version '{bleVersion}': {ex.Message}");
            }

            try
            {
                var iteParts = iteVersion.Split('.');
                Logger.LogDebug($"[FW] ITE version parts: [{string.Join(", ", iteParts)}]");
                if (iteParts.Length >= 3)
                {
                    iteNo = int.Parse(iteParts[2]);
                    Logger.LogDebug($"[FW] Parsed ITE version number: {iteNo}");
                }
                else
                {
                    Logger.LogWarning($"[FW] ITE version format unexpected: {iteVersion}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[FW] Failed to parse ITE version '{iteVersion}': {ex.Message}");
            }

            try
            {
                var mcuParts = mcuVersion.Split('_');
                Logger.LogDebug($"[FW] MCU version parts: [{string.Join(", ", mcuParts)}]");
                if (mcuParts.Length >= 2)
                {
                    var bspString = mcuParts[1].Replace("-", "");
                    bspNo = int.Parse(bspString);
                    Logger.LogDebug($"[FW] Parsed BSP version number: {bspNo} (from '{bspString}')");
                }
                else
                {
                    Logger.LogWarning($"[FW] MCU version format unexpected: {mcuVersion}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[FW] Failed to parse MCU version '{mcuVersion}': {ex.Message}");
            }

            try
            {
                battery = int.Parse(batteryString);
                Logger.LogDebug($"[FW] Parsed battery level: {battery}%");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[FW] Failed to parse battery level '{batteryString}': {ex.Message}");
            }

            Logger.LogInformation($"[FW] Parsed versions - BLE: {bleNo}, ITE: {iteNo}, BSP: {bspNo}, Battery: {battery}%");

            // Battery threshold check
            if (battery <= 15)
            {
                //askCharge = true;
                Logger.LogWarning($"[FW] Battery level too low ({battery}% <= 15%) - skipping upgrade for safety");
                return;
            }
            else
            {
                Logger.LogDebug($"[FW] Battery level OK ({battery}% > 15%) - proceeding with upgrade");
            }

            // ITE upgrade
            Logger.LogDebug($"[FW] Checking ITE upgrade: current={iteNo}, target={mIteVerNo}, file exists={File.Exists(iteBinPath)}");
            if (iteNo < mIteVerNo && File.Exists(iteBinPath))
            {
                Logger.LogInformation($"[FW] ITE upgrade needed: {iteNo} -> {mIteVerNo}, using file: {iteBinPath}");
                using var fileStream = File.OpenRead(iteBinPath);
                Logger.LogDebug($"[FW] ITE file opened, size: {fileStream.Length} bytes");

                if (await SendFileAsync(client, "ite_new.bin", fileStream))
                {
                    Logger.LogInformation("[FW] ITE file transfer successful");
                    if (bspNo >= 1700000)
                    {
                        Logger.LogDebug("[FW] BSP >= 1700000, querying new ITE version");
                        iteVersion = await SendAsync(client, Encoding.UTF8.GetBytes("iteversion")) ?? iteVersion;
                        Logger.LogInformation($"[FW] ITE updated to version: {iteVersion}");
                    }
                    else
                    {
                        Logger.LogDebug("[FW] BSP < 1700000, marking for reboot and OTA");
                        reboot = true;
                        updateOta = true;
                    }
                }
                else
                {
                    Logger.LogError("[FW] ITE file transfer failed");
                }
            }
            else if (iteNo >= mIteVerNo)
            {
                Logger.LogDebug($"[FW] ITE upgrade not needed: current version {iteNo} >= target {mIteVerNo}");
            }
            else if (!File.Exists(iteBinPath))
            {
                Logger.LogWarning($"[FW] ITE upgrade needed but file not found: {iteBinPath}");
            }

            // BLE upgrade
            Logger.LogDebug($"[FW] Checking BLE upgrade: current={bleNo}, target={mBleVerNo}, file exists={File.Exists(bleBinPath)}");
            if (bleNo < mBleVerNo && File.Exists(bleBinPath))
            {
                Logger.LogInformation($"[FW] BLE upgrade needed: {bleNo} -> {mBleVerNo}, using file: {bleBinPath}");
                using var fileStream = File.OpenRead(bleBinPath);
                Logger.LogDebug($"[FW] BLE file opened, size: {fileStream.Length} bytes");

                if (await SendFileAsync(client, "ble_new.bin", fileStream))
                {
                    Logger.LogInformation("[FW] BLE file transfer successful");
                    if (bspNo >= 1700000)
                    {
                        Logger.LogDebug("[FW] BSP >= 1700000, querying new BLE version");
                        bleVersion = await SendAsync(client, Encoding.UTF8.GetBytes("bleversion")) ?? bleVersion;
                        Logger.LogInformation($"[FW] BLE updated to version: {bleVersion}");
                    }
                    else
                    {
                        Logger.LogDebug("[FW] BSP < 1700000, marking for reboot and OTA");
                        reboot = true;
                        updateOta = true;
                    }
                }
                else
                {
                    Logger.LogError("[FW] BLE file transfer failed");
                }
            }
            else if (bleNo >= mBleVerNo)
            {
                Logger.LogDebug($"[FW] BLE upgrade not needed: current version {bleNo} >= target {mBleVerNo}");
            }
            else if (!File.Exists(bleBinPath))
            {
                Logger.LogWarning($"[FW] BLE upgrade needed but file not found: {bleBinPath}");
            }

            // BSP upgrade
            Logger.LogDebug($"[FW] Checking BSP upgrade: current={bspNo}, target={mBspVerNo}, file exists={File.Exists(bspBinPath)}");
            if (bspNo < mBspVerNo && File.Exists(bspBinPath))
            {
                Logger.LogInformation($"[FW] BSP upgrade needed: {bspNo} -> {mBspVerNo}, using file: {bspBinPath}");

                Logger.LogDebug("[FW] Querying mcuImage to determine target path");
                var mcuImgResp = await SendAsync(client, Encoding.UTF8.GetBytes("mcuImage")) ?? "0";
                Logger.LogDebug($"[FW] mcuImage response: '{mcuImgResp}'");

                var targetPath = (bspNo > 1700000 && mcuImgResp.Trim() == "1") ? "/sys/mcuimg3.bin" : "/sys/mcuimg2.bin";
                Logger.LogDebug($"[FW] BSP target path determined: {targetPath} (bspNo={bspNo}, mcuImgResp='{mcuImgResp.Trim()}')");

                using var fileStream = File.OpenRead(bspBinPath);
                Logger.LogDebug($"[FW] BSP file opened, size: {fileStream.Length} bytes");

                if (await SendFileAsync(client, targetPath, fileStream))
                {
                    Logger.LogInformation("[FW] BSP file transfer successful, marking for reboot");
                    reboot = true;
                }
                else
                {
                    Logger.LogError("[FW] BSP file transfer failed");
                }
            }
            else if (bspNo >= mBspVerNo)
            {
                Logger.LogDebug($"[FW] BSP upgrade not needed: current version {bspNo} >= target {mBspVerNo}");
            }
            else if (!File.Exists(bspBinPath))
            {
                Logger.LogWarning($"[FW] BSP upgrade needed but file not found: {bspBinPath}");
            }

            // Need OTA info
            if (updateOta)
            {
                Logger.LogInformation("[FW] Sending OTA info file (updateOta=true)");
                using var otaStream = new MemoryStream(new byte[] { 1, 0, 0, 0 });
                Logger.LogDebug("[FW] OTA info stream created with 4 bytes: [1, 0, 0, 0]");

                if (!await SendFileAsync(client, "ota_info.bin", otaStream))
                {
                    Logger.LogError("[FW] OTA info file transfer failed");
                }
                else
                {
                    Logger.LogDebug("[FW] OTA info file transfer successful");
                }
            }
            else
            {
                Logger.LogDebug("[FW] No OTA info needed (updateOta=false)");
            }

            // Final control
            Logger.LogDebug($"[FW] Final control: reboot={reboot}, bspNo={bspNo}");
            if (reboot)
            {
                if (bspNo < 1700000)
                {
                    Logger.LogInformation("[FW] Sending 'off' command (BSP < 1700000)");
                    await SendAsync(client, Encoding.UTF8.GetBytes("off"));
                    Logger.LogDebug("[FW] 'off' command sent");
                }
                else
                {
                    Logger.LogInformation("[FW] Sending 'reset' command (BSP >= 1700000)");
                    await SendAsync(client, Encoding.UTF8.GetBytes("reset"));
                    Logger.LogDebug("[FW] 'reset' command sent");
                }
            }
            else
            {
                Logger.LogDebug("[FW] No reboot needed");
            }

            Logger.LogInformation("[FW] Firmware upgrade process completed");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[FW] Upgrade process exception: {ex.GetType().Name}: {ex.Message}");
            Logger.LogError($"[FW] Exception stack trace: {ex.StackTrace}");
        }
        finally
        {
            Logger.LogDebug("[FW] Closing client connection");
            client?.Close();
            Logger.LogDebug("[FW] Client connection closed");
        }
    }
}
