using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShareX.ScreenCaptureLib;
using TrOCR.Helper;
using PaddleOCRSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using Timer = System.Windows.Forms.Timer;
using TencentCloud.Common;
using TencentCloud.Common.Profile;
using TencentCloud.Tmt.V20180321;
using TencentCloud.Tmt.V20180321.Models;
using System.Data;
// ReSharper disable StringLiteralTypo

namespace TrOCR
{
	/// <summary>
	/// OCR主窗体类，提供文字识别、翻译、语音朗读等核心功能
	/// 负责处理系统托盘交互、快捷键响应、图像识别、文本处理和翻译等主要业务逻辑
	/// </summary>
	public sealed partial class FmMain
	{
		private Size lastNormalSize; // 用于在整个会话中跟踪“基础”窗口大小

		private string lastClipboardText = "";  // 用于防止重复翻译

		private const int WM_DRAWCLIPBOARD = 776; // 定义消息常量
		private bool isAppLoading = true; // 【新增】用于标记程序是否正在加载

		private Timer clipboardDebounceTimer; // 【新增】用于剪贴板防抖的定时器

		private bool isAutoCopying = false;     // 【新增】标志位，表示程序正在进行自动复制
		private Timer autoCopyLockTimer;        // 【新增】用于自动复制后创建静默期的定时器

 		private bool isOriginalTextHidden = false; // 新增：用于跟踪原文窗口的显隐状态

		private bool isScreenshotTranslateMode = false; // 【新增】截图翻译模式标志

		// private DataTable lastRecognizedTable; // 存储最近一次识别的表格数据，不再需要
		private List<string> lastRecognizedHeader; // 新增
		private List<string> lastRecognizedFooter; // 新增

		private List<BaiduOcrHelper.CellInfo> lastBaiduCells;
		private List<TencentOcrHelper.TableCell> lastTencentCells;
		private string lastOcrProvider; // 用于区分是百度还是腾讯

		private bool isProgrammaticResize = false; // 用于屏蔽Form_Resize事件的标志位

	    // 【新增】用于手动计算托盘点击次数的计数器
    	private int trayClickCount = 0;

		private static Size settingWindowSize = new Size(0, 0); // 初始为0，记录设置窗口的大小

        // 定义是否在流式输出
        private bool isStreaming = false;
        // 定义是否在流式翻译输出
        private volatile bool isTransStreaming = false;

        // ====================================================================================================================
        // **构造函数与窗体事件**
        //
        // 负责窗体的初始化、加载、关闭以及核心窗口消息处理（WndProc）。
        // - FmMain(): 初始化组件、设置初始状态、加载配置、注册剪贴板查看器和热键。
        // - Load_Click(): 处理窗体加载事件，最小化并隐藏窗体。
        // - WndProc(): 窗口过程函数，用于处理系统消息，如热键、剪贴板变化、窗口状态改变等。
        // ====================================================================================================================
        #region 构造函数与窗体事件
        /// <summary>
        /// 初始化FmMain窗体实例，设置初始状态，加载配置并注册剪贴板监视器
        /// </summary>
        public FmMain()
		{
			// 初始化标志位
			set_merge = false;
			set_split = false;
			set_split = false;
			StaticValue.IsCapture = false;
			pinyin_flag = false;
			tranclick = false;

			// 初始化图像列表
			imagelist = new List<Image>();

			// 从配置文件读取记录数目并初始化笔记数组
			StaticValue.NoteCount = Convert.ToInt32(IniHelper.GetValue("配置", "记录数目"));
			baidu_flags = "";
			esc = "";
			voice_count = 0;
			fmNote = new FmNote(this);
			pubnote = new string[StaticValue.NoteCount];
			for (var i = 0; i < StaticValue.NoteCount; i++)
			{
				pubnote[i] = "";
			}
			StaticValue.v_note = pubnote;
			StaticValue.mainHandle = Handle;

			// 设置字体大小
			Font = new Font(Font.Name, 9f / StaticValue.DpiFactor, Font.Style, Font.Unit, Font.GdiCharSet, Font.GdiVerticalFont);
			googleTranslate_txt = "";
			num_ok = 0;
			F_factor = Program.Factor;
			components = null;

			// 【新增】开启双缓冲，减少闪烁
			// 加上这个双缓冲,闪烁就变成黑屏了，不加了(ps:更准确的说是黑底，不是黑屏)
			//SuspendLayout 是解决布局闪烁（控件乱跳）的，而 DoubleBuffered 是解决绘制闪烁（背景重绘）的？不知道对不对
			//注意：这里开启双缓冲，DoubleBuffered = true 只对 Form（窗体）本身生效，无法遗传给子控件
			// this.DoubleBuffered = true;
			//禁止系统擦背景(AllPaintingInWmPaint) + 自己绘制(UserPaint)
			// this.SetStyle(ControlStyles.UserPaint, true);
			// this.SetStyle(ControlStyles.AllPaintingInWmPaint, true); 
			// this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
		    
			//这个不知道加上有什么用，要不要加：
            // this.UpdateStyles(); // 强制应用样式
			
            //  手动设置一个背景色，防止默认黑底
            // this.BackColor = Color.White;
			//这个设置的颜色不知道为什么不生效，还是黑底
			
			// 初始化组件和系统设置
			InitializeComponent();
			this.lastNormalSize = this.Size;
			StaticValue.LoadConfig();//这个代码加不加都行，fmsetting.cs和program.cs里使用就足够了,加上更健壮
			// ====================【新增代码开始】====================
			// 加载并应用记忆的窗口大小
			LoadWindowState();
			LogState("Constructor End (Initial State)"); // <--- 添加这一行
                                                         // ====================【新增代码结束】====================
            // 默认给 AI 菜单绑定“未设置ai接口报错事件”
            // 稍后在 Load 方法里，如果发现有配置，会把它们解绑的
            this.ai_menu.MouseDown += ShowConfigWarning_MouseDown;
            this.ai_menu_trans.MouseDown += ShowConfigWarning_MouseDown;
            if (translationTimer == null)
            {
                translationTimer = new Timer();
                translationTimer.Tick += TranslationTimer_Tick;
				// 【修改】从 Raw 字符串中解析出基础时间间隔
				int initDelay = 5000; // 默认安全值 (万一还没加载配置或配置为空)
				string rawConfig = StaticValue.TextChangeAutotranslateDelayRaw;

				if (!string.IsNullOrWhiteSpace(rawConfig))
				{
					// 分割字符串 (处理 "2000,OpenAI" 这种情况)，取逗号前的第一项
					var parts = rawConfig.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
					
					// 尝试解析第一个部分为数字
					if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out int parsedVal))
					{
						// 只有大于0才是合法的 Interval
						if (parsedVal > 0) 
						{
							initDelay = parsedVal;
						}
					}
				}

				// 赋值给 Timer (确保 > 0，防止崩溃)
				translationTimer.Interval = initDelay;
            }

			// ====================【新增代码开始】====================
			clipboardDebounceTimer = new Timer();
			clipboardDebounceTimer.Interval = 150; // 设置一个较短的延迟，150毫秒足够
			clipboardDebounceTimer.Tick += ClipboardDebounceTimer_Tick;
			autoCopyLockTimer = new Timer();
			autoCopyLockTimer.Interval = 500; // 500毫秒的静默期，足以忽略所有连锁反应
			autoCopyLockTimer.Tick += (sender, e) =>
			{
				autoCopyLockTimer.Stop();
				isAutoCopying = false; // 静默期结束，解锁
				Debug.WriteLine("--- 自动复制锁已解除 ---");
			};
			// ====================【新增代码结束】====================

			nextClipboardViewer = (IntPtr)HelpWin32.SetClipboardViewer((int)Handle);
			InitMinimize();
			InitConfig();

			// 设置窗口初始状态为最小化并隐藏
			WindowState = FormWindowState.Minimized;
			Visible = false;
			split_txt = "";
			// MinimumSize = new Size((int)font_base.Width * 23, (int)font_base.Height * 24);
			// 可以调整可拖拽的最小窗口宽高,暂时先不加最小size限制
			// MinimumSize = new Size((int)(200 * F_factor), (int)(200 * F_factor));

			// 初始化OCR功能
			OCR_foreach("");
			//设置隐藏原文按钮样式
			btnToggleOriginalText.FlatStyle = FlatStyle.Flat;
			btnToggleOriginalText.FlatAppearance.BorderSize = 0; // 无边框
			btnToggleOriginalText.FlatAppearance.MouseOverBackColor = Color.FromArgb(224, 224, 224); // 悬浮时为浅灰色
			btnToggleOriginalText.FlatAppearance.MouseDownBackColor = Color.FromArgb(192, 192, 192); // 按下时为中灰色
			// // 可以将默认背景设为透明或与父容器一致
			// // btnToggleOriginalText.BackColor = Color.Transparent; 
			//另一种样式
			// btnToggleOriginalText.FlatStyle = FlatStyle.Flat;
			// btnToggleOriginalText.FlatAppearance.BorderSize = 0; // 确保无边框
			// btnToggleOriginalText.BackColor = Color.Transparent;
			// btnToggleOriginalText.ForeColor = Color.DimGray;
			// btnToggleOriginalText.FlatAppearance.MouseOverBackColor = Color.Gainsboro; // #E6E6E6
			// btnToggleOriginalText.FlatAppearance.MouseDownBackColor = Color.Silver;    // #C0C0C0
			//另一种样式
			// btnToggleOriginalText.FlatStyle = FlatStyle.Flat;
			// btnToggleOriginalText.FlatAppearance.BorderSize = 0;
			// btnToggleOriginalText.BackColor = Color.WhiteSmoke; // #F5F5F5
			// btnToggleOriginalText.ForeColor = Color.Black;
			// btnToggleOriginalText.FlatAppearance.MouseOverBackColor = Color.LightGray; // #D3D3D3
			// btnToggleOriginalText.FlatAppearance.MouseDownBackColor = Color.DarkGray;  // #A9A9A9

