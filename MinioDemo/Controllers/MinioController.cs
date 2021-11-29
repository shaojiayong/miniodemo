using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.Exceptions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace MinioDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MinIOController : ControllerBase
    {
        private static string bucketName = "test";//默认桶

        private readonly MinioClient _client;

        public MinIOController(MinioClient client)
        {
            _client = client;
        }
        /// <summary>
        /// 上传附件
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        [HttpPost("/file/UploadFile")]
        public async Task<dynamic> UploadFile(List<IFormFile> files)
        {
            long size = files.Sum(f => f.Length);
            try
            {
                bool found = await _client.BucketExistsAsync(bucketName);
                //如果桶不存在则创建桶
                if (!found)
                {
                    await _client.MakeBucketAsync(bucketName);
                }
                foreach (var formFile in files)
                {
                    string saveFileName = $"{Guid.NewGuid():N}{Path.GetExtension(formFile.FileName)}";//存储的文件名
                    string objectName = $"/{DateTime.Now:yyyy/MM/dd}/{saveFileName}";//文件保存路径
                    if (formFile.Length > 0)
                    {
                        Stream stream = formFile.OpenReadStream();
                        await _client.PutObjectAsync(bucketName,
                                 objectName,
                                 stream,
                                 formFile.Length,
                                 formFile.ContentType);

                    }
                }
            }
            catch (MinioException e)
            {
                Console.WriteLine("文件上传错误: {0}", e.Message);
            }
            return Ok(new { count = files.Count, size });
        }


        /// <summary>
        /// 下载附件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="width">所访问图片的宽度,放空则自动缩放</param>
        /// <param name="height">所访问图片的高度,放空则自动缩放</param>
        /// <returns></returns>
        [HttpGet("/file/DownloadFile")]
        public async Task<IActionResult> DownloadFile(string fileName, int? width, int? height)
        {
            var memoryStream = new MemoryStream();
            var contentType = GetContentType(fileName);
            string fileExt = Path.GetExtension(fileName);
            try
            {
                await _client.StatObjectAsync(bucketName, fileName);
                await _client.GetObjectAsync(bucketName, fileName,
                                    (stream) =>
                                    {
                                        stream.CopyTo(memoryStream);
                                    });
                memoryStream.Position = 0;
                #region 图片类型文件则可进行缩略操作
                if (IsImage(memoryStream))
                {
                    //缩放图片
                    using (var imgBmp = new Bitmap(memoryStream))
                    {
                        //原尺寸
                        var oldWidth = imgBmp.Width;
                        var oldHeight = imgBmp.Height;
                        int newWidth = 0, newHeigt = 0;
                        if (width.HasValue || height.HasValue)
                        {
                            if (width.HasValue && !height.HasValue)//按宽度等比例缩放
                            {
                                newWidth = width.Value;
                                newHeigt = width.Value * oldHeight / oldWidth;
                            }
                            else if (!width.HasValue && height.HasValue)//按高度等比例缩放
                            {
                                newHeigt = height.Value;
                                newWidth = height.Value * oldWidth / oldHeight;
                            }
                            else
                            {
                                newWidth = width.Value;
                                newHeigt = height.Value;
                            }
                        }
                        else
                        {
                            newWidth = oldWidth;
                            newHeigt = oldHeight;
                        }

                        var newImg = new Bitmap(imgBmp, newWidth, newHeigt);
                        //newImg.SetResolution(72, 72);
                        var ms = new MemoryStream();
                        newImg.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                        var bytes = ms.GetBuffer();
                        ms.Close();
                        //return File(bytes, contentType, DateTime.Now.ToString("yyyyMMddmmss") + fileExt);
                        return new FileContentResult(bytes, contentType);
                    }
                }
                #endregion

            }
            catch (MinioException e)
            {
                Console.Out.WriteLine("下载附件发生错误: " + e);
            }
            return File(memoryStream, contentType, DateTime.Now.ToString("yyyyMMddmmss") + fileExt);

        }
        #region 私有方法
        /// <summary>
        /// 根据文件名获取文件扩展名
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static string GetContentType(string fileName)
        {
            if (fileName.Contains(".jpg"))
            {
                return "image/jpg";
            }
            else if (fileName.Contains(".jpeg"))
            {
                return "image/jpeg";
            }
            else if (fileName.Contains(".png"))
            {
                return "image/png";
            }
            else if (fileName.Contains(".gif"))
            {
                return "image/gif";
            }
            else if (fileName.Contains(".pdf"))
            {
                return "application/pdf";
            }
            else
            {
                return "application/octet-stream";
            }
        }

        /// <summary>
        /// 判断文件是否为图片
        /// </summary>
        /// <param name="stream">文件流</param>
        /// <returns>返回结果</returns>
        private static bool IsImage(Stream stream)
        {
            try
            {
                System.Drawing.Image img = System.Drawing.Image.FromStream(stream);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
