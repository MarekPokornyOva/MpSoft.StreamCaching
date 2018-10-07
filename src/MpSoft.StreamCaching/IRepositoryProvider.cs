#region using
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#endregion using

namespace MpSoft.StreamCaching
{
	public interface IRepositoryProvider
	{
		Stream Get(int capacity);
		Task<Stream> GetAsync(int capacity, CancellationToken cancellationToken);
		void Release(Stream stream);
	}
}