			// 【新增】订阅翻译框(RichBoxBody_T)的临时翻译请求事件
			this.RichBoxBody_T.TemporaryTranslateRequested += RichBoxBody_T_OnTemporaryTranslateRequested;
	
        }
        // FmMain.cs

        // 这是一个专门用来“拦截并报错”的事件
        private void ShowConfigWarning_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            // 判断是哪个菜单触发的，显示对应的提示
            string msg = "检测到您尚未配置 AI 接口。\n\n请先去设置里配置";
            if (sender == ai_menu_trans) msg = "检测到您尚未配置 AI 翻译接口。\n\n请先去设置里配置";

            MessageBox.Show(msg, "配置提示", MessageBoxButtons.OK, MessageBoxIcon.None);

        }

        /// <summary>
        /// 点击加载按钮时触发的事件处理函数，将窗体最小化并隐藏
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void Load_Click(object sender, EventArgs e)
		{
			WindowState = FormWindowState.Minimized;
			Visible = false;
		}
		/// <summary>
		/// 从配置文件加载并应用窗口的基础尺寸
		/// </summary>
		private void LoadWindowState()
		{
			try
			{
					
				// 步骤 1: 尝试从 ini 文件读取上次保存的“逻辑尺寸”
		        bool widthParsed = int.TryParse(IniHelper.GetValue("WindowState", "Width"), out int savedLogicalWidth);
		        bool heightParsed = int.TryParse(IniHelper.GetValue("WindowState", "Height"), out int savedLogicalHeight);

		        if (widthParsed && heightParsed)
		        {
		            // 步骤 2: 将读取的逻辑尺寸，乘以当前的缩放因子，得到最终的物理像素尺寸
		            int finalWidth = (int)(savedLogicalWidth * F_factor);
		            int finalHeight = (int)(savedLogicalHeight * F_factor);

		            // 步骤 3: 【健壮性检查】确保最终的像素尺寸不会过小或超出屏幕,这里最小宽高不一样,是长方形窗口,改成最小宽高一样是正方形也行
		            int minWidth = (int)(18f * F_factor * 13);
		            int minHeight = (int)(17f * F_factor * 14);

		            finalWidth = Math.Max(finalWidth, minWidth);
		            finalHeight = Math.Max(finalHeight, minHeight);

		            finalWidth = Math.Min(finalWidth, Screen.PrimaryScreen.WorkingArea.Width);
		            finalHeight = Math.Min(finalHeight, Screen.PrimaryScreen.WorkingArea.Height);

		            // 步骤 4: 将计算出的最终像素尺寸应用给当前窗口
		            this.Size = new Size(finalWidth, finalHeight);
		        }
		    }
			catch (Exception ex)
			{
				// 如果发生意外错误，打印日志，防止程序崩溃
				System.Diagnostics.Debug.WriteLine($"加载窗口大小失败: {ex.Message}");
			}
		}
        private void SaveWindowState()
        {
            // 我们一直跟踪的 lastNormalSize 是像素尺寸，这里需要将它转换为逻辑尺寸再保存
            // 将像素尺寸除以缩放因子，得到DPI无关的逻辑尺寸
            int logicalWidth = (int)(this.lastNormalSize.Width / F_factor);
            int logicalHeight = (int)(this.lastNormalSize.Height / F_factor);

            IniHelper.SetValue("WindowState", "Width", logicalWidth.ToString());
            IniHelper.SetValue("WindowState", "Height", logicalHeight.ToString());
        }

		// 【新增】临时翻译事件的处理器
		private void RichBoxBody_T_OnTemporaryTranslateRequested(object sender, TempTranslateEventArgs e)
		{
					// 【新增调试代码】弹窗显示接收到的语言代码
					// MessageBox.Show($"接收到临时翻译请求：\n源语言: {e.SourceLanguage}\n目标语言: {e.TargetLanguage}");
					// 确保翻译的文本是最新的
					typeset_txt = RichBoxBody.Text;

			if (string.IsNullOrWhiteSpace(typeset_txt))
			{
				MessageBox.Show("请输入需要翻译的文本！");
				return;
			}

			// 调用我们改造后的翻译方法，并传入临时的语言代码
			trans_Calculate(e.SourceLanguage, e.TargetLanguage);
		}
        /// <summary>
        /// 重写Windows窗体的消息处理方法，用于处理发送到窗口的各种消息
        /// </summary>
        /// <param name="m">包含Windows消息信息的Message结构体引用</param>
        protected override void WndProc(ref Message m)
		{
			// 在方法的开始部分添加这个 switch 结构
    		switch (m.Msg)
    		{
    		    case WM_DRAWCLIPBOARD:
    		        // 首先，必须将消息传递给链中的下一个查看器
    		        HelpWin32.SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
					Debug.WriteLine("发送了消息"+m.Msg);

                    // 然后，执行我们自己的逻辑
                    HandleClipboardChange();
    		        return; // 直接返回，不再执行 base.WndProc
    		}

			if (m.Msg == 953)
			{
				speaking = false;
			}
			if (m.Msg == 274 && (int)m.WParam == 61536)
			{
				// ====================【新增：窗口关闭/隐藏时解绑事件】====================
				// 既然窗口都要隐藏了，肯定不需要监听输入了
				 // 1. 【核心】解绑事件，防止窗口隐藏/恢复过程中的误触
				RichBoxBody.richTextBox1.TextChanged -= RichBoxBody_TextChanged;
				
				// 可选：如果你希望关闭窗口时同时也重置翻译界面（恢复单栏），可以调用这个：
				// Trans_close_Click(null, null, false); 
				// 但通常仅仅隐藏窗口保留状态体验更好，所以只解绑事件即可
				 // 2. 【核心】停止自动翻译计时器，防止后台读秒结束触发翻译
    			if (translationTimer != null) translationTimer.Stop();
				// ====================【新增结束】====================
				WindowState = FormWindowState.Minimized;
				Visible = false;
				return;
			}
			if (m.Msg == 600 && (int)m.WParam == 725)
			{
				if (IniHelper.GetValue("工具栏", "顶置") == "True")
				{
					TopMost = true;
					return;
				}
				TopMost = false;
				return;
			}

			if (m.Msg == 786 && m.WParam.ToInt32() == 530 && RichBoxBody.Text != null)
			{
				p_note(RichBoxBody.Text);
				StaticValue.v_note = pubnote;
				if (fmNote.Created)
				{
					fmNote.TextNote = "";
				}
			}
			if (m.Msg == 786 && m.WParam.ToInt32() == 520)
			{
				fmNote.Show();
				fmNote.Focus();
				fmNote.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - fmNote.Width, Screen.PrimaryScreen.WorkingArea.Height - fmNote.Height);
				fmNote.WindowState = FormWindowState.Normal;
				return;
			}
			if (m.Msg == 786 && m.WParam.ToInt32() == 580)
			{
				HelpWin32.UnregisterHotKey(Handle, 205);
				change_QQ_screenshot = false;
				FormBorderStyle = FormBorderStyle.None;
				Hide();
				if (transtalate_fla == "开启")
				{
					form_width = Width / 2;
				}
				else
				{
					form_width = Width;
				}
				form_height = Height;
				minico.Visible = false;
				minico.Visible = true;
				menu.Close();
				menu_copy.Close();
				auto_fla = "开启";
				split_txt = "";
				RichBoxBody.richTextBox1.TextChanged -= RichBoxBody_TextChanged;
				// 避免不必要的文本更新
				if (RichBoxBody.Text != "***该区域未发现文本***")
				{
					RichBoxBody.Text = "***该区域未发现文本***";
				}
				RichBoxBody.richTextBox1.TextChanged += RichBoxBody_TextChanged;
				RichBoxBody_T.Text = "";
				typeset_txt = "";
				transtalate_fla = "关闭";
				Trans_close.PerformClick();
				Size = new Size((int)font_base.Width * 23, (int)font_base.Height * 24);
				FormBorderStyle = FormBorderStyle.Sizable;
				StaticValue.IsCapture = true;
				image_screen = StaticValue.image_OCR;
				if (IniHelper.GetValue("工具栏", "分栏") == "True")
				{
					minico.Visible = true;
					thread = new Thread(ShowLoading);
					thread.Start();
					ts = new TimeSpan(DateTime.Now.Ticks);
					var image = image_screen;
					var image2 = new Bitmap(image.Width, image.Height);
					var graphics = Graphics.FromImage(image2);
					graphics.DrawImage(image, 0, 0, image.Width, image.Height);
					graphics.Save();
					graphics.Dispose();
					image_ori = image2;
					((Bitmap)FindBoundingBoxFences((Bitmap)image)).Save("Data\\分栏预览图.jpg");
				}
				else
				{
					minico.Visible = true;
					thread = new Thread(ShowLoading);
					thread.Start();
					ts = new TimeSpan(DateTime.Now.Ticks);
					var messageLoad = new Messageload();
					messageLoad.ShowDialog();
					if (messageLoad.DialogResult == DialogResult.OK)
					{
						esc_thread = new Thread(Main_OCR_Thread);
						esc_thread.Start();
					}
				}
			}
			if (m.Msg == 786 && m.WParam.ToInt32() == 590 && speak_copyb == "朗读")
			{
				if (ActiveControl.Name == "htmlTextBoxBody")
				{
					//优先使用选中的文本；如果未选中任何文本，则使用全部文本
    				htmltxt = !string.IsNullOrEmpty(RichBoxBody.SelectText) 
              		? RichBoxBody.SelectText 
              		: RichBoxBody.Text;
				}
				if (ActiveControl.Name == "rich_trans")
				{
					htmltxt = RichBoxBody_T.Text;
				}
				if (htmltxt == "")
				{
					return;
				}
				TTS();
				return;
			}
			if (m.Msg == 786 && m.WParam.ToInt32() == 511)
			{
				//这里的代码和Trans_close_Click基本上一模一样，可以直接调用Trans_close_Click
				// 【核心优化】在执行任何关闭操作前，先检查原文是否被隐藏
    			if (isOriginalTextHidden)
    			{
    			    // 如果原文是隐藏的，则弹出提示，并阻止后续的关闭操作
    			    MessageBox.Show("请先点击 ▶ 按钮恢复原文，再关闭翻译窗口。", "操作提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    			    return; // 直接返回，不执行关闭
    			}
				btnToggleOriginalText.Visible = false;
				isOriginalTextHidden = false;
				// MinimumSize = new Size((int)font_base.Width * 23, (int)font_base.Height * 24);
				RichBoxBody_T.Visible = false;
				panelSeparator.Visible = false;
				PictureBox1.Visible = false;
				RichBoxBody_T.Text = "";
				if (WindowState == FormWindowState.Maximized)
				{
					WindowState = FormWindowState.Normal;
				}
				//3. 设置模式标志
                transtalate_fla = "关闭";
				// 4. 【关键】先恢复窗口尺寸
				// Size = new Size((int)font_base.Width * 23, (int)font_base.Height * 24);
                Size = this.lastNormalSize;
				// 5. 【关键】最后再修改Dock属性
                RichBoxBody.Dock = DockStyle.Fill;
               
			}
			if (m.Msg == 786 && m.WParam.ToInt32() == 512)
			{
				TransClick();
			}
			if (m.Msg == 786 && m.WParam.ToInt32() == 518)
			{
				if (ActiveControl.Name == "htmlTextBoxBody")
				{
					//优先使用选中的文本；如果未选中任何文本，则使用全部文本
    				htmltxt = !string.IsNullOrEmpty(RichBoxBody.SelectText) 
              		? RichBoxBody.SelectText 
              		: RichBoxBody.Text;
				}
				if (ActiveControl.Name == "rich_trans")
				{
					htmltxt = RichBoxBody_T.Text;
				}
				if (htmltxt == "")
				{
					return;
				}
				TTS();
			}
			if (m.Msg == 161)
			{
				HelpWin32.SetForegroundWindow(Handle);
				base.WndProc(ref m);
				return;
			}
			if (m.Msg != 163)
			{
				if (m.Msg == 786 && m.WParam.ToInt32() == 222)
				{
					try
					{
						StaticValue.IsCapture = false;
						esc = "退出";
						fmloading.FmlClose = "窗体已关闭";
						esc_thread.Abort();
					}
					catch (Exception ex)
					{
						MessageBox.Show(ex.Message);
					}
					FormBorderStyle = FormBorderStyle.Sizable;
					Visible = true;
					Show();
					WindowState = FormWindowState.Normal;
					if (IniHelper.GetValue("快捷键", "翻译文本") != "请按下快捷键")
					{
						var value = IniHelper.GetValue("快捷键", "翻译文本");
						var text = "None";
						var text2 = "F9";
						SetHotkey(text, text2, value, 205);
					}
					HelpWin32.UnregisterHotKey(Handle, 222);
				}
				if (m.Msg == 786 && m.WParam.ToInt32() == 200)
				{
					HelpWin32.UnregisterHotKey(Handle, 205);
					menu.Hide();
					RichBoxBody.Hide = "";
					RichBoxBody_T.Hide = "";
					MainOCRQuickScreenShots();
				}
				if (m.Msg == 786 && m.WParam.ToInt32() == 206)
				{
					if (!fmNote.Visible || Focused)
					{
						fmNote.Show();
						fmNote.WindowState = FormWindowState.Normal;
						fmNote.Visible = true;
					}
					else
					{
						fmNote.Hide();
						fmNote.WindowState = FormWindowState.Minimized;
						fmNote.Visible = false;
					}
				}
				if (m.Msg == 786 && m.WParam.ToInt32() == 235)
				{
					if (!Visible)
					{
						TopMost = true;
						Show();
						WindowState = FormWindowState.Normal;
						Visible = true;
						Thread.Sleep(100);
						 // 2. 【核心修正】在延时之后，窗口绝对稳定了，再绑定
						RichBoxBody.richTextBox1.TextChanged -= RichBoxBody_TextChanged;
						RichBoxBody.richTextBox1.TextChanged += RichBoxBody_TextChanged;
						if (IniHelper.GetValue("工具栏", "顶置") == "False")
						{
							TopMost = false;
							return;
						}
					}
					else
					{
						// 如果是隐藏窗口的操作，这里也可以顺手解绑（和点X关闭保持一致）
        				RichBoxBody.richTextBox1.TextChanged -= RichBoxBody_TextChanged;
						 // 【新增】防止后台读秒
            			if (translationTimer != null) translationTimer.Stop(); 
						Hide();
						Visible = false;
					}
				}
				if (m.Msg == 786 && m.WParam.ToInt32() == 205)
				{
					翻译文本();
				}
				if (m.Msg == 786 && m.WParam.ToInt32() == 240)
				{
					trayInputTranslateClick(null, null);
				}
				if (m.Msg == 786 && m.WParam.ToInt32() == 250)
				{
					traySilentOcrClick(null, null);
				}
				if (m.Msg == 786 && m.WParam.ToInt32() == 260)
				{
					trayScreenshotTranslateClick(null, null);
				}
				base.WndProc(ref m);
				return;
			}
			//下面是双击标题栏的事件处理
			if (transtalate_fla == "开启")
			{
				isProgrammaticResize = true; // --- 步骤A: 挂起Form_Resize的状态更新

				WindowState = FormWindowState.Normal;
				Size newSize;
				Size newLastNormalSize; // 准备一个新的基准尺寸
                if (isOriginalTextHidden)
                {
                    // 如果原文隐藏，则恢复到【硬编码的单栏】默认大小
                    // 计算单栏的默认尺寸
        			newSize = new Size((int)font_base.Width * 23, (int)font_base.Height * 24);
        			newLastNormalSize = newSize; //  关键：在这里手动计算出正确的基准尺寸
                }
                else
                {
                    // 如果原文可见（双栏），则恢复到【硬编码的双栏】默认大小
                    // 计算双栏的默认尺寸
        			newSize = new Size((int)font_base.Width * 23 * 2, (int)font_base.Height * 24);
        			newLastNormalSize = new Size(newSize.Width / 2, newSize.Height); //  关键：在这里手动计算出正确的基准尺寸
                }
				this.Size = newSize; // 更新视觉
				this.lastNormalSize = newLastNormalSize; //  关键：同步更新数据状态
				Location = (Point)new Size(Screen.PrimaryScreen.Bounds.Width / 2 - Screen.PrimaryScreen.Bounds.Width / 10 * 2, Screen.PrimaryScreen.Bounds.Height / 2 - Screen.PrimaryScreen.Bounds.Height / 6);
				isProgrammaticResize = false;
				return;
			}
			WindowState = FormWindowState.Normal;
			Location = (Point)new Size(Screen.PrimaryScreen.Bounds.Width / 2 - Screen.PrimaryScreen.Bounds.Width / 10, Screen.PrimaryScreen.Bounds.Height / 2 - Screen.PrimaryScreen.Bounds.Height / 6);
			Size = new Size((int)font_base.Width * 23, (int)font_base.Height * 24);
		}
		#endregion
