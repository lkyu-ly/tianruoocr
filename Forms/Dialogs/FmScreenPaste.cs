using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TrOCR.Helper;

namespace TrOCR
{

	public partial class FmScreenPaste : Form
	{
		private OwnedImage pasteImage;
		private bool isClosing;

		public FmScreenPaste(Image img, Point LocationPoint)
		{
			if (img == null)
			{
				throw new ArgumentNullException(nameof(img));
			}

			m_aeroEnabled = false;
			InitializeComponent();
			DoubleBuffered = true;

			pasteImage = new OwnedImage(img);
			Location = LocationPoint;
			FormBorderStyle = FormBorderStyle.None;
			MouseDown += Form1_MouseDown;
			MouseMove += Form1_MouseMove;
			MouseUp += Form1_MouseUp;

			var size = pasteImage.Size;
			MaximumSize = MinimumSize = size;
			Size = size;
			MouseDoubleClick += 双击_MouseDoubleClick;
		}

		private void RightCMS_Opening(object sender, CancelEventArgs e)
		{
			var topMost = TopMost;
			if (topMost)
			{
				置顶ToolStripMenuItem.Text = "取消置顶";
			}
			else
			{
				置顶ToolStripMenuItem.Text = "置顶窗体";
			}
		}

		private void 置顶ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			TopMost = !TopMost;
		}

