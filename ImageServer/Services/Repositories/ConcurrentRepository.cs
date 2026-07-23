using ImageServer.Abstractions;
using System.Collections.Concurrent;

namespace ImageServer.Services.Repositories
{
    public class ConcurrentRepository : IStorage
    {
        private readonly IStorage _innerStorage;

        private readonly ConcurrentDictionary<string, SemaphoreRefWrapper> _semaphoreFileLockers = new();

        private readonly Lock _locker = new();

        #region Обёртка над стримом, освобождающая семафор при закрытии 

        private sealed class LockReleaserStream : Stream
        {
            private readonly Stream _innerStream;

            private readonly IDisposable _lockReleaser;

            private bool _disposed;

            public override bool CanRead => _innerStream.CanRead;

            public override bool CanSeek => _innerStream.CanSeek;

            public override bool CanWrite => _innerStream.CanWrite;

            public override long Length => _innerStream.Length;

            public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

            public LockReleaserStream(Stream innerStream, IDisposable lockReleaser)
            {
                _innerStream = innerStream;
                _lockReleaser = lockReleaser;
            }

            public override void Flush() => _innerStream.Flush();

            public override int Read(byte[] buffer, int offset, int count) => 
                _innerStream.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => 
                _innerStream.Seek(offset, origin);

            public override void SetLength(long value) =>
                _innerStream.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) => 
                _innerStream.Write(buffer, offset, count);

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                await _innerStream.ReadAsync(buffer, cancellationToken);

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);

            public override async Task FlushAsync(CancellationToken cancellationToken) =>
                await _innerStream.FlushAsync(cancellationToken);

            protected override void Dispose(bool disposing)
            {
                if(!_disposed && disposing)
                {
                    _disposed = true;
                    try
                    {
                        _innerStream.Dispose();
                    }
                    finally
                    {
                        _lockReleaser.Dispose();
                    }
                }
                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                if(!_disposed)
                {
                    _disposed = true;
                    try
                    {
                        await _innerStream.DisposeAsync();
                    }
                    finally
                    {
                        _lockReleaser.Dispose();
                    }
                }
            }
        }

        #endregion

        #region Обёртка над семафором, которая хранит количество ссылок на него

        private sealed class SemaphoreRefWrapper
        {
            public readonly SemaphoreSlim Semaphore = new (1,1);

            public int RefCount;
        }

        #endregion

        #region Обёртка над семафором, которая освобождает его при закрытии и удаляет из словаря, если ссылок на него больше нет

        private sealed class SemaphoreReleaser : IDisposable
        {
            private readonly SemaphoreRefWrapper _semaphoreWrapper;

            private readonly ConcurrentDictionary<string, SemaphoreRefWrapper> _semaphoreFileLockers;

            private readonly string _fileId;

            private readonly Lock _locker;

            private bool _disposed;
            public SemaphoreReleaser(SemaphoreRefWrapper semaphoreWrapper,
                ConcurrentDictionary<string, SemaphoreRefWrapper> semaphoreFileLockers,
                Lock locker,
                string fileId)
            {
                _semaphoreWrapper = semaphoreWrapper;

                _semaphoreFileLockers = semaphoreFileLockers;

                _locker = locker;

                _fileId = fileId;
            }
            public void Dispose()
            {
                if(_disposed) return;
                _disposed = true;

                _semaphoreWrapper.Semaphore.Release();

                lock (_locker)
                {
                    if (--_semaphoreWrapper.RefCount == 0) _semaphoreFileLockers.TryRemove(_fileId, out _);
                }
            }
        }

        #endregion

        public ConcurrentRepository(IStorage storage) => _innerStorage = storage;

        private async Task<IDisposable> AcquireFileLockAsync(string fileName, CancellationToken ct = default)
        {
            SemaphoreRefWrapper semaphore;

            lock (_locker)
            {
                semaphore = _semaphoreFileLockers.GetOrAdd(fileName, _ => new SemaphoreRefWrapper());
                semaphore.RefCount++;
            }

            try
            {
                await semaphore.Semaphore.WaitAsync(ct);
            }
            catch
            {
                lock (_locker)
                {
                    if (--semaphore.RefCount == 0) _semaphoreFileLockers.TryRemove(fileName, out _);
                }
                throw;
            }

            return new SemaphoreReleaser(semaphore, _semaphoreFileLockers, _locker, fileName);
        }

        public async Task<Stream> GetFileAsync(string fileName,
            string relativePath,
            CancellationToken ct = default)
        {
            var releaser = await AcquireFileLockAsync(fileName, ct);

            try
            {
                var stream = await _innerStorage.GetFileAsync(fileName, relativePath, ct);
                return new LockReleaserStream(stream, releaser);
            }
            catch
            {
                releaser.Dispose();
                throw;
            }
        }

        public async Task SaveFileAsync(Stream stream,
            string fileName,
            string relativePath,
            CancellationToken ct = default)
        {
            using var releaser = await AcquireFileLockAsync(fileName, ct);

            await _innerStorage.SaveFileAsync(stream, fileName, relativePath, ct);
        }

        public async Task DeleteFileAsync(string fileName,
            string relativePath, 
            CancellationToken ct = default)
        {
            using var releaser = await AcquireFileLockAsync(fileName, ct);

            await _innerStorage.DeleteFileAsync(fileName, relativePath, ct);
        }

        public async Task ExecuteAsync(string fileName,
            Func<IStorage, Task> operation, 
            CancellationToken ct = default)
        {
            using var releaser = await AcquireFileLockAsync(fileName, ct);

            await operation(_innerStorage);
        }
    }
}
