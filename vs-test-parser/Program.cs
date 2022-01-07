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
		static void Main(string sourcePath, string batchFilePath, string testReportPath, string nunitPath, string unitTestAssemblyPath, bool generateBatchFile = false)
		{
			/*
			Run something like this to generate the batch file which generates the unit test report data:
			
			vs-test-parser.exe --generate-batch-file true --unit-test-assembly-path "C:\www\sensei-core\Web\bin\UnitTests.dll" --source-path "C:\www\sensei-core\UnitTests" --batch-file-path "C:\temp" --test-report-path "C:\Users\darcy\SynologyDrive\work\Sensei\Unit Testing\TestResults2022-01-07" --nunit-path "C:\www\sensei-core\Utils\NUnit.org\nunit-console" 

			Then execute the batch file, which takes about 40 minutes. If there are test categories throwing exceptions and halting the run, you can comment out those categories in the batch file. Once the run is complete, run a command like this:

			vs-test-parser.exe --test-report-path "C:\Users\darcy\SynologyDrive\work\Sensei\Unit Testing\TestResults2022-01-07"

			*/

			var testFilePrefix = @"TestResult-";


			if (generateBatchFile)
			{

				var filePath = GenerateBatchFile(sourcePath, batchFilePath, nunitPath, unitTestAssemblyPath, testFilePrefix, testReportPath);
				Console.WriteLine($"Batch file created at {filePath}");
				Console.WriteLine($"This will generate the report files in {testReportPath}");
				Console.WriteLine("Execute the batch file and then re-run this application to parse the test results");
				Environment.Exit(0);
			}

			var settings = new XmlReaderSettings();
			settings.IgnoreWhitespace = true;

			float minDuration = 0.1F;

			var files = Directory.GetFiles(testReportPath, $"{testFilePrefix}*.xml");

			var infos = new List<TestInfo>();

			foreach (var file in files)
			{
				var category = Path.GetFileNameWithoutExtension(file).Substring(testFilePrefix.Length);

				using (var fileStream = File.OpenText(Path.Combine(testReportPath, file)))
				using (var reader = XmlReader.Create(fileStream, settings))
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

			Console.WriteLine($"Total test count: {infos.Count:N0}");

			var csvPath = SaveToCsv(infos, testReportPath);
			Console.WriteLine($"Saved {csvPath}");

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

		private static string SaveToCsv(List<TestInfo> infos, string testReportPath)
		{
			var path = Path.Combine(testReportPath, "TestResults.csv");

			using (var tw = new StreamWriter(path))
			{
				tw.WriteLine("category,class,test,durationSeconds");
				foreach (var info in infos)
					tw.WriteLine($"{info.Category},{info.Class},\"{info.Name}\",{info.Duration}");
				tw.Close();
			}

			return path;
		}

		private static string GenerateBatchFile(string sourcePath, string batchFileTargetFolder, string nunitOutputFolder, string unitTestAssemblyPath, string testFilePrefix, string testReportPath)
		{
			var cats = GetAllCategories(sourcePath);
			var output = new StringBuilder();
			output.AppendLine($"c:");
			output.AppendLine($"cd {nunitOutputFolder}");
			foreach (var cat in cats)
				output.AppendLine(@$"nunit3-console.exe ""{unitTestAssemblyPath}"" --where ""cat == {cat}"" --result=""{Path.Combine(testReportPath, testFilePrefix + cat + ".xml")}""");
			var batchFilePath = Path.Combine(batchFileTargetFolder, "generate-test-reports.bat");
			File.WriteAllText(batchFilePath, output.ToString());
			return batchFilePath;
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
