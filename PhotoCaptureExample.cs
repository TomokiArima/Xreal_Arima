/****************************************************************************
* Copyright 2019 Xreal Techonology Limited. All rights reserved.
*                                                                                                                                                          
* This file is part of NRSDK.                                                                                                          
*                                                                                                                                                           
* https://www.xreal.com/        
* 
*****************************************************************************/

using NRKernal.Record;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Collections;            //追加
using System.Collections.Generic;   //追加
using NRKernal;                     //追加
using System.Diagnostics;           //追加

namespace NRKernal.NRExamples
{
#if UNITY_ANDROID && !UNITY_EDITOR
    using GalleryDataProvider = NativeGalleryDataProvider;
#else
    using GalleryDataProvider = MockGalleryDataProvider;
#endif

    /// <summary> A photo capture example. </summary>
    [HelpURL("https://developer.xreal.com/develop/unity/video-capture")]
    public class PhotoCaptureExample : MonoBehaviour
    {
        /// <summary> The photo capture object. </summary>
        private NRPhotoCapture m_PhotoCaptureObject;
        /// <summary> The camera resolution. </summary>
        private Resolution m_CameraResolution;
        private bool isOnPhotoProcess = false;
        GalleryDataProvider galleryDataTool;

        private float Point_count_L;        //追加
        private float Point_count_R;        //追加
        private bool takeflag_L;            //追加
        private bool takeflag_R;            //追加

        public HandEnum handEnum;
        private Vector3 IndexPositionR;    //オブジェクトの出す位置として人差し指の位置の情報を保管。
        private Quaternion IndexRotationR;  //オブジェクトの出す方向として手の位置のローテーションを保管。
        public GameObject SpawnItem;        //指先にスポーンさせるオブジェクトを指定

        public int AddX=0;    //角度調整用
        public int AddY=0;
        public int AddZ=0;



        void Update()
        {
            // 手のトラッキングが実行中かどうかを確認
            if (NRInput.Hands.IsRunning)        //追加
            {
                // 左手の状態を取得
                HandState handStateL = NRInput.Hands.GetHandState(HandEnum.LeftHand);
                HandState handStateR = NRInput.Hands.GetHandState(HandEnum.RightHand);

                // 左手でポイントジェスチャーが行われているかをチェック
                if (handStateL.isTracked && handStateL.isPoint)
                {
                    if (takeflag_L == false)
                    {
                        if (Point_count_L <= 50)       //連続で撮影されることを防ぐ
                        {
                            Point_count_L++;
                        }
                        else
                        {
                            CapturePhoto();
                        }
                    }
                }
                else    //ポイントジェスチャーがとられていない
                {
                    takeflag_L = false;
                    Point_count_L = 0;
                }

                // 右手でポイントジェスチャーが行われているかをチェック
                if (handStateR.isTracked && handStateR.isPoint)
                {
                    if (takeflag_R == false)
                    {
                        IndexPositionR = handStateR.GetJointPose(HandJointID.IndexTip).position;
                        IndexRotationR = handStateR.GetJointPose(HandJointID.IndexTip).rotation;

                        //オブジェクトの向きをAddX,Y,Zだけ調整する。
                             // X軸回りにAddX°の回転を作成
                            Quaternion xRotation = Quaternion.Euler(AddX, 0, 0);
                             // Y軸回りにAddY°の回転を作成
                            Quaternion yRotation = Quaternion.Euler(0, AddY, 0);
                            // Z軸回りにAddZ°の回転を作成
                            Quaternion zRotation = Quaternion.Euler(0, 0, AddZ);
                            // 各軸周りの回転を合成
                            Quaternion rotationAdjustment =xRotation * yRotation * zRotation;
                            // 元の回転に加算
                            IndexRotationR = IndexRotationR * rotationAdjustment;

                        SpawnModel();
                        
                    }
                }
            }
        }

        private void CapturePhoto()         //追加
        {
            takeflag_L = true;
            UnityEngine.Debug.Log("写真をとります");
            TakeAPhoto();
        }

        private void SpawnModel()           //人差し指の位置(IndexPositionR)と向き(IndexRotationR)に任意のオブジェクト(SpawnItem)を移動
        {
            //UnityEngine.Debug.Log(IndexPositionR);
            SpawnItem.transform.position = IndexPositionR;
            SpawnItem.transform.rotation = IndexRotationR;
        }
        


