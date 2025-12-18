// 文件：TrOCR\FmMain.AI.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text; // 确保引用
using System.Windows.Forms;
using Newtonsoft.Json;
using TrOCR.Helper;

namespace TrOCR
{
    public partial class FmMain
    {
        // 用于存储当前选中的 AI 模式，如果为 null 则使用默认逻辑
        private AIMode currentSelectedAIMode = null;

        /// <summary>
        /// 加载 AI 配置文件并初始化动态菜单
        /// </summary>
        private void LoadAIConfigMenus()
        {
            try
            {
                // 1. 获取配置文件路径 (假设在 Data 目录下)
                // 这里我们优先使用 Ini 中配置的路径，如果没有则尝试默认路径
                string configPath = IniHelper.GetValue("OpenAICompatible", "Config");
                if (string.IsNullOrEmpty(configPath))
                {
                    configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "AIConfig.json");
                }

                // 2. 如果文件不存在，直接返回，保持 Designer 中定义的默认行为（三级菜单点击）
                if (!File.Exists(configPath))
                {
                    return;
                }

                // 3. 读取并解析配置
                string jsonContent = File.ReadAllText(configPath, Encoding.UTF8);
                AIConfig aiConfig = JsonConvert.DeserializeObject<AIConfig>(jsonContent);

                // 4. 如果配置有效且有 modes
                if (aiConfig != null && aiConfig.modes != null && aiConfig.modes.Count > 0)
                {
                    // === 核心逻辑：转换为四级菜单 ===

                    // A. 移除父级菜单原有的点击事件 (使其点击只展开子菜单，不再直接触发 OCR)
                    this.ai_openai_compatible.Click -= new EventHandler(this.OCR_ai_openai_compatible_Click);
                    
                    // B. 清空可能存在的旧项
                    this.ai_openai_compatible.DropDownItems.Clear();

                    // C. 动态添加子菜单项
                    foreach (var mode in aiConfig.modes)
                    {
                        ToolStripMenuItem modeItem = new ToolStripMenuItem();
                        modeItem.Text = mode.mode; // 显示名称
                        modeItem.Tag = mode;       // 将 mode 对象存储在 Tag 中
                        modeItem.Click += new EventHandler(this.AI_SubMenu_Click); // 绑定点击事件

                        // 可以根据描述添加 ToolTipText
                        if (!string.IsNullOrEmpty(mode.description))
                        {
                            modeItem.ToolTipText = mode.description;
                        }

                        this.ai_openai_compatible.DropDownItems.Add(modeItem);
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果解析出错，不抛出异常，保持默认的三级菜单状态，但在调试窗口输出
                System.Diagnostics.Debug.WriteLine("加载 AI 菜单失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 动态生成的四级子菜单点击事件
        /// </summary>
        private void AI_SubMenu_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem clickedItem && clickedItem.Tag is AIMode mode)
            {
                // UI更新：实现单选效果
                foreach (ToolStripItem item in this.ai_openai_compatible.DropDownItems)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        // 只有当前点击的项设为 true
                        menuItem.Checked = (menuItem == clickedItem);
                    }
                }
                // 1. 记录当前选中的模式
                this.currentSelectedAIMode = mode;

                // 2. 更新父菜单的选中状态或文本（可选，用于提示用户当前选了哪个）
                // 比如：this.ai_openai_compatible.Text = $"OpenAICompatible ({mode.mode})";
                
                // 3. 触发 OCR 流程
                // 注意：这里调用 OCR_foreach 会触发 Main_OCR_Thread，最终调用 OCR_OpenAICompatible
                OCR_foreach("OpenAICompatible");
            }
        }

        /// <summary>
        /// OpenAICompatible OCR 执行入口 (被 Main_OCR_Thread 调用)
        /// </summary>
        public void OCR_OpenAICompatible()
        {
            try
            {
                // 调用 Helper，并传入当前选中的模式 (currentSelectedAIMode)
                // 如果是从三级菜单（默认无配置）进来的，currentSelectedAIMode 为 null，Helper 会处理
                string result = OpenAICompatibleHelper.OCR(image_screen, this.currentSelectedAIMode);

                if (string.IsNullOrEmpty(result))
                {
                    typeset_txt = "未识别到文本或接口返回为空。";
                }
                else
                {
                    typeset_txt = result;
                }
                //必须同时设置 split_txt
                split_txt = typeset_txt;
                
                // 识别完成后，建议将模式重置为空，或者保留上次选择（取决于你的需求）
                // 如果希望每次截图都重置为默认，取消下面的注释：
                // this.currentSelectedAIMode = null; 
            }
            catch (Exception ex)
            {
                typeset_txt = "OpenAICompatible 接口调用出错: " + ex.Message;
                split_txt = typeset_txt;
            }
        }
    }
}