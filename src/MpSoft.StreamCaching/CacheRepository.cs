#region using
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#endregion using

namespace MpSoft.StreamCaching
{
	public class CacheRepository : Stream
	{
		readonly int _blockSize;
		readonly IRepositoryProvider _repositoryProvider;

		long _length;
		List<Segment> _segments = new List<Segment>();

		public CacheRepository(int blockSize, IRepositoryProvider repositoryProvider)
		{
			if (blockSize < 1)
				throw new ArgumentOutOfRangeException("Block size must be greater than zero.");
			_repositoryProvider = repositoryProvider ?? throw new ArgumentNullException(nameof(repositoryProvider));
			_blockSize = blockSize;
		}

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => _length;

		public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public override void Flush()
		{
			lock (_segments)
			{
				foreach (Segment segment in _segments)
					segment.Data.Flush();
			}
		}

		public override async Task FlushAsync(CancellationToken cancellationToken)
		{
			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				Task[] flushes = new Task[_segments.Count];
				int a = 0;
				foreach (Segment segment in _segments)
					flushes[a++] = segment.Data.FlushAsync(cancellationToken);
				await Task.WhenAll(flushes);
			}
			finally
			{
				_lock.Release();
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			lock (_segments)
			{
				int readCount = 0;
				while ((_segments.Count != 0) && (count != 0))
				{
					Segment segment = _segments[0];
					segment.Data.Position = segment.ReadPosition;
					int read = segment.Data.Read(buffer, offset, count);
					if (segment.Data.Position == segment.Data.Length)
					{
						_segments.RemoveAt(0);
						segment.Data.Dispose();
					}
					else
						segment.ReadPosition = (int)segment.Data.Position;
					offset += read;
					count -= read;
					readCount += read;
					_length -= read;
				}
				return readCount;
			}
		}

		SemaphoreSlim _lock = new SemaphoreSlim(1);
		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				int readCount = 0;
				while ((_segments.Count != 0) && (count != 0))
				{
					Segment segment = _segments[0];
					segment.Data.Position = segment.ReadPosition;
					int read = await segment.Data.ReadAsync(buffer, offset, count, cancellationToken);
					if (segment.Data.Position == segment.Data.Length)
					{
						_segments.RemoveAt(0);
						_repositoryProvider.Release(segment.Data);
					}
					else
						segment.ReadPosition = (int)segment.Data.Position;
					offset += read;
					count -= read;
					readCount += read;
					_length -= read;
				}
				return readCount;
			}
			finally
			{
				_lock.Release();
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
			=> throw new InvalidOperationException("Seek is not supported.");

		public override void SetLength(long value)
			=> throw new InvalidOperationException("Set length is not supported.");

		public override void Write(byte[] buffer, int offset, int count)
		{
			lock (_segments)
			{
				if (_segments.Count != 0)
				{
					Segment lastSegment = _segments[_segments.Count - 1];
					int toWrite = _blockSize - lastSegment.WritePosition;
					if (toWrite != 0)
					{
						if (toWrite > count)
							toWrite = count;
						lastSegment.Data.Position = lastSegment.WritePosition;
						lastSegment.Data.Write(buffer, 0, toWrite);
						lastSegment.WritePosition += toWrite;
						offset += toWrite;
						count -= toWrite;
						_length += toWrite;
					}
				}
				Segment segment;
				while (count > 0)
				{
					_segments.Add(segment = new Segment { Data = _repositoryProvider.Get(_blockSize) });

					int toWrite = count > _blockSize ? _blockSize : count;
					segment.Data.Position = segment.WritePosition;
					segment.Data.Write(buffer, offset, toWrite);
					segment.WritePosition += toWrite;
					offset += toWrite;
					count -= toWrite;
					_length += toWrite;
				}
			}
		}

		public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				if (_segments.Count != 0)
				{
					Segment lastSegment = _segments[_segments.Count - 1];
					int toWrite = _blockSize - lastSegment.WritePosition;
					if (toWrite != 0)
					{
						if (toWrite > count)
							toWrite = count;
						lastSegment.Data.Position = lastSegment.WritePosition;
						await lastSegment.Data.WriteAsync(buffer, 0, toWrite);
						lastSegment.WritePosition += toWrite;
						offset += toWrite;
						count -= toWrite;
						_length += toWrite;
					}
				}
				Segment segment;
				while (count > 0)
				{
					_segments.Add(segment = new Segment { Data = await _repositoryProvider.GetAsync(_blockSize, cancellationToken) });

					int toWrite = count > _blockSize ? _blockSize : count;
					segment.Data.Position = segment.WritePosition;
					await segment.Data.WriteAsync(buffer, offset, toWrite);
					segment.WritePosition += toWrite;
					offset += toWrite;
					count -= toWrite;
					_length += toWrite;
				}
			}
			finally
			{
				_lock.Release();
			}
		}

		protected override void Dispose(bool disposing)
		{
			lock (_segments)
			{
				try
				{
					foreach (Segment segment in _segments)
						_repositoryProvider.Release(segment.Data);
				}
				finally
				{
					base.Dispose(disposing);
				}
			}
		}

		class Segment
		{
			internal Stream Data;
			internal int ReadPosition;
			internal int WritePosition;
		}
	}
}
