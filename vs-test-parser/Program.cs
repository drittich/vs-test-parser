using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace vs_test_parser
{
	class Program
	{
		// TODO: get total run time for each category
		// TODO: show top 5 slowest run times for each category
		// TODO: average run times for each category

		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");

			XmlReaderSettings settings = new XmlReaderSettings();
			settings.IgnoreWhitespace = true;

			float minDuration = 0.1F;
			var testFileDirectory = @"C:\www\sensei-core\Utils\NUnit.org\nunit-console";
			var testFilePrefix = @"TestResult-";

			var files = Directory.GetFiles(testFileDirectory, $"{testFilePrefix}*.xml");

			var infos = new List<TestInfo>();

			foreach (var file in files)
			{
				var category = Path.GetFileNameWithoutExtension(file).Substring(testFilePrefix.Length);

				using (var fileStream = File.OpenText(Path.Combine(testFileDirectory, file)))
				using (XmlReader reader = XmlReader.Create(fileStream, settings))
				{
					while (reader.Read())
					{
						if (reader.NodeType == XmlNodeType.Element && reader.Name == "test-case")
						{
							var ti = new TestInfo() { Name = reader.GetAttribute("name"), Class = reader.GetAttribute("classname"), Duration = float.Parse(reader.GetAttribute("duration")) , Category= category };
							infos.Add(ti);
						}
					}
				}
			}

			var sortedInfos = infos
				.Where(i => i.Duration > minDuration)
				.OrderByDescending(i => i.Duration)
				.Take(50);

			foreach (var info in sortedInfos)
			{
				Console.WriteLine($"classname: {info.Class}");
				Console.WriteLine($"Name: {info.Name}");
				Console.WriteLine($"Duration: {info.Duration.ToString()}");
				Console.WriteLine($"");
			}

			var ci = GetCategoryInfo(infos);

			var top5AverageSlowestCats = ci.OrderByDescending(ci => ci.AverageSeconds).Take(5);
			Console.WriteLine("Average Slowest");
			foreach (var c in top5AverageSlowestCats)
			{
				Console.WriteLine($"Cat: {c.Category} Avg: {c.AverageSeconds} Total: {c.TotalSeconds}");
			}
			Console.WriteLine("");

			var top5TotalSlowestCats = ci.OrderByDescending(ci => ci.TotalSeconds).Take(5);
			Console.WriteLine("Total Slowest");
			foreach (var c in top5TotalSlowestCats)
			{
				Console.WriteLine($"Cat: {c.Category} Avg: {c.AverageSeconds} Total: {c.TotalSeconds}");
			}

			Console.ReadKey();
		}

		private static IEnumerable<CategoryInfo> GetCategoryInfo(List<TestInfo> infos)
		{
			var categories = infos.Select(i => i.Category).Distinct();

			var catInfoList = new List<CategoryInfo>();
			foreach(var cat in categories)
			{
				var catInfos = infos.Where(i => i.Category == cat);
				var ci = new CategoryInfo();
				ci.Category = cat;
				ci.AverageSeconds = GetCategoryAverageSeconds(catInfos);
				ci.TotalSeconds = GetCategoryTotalSeconds(catInfos);

				catInfoList.Add(ci);
			}

			return catInfoList;
		}

		private static float GetCategoryTotalSeconds(IEnumerable<TestInfo> catInfos)
		{
			float totalSeconds = catInfos.Sum(i => i.Duration);
			return totalSeconds;
		}

		private static float GetCategoryAverageSeconds(IEnumerable<TestInfo> catInfos)
		{
			float averageSeconds = catInfos.Average(i => i.Duration);
			return averageSeconds;
		}

		IEnumerable<TestInfo> GetTopNSlowest(IEnumerable<TestInfo> infos, int count)
		{
			return infos
				.OrderByDescending(i => i.Duration)
				.Take(count);
		}
	}
}
