#region using
using System.IO;
#endregion using

namespace MpSoft.StreamCaching
{
	public class TempFileProvider : RepositoryProviderSync
	{
		public override Stream Get(int capacity)
			=> File.Open(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

		public override void Release(Stream stream)
		{
			base.Release(stream);
			try
			{
				if (stream is FileStream fs)
					File.Delete(fs.Name);
			}
			catch
			{ }
		}
	}
}
