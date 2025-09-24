using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using TrOCR.Helper; 
using OpenCvSharp;
using Sdcb.PaddleOCR;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR.Models;
using System.Diagnostics;
//支持ppocrv5，支持64位，不支持32位，不需要 CPU 支持 AVX指令集，cpu不支持avx的使用此接口
namespace TrOCR.Helper
{
    /// <summary>
    /// PaddleOCR2离线识别帮助类 (基于Sdcb.PaddleOCR)
    /// 采用单例模式和懒加载，支持从自定义本地路径加载模型。
    /// </summary>
    public sealed class PaddleOCR2Helper : IDisposable
    {
        // --- 1. Lazy<T> ，实现简洁的线程安全单例 ---
        private static Lazy<PaddleOCR2Helper> _lazyInstance =
            new Lazy<PaddleOCR2Helper>(() => new PaddleOCR2Helper(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static PaddleOCR2Helper Instance => _lazyInstance.Value;
        
        private static readonly object _resetLock = new object();
        private readonly PaddleOcrAll _ocrEngine;
        private bool _disposed = false;

        // --- 2. 构造函数现在负责所有初始化工作 ---
        private PaddleOCR2Helper()
        {
            // 检查系统架构
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new NotSupportedException("***PaddleOCR2仅支持64位系统, 不支持32位系统***");

            try
            {
                // --- 步骤1：从INI读取所有相关配置 ---
                string detModelPath = GetConfigValue("模型配置_PaddleOCR2", "Det");
                string clsModelPath = GetConfigValue("模型配置_PaddleOCR2", "Cls");
                string recModelPath = GetConfigValue("模型配置_PaddleOCR2", "Rec");
                string keysPath = GetConfigValue("模型配置_PaddleOCR2", "Keys");

                // 读取独立的版本号
                string detVersionStr = GetConfigValue("模型配置_PaddleOCR2", "Det_Version");
                string clsVersionStr = GetConfigValue("模型配置_PaddleOCR2", "Cls_Version");
                string recVersionStr = GetConfigValue("模型配置_PaddleOCR2", "Rec_Version");

                // 读取高级配置文件路径
                string advancedConfigPath = GetConfigValue("模型配置_PaddleOCR2", "AdvancedConfig");

                // 如果模型路径配置为空，使用默认路径 (保持不变)
                if (string.IsNullOrEmpty(detModelPath) || string.IsNullOrEmpty(clsModelPath) ||
                    string.IsNullOrEmpty(recModelPath) || string.IsNullOrEmpty(keysPath))
                {
                    string modelBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PaddleOCR2_data", "models");
                    detModelPath = string.IsNullOrEmpty(detModelPath) ? Path.Combine(modelBasePath, "PP-OCRv5_mobile_det_infer") : detModelPath;
                    clsModelPath = string.IsNullOrEmpty(clsModelPath) ? Path.Combine(modelBasePath, "ch_ppocr_mobile_v2.0_cls_infer") : clsModelPath;
                    recModelPath = string.IsNullOrEmpty(recModelPath) ? Path.Combine(modelBasePath, "PP-OCRv5_mobile_rec_infer") : recModelPath;
                    keysPath = string.IsNullOrEmpty(keysPath) ? Path.Combine(modelBasePath, "ppocr_keys.txt") : keysPath;
                }

                // --- 步骤2：解析独立的版本配置 ---
                ModelVersion detVersion = ParseModelVersion(detVersionStr);
                ModelVersion clsVersion = ParseModelVersion(clsVersionStr);
                ModelVersion recVersion = ParseModelVersion(recVersionStr);

                // --- 步骤3：创建并组合模型对象 ---
                DetectionModel detModel = DetectionModel.FromDirectory(detModelPath, detVersion);
                ClassificationModel clsModel = ClassificationModel.FromDirectory(clsModelPath, clsVersion);
                RecognizationModel recModel = RecognizationModel.FromDirectory(recModelPath, keysPath, recVersion);
                FullOcrModel customModel = new FullOcrModel(detModel, clsModel, recModel);
                 // --- 步骤4：初始化引擎 ---
                _ocrEngine = new PaddleOcrAll(customModel, PaddleDevice.Blas());

                // --- 步骤5：加载并应用高级JSON参数 ---
                if (!string.IsNullOrEmpty(advancedConfigPath) && File.Exists(advancedConfigPath))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(advancedConfigPath);
                        var configJson = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);
                        var paddleConfig = configJson["PaddleOCR2"];

                        if (paddleConfig != null)
                        {
                            // 应用顶层参数
                            _ocrEngine.Enable180Classification = paddleConfig["TopLevel"]?["Enable180Classification"]?.Value<bool>() ?? _ocrEngine.Enable180Classification;
                            _ocrEngine.AllowRotateDetection = paddleConfig["TopLevel"]?["AllowRotateDetection"]?.Value<bool>() ?? _ocrEngine.AllowRotateDetection;

                            // 应用检测器参数
                            var detConfig = paddleConfig["Detector"];
                            if (detConfig != null)
                            {
                                _ocrEngine.Detector.MaxSize = detConfig["MaxSize"]?.Value<int>() ?? _ocrEngine.Detector.MaxSize;
                                _ocrEngine.Detector.BoxScoreThreahold = detConfig["BoxScoreThreahold"]?.Value<float>() ?? _ocrEngine.Detector.BoxScoreThreahold;
                                _ocrEngine.Detector.BoxThreshold = detConfig["BoxThreshold"]?.Value<float>() ?? _ocrEngine.Detector.BoxThreshold;
                                _ocrEngine.Detector.MinSize = detConfig["MinSize"]?.Value<int>() ?? _ocrEngine.Detector.MinSize;
                                _ocrEngine.Detector.UnclipRatio = detConfig["UnclipRatio"]?.Value<float>() ?? _ocrEngine.Detector.UnclipRatio;
                            }

                            // 应用分类器参数
                            var clsConfig = paddleConfig["Classifier"];
                            if (clsConfig != null && _ocrEngine.Classifier != null)
                            {
                                _ocrEngine.Classifier.RotateThreshold = clsConfig["RotateThreshold"]?.Value<double>() ?? _ocrEngine.Classifier.RotateThreshold;
                            }
                            Debug.WriteLine($"成功加载并应用高级配置文件: {advancedConfigPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"解析或应用高级配置文件 '{advancedConfigPath}' 失败: {ex.Message}");
                        // 即使JSON解析失败，引擎也已经用默认参数初始化好了，程序可以继续运行
                    }
                }
                
            }
            catch (Exception ex)
            {
                // 1. 手动拼接一个更详细、更友好的错误消息
                string detailedMessage = $"从自定义路径加载模型失败，请检查路径和模型文件是否完整且版本匹配。\n根本原因: {ex.Message}";

                // 2. 抛出新异常，使用我们刚刚拼接好的详细消息，并依然保留完整的 ex 作为 InnerException
                throw new Exception(detailedMessage, ex);
            }
        }

        // --- 3. 公共方法恢复同步，调用更简单 ---
        /// <summary>
        /// 识别图像中的文字
        /// </summary>
        public static string RecognizeText(Image image)
        {
            try
            {
                return Instance.Execute(image);
            }
            catch (Exception ex)
            {
                Reset(); // 失败后重置，以便下次重试
                return $"***PaddleOCR2识别失败: {ex.Message}***";
            }
        }

        private string Execute(Image image)
        {
            if (_ocrEngine == null) return "***PaddleOCR2引擎未初始化***";
            if (image == null) return "***图像为空***";

            using (Mat src = ImageToMat(image))
            {
                PaddleOcrResult result = _ocrEngine.Run(src);
                Debug.WriteLine($"paddleOCR2识别结果: {result}");
                if (result?.Regions == null || result.Regions.Length == 0)
                    return "***该区域未发现文本***";

                var sb = new StringBuilder();
                foreach (var region in result.Regions)
                {
                    if (!string.IsNullOrWhiteSpace(region.Text))
                    {
                        sb.AppendLine(region.Text);
                    }
                }
                string finalResult = sb.ToString().Trim();
                return string.IsNullOrEmpty(finalResult) ? "***该区域未发现文本***" : finalResult;
            }
        }
        
        // --- 4. Reset 方法适配 Lazy<T> ---
        public static void Reset()
        {
            lock (_resetLock)
            {
                if (_lazyInstance.IsValueCreated)
                {
                    try { _lazyInstance.Value.Dispose(); }
                    catch { /* 忽略在失败实例上调用Dispose时可能发生的异常 */ }
                }
                // 关键：创建全新的Lazy实例以备下次使用
                _lazyInstance = new Lazy<PaddleOCR2Helper>(() => new PaddleOCR2Helper(), LazyThreadSafetyMode.ExecutionAndPublication);
            }
        }

        // --- 其他辅助方法和Dispose模式 ---
        /// <summary>
        /// 获取配置值的辅助方法
        /// </summary>
        private string GetConfigValue(string section, string key)
        {
            try
            {
                var value = TrOCR.Helper.IniHelper.GetValue(section, key);
                return value == "发生错误" ? "" : value;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 解析模型版本配置
        /// </summary>
        private ModelVersion ParseModelVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return ModelVersion.V5; // 默认v5

            switch (version.ToLower())
            {
                case "v2": return ModelVersion.V2;
                case "v3": return ModelVersion.V3;
                case "v4": return ModelVersion.V4;
                case "v5": return ModelVersion.V5;
                default: return ModelVersion.V5;
            }
        }

        private Mat ImageToMat(Image image)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
            }
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                _ocrEngine?.Dispose();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
        ~PaddleOCR2Helper() => Dispose();
    }
}