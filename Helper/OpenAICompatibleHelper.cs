using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Diagnostics; // 用于获取 StartupPath

namespace TrOCR.Helper
{
    internal class OpenAICompatibleHelper
    {
       
        private static readonly HttpClient httpClient = new HttpClient();

        // ===  缓存相关的静态变量 ===
        // 线程锁，防止并发读写文件冲突
        private static readonly object _configLock = new object();
        // 缓存的配置对象（内存变量）
        private static AIConfig _cachedConfig = null;
        // 上次读取配置文件的时间
        private static DateTime _lastConfigWriteTime = DateTime.MinValue;

        /// <summary>
        /// 执行 OCR 识别
        /// </summary>
        /// <returns>识别结果文本</returns>
        public static string OCR(Image image, AIMode manualMode = null)
        {
            Debug.WriteLine("传入AI接口的模式是" + JsonConvert.SerializeObject(manualMode, Formatting.Indented));
            // 1. 基础配置校验
            string baseUrl = StaticValue.OpenAICompatible_OCR_BASE_URL;
            string apiKey = StaticValue.OpenAICompatible_OCR_API_KEY;
            string modelName = StaticValue.OpenAICompatible_OCR_MODEL;
            string configJsonPath = StaticValue.OpenAICompatible_OCR_CONFIG_PATH;

            if (string.IsNullOrEmpty(baseUrl)) return "错误：未配置 BaseUrl";
            if (string.IsNullOrEmpty(apiKey)) return "错误：未配置 API Key";
            if (string.IsNullOrEmpty(modelName)) return "错误：未配置模型";
            

            // === 智能刷新配置逻辑 (检测文件变动) ===
            AIConfig freshConfig = null;
            
            // 加锁确保安全
            lock (_configLock)
            {
                if (!string.IsNullOrEmpty(configJsonPath) && File.Exists(configJsonPath))
                {
                    try
                    {
                        // 获取配置文件当前的修改时间 (不读取内容，速度极快)
                        var fileInfo = new FileInfo(configJsonPath);
                        DateTime currentWriteTime = fileInfo.LastWriteTime;

                        // 比较时间：如果文件被修改过(时间变了)，或者缓存是空的 -> 重新读取
                        if (_cachedConfig == null || currentWriteTime != _lastConfigWriteTime)
                        {
                            Debug.WriteLine("[配置更新] 检测到文件变化，正在重新读取...");
                            string jsonContent = File.ReadAllText(configJsonPath, Encoding.UTF8);
                            var loadedConfig = JsonConvert.DeserializeObject<AIConfig>(jsonContent);

                            // 检查配置文件类型是否为 ocr
                            if (loadedConfig != null && !string.Equals(loadedConfig.type, "ocr", StringComparison.OrdinalIgnoreCase))
                            {
                                return "配置错误：所选配置文件类型不匹配。\r\n当前文件 type 为: " + (loadedConfig.type ?? "null") + "，OCR 功能仅支持 type: \"ocr\"。";

                            }
                            else
                            {
                                // 更新缓存和时间戳
                                _cachedConfig = loadedConfig;
                                _lastConfigWriteTime = currentWriteTime;
                            }
                        }
                        
                        // 拿到最新的配置（不管是刚读的，还是缓存的）
                        freshConfig = _cachedConfig; 
                    }
                    catch (Exception ex)
                    {
                        return "读取配置文件出错" + ex.Message;
                    }
                }
            }

            // 1. 先准备一个“兜底”的内置安全模式 (Hardcoded Default)
            AIMode defaultSafeMode = new AIMode
            {
                mode = "默认模式 (内置)",
                system_prompt = "You are a professional OCR engine. Recognize the text in the image and output it directly. Do not use markdown code blocks. Do not output any conversational filler. Maintain the original line breaks. If the image contains code, remember to preserve the formatting.",
                prompt = "OCR this image.",
                temperature = 0.1,
                enable_thinking = false,
            };

            // 2. 确定使用的模式
            AIMode currentMode = null;

            if (manualMode != null)
            {
                // 情况A：从菜单选中了特定模式
                // === 【优化】即使传入了 manualMode，也尝试从 freshConfig 里找同名的最新版 ===
                // 这样用户修改了配置文件后，不需要切换菜单就能生效
                if (freshConfig != null && freshConfig.modes != null)
                {
                    var foundFreshMode = freshConfig.modes.FirstOrDefault(m => m.mode == manualMode.mode);
                    // 找到了就用新的（热更新生效），找不到就用传入的旧的
                    currentMode = foundFreshMode ?? manualMode;
                }
                else
                {
                    currentMode = manualMode;
                }
            }
            else
            {   
                // 情况B：没有特定模式（默认行为），尝试从 freshConfig 读取
                if (freshConfig != null && freshConfig.modes != null && freshConfig.modes.Count > 0)
                {
                    // 2. 尝试读取配置文件中保存的“模式名称”
                    string savedModeName = TrOCRUtils.LoadSetting("OpenAICompatible", "SelectedMode","");
                    AIMode foundMode = null;

                    if (!string.IsNullOrEmpty(savedModeName))
                    {
                        // 查找名称匹配的模式
                        foundMode = freshConfig.modes.FirstOrDefault(m => m.mode == savedModeName);
                    }

                    if (foundMode == null)
                    {
                        // === 【直接报错】 ===
                        return $"配置错误：无法找到模式“{savedModeName}”。\r\n原因：该模式可能已被从配置文件中删除或重命名。\r\n解决方法：请点击菜单重新选择一个有效的模式。";
                    }

                    currentMode = foundMode;
                }
                else
                {

                    //CommonHelper.ShowHelpMsg("配置文件不存在，将使用程序内置的默认模式");                    
                    // Debug.WriteLine("配置文件不存在或无效，将使用程序内置的默认模式");
                    // 4. 连配置文件都读不到 -> 直接用内置默认
                    currentMode = defaultSafeMode;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[Helper] 最终使用的模式名称: {currentMode.mode}");
            System.Diagnostics.Debug.WriteLine($"[Helper] 最终使用的模式详情: {JsonConvert.SerializeObject(currentMode, Formatting.Indented)}");
            
            try
            {
                // 3. 内存图片转 Base64 (直接使用传入的 Image 对象)
                string base64Image = ImageToBase64(image);
                if (string.IsNullOrEmpty(base64Image)) return "错误：图片转换失败";

                // === 4. 动态组装请求体 ===

                // 4.1 动态构建 messages 列表
                var messagesList = new List<object>();

                // === 顺序控制 ===
                // 1. System Prompt (系统提示词) - 始终在最前
                if (!string.IsNullOrEmpty(currentMode.system_prompt))
                {
                    messagesList.Add(new { role = "system", content = currentMode.system_prompt });
                }

                // 2. Assistant Prompt (助手提示词) - 紧随 System 之后
                // 作用：用于 Few-Shot (少样本) 示例，或者维持对话上下文
                if (!string.IsNullOrEmpty(currentMode.assistant_prompt))
                {
                    messagesList.Add(new { role = "assistant", content = currentMode.assistant_prompt });
                }

                // 3. User Prompt (用户提示词 + 图片) - 放在最后
                var userContentList = new List<object>();

                // 只有当 prompt 不为空时，才添加 text 类型的节点
                if (!string.IsNullOrEmpty(currentMode.prompt))
                {
                    userContentList.Add(new { type = "text", text = currentMode.prompt });
                }

                // 图片是必须添加的
                userContentList.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } });

                // 将构建好的 content 放入 user message
                messagesList.Add(new
                {
                    role = "user",
                    content = userContentList
                });

                
                // 4.2 构造请求体
                var requestBody = new
                {
                    // 1. 必填项：直接赋值
                    model = modelName,
                    messages = messagesList,

                    // 2. 温度 (Temperature) - 处理可空数值
                    temperature = currentMode.temperature,

                    // 3. 思考模式 (Thinking) - 处理复杂的嵌套对象逻辑
                    thinking = currentMode.enable_thinking.HasValue
                    // [分支 1] 如果 HasValue 为 true
                    ? new
                    {
                        type = currentMode.enable_thinking.Value ? "enabled" : "disabled"
                    }
                    // [分支 2] 如果 HasValue 为 false
                    : null
                };
            

                // 5. 发送请求
                string endpoint = baseUrl.TrimEnd('/');
                if (!endpoint.EndsWith("/chat/completions")) endpoint += "/chat/completions";
                var jsonSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody, jsonSettings), Encoding.UTF8, "application/json");
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = httpClient.PostAsync(endpoint, content).Result;
                string responseString = response.Content.ReadAsStringAsync().Result;