		private void 关闭ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			CloseAndDisposeImage();
		}

		private void 复制toolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (pasteImage == null || !pasteImage.IsUsable)
			{
				CommonHelper.ShowHelpMsg("贴图已失效");
				CloseAndDisposeImage();
				return;
			}

			using (var copy = pasteImage.CloneBitmap())
			{
				if (!ClipboardHelper.TrySetDataObject(copy, out var errorMessage))
				{
					CommonHelper.ShowHelpMsg("复制失败：剪贴板被占用", 1600u);
					System.Diagnostics.Debug.WriteLine(errorMessage);
				}
			}
		}

		private void 保存toolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (pasteImage == null || !pasteImage.IsUsable)
			{
				CommonHelper.ShowHelpMsg("贴图已失效");
				CloseAndDisposeImage();
				return;
			}

			using (var saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.Filter = "jpg图片(*.jpg)|*.jpg|png图片(*.png)|*.png|bmp图片(*.bmp)|*.bmp";
				saveFileDialog.AddExtension = false;
				saveFileDialog.FileName = string.Concat("tianruo_", DateTime.Now.Year, "-", DateTime.Now.Month, "-", DateTime.Now.Day, "-", DateTime.Now.Ticks);
				saveFileDialog.Title = "保存图片";
				saveFileDialog.FilterIndex = 1;
				saveFileDialog.RestoreDirectory = true;

				if (saveFileDialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}

				var extension = Path.GetExtension(saveFileDialog.FileName);
				var format = GetImageFormat(extension);
				if (format == null)
				{
					CommonHelper.ShowHelpMsg("不支持的图片格式");
					return;
				}

				try
				{
					pasteImage.Bitmap.Save(saveFileDialog.FileName, format);
				}
				catch (ExternalException ex)
				{
					CommonHelper.ShowHelpMsg("保存失败");
					System.Diagnostics.Debug.WriteLine("贴图保存失败: " + ex);
				}
				catch (ArgumentException ex)
				{
					CommonHelper.ShowHelpMsg("贴图已失效");
					System.Diagnostics.Debug.WriteLine("贴图保存失败: " + ex);
					CloseAndDisposeImage();
				}
			}
		}

		private static ImageFormat GetImageFormat(string extension)
		{
			if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
			{
				return ImageFormat.Jpeg;
			}

			if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
			{
				return ImageFormat.Png;
			}

			if (string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase))
			{
				return ImageFormat.Bmp;
			}

			return null;
		}

		[DllImport("user32.dll")]
		public static extern bool ReleaseCapture();

		[DllImport("user32.dll")]
		public static extern bool SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);

		private void Form_MouseDown(object sender, MouseEventArgs e)
		{
			var wMsg = 274;
			var num = 61456;
			var num2 = 2;
			ReleaseCapture();
			SendMessage(Handle, wMsg, num + num2, 0);
		}

		[DllImport("Gdi32.dll")]
		private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

		[DllImport("dwmapi.dll")]
		public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

		[DllImport("dwmapi.dll")]
		public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

		[DllImport("dwmapi.dll")]
		public static extern int DwmIsCompositionEnabled(ref int pfEnabled);

		private bool CheckAeroEnabled()
		{
			var flag = Environment.OSVersion.Version.Major >= 6;
			bool result;
			if (flag)
			{
				var num = 0;
				DwmIsCompositionEnabled(ref num);
				result = (num == 1);
			}
			else
			{
				result = false;
			}
			return result;
		}

		protected override void WndProc(ref Message m)
		{
			var msg = m.Msg;
			var flag = m.Msg == 132 && (int)m.Result == 1;
			if (flag)
			{
				m.Result = (IntPtr)2;
			}
			var flag2 = msg == 133 && m_aeroEnabled;
			if (flag2)
			{
				var num = 2;
				DwmSetWindowAttribute(Handle, 2, ref num, 4);
				var margins = new MARGINS
				{
					bottomHeight = 1,
					leftWidth = 1,
					rightWidth = 1,
					topHeight = 1
				};
				DwmExtendFrameIntoClientArea(Handle, ref margins);
			}
			base.WndProc(ref m);
		}

		protected override CreateParams CreateParams
		{
			get
			{
				m_aeroEnabled = CheckAeroEnabled();
				var createParams = base.CreateParams;
				var flag = !m_aeroEnabled;
				if (flag)
				{
					createParams.ClassStyle |= 131072;
				}
				return createParams;
			}
		}

		private void 双击_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			CloseAndDisposeImage();
		}

		private void CloseAndDisposeImage()
		{
			if (isClosing)
			{
				return;
			}

			isClosing = true;
			Close();
		}

		public void AdjustSize()
		{
			var size = new Size(10, 25);
			MaximumSize = (MinimumSize = size);
			Size = size;
		}

		private void Form1_MouseDown(object sender, MouseEventArgs e)
		{
			var flag = e.Button == MouseButtons.Left;
			if (flag)
			{
				mouseOff = new Point(-e.X, -e.Y);
				leftFlag = true;
			}
		}

		private void Form1_MouseMove(object sender, MouseEventArgs e)
		{
			var flag = leftFlag;
			if (flag)
			{
				var mousePosition = MousePosition;
				mousePosition.Offset(mouseOff.X, mouseOff.Y);
				Location = mousePosition;
			}
		}

		private void Form1_MouseUp(object sender, MouseEventArgs e)
		{
			var flag = leftFlag;
			if (flag)
			{
				leftFlag = false;
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			if (pasteImage == null || !pasteImage.IsUsable)
			{
				base.OnPaint(e);
				return;
			}

			try
			{
				var image = pasteImage.Bitmap;
				e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
				e.Graphics.DrawImage(
					image,
					new Rectangle(0, 0, Width, Height),
					0,
					0,
					image.Width,
					image.Height,
					GraphicsUnit.Pixel);
			}
			catch (ArgumentException ex)
			{
				System.Diagnostics.Debug.WriteLine("贴图窗口绘制失败: " + ex.Message);
				CloseAndDisposeImage();
				return;
			}
			catch (ObjectDisposedException ex)
			{
				System.Diagnostics.Debug.WriteLine("贴图窗口图像已释放: " + ex.Message);
				CloseAndDisposeImage();
				return;
			}

			base.OnPaint(e);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
		}

		private bool m_aeroEnabled;

		private const int CS_DROPSHADOW = 131072;

		private const int WM_NCPAINT = 133;

		private const int WM_ACTIVATEAPP = 28;

		private const int WM_NCHITTEST = 132;

		private const int HTCLIENT = 1;

		private const int HTCAPTION = 2;

		private Point mouseOff;

		private bool leftFlag;

		public struct MARGINS
		{

			public int leftWidth;

			public int rightWidth;

			public int topHeight;

			public int bottomHeight;
		}
	}
}