// ====================================================================================================================
		// **托盘菜单事件**
		//
		// 管理系统托盘图标的右键菜单及其事件处理。
		// - InitMinimize(): 初始化托盘菜单，添加“输入翻译”、“显示”、“设置”、“更新”、“帮助”和“退出”等菜单项。
		// - trayInputTranslateClick(): 处理“输入翻译”菜单项的点击事件，重置并显示翻译窗口。
		// - trayShowClick(): 处理“显示”菜单项的点击事件，显示主窗口。
		// - trayExitClick(): 处理“退出”菜单项的点击事件，保存配置、释放资源并终止应用程序。
		// ====================================================================================================================
		#region 托盘菜单事件
		private void btnToggleOriginalText_Click(object sender, EventArgs e)
		{	
			if (!(transtalate_fla == "开启"))
			{
				return;
			}
            //  核心修正：在所有操作之前，立刻更新状态 
            isOriginalTextHidden = !isOriginalTextHidden;
            // 暂停窗体的布局逻辑，防止在调整多个控件时发生闪烁
            this.SuspendLayout();
		    Size oldLastNormalSize = lastNormalSize;
		    Debug.WriteLine($"现在的size：{Size}");
			if (!isOriginalTextHidden) // 如果当前原文是隐藏的，那么点击后就要【显示】它
		    {
				Debug.WriteLine($"原文是隐藏的，恢复它，lastNormalSize为{lastNormalSize}");
		        // 步骤 1：暂停UI布局，防止闪烁
				this.SuspendLayout();

		        // 步骤 2：在窗口扩展之前，先将译文窗口“预先”放置到它最终该在的右侧位置
		        //         此时它的坐标会暂时超出单栏窗口的边界，但因为布局已暂停，用户看不到这个中间状态
		        RichBoxBody_T.Left = this.Width; 

		        // 步骤 3：现在才将窗口宽度扩展为双倍
		        this.Size = new Size(this.lastNormalSize.Width * 2, this.lastNormalSize.Height);
				Debug.WriteLine($"原文是隐藏的，恢复它，改变size为{Size}，改变后lastNormalSize为{lastNormalSize}");

		        // 步骤 4：在左侧“新生”出的空白区域，显示原文窗口和分隔条
		        RichBoxBody.Visible = true;
		        panelSeparator.Visible = true;

		        // 步骤 5：调用一次Resize事件处理函数，让左右两个窗口的宽度自动调整为各占一半
		        //         这一步会自动处理好所有控件的位置和尺寸，比手动计算更可靠
		        Form_Resize(null, EventArgs.Empty);
		        
		        // 步骤 6：更新按钮状态
				//btnToggleOriginalText.Left = RichBoxBody.Right - btnToggleOriginalText.Width;
				btnToggleOriginalText.Left= panelSeparator.Left - btnToggleOriginalText.Width - 10;
		        btnToggleOriginalText.Text = "◀";
				lastNormalSize = oldLastNormalSize;

		        // 步骤 7：恢复UI布局，让所有更改一次性生效
		        this.ResumeLayout(true);
		    }
		    else // 如果当前原文是显示的，那么点击后就要【隐藏】它
		    {
		        Debug.WriteLine($"原文是显示的，隐藏它，lastNormalSize为{lastNormalSize}");
		        // 1. 隐藏原文窗口
		        RichBoxBody.Visible = false;
				panelSeparator.Visible = false; // 也要隐藏分隔条

		        // 2. 【核心修正】在这里将主窗口的尺寸恢复为单栏大小
				this.Size = this.lastNormalSize;
                // 【关键日志1】在设置尺寸之后，立刻记录下当时的状态
                LogState("Click - HIDE - AFTER RESIZE");
                Debug.WriteLine($"原文是显示的，隐藏它，改变size为{Size}，改变后lastNormalSize为{lastNormalSize}");

		        // 3. 现在，在正确的窗口宽度下，让译文窗口填满整个空间
		        RichBoxBody_T.Left = 0;
		        RichBoxBody_T.Width = this.ClientRectangle.Width;

                // 4. 更新按钮文本和状态
				btnToggleOriginalText.Left = RichBoxBody_T.Right - btnToggleOriginalText.Width;
                btnToggleOriginalText.Text = "▶";
		        lastNormalSize = oldLastNormalSize;
                // 【关键日志2】在所有操作的最后，再次记录状态
                LogState("Click - HIDE - END of block");
            }

		    // 恢复窗体的布局逻辑，并强制立即应用所有更改
		    this.ResumeLayout(true);
		}
		private void btnToggleOriginalText_MouseUp(object sender, MouseEventArgs e)
		{
		    // 检查是否是右键单击
		    if (e.Button == MouseButtons.Right)
		    {
		        // 如果是，则隐藏按钮
		        btnToggleOriginalText.Visible = false;
		    }
		}

        /// <summary>
        /// 停止所有正在运行的逻辑定时器 (用于重置状态、关闭窗口或切换模式时)，暂时不使用.手动处理了
		/// 最核心的原则是：只要发生了“模式切换”或“窗口状态改变”，都应该注意是否停止 translationTimer等定时器。
        /// </summary>
        private void StopAllActiveTimers()
        {
            if (translationTimer != null)
                translationTimer.Stop();

            if (clipboardDebounceTimer != null)
                clipboardDebounceTimer.Stop();

            // autoCopyLockTimer 一般不需要强制停止，因为它负责释放锁，强制停止可能导致锁死。
            // trayClickTimer 处理托盘点击，也不建议随意停止。
        }

		#endregion
