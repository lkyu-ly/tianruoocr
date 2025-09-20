// PaddleOCRHelper.cs
using PaddleOCRSDK;
using System;
using System.Drawing;
using System.IO;
using System.Reflection; // 关键：引入反射命名空间
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// PaddleOCR引擎的帮助类，用于封装初始化、文本识别和资源管理等逻辑。
/// 该类采用单例模式，确保引擎只被初始化一次。
/// </summary>
public sealed class PaddleOCRHelper
{
    #region 单例实现 (Singleton Implementation)

    private static readonly Lazy<PaddleOCRHelper> lazyInstance = 
        new Lazy<PaddleOCRHelper>(() => new PaddleOCRHelper());

    /// <summary>
    /// 获取 PaddleOCRHelper 的唯一实例。
    /// </summary>
    public static PaddleOCRHelper Instance => lazyInstance.Value;

    private Lazy<IOCRService> ocrServiceLazy;

    /// <summary>
    /// 私有构造函数，防止外部直接创建实例。
    /// </summary>
    private PaddleOCRHelper()
    {
        InitializeOcrService();
    }

    #endregion

    #region 引擎初始化与重置 (Engine Initialization and Reset)

    /// <summary>
    /// 配置 IOCRService 的懒加载初始化逻辑。
    /// </summary>
    private void InitializeOcrService()
    {
        ocrServiceLazy = new Lazy<IOCRService>(() =>
        {
            IOCRService service = new OCRService();
            string modelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");

            if (!Directory.Exists(modelsPath) || !File.Exists(Path.Combine(modelsPath, "ppocr_keys.txt")))
            {
                throw new DirectoryNotFoundException(
                    "PaddleOCR 模型目录未找到或不完整。" +
                    $"请确保 'models' 目录存在于 '{modelsPath}'，并包含所需的模型文件和 ppocr_keys.txt。");
            }

            var para = new InitParamater
            {
                det_infer = Path.Combine(modelsPath, "PP-OCRv4_mobile_det_infer"),
                rec_infer = Path.Combine(modelsPath, "PP-OCRv4_mobile_rec_infer"),
                cls_infer = Path.Combine(modelsPath, "ch_ppocr_mobile_v2.0_cls_infer"),
                keyFile = Path.Combine(modelsPath, "ppocr_keys.txt"),
                paraType = EnumParaType.Class,
                ocrpara = new OCRParameter
                {
                    cpu_threads = 10,
                    enable_mkldnn = false,//导致内存泄漏的罪魁祸首：enable_mkldnn = true，但是改成false后识别速度非常慢
                    cls = false,
                    use_angle_cls = false,
                    use_gpu = false,
                    det = true,
                    rec = true
                }
            };

            bool success = service.Init(para);
            if (!success)
            {
                string error = service.GetError();
                throw new Exception($"初始化 PaddleOCR 引擎失败。错误信息: {error}");
            }

            return service;
        });
    }

    /// <summary>
    /// 【已更新】释放由PaddleOCR引擎使用的原生资源。
    /// 由于无法直接访问 internal 的 OCRSDK.FreeEngine() 方法，我们使用反射来调用它。
    /// </summary>
    public static void Reset()
    {
        if (lazyInstance.IsValueCreated && lazyInstance.Value.ocrServiceLazy.IsValueCreated)
        {
            try
            {
                // --- 使用反射调用 internal static 方法 ---

                // 1. 获取SDK所在的程序集 (Assembly)
                // 我们可以通过一个已知的 public 类型（如 OCRService）来定位它。
                Assembly sdkAssembly = typeof(OCRService).Assembly;

                // 2. 从程序集中按名称查找 internal 的 OCRSDK 类型
                // 需要提供完整的命名空间和类名。
                Type ocrSdkType = sdkAssembly.GetType("PaddleOCRSDK.OCRSDK");

                if (ocrSdkType != null)
                {
                    // 3. 查找名为 "FreeEngine" 的 static 和 non-public 方法
                    MethodInfo freeEngineMethod = ocrSdkType.GetMethod("FreeEngine", BindingFlags.Static | BindingFlags.NonPublic);

                    if (freeEngineMethod != null)
                    {
                        // 4. 调用方法。对于静态方法，第一个参数是 null。
                        freeEngineMethod.Invoke(null, null);
                    }
                    else
                    {
                        // 如果找不到方法，可能SDK版本有变动
                        throw new MissingMethodException("在 PaddleOCRSDK.OCRSDK 中未找到 FreeEngine 方法。");
                    }
                }
                else
                {
                    // 如果找不到类型，可能SDK结构发生了变化
                    throw new TypeLoadException("无法从SDK程序集中加载 PaddleOCRSDK.OCRSDK 类型。");
                }
            }
            catch (Exception ex)
            {
                // 记录或处理释放引擎时可能发生的错误
                System.Diagnostics.Debug.WriteLine($"通过反射释放 PaddleOCR 引擎时出错: {ex.Message}");
            }
            finally
            {
                // 无论成功与否，都重置初始化器，以便下次可以重新加载引擎
                lazyInstance.Value.InitializeOcrService();
            }
        }
    }

    #endregion

    #region 公共识别方法 (Public Recognition Method)

    /// <summary>
    /// 异步地从指定的图像中识别文本。
    /// </summary>
    /// <param name="image">要执行OCR的 System.Drawing.Image 对象。</param>
    /// <returns>一个包含识别结果的字符串。如果识别失败，则返回格式化的错误信息。</returns>
    public async Task<string> RecognizeTextAsync(Image image)
    {
        if (image == null)
        {
            return "***传入的图像为空***";
        }

        try
        {
            IOCRService currentService = ocrServiceLazy.Value;

            return await Task.Run(() =>
            {
                byte[] imageBytes;
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    imageBytes = ms.ToArray();
                }

                OCRResult ocrResult = currentService.Detect(imageBytes);

                if (ocrResult == null || ocrResult.WordsResult.Count == 0)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();
                foreach (var item in ocrResult.WordsResult)
                {
                    sb.AppendLine(item.Words);
                }

                return sb.ToString().Trim();
            });
        }
        catch (Exception ex)
        {
            return $"***PaddleOCR 发生错误: {ex.Message}***";
        }
    }

    #endregion
}