        /// <summary> Use this for initialization. </summary>
        void Create(Action<NRPhotoCapture> onCreated)
        {
            if (m_PhotoCaptureObject != null)
            {
                NRDebugger.Info("The NRPhotoCapture has already been created.");
                return;
            }

            // Create a PhotoCapture object
            NRPhotoCapture.CreateAsync(false, delegate (NRPhotoCapture captureObject)
            {
                m_CameraResolution = NRPhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();

                if (captureObject == null)
                {
                    NRDebugger.Error("Can not get a captureObject.");
                    return;
                }

                m_PhotoCaptureObject = captureObject;

                CameraParameters cameraParameters = new CameraParameters();
                cameraParameters.cameraResolutionWidth = m_CameraResolution.width;
                cameraParameters.cameraResolutionHeight = m_CameraResolution.height;
                cameraParameters.pixelFormat = CapturePixelFormat.PNG;
                cameraParameters.frameRate = NativeConstants.RECORD_FPS_DEFAULT;
                cameraParameters.blendMode = BlendMode.Blend;

                // Activate the camera
                m_PhotoCaptureObject.StartPhotoModeAsync(cameraParameters, delegate (NRPhotoCapture.PhotoCaptureResult result)
                {
                    NRDebugger.Info("Start PhotoMode Async");
                    if (result.success)
                    {
                        onCreated?.Invoke(m_PhotoCaptureObject);
                    }
                    else
                    {
                        isOnPhotoProcess = false;
                        this.Close();
                        NRDebugger.Error("Start PhotoMode faild." + result.resultType);
                    }
                }, true);
            });
        }

        /// <summary> Take a photo. </summary>
        void TakeAPhoto()
        {
            if (isOnPhotoProcess)
            {
                NRDebugger.Warning("Currently in the process of taking pictures, Can not take photo .");
                return;
            }

            isOnPhotoProcess = true;
            if (m_PhotoCaptureObject == null)
            {
                this.Create((capture) =>
                {
                    capture.TakePhotoAsync(OnCapturedPhotoToMemory);
                });
            }
            else
            {
                m_PhotoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
            }
        }

        /// <summary> Executes the 'captured photo memory' action. </summary>
        /// <param name="result">            The result.</param>
        /// <param name="photoCaptureFrame"> The photo capture frame.</param>
        void OnCapturedPhotoToMemory(NRPhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
        {
            var targetTexture = new Texture2D(m_CameraResolution.width, m_CameraResolution.height);
            // Copy the raw image data into our target texture
            photoCaptureFrame.UploadImageDataToTexture(targetTexture);

            // Create a gameobject that we can apply our texture to
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Renderer quadRenderer = quad.GetComponent<Renderer>() as Renderer;
            quadRenderer.material = new Material(Resources.Load<Shader>("Record/Shaders/CaptureScreen"));

            var headTran = NRSessionManager.Instance.NRHMDPoseTracker.centerAnchor;
            quad.name = "picture";
            quad.transform.localPosition = headTran.position + headTran.forward * 3f;
            quad.transform.forward = headTran.forward;
            quad.transform.localScale = new Vector3(1.6f, 0.9f, 0);
            quadRenderer.material.SetTexture("_MainTex", targetTexture);
            SaveTextureAsPNG(photoCaptureFrame);

            SaveTextureToGallery(photoCaptureFrame);
            // Release camera resource after capture the photo.
            this.Close();
        }

        void SaveTextureAsPNG(PhotoCaptureFrame photoCaptureFrame)
        {
            if (photoCaptureFrame.TextureData == null)
                return;
            try
            {
                string filename = string.Format("Xreal_Shot_{0}.png", NRTools.GetTimeStamp().ToString());
                string path = string.Format("{0}/XrealShots", Application.persistentDataPath);
                string filePath = string.Format("{0}/{1}", path, filename);

                byte[] _bytes = photoCaptureFrame.TextureData;
                NRDebugger.Info("Photo capture: {0}Kb was saved to [{1}]",  _bytes.Length / 1024, filePath);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                File.WriteAllBytes(string.Format("{0}/{1}", path, filename), _bytes);

            }
            catch (Exception e)
            {
                NRDebugger.Error("Save picture faild!");
                throw e;
            }
        }

        /// <summary> Closes this object. </summary>
        void Close()
        {
            if (m_PhotoCaptureObject == null)
            {
                NRDebugger.Error("The NRPhotoCapture has not been created.");
                return;
            }
            // Deactivate our camera
            m_PhotoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
        }

        /// <summary> Executes the 'stopped photo mode' action. </summary>
        /// <param name="result"> The result.</param>
        void OnStoppedPhotoMode(NRPhotoCapture.PhotoCaptureResult result)
        {
            // Shutdown our photo capture resource
            m_PhotoCaptureObject?.Dispose();
            m_PhotoCaptureObject = null;
            isOnPhotoProcess = false;
        }

        /// <summary> Executes the 'destroy' action. </summary>
        void OnDestroy()
        {
            // Shutdown our photo capture resource
            m_PhotoCaptureObject?.Dispose();
            m_PhotoCaptureObject = null;
        }

        public void SaveTextureToGallery(PhotoCaptureFrame photoCaptureFrame)
        {
            if (photoCaptureFrame.TextureData == null)
                return;
            try
            {
                string filename = string.Format("Xreal_Shot_{0}.png", NRTools.GetTimeStamp().ToString());
                byte[] _bytes = photoCaptureFrame.TextureData;
                NRDebugger.Info(_bytes.Length / 1024 + "Kb was saved as: " + filename);
                if (galleryDataTool == null)
                {
                    galleryDataTool = new GalleryDataProvider();
                }

                galleryDataTool.InsertImage(_bytes, filename, "Screenshots");
            }
            catch (Exception e)
            {
                NRDebugger.Error("[TakePicture] Save picture faild!");
                throw e;
            }
        }
    }
}
