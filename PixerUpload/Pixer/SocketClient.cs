using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PixerUpload;

public class SocketClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger<SocketClient> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    public SocketClient(string host = "192.168.1.1", int port = 6000, ILogger<SocketClient>? logger = null)
    {
        _host = host;
        _port = port;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SocketClient>.Instance;
    }

    public async Task<bool> ConnectAsync()
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = 2000;
                _tcpClient.SendTimeout = 2000;

                await _tcpClient.ConnectAsync(_host, _port);
                _stream = _tcpClient.GetStream();

                _logger.LogDebug("Connected successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Connection attempt {i + 1} failed: {ex.Message}");
                _tcpClient?.Close();
                _tcpClient = null;
                _stream = null;

                if (i < 9) // Don't wait after the last attempt
                {
                    await Task.Delay(2000);
                }
            }
        }

        _logger.LogError("Failed to connect after 10 attempts");
        return false;
    }

    public async Task<string?> SendAsync(byte[] data)
    {
        if (_stream == null)
        {
            _logger.LogError("Not connected");
            return null;
        }

        try
        {
            await _stream.WriteAsync(data);
            await _stream.FlushAsync();

            // Try to read response with retries
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var buffer = new byte[64];
                    var bytesRead = await _stream.ReadAsync(buffer.AsMemory(0, 64));

                    if (bytesRead > 0)
                    {
                        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is IOException)
                {
                    _logger.LogDebug($"Timeout, retrying... {i + 1}");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in send: {ex.Message}");
            return null;
        }
    }

    public async Task UploadAsync(byte[] data)
    {
        if (_stream == null)
        {
            _logger.LogError("Not connected");
            return;
        }

        try
        {
            _tcpClient!.ReceiveTimeout = 10000;
            _tcpClient.SendTimeout = 10000;

            const int chunkSize = 4096;
            int offset = 0;

            while (offset < data.Length)
            {
                int currentChunkSize = Math.Min(chunkSize, data.Length - offset);
                var chunk = data.AsSpan(offset, currentChunkSize);

                await _stream.WriteAsync(chunk.ToArray());
                offset += currentChunkSize;

                int progress = offset * 100 / data.Length;
                Console.Write($"\rprogress: {progress}%");
            }

            Console.WriteLine();

            // Send tail command
            string tail = "#MOVE#d";
            byte[] tailBytes = Encoding.UTF8.GetBytes(tail);
            await _stream.WriteAsync(tailBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in upload: {ex.Message}");
        }
    }

    public void Close()
    {
        _stream?.Close();
        _tcpClient?.Close();
        _stream = null;
        _tcpClient = null;
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }
}
