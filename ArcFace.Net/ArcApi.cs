using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static ArcFace.Net.ArcModel;

namespace ArcFace.Net
{
    public class ArcApi
    {
        const string AppId = "BmGSsaGTFocumTxjVEVPmsXGE5o2VWauQhL8Ry39v2gj";
        const string DKey = "7TNnXCVs56y4b4EqpDEhwhyqo7db3CDAQMwNfSmW93ZG";
        const string RKey = "7TNnXCVs56y4b4EqpDEhwhzLSigCXJ55HrYcqg3YyNRR";
        private static string _FaceDataPath = "E:\\FeatureData";
        private static string _FaceImagePath = Path.Combine(_FaceDataPath, "Image");

        private static IntPtr _DBuffer = IntPtr.Zero;
        private static IntPtr _DEnginer = IntPtr.Zero;

        private static IntPtr _RBuffer = IntPtr.Zero;
        private static IntPtr _REnginer = IntPtr.Zero;
        private static FaceLib _FaceLib = new FaceLib();

        public ArcApi()
        {
            Init();
        }

        //public ArcApi(string AppId, string DKey, string DBufferSize, int OrientPriority, int scale, int maxFaceNumber)
        //{
        //    Init();
        //}

        //默认参数初始化
        private bool Init()
        {
            //人脸检测初始化            
            var dOk = Dinit(AppId, DKey, _DBuffer, 20 * 1024 * 1024, out _DEnginer, (int)EOrientPriority.Only0, 16, 1);
            var rOk = Rinit(AppId, RKey, _RBuffer, 40 * 1024 * 1024, out _REnginer);
            var lOk = Linit();

            return dOk && rOk && lOk;
        }

