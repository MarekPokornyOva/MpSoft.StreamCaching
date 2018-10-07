#region using
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#endregion using

namespace MpSoft.StreamCaching
{
	public abstract class RepositoryProviderSync : IRepositoryProvider
	{
		public abstract Stream Get(int capacity);

		public virtual Task<Stream> GetAsync(int capacity, CancellationToken cancellationToken)
			=> Task.FromResult(Get(capacity));

		public virtual void Release(Stream stream)
			=> stream.Dispose();
	}
}
