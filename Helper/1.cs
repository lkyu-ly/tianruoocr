private async Task<string> GetTranslationAsync(string textToTranslate, string overrideSource = null, string overrideTarget = null)
		{
		    // 获取当前使用的翻译服务
			string transService = StaticValue.Translate_Current_API;
			string sectionName;
			// 根据翻译服务名称确定配置节名称
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

			// 尝试获取翻译配置，如果不存在则使用默认配置
			if (!StaticValue.Translate_Configs.TryGetValue(sectionName, out var config))
			{
				config = new StaticValue.TranslateConfig { Source = "auto", Target = "自动判断" };
			}

			string toLang;
			// 【修改】如果临时源语言(overrideSource)不为空，则使用它，否则才用配置文件中的
			string fromLang = overrideSource ?? config.Source; 
			// 【修改】优先使用临时目标语言
			if (!string.IsNullOrEmpty(overrideTarget))
			{
			    toLang = overrideTarget;
			}
			// 根据目标语言配置自动判断需要翻译成的语言
			else if (config.Target == "自动判断")
			{
				toLang = "en"; // 默认翻译为英文
				if (StaticValue.ZH2EN)
				{
					//中文和英文互译逻辑
					// 中文转英文逻辑：比较中英文字符数量确定源语言
					if (ch_count(textToTranslate.Trim()) > en_count(textToTranslate.Trim()) || (en_count(textToTranslate.Trim()) == 1 && ch_count(textToTranslate.Trim()) == 1))
					{
						toLang = "en";
					}
					else
					{
						toLang = "zh-CN";
					}
				}
				else if (StaticValue.ZH2JP)
				{
					// 中文和日文互译逻辑
					// 统计中文字符和日文字符数量来判断主要语言
					string textToCheck = textToTranslate.Trim();
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
				else if (StaticValue.ZH2KO)
				{
					// 中文和韩文互译逻辑
					if (contain_kor(textToTranslate.Trim()))
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
				// 使用配置中指定的目标语言
				toLang = config.Target;
			}

			// 百度和腾讯翻译服务需要特殊处理语言代码
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

			// 根据翻译服务调用相应的翻译方法
			switch (transService)
			{
				case "谷歌":
					googleTranslate_txt = await GTranslateHelper.TranslateAsync(textToTranslate, fromLang, toLang, "google");
					break;
				case "Bing":
					googleTranslate_txt = await BingTranslator.TranslateAsync(textToTranslate, fromLang, toLang);
					break;
				case "Bing2":
				case "BingNew":
					googleTranslate_txt = await BingTranslator2.TranslateAsync(textToTranslate, fromLang, toLang);
					break;
				case "Microsoft":
					googleTranslate_txt = await GTranslateHelper.TranslateAsync(textToTranslate, fromLang, toLang, "microsoft");
					break;
				case "Yandex":
					googleTranslate_txt = await GTranslateHelper.TranslateAsync(textToTranslate, fromLang, toLang, "yandex");
					break;
				case "百度":
					googleTranslate_txt = TranslateBaidu(textToTranslate, fromLang, toLang, config.AppId, config.ApiKey);
					break;
				case "腾讯":
					googleTranslate_txt = Translate_Tencent(textToTranslate, fromLang, toLang, config.AppId, config.ApiKey);
					break;
				case "腾讯交互翻译":
					googleTranslate_txt = await TencentTranslator.TranslateAsync(textToTranslate, fromLang, toLang);
					break;
				case "彩云小译":
					googleTranslate_txt = await CaiyunTranslator.TranslateAsync(textToTranslate, fromLang, toLang);
					break;
				case "彩云小译2":
					if (string.IsNullOrEmpty(config.ApiKey))
						googleTranslate_txt = "[彩云小译2]：未配置Token";
					else
						googleTranslate_txt = await CaiyunTranslator2.TranslateAsync(textToTranslate, fromLang, toLang, config.ApiKey);
					break;
				case "火山翻译":
					googleTranslate_txt = await VolcanoTranslator.TranslateAsync(textToTranslate, fromLang, toLang);
					break;
				case "百度2":
					googleTranslate_txt = await BaiduTranslator2Helper.TranslateAsync(textToTranslate, fromLang, toLang);
					break;
                case "OpenAICompatible": // 注意：这个名字要和 Trans_foreach 里设置的一致
										 // 调用 OpenAICompatibleTranslate.Translate
										 // textToTranslate: 原文
										 // currentSelectedAITransMode: 当前选中的模式 (定义在 FmMain.AI.Translate.cs 中)
										 // toLang: 目标语言
										 // fromLang: 源语言
										 // 注意：这里通常不需要 await，因为 Translate 内部用了 .Result (同步等待)，除非你改造 Helper 为异步
					//await System.Threading.Tasks.Task.Run(() =>
					//{
					//	googleTranslate_txt = OpenAICompatibleTranslate.Translate(
					//	 textToTranslate,
					//						 this.currentSelectedAITransMode,
					//						 toLang,
					//						 fromLang
					//					 );
					//});
					await System.Threading.Tasks.Task.Run(() =>
					{
						Trans_OpenAICompatible(textToTranslate,fromLang,toLang);

					});
					break;
                // =============== 【新增代码结束】 ===============
                default:
					googleTranslate_txt = await GTranslateHelper.TranslateAsync(textToTranslate, fromLang, toLang, "google");
					break;
			}
		    return googleTranslate_txt; 
		}