        //释放内存
        public void Close()
        {
            if (_DEnginer != IntPtr.Zero)
            {
                ArcWrapper.DClose(_DEnginer);
                _DEnginer = IntPtr.Zero;
            }
            if (_DBuffer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_DBuffer);
                _DBuffer = IntPtr.Zero;
            }
            if (_REnginer != IntPtr.Zero)
            {
                ArcWrapper.RClose(_REnginer);
                _REnginer = IntPtr.Zero;
            }
            if (_RBuffer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_RBuffer);
                _RBuffer = IntPtr.Zero;
            }
            foreach (var item in _FaceLib.Items)
            {
                Marshal.FreeCoTaskMem(item.FaceModel.PFeature);
            }
        }

        //人脸检测初始化
        private bool Dinit(string appId, string sdkKey, IntPtr pBuffer, int bufferSize
            , out IntPtr engine, int orientPriority, int scale, int maxFaceNumber)
        {
            pBuffer = Marshal.AllocCoTaskMem(bufferSize);
            var initResult = (ErrorCode)ArcWrapper.DInit(appId, sdkKey, pBuffer, bufferSize
                , out engine, (int)EOrientPriority.Only0, scale, maxFaceNumber);
            if (initResult != ErrorCode.Ok)
            {
                return false;
            }
            return true;
        }

        //人脸比对初始化
        private bool Rinit(string appId, string sdkKey, IntPtr pBuffer, int bufferSize, out IntPtr engine)
        {
            pBuffer = Marshal.AllocCoTaskMem(bufferSize);
            var initResult = (ErrorCode)ArcWrapper.RInit(appId, sdkKey, pBuffer, bufferSize, out engine);
            if (initResult != ErrorCode.Ok)
            {
                return false;
            }
            return true;
        }

        //人脸库初始化
        private bool Linit()
        {
            if (!Directory.Exists(_FaceDataPath))
                Directory.CreateDirectory(_FaceDataPath);
            if (!Directory.Exists(_FaceImagePath))
                Directory.CreateDirectory(_FaceImagePath);

            foreach (var file in Directory.GetFiles(_FaceDataPath))
            {
                var info = new FileInfo(file);
                var data = File.ReadAllBytes(file);
                var pFeature = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, pFeature, data.Length);
                _FaceLib.Items.Add(new FaceLib.Item()
                {
                    OrderId = 0,
                    ID = info.Name.Replace(info.Extension, ""),
                    FaceModel = new FaceModel { Size = data.Length, PFeature = pFeature }
                });
            }
            return true;
        }

        //人脸检测
        public DetectResult Detection(Bitmap bitmap)
        {
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height)
                , ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            var imageData = new ImageData
            {
                PixelArrayFormat = 513,//Rgb24,
                Width = bitmap.Width,
                Height = bitmap.Height,
                Pitch = new int[4] { bmpData.Stride, 0, 0, 0 },
                ppu8Plane = new IntPtr[4] { bmpData.Scan0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero }
            };

            var ret = (ErrorCode)ArcWrapper.Detection(_DEnginer, ref imageData, out var pDetectResult);
            if (ret != ErrorCode.Ok)
            {
                bitmap.UnlockBits(bmpData);
                return new DetectResult { FaceCout = 0 };
            }
            var detectResult = Marshal.PtrToStructure<DetectResult>(pDetectResult);
            bitmap.UnlockBits(bmpData);
            return detectResult;
        }

        //特征提取
        private FaceModel ExtractFeature(Bitmap bitmap)
        {
            var detectResult = Detection(bitmap);
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height)
                , ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            var imageData = new ImageData
            {
                PixelArrayFormat = 513,//Rgb24,
                Width = bitmap.Width,
                Height = bitmap.Height,
                Pitch = new int[4] { bmpData.Stride, 0, 0, 0 },
                ppu8Plane = new IntPtr[4] { bmpData.Scan0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero }
            };

            var FFI = new FaceFeatureInput();
            FFI.FaceRect = Marshal.PtrToStructure<FaceRect>(detectResult.PFaceRect);
            FFI.Orient = 1;
            FaceModel faceModel = new FaceModel() { Size = 22020, PFeature = Marshal.AllocCoTaskMem(22020) };
            faceModel.Size = 0;
            if (ArcWrapper.ExtractFeature(_REnginer, ref imageData, ref FFI, out var fm) == (int)ErrorCode.Ok)
            {
                faceModel.Size = fm.Size;
                ArcWrapper.CopyMemory(faceModel.PFeature, fm.PFeature, fm.Size);
            }

            return faceModel;
        }

        //添加人脸
        public bool AddFace(string id, Bitmap bitmap)
        {
            try
            {
                var faceModel = ExtractFeature(bitmap);
                _FaceLib.Items.Add(new FaceLib.Item()
                {
                    OrderId = DateTime.Now.Ticks,
                    ID = id,
                    FaceModel = faceModel
                });
                var featureData = new byte[faceModel.Size];
                Marshal.Copy(faceModel.PFeature, featureData, 0, faceModel.Size);
                SaveFile(id, bitmap, featureData);
            }
            catch
            {
                return false;
            }
            return true;
        }

        //保存图片与特征文件
        private void SaveFile(string id, Bitmap bitmap, byte[] featureData)
        {
            Image img = bitmap;
            var fileName = Path.Combine(_FaceDataPath, id + ".dat");
            System.IO.File.WriteAllBytes(fileName, featureData);
            fileName = Path.Combine(_FaceImagePath, id + ".bmp");
            img.Save(fileName);
        }

        //检查用户是否存在
        public bool CheckID(string id)
        {
            var count = _FaceLib.Items.Count(ii => ii.ID == id);
            return count > 0;
        }

        //人脸比对
        public FaceMatchResults Match(Bitmap bitmap, float degree = 0.90f, int num = 1)
        {
            var faceMatchResults = new FaceMatchResults()
            {
                Items = new List<FaceMatchResult>()
            };
            var faceModel = ExtractFeature(bitmap);
            foreach (var item in _FaceLib.Items.OrderByDescending(ii => ii.OrderId))
            {
                ArcWrapper.Match(_REnginer, ref faceModel, ref item.FaceModel, out float score);
                if (score > degree)
                {
                    faceMatchResults.Items.Add(new FaceMatchResult
                    {
                        ID = item.ID,
                        Score = score
                    });
                    if (faceMatchResults.Items.Count >= num)
                    {
                        return faceMatchResults;
                    }
                }
            }
            return faceMatchResults;
        }
    }
}
}
