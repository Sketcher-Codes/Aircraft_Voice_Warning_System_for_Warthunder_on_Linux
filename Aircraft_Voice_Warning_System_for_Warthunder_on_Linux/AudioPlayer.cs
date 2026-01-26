//AI Disclaimer: This whole file was developed with VS Code's AI assistant using ChatGTP 5 mini.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Aircraft_Voice_Warning_System_for_Warthunder_on_Linux
{
    /// <summary>
    /// Precaches WAV files into RAM and plays them using the system `aplay` utility.
    /// Only one sound is played at a time; starting a new sound stops the previous one.
    /// This approach is suitable on Linux systems (ALSA) where `aplay` is available.
    /// </summary>
    public sealed class AudioPlayer : IDisposable
    {
        private readonly Dictionary<string, byte[]> _cache = new();
        private readonly object _lock = new();
        private Process? _process;
        private bool _disposed;

        /// <summary>
        /// Preloads a WAV file into memory. If already cached, returns immediately.
        /// </summary>
        /// <param name="path">Path to a WAV file.</param>
        public void Precache(string path)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            lock (_lock)
            {
                if (_cache.ContainsKey(path)) return;
                var data = File.ReadAllBytes(path);
                _cache[path] = data;
            }
        }

        /// <summary>
        /// Plays the specified WAV file. Stops any currently playing sound.
        /// Uses `aplay` and streams the cached WAV bytes to its stdin.
        /// </summary>
        /// <param name="path">Path to a WAV file.</param>
        public void Play(string path)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));

            byte[] data;
            lock (_lock)
            {
                if (!_cache.TryGetValue(path, out data))
                {
                    data = File.ReadAllBytes(path);
                    _cache[path] = data;
                }

                // Stop existing playback
                if (_process != null && !_process.HasExited)
                {
                    try { _process.Kill(); } catch { }
                    _process.Dispose();
                    _process = null;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "aplay",
                    Arguments = "-q -t wav -",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                };

                _process = new Process { StartInfo = startInfo };
                _process.Start();

                // Write data asynchronously to avoid blocking caller
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        using var stdin = _process!.StandardInput.BaseStream;
                        stdin.Write(data, 0, data.Length);
                        stdin.Flush();
                        stdin.Close();
                    }
                    catch
                    {
                        // ignore write errors
                    }
                });
            }
        }

        /// <summary>
        /// Stops any current playback immediately.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_process != null && !_process.HasExited)
                {
                    try { _process.Kill(); } catch { }
                    _process.Dispose();
                    _process = null;
                }
            }
        }

        /// <summary>
        /// Returns true if a sound is currently playing.
        /// </summary>
        public bool IsPlaying()
        {
            lock (_lock)
            {
                return _process != null && !_process.HasExited;
            }
        }

        /// <summary>
        /// Releases resources and stops playback.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            lock (_lock)
            {
                Stop();
                _cache.Clear();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
