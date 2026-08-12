using System.IO;
using NAudio.Wave;

namespace CrewChiefRUAssistant.Responses;

public sealed class VoiceBankPlayer : IDisposable
{
    private readonly string _bankDirectory;
    private readonly int _deviceNumber;
    private readonly float _volume;
    private readonly object _sync = new();

    private WaveOutEvent? _output;
    private WaveStream? _reader;
    private MemoryStream? _audioMemory;

    public VoiceBankPlayer(
        string bankDirectory,
        int deviceNumber,
        float volume)
    {
        _bankDirectory = bankDirectory;
        _deviceNumber = deviceNumber;
        _volume = Math.Clamp(volume, 0f, 1f);
    }

    public bool IsPlaying
    {
        get
        {
            lock (_sync)
            {
                try
                {
                    return _output?.PlaybackState ==
                           PlaybackState.Playing;
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    public bool IsReady =>
        File.Exists(
            Path.Combine(
                _bankDirectory,
                "READY.json")) &&
        File.Exists(
            Path.Combine(
                _bankDirectory,
                "phrases",
                "unknown.wav")) &&
        File.Exists(
            Path.Combine(
                _bankDirectory,
                "numbers",
                "0.wav")) &&
        File.Exists(
            Path.Combine(
                _bankDirectory,
                "radio",
                "open.wav")) &&
        File.Exists(
            Path.Combine(
                _bankDirectory,
                "radio",
                "close.wav"));

    public void Play(
        IReadOnlyList<string> tokens)
    {
        if (!IsReady ||
            tokens.Count == 0)
        {
            return;
        }

        var files = new List<string>(
            tokens.Count + 2)
        {
            Path.Combine(
                _bankDirectory,
                "radio",
                "open.wav")
        };

        foreach (var token in tokens)
        {
            var path = Path.Combine(
                _bankDirectory,
                token.Replace(
                    '/',
                    Path.DirectorySeparatorChar) +
                ".wav");

            if (File.Exists(path))
                files.Add(path);
        }

        files.Add(
            Path.Combine(
                _bankDirectory,
                "radio",
                "close.wav"));

        if (files.Count <= 2)
            return;

        var combinedWave =
            CreateCombinedWave(files);

        Stop();

        lock (_sync)
        {
            _audioMemory =
                new MemoryStream(
                    combinedWave,
                    writable: false);

            _reader =
                new WaveFileReader(
                    _audioMemory);

            _output =
                new WaveOutEvent
                {
                    DeviceNumber = _deviceNumber,
                    Volume = _volume,
                    DesiredLatency = 80,
                    NumberOfBuffers = 3
                };

            _output.Init(_reader);
            _output.Play();
        }
    }

    private static byte[] CreateCombinedWave(
        IReadOnlyList<string> files)
    {
        using var first =
            new WaveFileReader(files[0]);

        var format = first.WaveFormat;

        using var memory =
            new MemoryStream();

        using (var writer =
               new WaveFileWriter(
                   memory,
                   format))
        {
            CopyReader(first, writer);
            WriteSilence(
                writer,
                format,
                12);

            for (var index = 1;
                 index < files.Count;
                 index++)
            {
                using var reader =
                    new WaveFileReader(
                        files[index]);

                if (!FormatsMatch(
                        format,
                        reader.WaveFormat))
                {
                    throw new InvalidOperationException(
                        $"Формат голосового фрагмента отличается: {files[index]}");
                }

                CopyReader(
                    reader,
                    writer);

                if (index < files.Count - 1)
                {
                    WriteSilence(
                        writer,
                        format,
                        12);
                }
            }
        }

        return memory.ToArray();
    }

    private static bool FormatsMatch(
        WaveFormat left,
        WaveFormat right) =>
        left.SampleRate == right.SampleRate &&
        left.Channels == right.Channels &&
        left.BitsPerSample ==
        right.BitsPerSample &&
        left.Encoding == right.Encoding;

    private static void CopyReader(
        WaveFileReader reader,
        WaveFileWriter writer)
    {
        var buffer = new byte[32768];
        int read;

        while ((read = reader.Read(
                   buffer,
                   0,
                   buffer.Length)) > 0)
        {
            writer.Write(
                buffer,
                0,
                read);
        }
    }

    private static void WriteSilence(
        WaveFileWriter writer,
        WaveFormat format,
        int milliseconds)
    {
        var byteCount =
            format.AverageBytesPerSecond *
            milliseconds /
            1000;

        byteCount -=
            byteCount %
            Math.Max(
                1,
                format.BlockAlign);

        if (byteCount <= 0)
            return;

        writer.Write(
            new byte[byteCount],
            0,
            byteCount);
    }

    public void Stop()
    {
        WaveOutEvent? output;
        WaveStream? reader;
        MemoryStream? audioMemory;

        lock (_sync)
        {
            output = _output;
            reader = _reader;
            audioMemory = _audioMemory;

            _output = null;
            _reader = null;
            _audioMemory = null;
        }

        try
        {
            output?.Stop();
        }
        catch
        {
            // The selected output device may have disappeared.
        }

        output?.Dispose();
        reader?.Dispose();
        audioMemory?.Dispose();
    }

    public void Dispose()
    {
        Stop();
    }
}