// ====================================================================================================================
		// **主菜单事件**
		//
		// 处理主文本框（RichBoxBody）的右键上下文菜单事件。
		// - MainCopyClick(): 实现“复制”功能。
		// - Main_SelectAll_Click(): 实现“全选”功能。
		// - Main_paste_Click(): 实现“粘贴”功能。
		// ====================================================================================================================
		#region 主菜单事件
		/// <summary>
		/// 复制操作事件处理函数
		/// </summary>
		/// <param name="sender">事件发送者</param>
		/// <param name="e">事件参数</param>
		private void MainCopyClick(object sender, EventArgs e)
		{
			RichBoxBody.Focus();
			RichBoxBody.richTextBox1.Copy();
		}

		/// <summary>
		/// 全选操作事件处理函数
		/// </summary>
		/// <param name="sender">事件发送者</param>
		/// <param name="e">事件参数</param>
		private void Main_SelectAll_Click(object sender, EventArgs e)
		{
			RichBoxBody.Focus();
			RichBoxBody.richTextBox1.SelectAll();
		}

		/// <summary>
		/// 粘贴操作事件处理函数
		/// </summary>
		/// <param name="sender">事件发送者</param>
		/// <param name="e">事件参数</param>
		private void Main_paste_Click(object sender, EventArgs e)
		{
			RichBoxBody.Focus();
			RichBoxBody.richTextBox1.Paste();
		}
		#endregion
// ====================================================================================================================
		// **OCR 引擎调用**
		//
		// 包含调用不同 OCR 服务（腾讯、微信、白描、百度等）的实现方法。
		// - OCR_Tencent(): 调用腾讯云 OCR API（通用版与高精度版）进行文字识别。
		// - OCR_WeChat(): 调用微信 OCR API 进行文字识别。
		// - OCR_Baimiao(): 调用白描 OCR API 进行文字识别。
		// - OCR_baidu(), OCR_baidu_accurate(): 调用百度标准版和高精度版OCR API。
		// - OCR_youdao(): 调用有道 OCR API 进行文字识别。
		// ====================================================================================================================
// ====================================================================================================================
		// **OCR 接口切换 (事件)**
		//
		// 包含用户在界面上选择不同 OCR 引擎的事件处理程序。
		// 每个事件处理程序通过调用 OCR_foreach(string name) 方法来更新当前使用的 OCR 接口。
		// ====================================================================================================================
// ====================================================================================================================
		// **文本操作与格式化**
		//
		// 提供中英文标点符号转换等文本处理功能。
		// - change_Chinese_Click(): 将文本中的英文标点符号转换为中文标点。
		// - change_English_Click(): 将文本中的中文标点符号转换为英文标点。
		// - punctuation_ch_en(): 具体的中文转英文标点实现。
		// ====================================================================================================================
