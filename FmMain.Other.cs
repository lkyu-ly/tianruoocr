//存储一些疑似无用的代码片段

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text; // 确保引用
using System.Threading;
using System.Web;
using System.Windows.Forms;
using TrOCR.Helper;

namespace TrOCR
{
    public partial class FmMain
    {
        /// <summary>
		/// 使用百度OCR识别图片内容，并将结果添加到OCR_baidu_b变量中
		/// </summary>
		/// <param name="image">需要识别的图片</param>
		public void OcrBdUseB(Image image)
        {
            try
            {
                var str = "CHN_ENG";
                var array = OcrHelper.ImgToBytes(image);
                var data = "type=general_location&image=data" + HttpUtility.UrlEncode(":image/jpeg;base64," + Convert.ToBase64String(array)) + "&language_type=" + str;
                var url = "http://ai.baidu.com/aidemo";
                var referer = "http://ai.baidu.com/tech/ocr/general";
                var value = CommonHelper.PostStrData(url, data, "", referer);
                var jArray = JArray.Parse(((JObject)JsonConvert.DeserializeObject(value))["data"]["words_result"].ToString());
                var text = "";
                var array2 = new string[jArray.Count];
                for (var i = 0; i < jArray.Count; i++)
                {
                    var jObject = JObject.Parse(jArray[i].ToString());
                    text += jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                    array2[jArray.Count - 1 - i] = jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                }
                OCR_baidu_b = (OCR_baidu_b + text + "\r\n").Replace("\r\n\r\n", "");
                Thread.Sleep(10);
            }
            catch (Exception)
            {
                //
            }
        }

        /// <summary>
        /// 使用百度OCR识别图片内容，并将结果添加到OCR_baidu_a变量中
        /// </summary>
        /// <param name="image">需要识别的图片</param>
        public void OcrBdUseA(Image image)
        {
            try
            {
                var str = "CHN_ENG";
                var array = OcrHelper.ImgToBytes(image);
                var data = "type=general_location&image=data" + HttpUtility.UrlEncode(":image/jpeg;base64," + Convert.ToBase64String(array)) + "&language_type=" + str;
                var bytes = Encoding.UTF8.GetBytes(data);
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://ai.baidu.com/tech/ocr/general");
                httpWebRequest.CookieContainer = new CookieContainer();
                httpWebRequest.GetResponse().Close();
                var url = "http://ai.baidu.com/aidemo";
                var referer = "http://ai.baidu.com/tech/ocr/general";
                var value = CommonHelper.PostStrData(url, data, "", referer);
                var jArray = JArray.Parse(((JObject)JsonConvert.DeserializeObject(value))["data"]["words_result"].ToString());
                var text = "";
                var array2 = new string[jArray.Count];
                for (var i = 0; i < jArray.Count; i++)
                {
                    var jObject = JObject.Parse(jArray[i].ToString());
                    text += jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                    array2[jArray.Count - 1 - i] = jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                }
                OCR_baidu_a = (OCR_baidu_a + text + "\r\n").Replace("\r\n\r\n", "");
                Thread.Sleep(10);
            }
            catch (Exception)
            {
                //
            }
        }
        /// <summary>
		/// 使用百度OCR识别图片内容，并将结果添加到OCR_baidu_e变量中
		/// </summary>
		/// <param name="image">需要识别的图片</param>
		public void OcrBdUseE(Image image)
        {
            try
            {
                var str = "CHN_ENG";
                var array = OcrHelper.ImgToBytes(image);
                var data = "type=general_location&image=data" + HttpUtility.UrlEncode(":image/jpeg;base64," + Convert.ToBase64String(array)) + "&language_type=" + str;
                var url = "http://ai.baidu.com/aidemo";
                var referer = "http://ai.baidu.com/tech/ocr/general";
                var value = CommonHelper.PostStrData(url, data, "", referer);
                var jArray = JArray.Parse(((JObject)JsonConvert.DeserializeObject(value))["data"]["words_result"].ToString());
                var text = "";
                var array2 = new string[jArray.Count];
                for (var i = 0; i < jArray.Count; i++)
                {
                    var jObject = JObject.Parse(jArray[i].ToString());
                    text += jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                    array2[jArray.Count - 1 - i] = jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                }
                OCR_baidu_e = (OCR_baidu_e + text + "\r\n").Replace("\r\n\r\n", "");
                Thread.Sleep(10);
            }
            catch
            {
                //
            }
        }

        /// <summary>
        /// 使用百度OCR识别图片内容，并将结果添加到OCR_baidu_d变量中
        /// </summary>
        /// <param name="image">需要识别的图片</param>
        public void OcrBdUseD(Image image)
        {
            try
            {
                var str = "CHN_ENG";
                var array = OcrHelper.ImgToBytes(image);
                var data = "type=general_location&image=data" + HttpUtility.UrlEncode(":image/jpeg;base64," + Convert.ToBase64String(array)) + "&language_type=" + str;
                var url = "http://ai.baidu.com/aidemo";
                var referer = "http://ai.baidu.com/tech/ocr/general";
                var value = CommonHelper.PostStrData(url, data, "", referer);
                var jArray = JArray.Parse(((JObject)JsonConvert.DeserializeObject(value))["data"]["words_result"].ToString());
                var text = "";
                var array2 = new string[jArray.Count];
                for (var i = 0; i < jArray.Count; i++)
                {
                    var jObject = JObject.Parse(jArray[i].ToString());
                    text += jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                    array2[jArray.Count - 1 - i] = jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                }
                OCR_baidu_d = (OCR_baidu_d + text + "\r\n").Replace("\r\n\r\n", "");
                Thread.Sleep(10);
            }
            catch
            {
                //
            }
        }

        /// <summary>
        /// 使用百度OCR识别图片内容，并将结果添加到OCR_baidu_c变量中
        /// </summary>
        /// <param name="image">需要识别的图片</param>
        public void OcrBdUseC(Image image)
        {
            try
            {
                var str = "CHN_ENG";
                var array = OcrHelper.ImgToBytes(image);
                var data = "type=general_location&image=data" + HttpUtility.UrlEncode(":image/jpeg;base64," + Convert.ToBase64String(array)) + "&language_type=" + str;
                var url = "http://ai.baidu.com/aidemo";
                var referer = "http://ai.baidu.com/tech/ocr/general";
                var value = CommonHelper.PostStrData(url, data, "", referer);
                var jArray = JArray.Parse(((JObject)JsonConvert.DeserializeObject(value))["data"]["words_result"].ToString());
                var text = "";
                var array2 = new string[jArray.Count];
                for (var i = 0; i < jArray.Count; i++)
                {
                    var jObject = JObject.Parse(jArray[i].ToString());
                    text += jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                    array2[jArray.Count - 1 - i] = jObject["words"].ToString().Replace("\r", "").Replace("\n", "");
                }
                OCR_baidu_c = (OCR_baidu_c + text + "\r\n").Replace("\r\n\r\n", "");
                Thread.Sleep(10);
            }
            catch
            {
                //
            }
        }
    }
}