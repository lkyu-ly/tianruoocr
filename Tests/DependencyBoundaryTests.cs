using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TrOCR.Helper;

namespace TrOCR.Tests
{
    [TestFixture]
    public class DependencyBoundaryTests
    {
        [Test]
        public void Project_DoesNotReferenceLegacyNewtonsoftOrEmguWorld()
        {
            var project = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "TrOCR.csproj"));

            Assert.That(project, Does.Not.Contain(@"DLL\Newtonsoft\Newtonsoft.Json.dll"));
            Assert.That(project, Does.Not.Contain(@"DLL\Emgu\CV\Emgu.CV.World.dll"));
        }

        [Test]
        public void Project_StillTargetsNetFramework481()
        {
            var project = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "TrOCR.csproj"));

            Assert.That(project, Does.Contain("<TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>"));
            Assert.That(project, Does.Not.Contain("<TargetFramework>net9.0-windows</TargetFramework>"));
        }

        [Test]
        public void DllDirectory_DoesNotContainLegacyNewtonsoftAfterUpgrade()
        {
            var root = FindRepositoryRoot();

            Assert.That(File.Exists(Path.Combine(root, "DLL", "Newtonsoft", "Newtonsoft.Json.dll")), Is.False);
        }

        [Test]
        public void DllDirectory_DoesNotContainLegacyZxingAfterUpgrade()
        {
            var root = FindRepositoryRoot();

            Assert.That(File.Exists(Path.Combine(root, "DLL", "zxing.dll")), Is.False);
        }

        [Test]
        public void DllDirectory_DoesNotContainLegacyEmguWorldAfterCleanup()
        {
            var root = FindRepositoryRoot();

            Assert.That(File.Exists(Path.Combine(root, "DLL", "Emgu", "CV", "Emgu.CV.World.dll")), Is.False);
        }

        [Test]
        public void DllDirectory_DoesNotContainLegacyShareXBinariesAfterSourceMigration()
        {
            var root = FindRepositoryRoot();

            Assert.That(File.Exists(Path.Combine(root, "DLL", "ShareX", "ShareX.HelpersLib.dll")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "DLL", "ShareX", "ShareX.ScreenCaptureLib.dll")), Is.False);
        }

        [Test]
        public void DependencyDiagnostics_DetectsManagedDlls()
        {
            var dllDirectory = Path.Combine(FindRepositoryRoot(), "DLL");
            var assemblies = DependencyDiagnostics.FindManagedDlls(dllDirectory);

            Assert.That(assemblies.Select(item => item.Name), Does.Not.Contain("ShareX.ScreenCaptureLib"));
            Assert.That(assemblies.Select(item => item.Name), Does.Not.Contain("Newtonsoft.Json"));
            Assert.That(assemblies.Select(item => item.Name), Does.Not.Contain("zxing"));
        }

        [Test]
        public void Forms_DoNotCallShareXRegionCaptureTasksDirectly()
        {
            var root = FindRepositoryRoot();
            var formsDirectory = Path.Combine(root, "Forms");

            var violations = Directory
                .EnumerateFiles(formsDirectory, "*.cs", SearchOption.AllDirectories)
                .SelectMany(file => File.ReadLines(file)
                    .Select((line, index) => new { file, line, lineNumber = index + 1 }))
                .Where(item => item.line.Contains("RegionCaptureTasks.GetRegionImage_Mo"))
                .Select(item => item.file + ":" + item.lineNumber + ": " + item.line.Trim())
                .ToArray();

            Assert.That(
                violations,
                Is.Empty,
                "Forms code should use IScreenCaptureService instead of ShareX private API:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
        }

        private static string FindRepositoryRoot()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                if (File.Exists(Path.Combine(directory, "TrOCR.sln")))
                {
                    return directory;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new DirectoryNotFoundException("无法从测试输出目录定位仓库根目录: " + TestContext.CurrentContext.TestDirectory);
        }
    }
}