                if (!response.IsSuccessStatusCode) return $"API 请求失败: {response.StatusCode}\n{responseString}";

                JObject resultJson = JObject.Parse(responseString);
                string textResult = resultJson["choices"]?[0]?["message"]?["content"]?.ToString();

                return textResult ?? "未收到有效内容";
            }
            catch (Exception ex)
            {
                return $"发生异常: {ex.Message}";
            }
        }

        /// <summary>
        /// 将图片转换为 Base64 字符串
        /// </summary>
        private static string ImageToBase64(Image image)
        {
            try
            {
                if (image == null) return null;
                using (MemoryStream m = new MemoryStream())
                {
                    // 统一保存为 Jpeg 格式以减小体积，或者使用 image.RawFormat
                    image.Save(m, ImageFormat.Jpeg); 
                    byte[] imageBytes = m.ToArray();
                    return Convert.ToBase64String(imageBytes);
                }
            }
            catch
            {
                return null;
            }
        }
    }

    // 实体类定义
    public class AIConfig
    {
        public string type { get; set; }
        public List<AIMode> modes { get; set; }
    }

    public class AIMode
    {
        public string mode { get; set; }
        public string description { get; set; }
        public string system_prompt { get; set; }
        //user_prompt
        public string prompt { get; set; }
        public string assistant_prompt { get; set; }
        // 改为可空类型 (double?)，如果 json 里没填，值为 null
        public double? temperature { get; set; }

        // 改为可空类型 (bool?)，如果 json 里没填，值为 null
        public bool? enable_thinking { get; set; }
    }
}