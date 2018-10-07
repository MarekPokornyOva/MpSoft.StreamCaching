#region using
using System;
using System.IO;
using System.Net;
using System.Xml;
using MpSoft.StreamCaching;
#endregion using

namespace RssReader
{
	class Program
	{
		static void Main(string[] args)
		{
			//declare xml reading status variables
			bool inItem = false, inTitle = false;
			int indexMax = 10;

			//declare
			int blockSize = 4096;
			//open the raw data source ...
			using (Stream dataSource = File.OpenRead(@"C:\Users\pokormar\Documents\vsp\private\MpSoft.StreamCaching\Samples\RssReader\blog-feed.xml"))
			//WebRequest req = WebRequest.CreateHttp("http://www.rss-specifications.com/blog-feed.xml");
			//using (WebResponse res = req.GetResponse())
			//using (Stream dataSource = res.GetResponseStream())
			//... and setup caching and ahead reading.
			using (CacheRepository cache = new CacheRepository(blockSize, new MemStreamProvider()))
			using (Stream source = new ReadCacheStream(dataSource, cache, 1024 * 1024 /*Store up to 1MB of source data to temporary storage...*/, blockSize /* ... reading per 4KB blocks.*/))
			//setup xml reader
			using (XmlReader xml = XmlReader.Create(source))
				while (xml.Read())
					switch (xml.NodeType)
					{
						case XmlNodeType.Element:
							if ((xml.Depth == 2) && (xml.Name == "item"))
								inItem = true;
							else if ((inItem) && (xml.Depth == 3) && (xml.Name == "title"))
								inTitle = true;
							break;
						case XmlNodeType.EndElement:
							if ((inTitle) && (xml.Depth == 3) && (xml.Name == "title"))
								inTitle = false;
							else if ((inItem) && (xml.Depth == 2) && (xml.Name == "item"))
								inItem = false;
							break;
						case XmlNodeType.Text:
							if (inTitle)
							{
								Console.WriteLine(xml.Value);
								if (--indexMax == 0)
									xml.Close();
							}
							break;
					}
		}

		#region MemStreamProvider
		class MemStreamProvider : RepositoryProviderSync
		{
			internal MemStreamProvider()
			{ }

			public override Stream Get(int capacity)
				=> new MemoryStream(capacity);
		}
		#endregion MemStreamProvider
	}
}
