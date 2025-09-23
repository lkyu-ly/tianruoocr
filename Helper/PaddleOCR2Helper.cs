using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading;
using OpenCvSharp;
using Sdcb.PaddleOCR;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;
//支持ppocrv5，支持64位，不支持32位，不需要 CPU 支持 AVX指令集，cpu不支持avx的使用此接口
namespace TrOCR.Helper
{
    /// <summary>
    /// PaddleOCR2离线识别帮助类 (基于Sdcb.PaddleOCR)
    /// 采用单例模式和懒加载，支持资源回收
    /// </summary>
    public sealed class PaddleOCR2Helper : IDisposable
    {
        // Windows API 函数声明，用于内存优化
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process,
            IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

        // 使用一个 Lazy<T> 实例替换原来的 _instance 和 _lock 字段
        // LazyThreadSafetyMode.ExecutionAndPublication 确保了构造函数在多线程环境下只会被执行一次
        private static Lazy<PaddleOCR2Helper> _lazyInstance =
            new Lazy<PaddleOCR2Helper>(() => new PaddleOCR2Helper(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 获取单例实例。实例在第一次访问时被创建。
        /// </summary>
        public static PaddleOCR2Helper Instance => _lazyInstance.Value;

        private static readonly object _resetLock = new object();
        private PaddleOcrAll _ocrEngine;
        private readonly Architecture _architecture;
        private bool _disposed = false;

        /// <summary>
        /// 获取单例实例
        /// </summary>
       // 构造函数保持私有，它将由 Lazy<T> 在需要时调用

        private PaddleOCR2Helper()
        {
            _architecture = RuntimeInformation.ProcessArchitecture;
            
            if (_architecture != Architecture.X64)
                throw new NotSupportedException("***PaddleOCR2仅支持64位系统, 不支持32位系统***");

            InitializeEngine();
        }

        private void InitializeEngine()
        {
            try
            {
                // 使用本地中文V5模型
                FullOcrModel model = LocalFullModels.ChineseV5;

                // 启用内存优化
                // 创建PaddleOCR引擎，使用MKLDNN设备以获得更好的CPU性能
                //_ocrEngine = new PaddleOcrAll(model, PaddleDevice.Mkldnn(memoryOptimized:false))
                //_ocrEngine = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
                //{
                //    AllowRotateDetection = true,     // 允许识别有角度的文字
                //    Enable180Classification = false  // 禁用180度分类以提升性能
                //};

                _ocrEngine = new PaddleOcrAll(model, PaddleDevice.Blas())
                {
                    AllowRotateDetection = true,     // 允许识别有角度的文字
                    Enable180Classification = false  // 禁用180度分类以提升性能
                };

                // 优化检测参数
                _ocrEngine.Detector.MaxSize = 960;      // 设置最大检测尺寸
                _ocrEngine.Detector.UnclipRatio = 1.6f; // 设置文本框扩展比例


                //    // 1. 创建一个自定义的配置 Action
                //    Action<PaddleConfig> customDeviceConfig = cfg =>
                //    {
                //        // 2. 在这里应用你想设置的参数
                //        cfg.MkldnnEnabled = false; // 基础设置
                //        //cfg.MemoryOptimized = false; // <<< 你想验证的参数

                //        // 3. 打印出最终的配置信息到控制台或调试输出窗口
                //        Console.WriteLine("--- PaddleConfig Summary ---");
                //        Console.WriteLine(cfg.Summary);
                //        Console.WriteLine("--------------------------");
                //    };

                //    // 4. 使用这个自定义的配置来创建引擎
                //    _ocrEngine = new PaddleOcrAll(model, customDeviceConfig);
            }
            catch (Exception ex)
            {
                _ocrEngine = null;
                throw new Exception($"PaddleOCR2引擎初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 识别图像中的文字
        /// </summary>
        /// <param name="image">要识别的图像</param>
        /// <returns>识别结果文本</returns>
        public static string RecognizeText(Image image)
        {
             try
            {
                return Instance.Execute(image);
            }
            catch (Exception ex)
            {

                // 关键：捕获初始化或执行期间的任何异常
                // 调用 Reset() 来清除“中毒”的 Lazy 实例
                Reset();
                // 返回一个对用户友好的错误信息
                return $"***PaddleOCR2识别失败: {ex.Message}***";
            }
        }

        private string Execute(Image image)
        {
            try
            {
                // if (_architecture != Architecture.X64)
                //     return "***PaddleOCR2不支持32位系统，请使用64位系统***";
                
                if (_ocrEngine == null)
                    return "***PaddleOCR2引擎未初始化***";

                if (image == null)
                    return "***图像为空***";

                // 将System.Drawing.Image转换为OpenCvSharp.Mat
                using (Mat src = ImageToMat(image))
                {
                    // 执行OCR识别
                    PaddleOcrResult result = _ocrEngine.Run(src);
                    Debug.WriteLine($"识别结果: {result.Text}");
                    if (result?.Regions == null || result.Regions.Length == 0)
                        return "***该区域未发现文本***";

                    // 构建识别结果
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
            catch (Exception ex)
            {
                return $"***PaddleOCR2识别失败: {ex.Message}***";
            }
        }

        /// <summary>
        /// 将System.Drawing.Image转换为OpenCvSharp.Mat
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <returns>OpenCvSharp.Mat对象</returns>
        private Mat ImageToMat(Image image)
        {
            using (var ms = new MemoryStream())
            {
                // 将图像保存为PNG格式的字节数组
                image.Save(ms, ImageFormat.Png);
                byte[] imageBytes = ms.ToArray();
                
                // 使用OpenCvSharp解码图像
                return Cv2.ImDecode(imageBytes, ImreadModes.Color);
            }
        }

        /// <summary>
        /// 获取详细的OCR识别结果（包含位置信息）
        /// </summary>
        /// <param name="image">要识别的图像</param>
        /// <returns>详细识别结果</returns>
        public static PaddleOcrResult GetDetailedResult(Image image)
        {
            return Instance.ExecuteDetailed(image);
        }

        private PaddleOcrResult ExecuteDetailed(Image image)
        {
            try
            {
                if (_architecture != Architecture.X64 || _ocrEngine == null || image == null)
                    return null;

                using (Mat src = ImageToMat(image))
                {
                    return _ocrEngine.Run(src);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 检查是否支持PaddleOCR2
        /// </summary>
        /// <returns>是否支持</returns>
        public static bool IsSupported()
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.X64;
        }

        /// <summary>
        /// 重置引擎实例
        /// </summary>
       public static void Reset()
        {
            lock (_resetLock)
            {
                // 检查实例是否已经被创建
                if (_lazyInstance.IsValueCreated)
                {
                    try
                    {
                        // 尝试释放旧实例。如果初始化失败，访问.Value会抛出异常
                        _lazyInstance.Value.Dispose();
                    }
                    catch
                    {
                        // 忽略异常，因为实例本身就是坏的，继续执行重置核心逻辑
                    }
                }
                // 创建一个新的 Lazy<T> 实例，下一次访问 Instance 属性时会创建一个全新的对象
                _lazyInstance = new Lazy<PaddleOCR2Helper>(() => new PaddleOCR2Helper(), LazyThreadSafetyMode.ExecutionAndPublication);
                TrOCRUtils.CleanMemory();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
             if (!_disposed)
            {
                _ocrEngine?.Dispose();
                _ocrEngine = null;
                _disposed = true;

                // 通知GC：这个对象已经由我手动清理干净了，你不需要再调用它的终结器了。
                // 这是一个好的实践，可以轻微提升性能。
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~PaddleOCR2Helper()
        {
            try
            {
                Dispose();
            }
            catch (Exception ex)
            {
               Debug.WriteLine($"PaddleOCR2 Error in finalizer: {ex.Message}");
               return;
            }
            
        }
    }
}