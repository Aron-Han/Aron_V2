using Cognex.VisionPro;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2
{
	public static class VpImageUtil
	{
		/// <summary>
		/// 把 ICogImage 转成完全托管的 Bitmap（已 Clone，安全跨线程/延迟使用）
		/// </summary>
		public static Bitmap ToManagedBitmap(ICogImage img)
		{
			if (img == null) return null;

			try
			{
				// 1) 取 Bitmap（依赖你版本的 ToBitmap）
				using (var bmp = img.ToBitmap()) // 若 ToBitmap 返回的不是 IDisposable，也可以去掉 using
				{
					// 2) 立即 Clone，切断与底层 RCW/句柄的关系
					return (Bitmap)bmp.Clone();
				}
			}
			catch (System.Runtime.InteropServices.InvalidComObjectException)
			{
				// RCW 已分离（图像/工具已释放）→ 提前 ToManagedBitmap，或改为 CopyPixels 后再转
				return null;
			}
			catch (ObjectDisposedException)
			{
				// 底层已释放 → 同上
				return null;
			}
		}

		/// <summary>
		/// 异步保存为 PNG（不阻塞主流程）
		/// </summary>
		public static void SavePngAsync(ICogImage img, string pathPng)
		{
			var managed = ToManagedBitmap(img);
			if (managed == null) return;

			Task.Run(() =>
			{
				using (managed) // 这里把 Clone 的这份在保存后释放
				{
					managed.Save(pathPng, ImageFormat.Png);
				}
			});
		}

		/// <summary>
		/// 异步保存为 JPEG（可调质量）
		/// </summary>
		public static void SaveJpegAsync(ICogImage img, string pathJpg, long quality = 90L)
		{
			var managed = ToManagedBitmap(img);
			if (managed == null) return;

			Task.Run(() =>
			{
				using (managed)
				{
					var enc = GetJpegEncoder();
					var ep = new EncoderParameters(1);
					ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
					managed.Save(pathJpg, enc, ep);
				}
			});
		}

		private static ImageCodecInfo GetJpegEncoder()
		{
			var encoders = ImageCodecInfo.GetImageEncoders();
			foreach (var e in encoders)
				if (e.MimeType == "image/jpeg") return e;
			return null;
		}
	}
}
