#region using
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#endregion using

namespace MpSoft.StreamCaching
{
	public class ReadCacheStream : Stream
	{
		readonly Stream _source;
		readonly Stream _repository;
		readonly int _aheadLength, _blockSize;

		Task _aheadReadTask;
		ResetableCancellationTokenSource _aheadReadTaskCancel = new ResetableCancellationTokenSource();

		public ReadCacheStream(Stream source, Stream repository, int aheadLength, int blockSize)
		{
			_source = source ?? throw new ArgumentNullException(nameof(source));
			if (!_source.CanRead)
				throw new ArgumentException("Source must be readable.");
			_repository = repository ?? throw new ArgumentNullException(nameof(repository));
			if (aheadLength < 1)
				throw new ArgumentOutOfRangeException("Ahead length must be positive greater than zero.");
			if (blockSize < 1)
				throw new ArgumentOutOfRangeException("Block size must be positive greater than zero.");
			if (aheadLength < blockSize)
				throw new ArgumentOutOfRangeException("Ahead length must not be bigger the block size.");

			_aheadLength = aheadLength;
			_blockSize = blockSize;

			//read ahead immediately on background task
			_aheadReadTask = ReadAhead(aheadLength);
		}

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => _source.Length;

		public override long Position { get => throw new InvalidOperationException("Get position is not supported."); set => throw new InvalidOperationException("Set position is not supported."); }

		public override void Flush()
			=> throw new InvalidOperationException("Flush is not supported.");

		public override long Seek(long offset, SeekOrigin origin)
			=> throw new InvalidOperationException("Seek is not supported.");

		public override void SetLength(long value)
			=> throw new InvalidOperationException("Set length is not supported.");

		public override void Write(byte[] buffer, int offset, int count)
			=> throw new InvalidOperationException("Write is not supported.");

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			=> throw new InvalidOperationException("Write is not supported.");

		public override int Read(byte[] buffer, int offset, int count)
		{
			int read = _repository.Read(buffer, offset, count);
			int left = count - read;
			if (left > 0)
			{
				_aheadReadTaskCancel.Cancel();
				_aheadReadTask.Wait();
				_aheadReadTaskCancel.Reset();
				read += _repository.Read(buffer, offset + read, left);
				left = count - read;
				if (left > 0)
					read += _source.Read(buffer, offset + read, left);

				_aheadReadTask = ReadAhead(_repository.Length > _aheadLength ? 0 : _aheadLength - (int)_repository.Length);
			}
			else
				_aheadReadTask = _aheadReadTask.ContinueWith(t => ReadAhead(count));

			return read;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			int read = await _repository.ReadAsync(buffer, offset, count);
			int left = count - read;
			if (left > 0)
			{
				_aheadReadTaskCancel.Cancel();
				await _aheadReadTask;
				_aheadReadTaskCancel.Reset();
				read += await _repository.ReadAsync(buffer, offset + read, left);
				left = count - read;
				if (left > 0)
					read += _source.Read(buffer, offset + read, left);

				_aheadReadTask = ReadAhead(_repository.Length > _aheadLength ? 0 : _aheadLength - (int)_repository.Length);
			}
			else
				_aheadReadTask = _aheadReadTask.ContinueWith(t => ReadAhead(count));
			return read;
		}

		Task ReadAhead(int length)
			=> Task.Run(() =>
			{
				if (length == 0)
					return;

				byte[] buffer = new byte[_blockSize]; //cache somehow

				int readLen;
				while (length > 0)
				{
					if (_aheadReadTaskCancel.IsCancellationRequested)
						break;

					int toRead = _blockSize > length ? length : _blockSize;
					_repository.Write(buffer, 0, readLen = _source.Read(buffer, 0, toRead));
					length -= _blockSize;
				}
			});

		class ResetableCancellationTokenSource
		{
			internal bool IsCancellationRequested;
			public void Cancel() => IsCancellationRequested = true;
			public void Reset() => IsCancellationRequested = true;
		}
	}
}
