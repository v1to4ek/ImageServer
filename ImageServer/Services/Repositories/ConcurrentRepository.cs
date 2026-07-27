using ImageServer.Abstractions;
using System.Collections.Concurrent;

namespace ImageServer.Services.Repositories
{
    /// <summary>
    /// Потокобезопасный декоратор над основным классом доступа к файловой системе.
    /// Метды класса добавляют дополнительный функционал для потокобезопасного доступа к файлам, не модифицируя методы основого класса.
    /// Основной класс передаётся в _innerStorage.
    /// Класс хранит потокобезопасный словарь с объектами с семафорами для каждого отдельного fileId,
    /// для реализации асинхронного блокирования не ко всей ФС целиком, а к отдельному файлу на один поток.
    /// <para> Принцип работы: </para>
    /// <para> 1) При вызове метода для взаимодействия с файлом поток попадает в метод AcquireFileLockAsync. </para>
    /// <para> 2) Поток создаёт или получает из словаря объект с семафором и ссылками на этот семаор(ссылки нужны для последующего уничтожения, если ссылок не будет) по ключу-имени файла, 
    ///   (это нужно, чтобы привязать семафоры к файлам: то есть семафор блокирует один конуреный файл, а не всю ФС) для прохода к критической секции. </para>
    /// <para> 3) Поток проходит WaitAsync дальше, либо асинхронно освобождается на время ожидания. </para>
    /// <para> 4) После прохода WaitAsync, поток получает объект токена SemaphoreReleaserToken для освобождения семафора, токен содержит в себе ссылку на словарь с семафорами и ключ-имя файла
    ///   чтобы удалять объекты семафоры из словаря по этому ключу, если на семафор больше нет ссылок. Данные действия выполняются в методе dispose, так как токен реализует
    ///   IDisposable, чтобы все эти дейтсвия происходили после прохода метода основного класса.</para>
    /// </summary>
    public class ConcurrentRepository : IStorage
    {
        private readonly IStorage _innerStorage;

        private readonly ConcurrentDictionary<string, SemaphoreRefWrapper> _semaphoreFileLockers = new();

        private readonly Lock _locker = new();

        #region Декоратор над стримом, освобождающая объект с семафором при закрытии 

        /// <summary>
        /// Декоратор над стримом, имеющий в себе IDisposable токен для открытия семафора при закрытии стрима.
        /// При закрытии стрима и вызове у него dispose, вызывается dispose и у токена, что приводит к освобождению семафора.
        /// </summary>
        private sealed class LockReleaserStream : Stream
        {
            private readonly Stream _innerStream;

            private readonly IDisposable _releaseToken;

            private bool _disposed;

            public override bool CanRead => _innerStream.CanRead;

            public override bool CanSeek => _innerStream.CanSeek;

            public override bool CanWrite => _innerStream.CanWrite;

            public override long Length => _innerStream.Length;

            public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }

            public LockReleaserStream(Stream innerStream, IDisposable lockReleaser)
            {
                _innerStream = innerStream;
                _releaseToken = lockReleaser;
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
                        _releaseToken.Dispose();
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
                        _releaseToken.Dispose();
                    }
                }
            }
        }

        #endregion

        #region Обёртка над семафором, которая хранит количество ссылок на него

        /// <summary>
        /// Обёртка над SemaphoreSlim создающая новый семафор, пропускающий один поток и хранящая количетсво ссылок на него.
        /// Новый объект этого класса и ,соответственно, сам семафор создаются только для новых fileId, которых ещё нет в _semaphoreFileLockers. В противном случае инкрементируется RefCount.
        /// При вызове dispose у SemaphoreReleaserToken, в случае если у SemaphoreRefWrapper количество ссылок RefCount = 1, он удаляется из словаря, если нет, то RefCount декрементируется.
        /// Данный механизм используется только для того, чтобы постоянно не забивать словарь новыми SemaphoreRefWrapper, даже если файлы уже не используются, чтобы избежать утечки памяти.
        /// </summary>
        private sealed class SemaphoreRefWrapper
        {
            public readonly SemaphoreSlim Semaphore = new (1,1);

            public int RefCount;
        }

        #endregion

        #region Класс-токен синхронизации для освобождения семафора при вызове dispose и удаления его из словаря, если на него больше нет ссылок.

        /// <summary>
        /// Токен освобождения семафора для других потоков при вызове метода dispose у этого класса.
        /// Содержит в себе ссылку на словарь _semaphoreFileLockers из основного класса,
        /// для удаления SemaphoreRefWrapper по ключу _fileId из него в случае, если на SemaphoreRefWrapper больше нет ссылок.
        /// Также имеет сам объект семафора для его открытия при вызове метода dispose.
        /// </summary>
        private sealed class SemaphoreReleaserToken : IDisposable
        {
            private readonly SemaphoreRefWrapper _semaphoreWrapper;

            private readonly ConcurrentDictionary<string, SemaphoreRefWrapper> _semaphoreFileLockers;

            private readonly string _fileId;

            private readonly Lock _locker;

            private bool _disposed;

            public SemaphoreReleaserToken(SemaphoreRefWrapper semaphoreWrapper,
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

        /// <summary>
        /// Метод для получения токена синхронизации для доступа к конкретному файлу.
        /// <para> Получает или создаёт семафор и кладёт его в словарь с ключом fileId, инкрементируюя количество ссылок на него.</para>
        /// <para> Отпускает поток в ожидание или пропускает дальше. </para>
        /// <para> В случае отмены операции декрементирует количество ссылок на семафор, либо удаляет его из словаря. Лок нужен для избежания гонки за ресурсы в случае отмены нескольких запросов сразу</para>
        /// <para> После прохода ожидания WaitAsync даёт потоку, который его прошёл, токен на релиз семафора. Поток выходит из метода и переходит к критической секции </para>
        /// </summary>
        /// <param name="fileName">Имя файла, к которому будет предоставлен доступ</param>
        /// <param name="ct">Токен отмены операции</param>
        /// <returns>Токен синхронизации на освобождение семафора</returns>
        private async Task<IDisposable> AcquireFileLockTokenAsync(string fileName, CancellationToken ct = default)
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

            return new SemaphoreReleaserToken(semaphore, _semaphoreFileLockers, _locker, fileName);
        }

        public async Task<Stream> GetFileAsync(string fileName,
            string relativePath,
            CancellationToken ct = default)
        {
            var syncToken = await AcquireFileLockTokenAsync(fileName, ct);

            try
            {
                var stream = await _innerStorage.GetFileAsync(fileName, relativePath, ct);
                return new LockReleaserStream(stream, syncToken);
            }
            catch
            {
                syncToken.Dispose();
                throw;
            }
        }

        public async Task SaveFileAsync(Stream stream,
            string fileName,
            string relativePath,
            CancellationToken ct = default)
        {
            using var syncToken = await AcquireFileLockTokenAsync(fileName, ct);

            await _innerStorage.SaveFileAsync(stream, fileName, relativePath, ct);
        }

        public async Task DeleteFileAsync(string fileName,
            string relativePath, 
            CancellationToken ct = default)
        {
            using var syncToken = await AcquireFileLockTokenAsync(fileName, ct);

            await _innerStorage.DeleteFileAsync(fileName, relativePath, ct);
        }

        public async Task ExecuteAsync(string fileName,
            Func<IStorage, Task> operation, 
            CancellationToken ct = default)
        {
            using var syncToken = await AcquireFileLockTokenAsync(fileName, ct);

            await operation(_innerStorage);
        }
    }
}
