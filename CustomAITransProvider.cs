using System;

namespace TrOCR.Helper // 根据你的项目命名空间调整
{
    public class CustomAITransProvider : CustomAIProvider
    {
 
        //源语言
        public string Source { get; set; } = "auto detect";
        //目标语言
        public string Target { get; set; } = "自动判断";

      
    }
}