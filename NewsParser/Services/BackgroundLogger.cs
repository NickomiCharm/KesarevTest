using System.Text;
using System.Threading.Channels;

public class BackgroundLogger : IAsyncDisposable
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
    private readonly StreamWriter _writer;
    private readonly Task _worker;

    public BackgroundLogger(string path)
    {
        _writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
        _worker = Task.Run(ProcessQueueAsync);
    }

    // Добавляем сообщение в канал
    public void Log(string message)
    {
        string line = $"{DateTime.Now:dd-MM-yyyy, HH:mm:ss} - {message}";
        _channel.Writer.TryWrite(line);
    }

    // Фоновый метод в параллельном потоке для пополнения канала логов
    private async Task ProcessQueueAsync()
    {
        await foreach (var line in _channel.Reader.ReadAllAsync())
        {
            await _writer.WriteLineAsync(line);
        }
    }

    // Утилизация ресурсов, ожидание окончания работы канала
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _worker;
        await _writer.FlushAsync();
        _writer.Dispose();
    }
}