using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
			/*
			- get categories attribs from source
			- generate BAT file from above to run tests
			- run tests
			- analyze output
			*/

			var sourceDirectory = @"C:\www\sensei-core\UnitTests";
			var batchFileTargetFolder = @"C:\Users\darcy\SynologyDrive\work\Sensei\Unit Testing";
			var nunitOutputFolder = @"C:\www\sensei-core\Utils\NUnit.org\nunit-console";
			var unitTestAssemblyPath = @"C:\www\sensei-core\Web\bin\UnitTests.dll";
			var testFilePrefix = @"TestResult-";

			var cats = GetAllCategories(sourceDirectory);
			
			GenerateBatchFile(cats, batchFileTargetFolder, nunitOutputFolder, unitTestAssemblyPath, testFilePrefix);
			
			// RUN THE BATCH FILE TO GEN UNIT TEST REPORTS, THEN RE-RUN THIS APP

			var settings = new XmlReaderSettings();
			settings.IgnoreWhitespace = true;

			float minDuration = 0.1F;
			var testFileDirectory = @"C:\www\sensei-core\Utils\NUnit.org\nunit-console";

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
							var ti = new TestInfo() { Name = reader.GetAttribute("name"), Class = reader.GetAttribute("classname"), Duration = float.Parse(reader.GetAttribute("duration")), Category = category };
							infos.Add(ti);
						}
					}
				}
			}

			var topCount = 50;
			var sortedInfos = infos
				.Where(i => i.Duration > minDuration)
				.OrderByDescending(i => i.Duration)
				.Take(topCount);

			Console.WriteLine($"Top {topCount} slowest tests (seconds)");
			foreach (var info in sortedInfos)
			{
				Console.WriteLine($"classname: {info.Class}");
				Console.WriteLine($"Name: {info.Name}");
				Console.WriteLine($"Duration: {info.Duration:N2}");
				Console.WriteLine($"");
			}

			var ci = GetCategoryInfo(infos);

			var averageSlowestCats = ci.OrderByDescending(ci => ci.AverageSeconds).Take(5);
			Console.WriteLine("Slowest Category (avg seconds)");
			foreach (var c in averageSlowestCats)
				Console.WriteLine($"Cat: {c.Category} Avg: {c.AverageSeconds:N2} Total: {c.TotalSeconds:N2}");
			Console.WriteLine("");

			var slowestCats = ci.OrderByDescending(ci => ci.TotalSeconds).Take(5);
			Console.WriteLine("Slowest Category (total seconds)");
			foreach (var c in slowestCats)
				Console.WriteLine($"Cat: {c.Category} Avg: {c.AverageSeconds:N2} Total: {c.TotalSeconds:N2}");

			Console.WriteLine($"\nTotal Test Time: {GetTotalTime(infos):N0} seconds, {GetTotalTime(infos) / 60:N2} mins, {GetTotalTime(infos) / 60 / 60:N2} hours");


			Console.WriteLine("Done");
			Console.ReadKey();
		}

		private static void GenerateBatchFile(IEnumerable<string> cats, string batchFileTargetFolder, string nunitOutputFolder, string unitTestAssemblyPath, string testFilePrefix)
		{
			var output = new StringBuilder();
			output.AppendLine($"c:");
			output.AppendLine($"cd {nunitOutputFolder}");
			foreach (var cat in cats)
				output.AppendLine(@$"nunit3-console.exe ""{unitTestAssemblyPath}"" --where ""cat == {cat}"" --result={testFilePrefix}{cat}.xml");
			File.WriteAllText(Path.Combine(batchFileTargetFolder, "go.bat"), output.ToString());
		}

		private static IEnumerable<CategoryInfo> GetCategoryInfo(List<TestInfo> infos)
		{
			var categories = infos.Select(i => i.Category).Distinct();

			var catInfoList = new List<CategoryInfo>();
			foreach (var cat in categories)
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

		private static IEnumerable<string> GetAllCategories(string sourceDirectory)
		{
			var catRegex = @"\s*\[\s*Category\s*\(\s*""([a-zA-Z]+)""\s*\)\s*\]";
			var files = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);

			var cats = new HashSet<string>();

			foreach (var file in files)
			{
				var source = File.ReadAllText(file);
				var matches = Regex.Matches(source, catRegex);
				foreach (Match match in matches)
				{
					foreach (var v in match.Groups.Values.Skip(1))
						cats.UnionWith(match.Groups.Values.Skip(1).Select(v => v.Value));
				}
			}
			return cats.OrderBy(c => c);
		}

		private static float GetTotalTime(List<TestInfo> infos)
		{
			return infos.Sum(i => i.Duration);
		}
	}
}
