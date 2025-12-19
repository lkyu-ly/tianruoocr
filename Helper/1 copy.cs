public async void 翻译文本()
		{
			// 检查是否启用了快速翻译功能
			if (IniHelper.GetValue("配置", "快速翻译") == "True")
			{
				var data = "";
				try
				{
					// 根据焦点位置获取待翻译文本
					if (ContainsFocus)
					{
						if (RichBoxBody.richTextBox1.Focused)
						{
							trans_hotkey = RichBoxBody.richTextBox1.SelectedText;
						}
						else if (RichBoxBody_T.richTextBox1.Focused)
						{
							trans_hotkey = RichBoxBody_T.richTextBox1.SelectedText;
						}
						else
						{
							trans_hotkey = GetTextFromClipboard();
						}
					}
					else
					{
						trans_hotkey = GetTextFromClipboard();
					}
					if (string.IsNullOrEmpty(trans_hotkey)) return;

					// 获取当前翻译服务配置
					string transService = StaticValue.Translate_Current_API;
					string sectionName;
					switch (transService)
					{
						case "谷歌":
							sectionName = "Google";
							break;
						case "百度":
							sectionName = "Baidu";
							break;
						case "腾讯":
							sectionName = "Tencent";
							break;
						case "腾讯交互翻译":
							sectionName = "TencentInteractive";
							break;
						case "彩云小译":
							sectionName = "Caiyun";
							break;
						case "彩云小译2":
							sectionName = "Caiyun2";
							break;
						case "百度2":
							sectionName = "Baidu2";
							break;
						case "火山翻译":
							sectionName = "Volcano";
							break;
						default:
							sectionName = transService;
							break;
					}
					if (!StaticValue.Translate_Configs.TryGetValue(sectionName, out var config))
					{
						config = new StaticValue.TranslateConfig { Source = "auto", Target = "自动判断" };
					}

					// 确定源语言和目标语言
					string toLang;
					string fromLang = config.Source;

					// 自动判断目标语言
					if (config.Target == "自动判断")
					{
						toLang = "en"; // 默认翻译为英文
						// 中文<->英文互译逻辑
						if (StaticValue.ZH2EN)
						{
							if (ch_count(trans_hotkey.Trim()) > en_count(trans_hotkey.Trim()) || (en_count(trans_hotkey.Trim()) == 1 && ch_count(trans_hotkey.Trim()) == 1))
							{
								toLang = "en";
							}
							else
							{
								toLang = "zh-CN";
							}
						}
						// 中文<->日文互译逻辑
						else if (StaticValue.ZH2JP)
						{
							// 统计中文字符和日文字符数量来判断主要语言
							string textToCheck = trans_hotkey.Trim();
							int chineseCount = ch_count(textToCheck);
							// 对于日文，我们需要统计假名的数量，因为汉字在中日文都存在
							int japaneseKanaCount = 0;
							foreach (char c in textToCheck)
							{
								// 统计平假名 (U+3040-U+309F) 和片假名 (U+30A0-U+30FF)
								if ((c >= '\u3040' && c <= '\u309F') || (c >= '\u30A0' && c <= '\u30FF'))
								{
									japaneseKanaCount++;
								}
							}
							
							// 如果日文假名多于中文字符，说明是日文文本，翻译到中文
							// 否则翻译到日文
							if (japaneseKanaCount > 0 && japaneseKanaCount >= chineseCount / 2)
							{
								// 有相当数量的假名，判断为日文，翻译到中文
								toLang = "zh-CN";
							}
							else
							{
								// 中文字符占主导，翻译到日文
								toLang = "ja";
							}
						}
						// 中文<->韩文互译逻辑
						else if (StaticValue.ZH2KO)
						{
							if (contain_kor(trans_hotkey.Trim()))
							{
								toLang = "zh-CN";
							}
							else
							{
								toLang = "ko";
							}
						}
					}
					else
					{
						toLang = config.Target;
					}

					// 处理百度和腾讯翻译服务的语言代码映射
					if (transService == "百度")
					{
						if (fromLang == "zh-CN") fromLang = "zh";
						if (toLang == "zh-CN") toLang = "zh";
						if (fromLang == "ja") fromLang = "jp";
						if (toLang == "ja") toLang = "jp";
						if (fromLang == "ko") fromLang = "kor";
						if (toLang == "ko") toLang = "kor";
					}
					if (transService == "腾讯")
					{
						if (fromLang == "zh-CN") fromLang = "zh";
						if (toLang == "zh-CN") toLang = "zh";
					}

					// 调用相应的翻译服务进行翻译
					switch (transService)
					{
						case "谷歌":
							data = await GTranslateHelper.TranslateAsync(trans_hotkey, fromLang, toLang, "google");
							break;
						case "Bing":
							data = await BingTranslator.TranslateAsync(trans_hotkey, fromLang, toLang);
							break;
						case "Bing2":
						case "BingNew":
							data = await BingTranslator2.TranslateAsync(trans_hotkey, fromLang, toLang);
							break;
						case "Microsoft":
							data = await GTranslateHelper.TranslateAsync(trans_hotkey, fromLang, toLang, "microsoft");
							break;
						case "Yandex":
							data = await GTranslateHelper.TranslateAsync(trans_hotkey, fromLang, toLang, "yandex");
							break;
						case "百度":
							data = TranslateBaidu(trans_hotkey, fromLang, toLang, config.AppId, config.ApiKey);
							break;
						case "腾讯":
							data = Translate_Tencent(trans_hotkey, fromLang, toLang, config.AppId, config.ApiKey);
							break;
						case "腾讯交互翻译":
							data = await TencentTranslator.TranslateAsync(trans_hotkey, fromLang, toLang);
							break;
						case "彩云小译":
							data = await CaiyunTranslator.TranslateAsync(trans_hotkey, fromLang, toLang);
							break;
						case "彩云小译2":
							if (string.IsNullOrEmpty(config.ApiKey))
								data = "[彩云小译2]：未配置Token";
							else
								data = await CaiyunTranslator2.TranslateAsync(trans_hotkey, fromLang, toLang, config.ApiKey);
							break;
						case "火山翻译":
							data = await VolcanoTranslator.TranslateAsync(trans_hotkey, fromLang, toLang);
							break;
						case "百度2":
							data = await BaiduTranslator2Helper.TranslateAsync(trans_hotkey, fromLang, toLang);
							break;
                        case "OpenAICompatible": // 注意：这个名字要和 Trans_foreach 里设置的一致
                                                      // 调用 OpenAICompatibleTranslate.Translate
                                                      // textToTranslate: 原文
                                                      // currentSelectedAITransMode: 当前选中的模式 (定义在 FmMain.AI.Translate.cs 中)
                                                      // toLang: 目标语言
                                                      // fromLang: 源语言
                                                      // 注意：这里通常不需要 await，因为 Translate 内部用了 .Result (同步等待)，除非你改造 Helper 为异步
                            // await System.Threading.Tasks.Task.Run(() =>
                            // {
                            //     googleTranslate_txt = OpenAICompatibleTranslate.Translate(
                            //         trans_hotkey,
                            //         this.currentSelectedAITransMode,
                            //         toLang,
                            //         fromLang
                            //     );
                            // });
							await System.Threading.Tasks.Task.Run(() =>
							{
								Trans_OpenAICompatible(trans_hotkey,fromLang,toLang);

							});
                            break;
                        default:
							data = await GTranslateHelper.TranslateAsync(trans_hotkey, fromLang, toLang, "google");
							break;
					}
					// 将翻译结果复制到剪贴板并粘贴到当前焦点位置
					SetClipboardDataWithLock(DataFormats.UnicodeText, data);        
					Debug.WriteLine("快速翻译译文复制到剪贴板");
					SendKeys.SendWait("^v");
					return;
				}
				catch
				{
                    // 出现异常时也尝试粘贴当前结果
                    SetClipboardDataWithLock(DataFormats.UnicodeText, data);
					Debug.WriteLine("出现异常快速翻译译文也复制到剪贴板");
					SendKeys.SendWait("^v");
					return;
				}
			}
			// 如果未启用快速翻译，则执行常规翻译流程
			SendKeys.SendWait("^c");
			SendKeys.Flush();
			RichBoxBody.richTextBox1.TextChanged -= RichBoxBody_TextChanged;
			RichBoxBody.Text = Clipboard.GetText();
			RichBoxBody.richTextBox1.TextChanged += RichBoxBody_TextChanged;
            // 1. 先让窗口以正常状态显示出来
            FormBorderStyle = FormBorderStyle.Sizable;
            Visible = true;
            Show();
            WindowState = FormWindowState.Normal;
            HelpWin32.SetForegroundWindow(StaticValue.mainHandle); // 激活窗口

            // 2. 在窗口已经可见且状态正常后，再调用 TransClick() 来设置双栏布局
            TransClick();

			if (IniHelper.GetValue("工具栏", "顶置") == "True")
			{
				TopMost = true;
				return;
			}
			TopMost = false;
		}