// ====================================================================================================================
		// **配置文件与初始化**
		//
		// 负责加载和保存应用程序的配置信息（config.ini）。
		// - saveIniFile(): 保存当前配置到 ini 文件。
		// - LoadTranslateConfig(): 从 ini 文件加载所有翻译服务的配置（源语言、目标语言、密钥等）。
		// - InitConfig(): 在程序启动时初始化所有配置，包括 OCR 接口、翻译接口、热键和各 API 密钥。
		// - tray_Set_Click(): 处理托盘菜单中的“设置”点击事件，打开设置窗口并重新加载所有配置。
		// ====================================================================================================================
		#region 配置文件与初始化
		/// <summary>
		/// 保存当前选择的OCR接口配置到配置文件中
		/// </summary>
		public void saveIniFile()
		{
			IniHelper.SetValue("配置", "接口", interface_flag);
		}


		/// <summary>
		/// 安全获取配置值的辅助方法
		/// </summary>
		/// <param name="section">配置节</param>
		/// <param name="key">配置键</param>
		/// <param name="defaultValue">默认值</param>
		/// <returns>配置值或默认值</returns>
		private string GetConfigValueSafely(string section, string key, string defaultValue = "")
		{
			try
			{
				var value = IniHelper.GetValue(section, key);
				return (string.IsNullOrEmpty(value) || value == "发生错误") ? defaultValue : value;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"读取配置失败 [{section}][{key}]: {ex.Message}");
				return defaultValue;
			}
		}

		/// <summary>
		/// 读取配置文件(config.ini)，按照配置文件初始化应用程序配置到内存，包括OCR接口、翻译接口、快捷键和各种API密钥
		/// 读取配置文件，并根据其内容将应用程序的各项配置初始化到内存中。这包括设置OCR与翻译接口、注册全局快捷键以及加载各种API密钥。
		/// </summary>
		private void InitConfig()
		{
			// 初始化API菜单
			InitializeApiMenus();
			// 【新增】加载 AI 动态菜单
			LoadCustomOpenAIMenus();
			LoadCustomOpenAITransMenus();
			
			// 初始化OCR接口配置
			interface_flag = GetConfigValueSafely("配置", "接口", "搜狗");
			if (string.IsNullOrEmpty(interface_flag))
			{
				interface_flag = "搜狗";
				IniHelper.SetValue("配置", "接口", interface_flag);
			}
			OCR_foreach(interface_flag);
			
			// 初始化翻译接口配置
			StaticValue.Translate_Current_API = GetConfigValueSafely("配置", "翻译接口", "Bing2");
			if (string.IsNullOrEmpty(StaticValue.Translate_Current_API))
			{
				StaticValue.Translate_Current_API = "Bing2";
			}
			Trans_foreach(StaticValue.Translate_Current_API);
			LoadTranslateConfig();
			
			// 初始化快捷键配置
			var filePath = AppDomain.CurrentDomain.BaseDirectory + "Data\\config.ini";
			
			// 安全加载热键配置的辅助方法
			Action<string, string, int> loadHotkey = (section, key, flagId) =>
			{
				try
				{
					var hotkeyValue = IniHelper.GetValue(section, key);
					if (!string.IsNullOrEmpty(hotkeyValue) && 
					    hotkeyValue != "请按下快捷键" && 
					    hotkeyValue != "发生错误")
					{
						SetHotkey("None", "", hotkeyValue, flagId);
					}
				}
				catch (Exception ex)
				{
					// 记录错误但不中断程序执行
					System.Diagnostics.Debug.WriteLine($"加载热键配置失败 [{section}][{key}]: {ex.Message}");
				}
			};

			// 加载各个热键配置
			loadHotkey("快捷键", "文字识别", 200);
			loadHotkey("快捷键", "翻译文本", 205);
			loadHotkey("快捷键", "记录界面", 206);
			loadHotkey("快捷键", "识别界面", 235);
			loadHotkey("快捷键", "输入翻译", 240);
			loadHotkey("快捷键", "静默识别", 250);
			loadHotkey("快捷键", "截图翻译", 260);

			// --- 加载OCR密钥 ---
			// 加载百度OCR密钥
			StaticValue.BD_API_ID = TrOCRUtils.LoadSetting("密钥_百度", "secret_id", "");
			StaticValue.BD_API_KEY = TrOCRUtils.LoadSetting("密钥_百度", "secret_key","");
			StaticValue.BD_LANGUAGE = TrOCRUtils.LoadSetting("密钥_百度", "language_code","CHN_ENG");

			// 加载百度表格识别密钥
			StaticValue.BD_TABLE_API_ID = TrOCRUtils.LoadSetting("密钥_百度表格", "secret_id","");
			StaticValue.BD_TABLE_API_KEY = TrOCRUtils.LoadSetting("密钥_百度表格", "secret_key","");
			
			// 【新增】加载百度手写识别密钥
			StaticValue.BD_HANDWRITING_API_ID = TrOCRUtils.LoadSetting("密钥_百度手写", "secret_id", "");
			StaticValue.BD_HANDWRITING_API_KEY = TrOCRUtils.LoadSetting("密钥_百度手写", "secret_key", "");
			StaticValue.BD_HANDWRITING_LANGUAGE = TrOCRUtils.LoadSetting("密钥_百度手写", "language_code", "CHN_ENG");

			// 加载腾讯OCR密钥
			StaticValue.TX_API_ID = TrOCRUtils.LoadSetting("密钥_腾讯", "secret_id","");		
			StaticValue.TX_API_KEY = TrOCRUtils.LoadSetting("密钥_腾讯", "secret_key","");	
			StaticValue.TX_LANGUAGE = TrOCRUtils.LoadSetting("密钥_腾讯", "language_code","zh");

			// 加载腾讯高精度OCR密钥
			StaticValue.TX_ACCURATE_API_ID = TrOCRUtils.LoadSetting("密钥_腾讯高精度", "secret_id","");		
			StaticValue.TX_ACCURATE_API_KEY = TrOCRUtils.LoadSetting("密钥_腾讯高精度", "secret_key","");		
			StaticValue.TX_ACCURATE_LANGUAGE = TrOCRUtils.LoadSetting("密钥_腾讯高精度", "language","auto");
			
			// 加载腾讯表格v3的OCR密钥
			StaticValue.TX_TABLE_API_ID = TrOCRUtils.LoadSetting("密钥_腾讯表格v3", "secret_id","");			
			StaticValue.TX_TABLE_API_KEY = TrOCRUtils.LoadSetting("密钥_腾讯表格v3", "secret_key","");			

			// 加载百度高精度OCR密钥
			StaticValue.BD_ACCURATE_API_ID = TrOCRUtils.LoadSetting("密钥_百度高精度", "secret_id","");			
			StaticValue.BD_ACCURATE_API_KEY = TrOCRUtils.LoadSetting("密钥_百度高精度", "secret_key","");			
			StaticValue.BD_ACCURATE_LANGUAGE = TrOCRUtils.LoadSetting("密钥_百度高精度", "language_code","CHN_ENG");			
	
			// --- 加载白描OCR凭据 ---
			StaticValue.BaimiaoUsername = TrOCRUtils.LoadSetting("密钥_白描", "username","");
			StaticValue.BaimiaoPassword = TrOCRUtils.LoadSetting("密钥_白描", "password","");

			// 加载持久化的token信息
			string savedToken = IniHelper.GetValue("密钥_白描", "token");
			string savedExpiry = IniHelper.GetValue("密钥_白描", "token_expiry");
			string savedUsername = IniHelper.GetValue("密钥_白描", "token_username");
			string savedUuid = IniHelper.GetValue("密钥_白描", "device_uuid");

			if (!string.IsNullOrEmpty(savedToken) && savedToken != "发生错误" &&
			    !string.IsNullOrEmpty(savedUsername) && savedUsername != "发生错误" &&
			    savedUsername == StaticValue.BaimiaoUsername && // 确保token属于当前用户
			    DateTime.TryParse(savedExpiry, out DateTime expiry) && DateTime.Now < expiry)
			{
			 StaticValue.BaimiaoToken = savedToken;
			 StaticValue.BaimiaoTokenExpiry = expiry;
			}

			if (!string.IsNullOrEmpty(savedUuid) && savedUuid != "发生错误")
			{
			 StaticValue.BaimiaoDeviceUuid = savedUuid;
			}
		}
		
		/// <summary>
		/// 检查并替换文本中的中文冒号为英文冒号
		/// 当中文冒号前后都是英文字符或标点符号时，将其替换为英文冒号
		/// </summary>
		/// <param name="text">需要处理的文本</param>
		/// <returns>处理后的文本，其中符合条件的中文冒号已被替换为英文冒号</returns>
		public static string check_ch_en(string text)
		{
			var array = text.ToCharArray();
			for (var i = 0; i < array.Length; i++)
			{
				var num = "：".IndexOf(array[i]);
				if (num != -1 && i - 1 >= 0 && i + 1 < array.Length && contain_en(array[i - 1].ToString()) && contain_en(array[i + 1].ToString()))
				{
					array[i] = ":"[num];
				}
				if (num != -1 && i - 1 >= 0 && i + 1 < array.Length && contain_en(array[i - 1].ToString()) && contain_punctuation(array[i + 1].ToString()))
				{
					array[i] = ":"[num];
				}
			}
			return new string(array);
		}


        #endregion

        // ====================================================================================================================
        // **翻译功能**
        //
        // 实现了文本翻译的核心逻辑和界面交互。
        // - TransClick(): 启动翻译模式的入口，调整窗口布局以显示原文和译文两个文本框。
        // - Form_Resize(): 处理窗口大小变化事件，确保翻译界面布局正确。
        // - Trans_copy_Click(), Trans_paste_Click(), Trans_SelectAll_Click(): 翻译文本框的右键菜单功能。
        // - trans_Calculate(): 异步执行翻译的核心方法，根据当前选择的翻译服务和语言设置调用相应的翻译API。
        // - Trans_close_Click(): 关闭翻译模式，恢复原始窗口布局。
        // ====================================================================================================================
        #region 翻译功能

		/// <summary>
		/// 处理窗体大小调整事件，当翻译功能开启时调整文本框大小和位置
		/// </summary>
		/// <param name="sender">事件发送者</param>
		/// <param name="e">事件参数</param>
		private void Form_Resize(object sender, EventArgs e)
		{
			LogState("Form_Resize Start");
			// --- 步骤 1: 更新窗口尺寸记忆（状态管理，保持在最前） ---
			if (!isProgrammaticResize)
			{
				// 仅当窗口处于“正常”状态时，才考虑更新尺寸记忆
				if (WindowState == FormWindowState.Normal)
				{
					if (transtalate_fla == "开启")
					{
						// 双栏模式：记录一半的宽度为基础尺寸
						if (!isOriginalTextHidden)
						{
							this.lastNormalSize = new Size(this.Size.Width / 2, this.Size.Height);
						}
						else
						{
							// 单栏模式（原文隐藏）：基准尺寸就是当前的窗口尺寸
							// 这能确保用户在此模式下缩放窗口后，新尺寸能被记住
							this.lastNormalSize = this.Size;
						}
					}
					else // 单栏模式 (transtalate_fla == "关闭")
					{
						// 【核心防御逻辑】
						// 检查这是否是一次可疑的Resize：即在单栏模式下，窗口宽度突然变得接近之前基础宽度的两倍。
						// 我们用1.8倍作为阈值，以允许一些误差。
						if (this.lastNormalSize.Width > 0 && this.Size.Width > this.lastNormalSize.Width * 1.8)
						{
							// 如果是，则判定为“幽灵事件”，拒绝更新lastNormalSize，并强制将窗口尺寸改回正确的值。
							System.Diagnostics.Debug.WriteLine($"  REJECTED suspicious resize. Forcing size back to {this.lastNormalSize}");
							this.Size = this.lastNormalSize;
						}
						else
						{
							// 如果不是可疑的Resize（例如用户正常拖动边框），则正常更新尺寸记忆
							this.lastNormalSize = this.Size;
						}
					}
				}
			}

            // --- 步骤 2: 布局主容器（文本框和分隔条）---
            // 当RichBoxBody未设置停靠样式时调整大小. Dock != Fill 意味着处于双栏翻译模式
            if (RichBoxBody.Dock != DockStyle.Fill)
            {//  核心修正：在此处添加对 isOriginalTextHidden 状态的判断 
                if (isOriginalTextHidden)
                {
                    // 特殊状态：翻译模式已开启，但原文被隐藏了
                    // 此时，译文框应该填满整个窗口
                    RichBoxBody.Visible = false;      // 确保原文框是隐藏的
                    panelSeparator.Visible = false; // 确保分隔条是隐藏的
                    RichBoxBody_T.Location = new Point(0, 0);
                    RichBoxBody_T.Size = this.ClientRectangle.Size;
				}
				else
				{

                    // --- 替换为带分隔条的布局逻辑 ---
                    // 1. 计算每个文本框的理想宽度
                    int panelWidth = (this.ClientRectangle.Width - panelSeparator.Width) / 2;

                    // 2. 设置左侧文本框的位置和大小
                    RichBoxBody.Location = new Point(0, 0);
                    RichBoxBody.Size = new Size(panelWidth, this.ClientRectangle.Height);

                    // 3. 设置分隔条的位置和高度
                    panelSeparator.Location = new Point(RichBoxBody.Right, 0);
                    panelSeparator.Height = this.ClientRectangle.Height;

                    // 4. 设置右侧文本框的位置和大小
                    RichBoxBody_T.Location = new Point(panelSeparator.Right, 0);
                    RichBoxBody_T.Size = new Size(this.ClientRectangle.Width - panelSeparator.Right, this.ClientRectangle.Height);
                }
            }
			if ((WindowState == FormWindowState.Normal) || (WindowState == FormWindowState.Maximized))
			{
				if (transtalate_fla == "开启")
				{

					if (isOriginalTextHidden)
            		{
            		    // 如果原文隐藏，则将按钮锚定在窗口右侧
            		    btnToggleOriginalText.Left = this.ClientRectangle.Width - btnToggleOriginalText.Width;
            		}
            		else
            		{
            		    // 如果原文可见，则将按钮放在分隔条旁边
            		    btnToggleOriginalText.Left = panelSeparator.Left - btnToggleOriginalText.Width - 10;
            		}

				}
				
			}

			LogState("Form_Resize End"); // <--- 添加这一行
		}


        /// <summary>
        /// 使用“限时状态锁”安全地将数据对象设置到剪贴板，以防止无限循环。
        /// </summary>
        /// <param name="data">要复制到剪贴板的对象 (可以是 string, DataObject, Image 等)。</param>
        public void SetClipboardWithLock(object data)
        {
            // 检查传入的对象是否为 null
            if (data == null) return;

            // 如果传入的是字符串，额外检查它是否为空或仅包含空白字符
            if (data is string textData && string.IsNullOrWhiteSpace(textData))
            {
                return;
            }

            try
            {
                // 1. 激活状态锁
                isAutoCopying = true;
                Debug.WriteLine("--- 剪贴板锁已激活 ---");

                // 2. 执行复制操作
                // 这个调用现在是通用的，可以处理 string、DataObject 等多种类型
                Clipboard.SetDataObject(data, true, 5, 100);

                // 3. 启动定时器，创建静默期
                autoCopyLockTimer.Stop(); // 确保定时器被重置
                autoCopyLockTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"带锁设置剪贴板失败: {ex.Message}");
                // 如果复制失败，必须立即解除锁定
                isAutoCopying = false;
                autoCopyLockTimer.Stop();
            }
        }
        /// <summary>
        /// 使用“限时状态锁”安全地为特定格式设置剪贴板数据。
        /// 主要用于处理“快速翻译”后的粘贴操作。
        /// </summary>
        /// <param name="format">剪贴板格式 (例如 DataFormats.UnicodeText)。</param>
        /// <param name="data">要为该格式复制的数据。</param>
        private void SetClipboardDataWithLock(string format, object data)
        {
            if (string.IsNullOrEmpty(format) || data == null) return;

            try
            {
                isAutoCopying = true;
                Debug.WriteLine("--- 剪贴板锁已激活 (Data) ---");
                // 此调用处理 Clipboard.SetData 的情况
                Clipboard.SetData(format, data);
                autoCopyLockTimer.Stop();
                autoCopyLockTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"带锁设置剪贴板数据失败: {ex.Message}");
                isAutoCopying = false;
                autoCopyLockTimer.Stop();
            }
        }
        /// <summary>
        /// 原始文本框内容改变事件，用于实现编辑后自动翻译
        /// </summary>
        private void RichBoxBody_TextChanged(object sender, EventArgs e)
		{
            Debug.WriteLine("启动了RichBoxBody_TextChanged");
            // 【新增】如果正在流式输出，直接忽略，不启动定时器
            if (isStreaming) return;

            // 【新增】拦截逻辑
            // 如果 RichBoxBody 正在进行字体切换，直接返回，不启动翻译倒计时
            if (RichBoxBody.IsFontChanging)
            {
                // 可选：打印日志确认拦截成功
                 System.Diagnostics.Debug.WriteLine("字体切换中，忽略 TextChanged 事件");
                return;
            }
            // === 【修改步骤 1】读取原始配置字符串 ===
            string rawConfig = StaticValue.TextChangeAutotranslateDelayRaw;
            // === 【修改步骤 2】调用解析方法 ===
            // CheckTextChangeAutoTranslateConfig 会处理：
            // 1. 解析时间，如果非数字或<=0，返回 false
            // 2. 解析黑白名单，判断当前接口是否允许
            // 3. 将解析出的时间赋值给 validDelay
            if (!CheckTextChangeAutoTranslateConfig(rawConfig, StaticValue.Translate_Current_API, out int validDelay))
            {
                // 如果不允许（时间为0、格式错误、或当前接口被排除），直接退出
                return;
            }

            // --- 日志: 事件触发入口 ---
            // 为了日志清晰，将换行符替换为可见的转义字符
            string currentTextForLog = RichBoxBody.Text.Replace("\r", "\\r").Replace("\n", "\\n");
			Debug.WriteLine($"---> TextChanged 事件触发。文本: \"{currentTextForLog}\" | isContentFromOcr: {isContentFromOcr} | transtalate_fla: {transtalate_fla}");
			// 关键修复：添加一个“守卫”，如果文本是默认占位符，则直接忽略，不执行任何逻辑。
			// 这一步不做也行，因为下面2880行做了事件临时解绑。
			//这一步做了有个小问题：当OCR识别的结果恰好是 ***该区域未发现文本*** 时，也会直接return,需要优化
    		// if (RichBoxBody.Text == "***该区域未发现文本***")
    		// {
    		//     return;
    		// }

			// 使用安全的字符串比较方式，避免因 "发生错误" 或空值导致异常
			bool autoTranslateInputEnabled = StaticValue.InputTranslateAutoTranslate;
			// 定义纯手动输入状态：非OCR 也 非剪贴板
        	bool isPureManualInput = !isContentFromOcr && !isFromClipboardListener;

			 // 场景1: 当翻译窗口关闭时 (只处理纯手动输入的情况)
    		if (transtalate_fla == "关闭")
    		{
				Debug.WriteLine("    |--> 满足 [场景1：输入翻译 & 翻译窗口关闭]");
        		if (isPureManualInput && autoTranslateInputEnabled && !string.IsNullOrWhiteSpace(RichBoxBody.Text))
				{
					Debug.WriteLine("        |--> 文本不为空，准备调用 TransClick() 来打开翻译窗口...");
					translationTimer.Stop();
                    // 【修改后新增】在此处动态更新 Timer 的间隔
                    // 这样可以确保：
                    // 1. 此时 currentDelay 肯定 > 0，赋值安全，不会崩溃
                    // 2. 如果用户在设置里修改了时间，这里会立即生效，无需重启
                    // === 【修改步骤 3】使用解析出的有效时间 ===
                    translationTimer.Interval = validDelay;
                    translationTimer.Start();
				}
				Debug.WriteLine("    |<-- 场景1 结束,定时器已开始或重置。");
				Debug.WriteLine("---> TextChanged 事件结束。");
    		    return;
    		}

    		// 场景2: 当翻译窗口已经打开时
    		if (transtalate_fla == "开启")
    		{
				// 允许重新翻译的条件是：
				// 1. 内容来源于OCR（允许用户随时修改OCR结果并重翻）。
				// 2. 或者，内容来源于剪贴板监听（无条件允许重翻）。
				// 3. 或者，是纯手动输入状态 并且 开启了“输入时自动翻译”功能。   
				//上面的不对,现在改成任意情况下,只要是双栏窗口,就可以进行编辑后自动重新翻译   
        		// if (isContentFromOcr || (isPureManualInput && autoTranslateInputEnabled))
				// {
					Debug.WriteLine("        |--> 满足 [isContentFromOcr 或 autoTranslateInputEnabled]，准备启动/重置 定时器...");
        		    translationTimer.Stop();
                // 【修改后新增】在此处动态更新 Timer 的间隔
                // 这样可以确保：
                // 1. 此时 currentDelay 肯定 > 0，赋值安全，不会崩溃
                // 2. 如果用户在设置里修改了时间，这里会立即生效，无需重启
                // === 【修改步骤 3】使用解析出的有效时间 ===
                translationTimer.Interval = validDelay;
                translationTimer.Start();

					Debug.WriteLine("        |--> 定时器已重置。");

        		// }
    		}
			Debug.WriteLine("---> TextChanged 事件结束。");

		}
		

		private void HandleClipboardChange()
		{
			Debug.WriteLine("HandleClipboardChange执行了");
    		// 最优先检查：如果程序正处于自动复制后的静默期，则忽略所有剪贴板事件
    		if (isAutoCopying)
    		{
    		    Debug.WriteLine("忽略剪贴板事件：自动复制锁激活中");
    		    return;
    		}
    		
			// 在程序刚启动时，忽略第一次自动触发的剪贴板消息
			if (isAppLoading)
			{
				isAppLoading = false; // 将标志位置为false，确保此逻辑只执行一次
                // 使用 try-catch 保护剪贴板读取，防止启动时其他程序占用剪贴板导致崩溃
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        // 同步初始的剪贴板内容，以便下一次真正的复制可以被正确比较
                        lastClipboardText = Clipboard.GetText();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("程序启动时同步剪贴板状态失败: " + ex.Message);
                    // 启动时读取失败没关系，直接忽略即可，不影响后续监听
                }
                return; // 关键：直接退出，不执行任何后续的翻译操作
			}
			
			//  检查功能是否开启
			if (!StaticValue.ListenClipboardTranslation)
			{
				return;
			}

			// 不立即处理，而是重置防抖定时器
			// 无论来多少次通知，都只是不断地重置计时器
			clipboardDebounceTimer.Stop();
			clipboardDebounceTimer.Start();
			//定时器防抖解决监听剪贴板时多次翻译的问题,此问题原因是剪贴板软件获取多次系统消息导致:应注意剪贴板查看器链(不同剪贴板软件和系统的交互链路不同)
			
		}
		private void ClipboardDebounceTimer_Tick(object sender, EventArgs e)
		{
		    // 1. 首先停止定时器，防止重复执行
		    clipboardDebounceTimer.Stop();

            string clipboardText = null;

            // 2. 将 ContainsText 和 GetText 全部放入 try-catch 中
            try
            {
                if (Clipboard.ContainsText())
                {
                    clipboardText = Clipboard.GetText();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("获取剪贴板文本失败，可能被其他程序占用: " + ex.Message);
                return; // 获取失败则直接返回，等待下一次用户复制
            }

            // 3. 检查是否是新的、非空的文本
            if (!string.IsNullOrEmpty(clipboardText) && clipboardText != lastClipboardText)
			{
				// 更新最后一次的文本记录，防止重复触发
				lastClipboardText = clipboardText;

				// 显示提示
				CommonHelper.ShowHelpMsg("已捕获剪贴板，正在翻译...");

				// 设置正确的标志位
				isContentFromOcr = false;
				isFromClipboardListener = true;

				// 调用UI启动方法
				InitiateTranslationUI(clipboardText);
			
		    }
		    
		}

#endregion

// ====================================================================================================================
		// **截图与图像处理**
		//
		// 包含了屏幕截图、二维码扫描、图像处理和文件操作等辅助功能。
		// - CreateParams: 设置窗口样式，启用无边框窗口特性。
		// - ScanQRCode(): 扫描屏幕截图中的二维码并返回解码后的文本。
		// - SearchSelText(): 使用默认浏览器搜索选中的文本。
		// - tray_update_Click(): 检查应用程序更新。
		// - contain_jap(), contain_kor(): 判断字符串是否包含日文或韩文字符。
		// - ReFileName(), GetUniqueFileName(): 生成唯一的文件名以避免覆盖。
		// - PlaySong(): 播放音频文件。
		// ====================================================================================================================
#region 截图与图像处理
		/// <summary>
		/// 创建窗口参数，设置窗口的扩展样式
		/// </summary>
		/// <value>窗口创建参数</value>
		protected override CreateParams CreateParams
		{
			get
			{
				var createParams = base.CreateParams;
				createParams.ExStyle |= 134217728;
				return createParams;
			}
		}

		/// <summary>
		/// 扫描屏幕图像中的二维码内容
		/// </summary>
		/// <returns>二维码中的文本内容，如果扫描失败则返回空字符串</returns>
		public string ScanQRCode()
		{
			var result = "";
			try
			{
				var image = new BinaryBitmap(new HybridBinarizer(new BitmapLuminanceSource((Bitmap)image_screen)));
				var result2 = new QRCodeReader().decode(image);
				if (result2 != null)
				{
					result = result2.Text;
				}
			}
			catch
			{
			}
			return result;
		}

		/// <summary>
		/// 使用百度搜索RichBoxBody控件中选中的文本
		/// </summary>
		/// <param name="sender">事件发送者</param>
		/// <param name="e">事件参数</param>
		public void SearchSelText(object sender, EventArgs e)
		{
			Process.Start("https://www.baidu.com/s?wd=" + RichBoxBody.SelectText);
		}


		/// <summary>
		/// 重命名文件，在文件名后添加序号以避免重复
		/// </summary>
		/// <param name="strFolderPath">文件夹路径</param>
		/// <param name="strFileName">原始文件名</param>
		/// <returns>新的文件名</returns>
		public static string ReFileName(string strFolderPath, string strFileName)
		{
			var text = strFolderPath + "\\" + strFileName;
			var startIndex = text.LastIndexOf('.');
			text = text.Insert(startIndex, "_{0}");
			var num = 1;
			var path = string.Format(text, num);
			while (File.Exists(path))
			{
				path = string.Format(text, num);
				num++;
			}
			return Path.GetFileName(path);
		}

#endregion

// ====================================================================================================================
		// **右键菜单 - 朗读事件**
		//
		// 处理原文和译文文本框中通过右键菜单触发的朗读功能。
		// - Main_Voice_Click(): 获取原文框中选中的文本并触发朗读。
		// - Trans_Voice_Click(): 获取译文框中选中的文本并触发朗读。
		// - Speak_child(): 在 UI 线程中播放朗读音频。
		// ====================================================================================================================
// ====================================================================================================================
		// **右键菜单 - 文本转换**
		//
		// 提供文本的大小写转换和简繁体转换功能。
		// - change_zh_tra_Click(): 将文本转换为繁体。
		// - change_tra_zh_Click(): 将文本转换为简体。
		// - change_str_Upper_Click(): 将文本转换为大写。
		// - change_Upper_str_Click(): 将文本转换为小写。
		// ====================================================================================================================
// ====================================================================================================================
		// **热键管理**
		//
		// 负责解析快捷键字符串并注册系统范围的全局热键。
		// - SetHotkey(): 核心方法，调用 Win32 API (RegisterHotKey) 来注册一个全局热键，
		//              允许用户在任何地方通过快捷键触发程序功能（如截图、翻译）。
		// ====================================================================================================================
#region 热键管理

		/// <summary>
		/// 设置并注册全局热键
		/// </summary>
		/// <param name="text">修饰键（如Ctrl、Alt等）</param>
		/// <param name="text2">按键（如A、B、F1等）</param>
		/// <param name="value">完整的快捷键字符串，格式如"Ctrl+Alt+A"或"Alt+A"</param>
		/// <param name="flag">热键标识符</param>
		public void SetHotkey(string text, string text2, string value, int flag)
		{
			var parts = value.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
			if (parts.Count == 0) return;

			Keys key = (Keys)Enum.Parse(typeof(Keys), parts.Last());
			HelpWin32.KeyModifiers keyModifiers = HelpWin32.KeyModifiers.None;

			foreach (var part in parts.Take(parts.Count - 1))
			{
				if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
					keyModifiers |= HelpWin32.KeyModifiers.Ctrl;
				else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
					keyModifiers |= HelpWin32.KeyModifiers.Shift;
				else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
					keyModifiers |= HelpWin32.KeyModifiers.Alt;
			}
			try
			{
				if (!HelpWin32.RegisterHotKey(Handle, flag, keyModifiers, key))
				{
					CommonHelper.ShowHelpMsg($"快捷键 '{value}' 注册失败，可能已被其他程序占用！");

				}
			}
			catch (Exception ex)
			{
				// 捕获任何异常，避免程序崩溃
				CommonHelper.ShowHelpMsg($"热键注册失败: {ex.Message}");
			}
		}
#endregion
// ====================================================================================================================
		// **辅助方法与工具**
		//
		// 包含一些通用的辅助方法，例如记录管理和剪贴板操作。
		// - p_note(): 将新的识别结果添加到历史记录队列中。
		// - GetTextFromClipboard(): 从系统剪贴板安全地获取文本内容，处理线程问题。
		// ====================================================================================================================
#region 辅助方法与工具
		/// <summary>
		/// 将指定的字符串添加到历史记录队列中
		/// </summary>
		/// <param name="a">要添加到历史记录的字符串</param>
		public void p_note(string a)
		{
			// 循环更新历史记录数组，实现队列效果
			for (var i = 0; i < StaticValue.NoteCount; i++)
			{
				if (i == StaticValue.NoteCount - 1)
				{
					pubnote[StaticValue.NoteCount - 1] = a;
				}
				else
				{
					pubnote[i] = pubnote[i + 1];
				}
			}
		}

		/// <summary>
		/// 从系统剪贴板安全地获取文本内容
		/// </summary>
		/// <returns>剪贴板中的文本内容，如果为空则返回null</returns>
		private string GetTextFromClipboard()
		{
			// 检查当前线程的单元状态，确保在STA模式下执行剪贴板操作
			if (Thread.CurrentThread.GetApartmentState() > ApartmentState.STA)
			{
				var thread = new Thread(delegate()
				{
					SendKeys.SendWait("^c");
					SendKeys.Flush();
				});
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
				thread.Join();
			}
			else
			{
				SendKeys.SendWait("^c");
				SendKeys.Flush();
			}
			var text = Clipboard.GetText();
			text = (string.IsNullOrWhiteSpace(text) ? null : text);
			// 如果获取到文本内容，则清空剪贴板
			if (text != null)
			{
				Clipboard.Clear();
			}
			return text;
		}
#endregion

// ====================================================================================================================
		// **截图与核心OCR流程**
		//
		// 这是应用程序的核心功能所在，集成了截图、图像处理和OCR识别的完整流程。
		// - MainOCRQuickScreenShots(): 启动截图功能，隐藏主窗口，调用 ShareX 库进行区域捕捉。
		//                             根据用户的操作（如截图、贴图、保存、多区域选择等）执行不同逻辑。
		// - Main_OCR_Thread(): 截图完成后，在此线程中执行 OCR 识别。
		//                      它会先尝试扫描二维码，然后根据当前选择的 OCR 接口调用相应的识别方法。
		// - Main_OCR_Thread_last(): OCR 识别完成后，在 UI 线程中更新界面，显示识别结果，处理自动翻译、
		//                           分段合并等后续操作，并重新显示主窗口。
		// - SougouOCR(): 调用搜狗OCR。
		// - BdTableOCR(), OCR_ali_table(): 处理表格识别。
		// - select_image(), FindBundingBox(): 使用 Emgu.CV进行图像处理，用于竖排文字的识别。
		// ====================================================================================================================
#region 截图与核心OCR流程


		/// <summary>
		/// 刷新ocr接口界面控件文本显示
		/// </summary>
		private new void Refresh()
		{
			sougou.Text = "搜狗";
			tencent.Text = "腾讯";
			tencent_accurate.Text = "腾讯-高精度";
			baidu.Text = "百度";
			youdao.Text = "有道";
			wechat.Text = "微信";
			baimiao.Text = "白描";
			baidu_accurate.Text = "百度-高精度";
			shupai.Text = "竖排";
			ocr_table.Text = "表格";
			ch_en.Text = "中英";
			jap.Text = "日语";
			kor.Text = "韩语";
			left_right.Text = "从左向右";
			righ_left.Text = "从右向左";
			baidu_table.Text = "百度";
			tx_table.Text = "腾讯";
			ali_table.Text = "阿里";
			Mathfuntion.Text = "公式";
			paddleocr.Text = "PaddleOCR";
			paddleocr2.Text = "PaddleOCR2";
			rapidocr.Text = "RapidOCR";
			write.Text = "手写";
			baidu_handwriting.Text = "百度手写";
			ai_menu.Text = "AI";

		}


#endregion
		//控制右键菜单-接口项的可见性
		private void InitializeApiMenus()
		{
			// OCR 接口可见性设置
			SetMenuItemVisibility(sougou, "Ocr接口显示", "Sougou");
			SetMenuItemVisibility(tencent, "Ocr接口显示", "Tencent");
			SetMenuItemVisibility(tencent_accurate, "Ocr接口显示", "TencentAccurate");
			SetMenuItemVisibility(youdao, "Ocr接口显示", "Youdao");
			SetMenuItemVisibility(wechat, "Ocr接口显示", "WeChat");
			SetMenuItemVisibility(baimiao, "Ocr接口显示", "Baimiao");
			SetMenuItemVisibility(baidu, "Ocr接口显示", "Baidu");
			SetMenuItemVisibility(baidu_accurate, "Ocr接口显示", "BaiduAccurate");
			SetMenuItemVisibility(Mathfuntion, "Ocr接口显示", "Mathfuntion");
			SetMenuItemVisibility(ocr_table, "Ocr接口显示", "Table");
			SetMenuItemVisibility(shupai, "Ocr接口显示", "Shupai");
			SetMenuItemVisibility(paddleocr, "Ocr接口显示", "PaddleOCR");
			SetMenuItemVisibility(paddleocr2, "Ocr接口显示", "PaddleOCR2");
			SetMenuItemVisibility(rapidocr, "Ocr接口显示", "RapidOCR");

			// OCR 子菜单接口可见性设置
			SetMenuItemVisibility(baidu_table, "Ocr接口显示", "TableBaidu");
			SetMenuItemVisibility(tx_table, "Ocr接口显示", "TencentTable");
			SetMenuItemVisibility(ali_table, "Ocr接口显示", "TableAli");
			SetMenuItemVisibility(left_right, "Ocr接口显示", "ShupaiLR");
			SetMenuItemVisibility(righ_left, "Ocr接口显示", "ShupaiRL");

			// 翻译接口可见性设置
			SetMenuItemVisibility(trans_google, "翻译接口显示", "Google");
			SetMenuItemVisibility(trans_baidu, "翻译接口显示", "Baidu");
			SetMenuItemVisibility(trans_tencent, "翻译接口显示", "Tencent");
			SetMenuItemVisibility(trans_bing, "翻译接口显示", "Bing");
			SetMenuItemVisibility(trans_bing2, "翻译接口显示", "Bing2");
			SetMenuItemVisibility(trans_microsoft, "翻译接口显示", "Microsoft");
			SetMenuItemVisibility(trans_yandex, "翻译接口显示", "Yandex");
			SetMenuItemVisibility(trans_tencentinteractive, "翻译接口显示", "TencentInteractive");
			SetMenuItemVisibility(trans_caiyun, "翻译接口显示", "Caiyun");
			SetMenuItemVisibility(trans_volcano, "翻译接口显示", "Volcano");
			SetMenuItemVisibility(trans_caiyun2, "翻译接口显示", "Caiyun2");
			SetMenuItemVisibility(trans_baidu2, "翻译接口显示", "Baidu2");
		}

		/// <summary>
		/// 根据配置文件中指定节和键的值设置菜单项的可见性
		/// </summary>
		/// <param name="menuItem">要设置可见性的菜单项</param>
		/// <param name="section">配置文件中的节名称</param>
		/// <param name="key">节中的键名称</param>
		private void SetMenuItemVisibility(ToolStripItem menuItem, string section, string key)
		{
			if (menuItem != null)
			{
				string visibilityValue = IniHelper.GetValue(section, key);
				bool isVisible;

				if (bool.TryParse(visibilityValue, out isVisible))
				{
					// Value was "True" or "False", isVisible is now set correctly.
				}
				else // Value was "发生错误" or something else. Apply default logic.
				{
					if (section == "翻译接口显示")
					{
						switch (key)
						{
							case "TencentInteractive":
							case "Caiyun":
							case "Volcano":
							case "Baidu2":
								isVisible = false;
								break;
							default:
								isVisible = true;
								break;
						}
					}
					else if (section == "Ocr接口显示")
					{
						switch (key)
						{
							case "Baimiao":
								isVisible = false;
								break;
							default:
								isVisible = true;
								break;
						}
					}
					else
					{
						isVisible = true; // Default for any other section
					}
				}
				
				menuItem.Visible = isVisible;
			}
		}

// ====================================================================================================================
		// **字段声明**
		//
		// 定义了 FmMain 类中使用的所有字段（成员变量）。
		// 这些字段用于存储窗体的状态、配置信息、OCR 和翻译结果、图像数据以及其他在整个类中需要共享的数据。
		// ====================================================================================================================
		#region 字段声明

		/// OCR接口标识，用于标识当前使用的OCR接口类型
		public string interface_flag;

		/// 语言标识，用于标识当前处理的文本语言类型
		public string language;

		/// 分割文本内容，用于存储OCR识别后经过分割处理的文本
		public string split_txt;

		/// 空格字符，用于文本处理时的空格表示
		public string spacechar;

		/// RichTextBox1的注释内容
		public string richTextBox1_note;

		/// 翻译标志，用于标识翻译功能是否开启
		public string transtalate_fla;

		/// 加载窗口实例，用于显示加载动画
		public FmLoading fmloading;

		/// 线程实例，用于执行耗时操作
		public Thread thread;

		/// 菜单项实例，用于设置相关功能
		public MenuItem Set;

		/// Google翻译文本内容
		public string googleTranslate_txt;

		/// 成功计数器，用于记录操作成功的次数
		public int num_ok;

		/// 激活状态标识，用于标识当前窗口是否处于激活状态
		public bool bolActive;

		/// 腾讯VIP标识，用于标识是否使用腾讯VIP服务
		public bool tencent_vip_f;

		/// 自动标志，用于标识自动功能是否开启
		public string auto_fla;

		/// 百度VIP标识，用于标识是否使用百度VIP服务
		public string baidu_vip;

		/// HTML文本内容
		public string htmltxt;

		/// 提示文本，用于显示系统提示信息
		public static string TipText;

		/// 朗读状态标识，用于标识是否正在进行文本朗读
		public bool speaking;

		/// 朗读复制标志，用于控制朗读复制功能
		public string speak_copyb;

		/// TTS数据，用于存储文本转语音的音频数据
		public byte[] ttsData;

		/// 公共注释数组，用于存储公共注释内容
		public string[] pubnote;

		/// 注释窗口实例，用于显示注释内容
		public FmNote fmNote;

		/// 屏幕截图图像，用于存储屏幕截图内容
		public Image image_screen;

		/// 语音计数器，用于记录语音相关操作的次数
		public int voice_count;

		/// 窗体宽度，用于存储窗体的宽度值
		public int form_width;

		/// 窗体高度，用于存储窗体的高度值
		public int form_height;

		/// QQ截图更改标识，用于标识QQ截图功能是否启用
		public bool change_QQ_screenshot;

		/// 标志窗口实例，用于显示标志相关内容
		private FmFlags fmflags;

		/// 翻译热键，用于存储翻译功能的快捷键
		public string trans_hotkey;

		/// 时间间隔，用于存储时间间隔信息
		public TimeSpan ts;

		/// ESC定时器，用于ESC相关操作的定时控制
		public Timer esc_timer;

		/// ESC线程，用于执行ESC相关操作
		public Thread esc_thread;

		/// ESC标志，用于标识ESC操作的状态
		public string esc;

		/// 语言标志，用于标识当前使用的语言类型
		private string languagle_flag;

		/// 排版文本，用于存储经过排版处理的文本内容
		public string typeset_txt;

		/// 百度标志，用于标识百度相关功能的状态
		public string baidu_flags;

		/// 原始图像，用于存储处理前的原始图像内容
		private Image image_ori;

		/// 竖排右侧文本，用于存储竖排文本的右侧内容
		public string shupai_Right_txt;

		/// 竖排左侧文本，用于存储竖排文本的左侧内容
		public string shupai_Left_txt;

		/// 百度OCR参数A，用于百度OCR服务的参数配置
		public string OCR_baidu_a;

		/// 百度OCR参数B，用于百度OCR服务的参数配置
		public string OCR_baidu_b;

		/// 图像列表，用于存储图像对象集合
		public List<Image> imagelist;

		/// 图像列表长度，用于存储图像列表的长度信息
		public int imagelist_lenght;

		/// 百度OCR参数D，用于百度OCR服务的参数配置
		public string OCR_baidu_d;

		/// 百度OCR参数C，用于百度OCR服务的参数配置
		public string OCR_baidu_c;

		/// 百度OCR参数E，用于百度OCR服务的参数配置
		public string OCR_baidu_e;

		/// 图像编号数组，用于存储图像的编号信息
		public int[] image_num;

		/// 拼音标志，用于标识是否启用拼音功能
		public bool pinyin_flag;

		/// 分割标志，用于标识文本分割功能是否启用
		public bool set_split;

		/// 合并标志，用于标识文本合并功能是否启用
		public bool set_merge;

		/// 翻译点击标识，用于标识翻译功能的点击状态
		public bool tranclick;

		/// 段落标识，用于标识段落处理状态
		public bool paragraph;

		/// 阿里表格实例，用于处理阿里表格相关功能
		private AliTable ailibaba;

		/// 标识是否为静默识别模式，识别后不显示窗口，只复制结果
		private bool isSilentMode = false;
		/// 标识是ocr的翻译还是输入翻译
		private bool isOcrTranslation = false;

		/// OCR 翻译: isContentFromOcr 为 true，isFromClipboardListener 为 false。
		/// 监听剪贴板翻译: isContentFromOcr 为 false，isFromClipboardListener 为 true。
		/// 手动输入翻译: isContentFromOcr 为 false，isFromClipboardListener 为 false。
		private bool isContentFromOcr = false;
		private bool isFromClipboardListener = false;
		
		private Timer translationTimer;
#endregion

		// ====================================================================================================================
		// **内部类、委托与枚举**
		//
		// 包含了 FmMain 类内部使用的辅助类型定义。
		// - 委托 (Delegates): 定义了用于跨线程调用的委托类型，如 `Translate` 和 `OcrThread`。
		// - 内部类 (Inner Classes):
		//   - AutoClosedMsgBox: 一个可以自动关闭的消息框。
		//   - TransObj, TransResult, Rootobject, Trans_result: 用于反序列化百度翻译 API 返回的 JSON 结果。
		//   - HtmlToText: 用于将 HTML 内容转换为纯文本。
		// - 枚举 (Enum):
		//   - MsgBoxStyle: 定义了消息框的样式。
		// ====================================================================================================================
		#region 内部类、委托与枚举
		public delegate void Translate();

		public delegate void OcrThread();

		private class HtmlToText
		{

			static HtmlToText()
			{
				Tags.Add("address", "\n");
				Tags.Add("blockquote", "\n");
				Tags.Add("div", "\n");
				Tags.Add("dl", "\n");
				Tags.Add("fieldset", "\n");
				Tags.Add("form", "\n");
				Tags.Add("h1", "\n");
				Tags.Add("/h1", "\n");
				Tags.Add("h2", "\n");
				Tags.Add("/h2", "\n");
				Tags.Add("h3", "\n");
				Tags.Add("/h3", "\n");
				Tags.Add("h4", "\n");
				Tags.Add("/h4", "\n");
				Tags.Add("h5", "\n");
				Tags.Add("/h5", "\n");
				Tags.Add("h6", "\n");
				Tags.Add("/h6", "\n");
				Tags.Add("p", "\n");
				Tags.Add("/p", "\n");
				Tags.Add("table", "\n");
				Tags.Add("/table", "\n");
				Tags.Add("ul", "\n");
				Tags.Add("/ul", "\n");
				Tags.Add("ol", "\n");
				Tags.Add("/ol", "\n");
				Tags.Add("/li", "\n");
				Tags.Add("br", "\n");
				Tags.Add("/td", "\t");
				Tags.Add("/tr", "\n");
				Tags.Add("/pre", "\n");
				IgnoreTags = new HashSet<string>();
				IgnoreTags.Add("script");
				IgnoreTags.Add("noscript");
				IgnoreTags.Add("style");
				IgnoreTags.Add("object");
			}

			public string Convert(string html)
			{
				_text = new TextBuilder();
				_html = html;
				_pos = 0;
				while (!EndOfText)
				{
					if (Peek() == '<')
					{
						bool flag;
						var text = ParseTag(out flag);
						if (text == "body")
						{
							_text.Clear();
						}
						else if (text == "/body")
						{
							_pos = _html.Length;
						}
						else if (text == "pre")
						{
							_text.Preformatted = true;
							EatWhitespaceToNextLine();
						}
						else if (text == "/pre")
						{
							_text.Preformatted = false;
						}
						string s;
						if (Tags.TryGetValue(text, out s))
						{
							_text.Write(s);
						}
						if (IgnoreTags.Contains(text))
						{
							EatInnerContent(text);
						}
					}
					else if (char.IsWhiteSpace(Peek()))
					{
						_text.Write(_text.Preformatted ? Peek() : ' ');
						MoveAhead();
					}
					else
					{
						_text.Write(Peek());
						MoveAhead();
					}
				}
				return HttpUtility.HtmlDecode(_text.ToString());
			}

			protected string ParseTag(out bool selfClosing)
			{
				var result = string.Empty;
				selfClosing = false;
				if (Peek() == '<')
				{
					MoveAhead();
					EatWhitespace();
					var pos = _pos;
					if (Peek() == '/')
					{
						MoveAhead();
					}
					while (!EndOfText && !char.IsWhiteSpace(Peek()) && Peek() != '/' && Peek() != '>')
					{
						MoveAhead();
					}
					result = _html.Substring(pos, _pos - pos).ToLower();
					while (!EndOfText && Peek() != '>')
					{
						if (Peek() == '"' || Peek() == '\'')
						{
							EatQuotedValue();
						}
						else
						{
							if (Peek() == '/')
							{
								selfClosing = true;
							}
							MoveAhead();
						}
					}
					MoveAhead();
				}
				return result;
			}

			protected void EatInnerContent(string tag)
			{
				var b = "/" + tag;
				while (!EndOfText)
				{
					if (Peek() == '<')
					{
						bool flag;
						if (ParseTag(out flag) == b)
						{
							return;
						}
						if (!flag && !tag.StartsWith("/"))
						{
							EatInnerContent(tag);
						}
					}
					else
					{
						MoveAhead();
					}
				}
			}

			protected bool EndOfText => _pos >= _html.Length;

			protected char Peek()
			{
				if (_pos >= _html.Length)
				{
					return '\0';
				}
				return _html[_pos];
			}

			protected void MoveAhead()
			{
				_pos = Math.Min(_pos + 1, _html.Length);
			}

			private void EatWhitespace()
			{
				while (char.IsWhiteSpace(Peek()))
				{
					MoveAhead();
				}
			}

			private void EatWhitespaceToNextLine()
			{
				while (char.IsWhiteSpace(Peek()))
				{
					var num = (int)Peek();
					MoveAhead();
					if (num == 10)
					{
						break;
					}
				}
			}

			private void EatQuotedValue()
			{
				var c = Peek();
				if (c == '"' || c == '\'')
				{
					MoveAhead();
					_pos = _html.IndexOfAny(new[]
					{
						c,
						'\r',
						'\n'
					}, _pos);
					if (_pos < 0)
					{
						_pos = _html.Length;
						return;
					}
					MoveAhead();
				}
			}

			private static readonly Dictionary<string, string> Tags = new Dictionary<string, string>();

			private static readonly HashSet<string> IgnoreTags;

			protected TextBuilder _text;

			private string _html;

			private int _pos;

			protected class TextBuilder
			{

				public TextBuilder()
				{
					_text = new StringBuilder();
					_curLine = new StringBuilder();
					_emptyLines = 0;
					_preformatted = false;
				}

				public bool Preformatted
				{
					get => _preformatted;
					set
					{
						if (value)
						{
							if (_curLine.Length > 0)
							{
								FlushCurLine();
							}
							_emptyLines = 0;
						}
						_preformatted = value;
					}
				}

				public void Clear()
				{
					_text.Length = 0;
					_curLine.Length = 0;
					_emptyLines = 0;
				}

				public void Write(string s)
				{
					foreach (var c in s)
					{
						Write(c);
					}
				}

				public void Write(char c)
				{
					if (_preformatted)
					{
						_text.Append(c);
						return;
					}
					if (c != '\r')
					{
						if (c == '\n')
						{
							FlushCurLine();
							return;
						}
						if (char.IsWhiteSpace(c))
						{
							var length = _curLine.Length;
							if (length == 0 || !char.IsWhiteSpace(_curLine[length - 1]))
							{
								_curLine.Append(' ');
							}
						}
						else
						{
							_curLine.Append(c);
						}
					}
				}

				private void FlushCurLine()
				{
					var text = _curLine.ToString().Trim();
					if (text.Replace("\u00a0", string.Empty).Length == 0)
					{
						_emptyLines++;
						if (_emptyLines < 2 && _text.Length > 0)
						{
							_text.AppendLine(text);
						}
					}
					else
					{
						_emptyLines = 0;
						_text.AppendLine(text);
					}
					_curLine.Length = 0;
				}

				public override string ToString()
				{
					if (_curLine.Length > 0)
					{
						FlushCurLine();
					}
					return _text.ToString();
				}

				private readonly StringBuilder _text;

				private readonly StringBuilder _curLine;

				private int _emptyLines;

				private bool _preformatted;
			}
		}

		// 计算MD5值
		public static string EncryptString(string str)
		{
			var md5 = MD5.Create();
			// 将字符串转换成字节数组
			var byteOld = Encoding.UTF8.GetBytes(str);
			// 调用加密方法
			var byteNew = md5.ComputeHash(byteOld);
			// 将加密结果转换为字符串
			var sb = new StringBuilder();
			foreach (var b in byteNew)
				// 将字节转换成16进制表示的字符串，
				sb.Append(b.ToString("x2"));
			// 返回加密的字符串
			return sb.ToString();
		}


        #endregion
    }
    public class Rootobject
	{
		public string from { get; set; }
		public string to { get; set; }
		public Trans_result[] trans_result { get; set; }
		public string error_code { get; set; }
		public string error_msg { get; set; }
	}

	public class Trans_result
	{
		public string src { get; set; }
		public string dst { get; set; }
	}
}
