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
        public void DllDirectory_CurrentlyContainsKnownLegacyResearchArtifacts()
        {
            var root = FindRepositoryRoot();

            Assert.That(File.Exists(Path.Combine(root, "DLL", "Newtonsoft", "Newtonsoft.Json.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "DLL", "Emgu", "CV", "Emgu.CV.World.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "DLL", "zxing.dll")), Is.True);
        }

        [Test]
        public void DependencyDiagnostics_DetectsManagedDlls()
        {
            var dllDirectory = Path.Combine(FindRepositoryRoot(), "DLL");
            var assemblies = DependencyDiagnostics.FindManagedDlls(dllDirectory);

            Assert.That(assemblies.Select(item => item.Name), Does.Contain("ShareX.ScreenCaptureLib"));
            Assert.That(assemblies.Select(item => item.Name), Does.Contain("Newtonsoft.Json"));
            Assert.That(assemblies.Select(item => item.Name), Does.Contain("zxing"));
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
