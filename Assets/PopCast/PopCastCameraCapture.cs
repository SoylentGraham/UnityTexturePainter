using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PopCastCameraCapture : MonoBehaviour
{

	public PopCastParams CastParams;
	public PopCast mCast = null;
	public string mOutputTarget = "file:StreamingAssets/*.gif";
	public Camera mCaptureCamera;
	public RenderTexture mCaptureCameraTexture;
	public bool EnableDebug = true;

	void OnEnable()
	{
		if (mCast == null)
		{
			mCast = new PopCast(mOutputTarget, CastParams);
			if (EnableDebug)
				PopCast.EnableDebugLog = EnableDebug;
		}
	}

	void OnDisable()
	{
		if ( mCast != null )
		{
			mCast.Free();
			mCast = null;
		}
	}

	void LateUpdate()
	{
		//	capture camera
		if (mCaptureCamera != null)
		{

			//	if no texture provided, make one
			if (mCaptureCameraTexture == null)
				mCaptureCameraTexture = new RenderTexture(mCaptureCamera.pixelWidth, mCaptureCamera.pixelHeight, 16);

			//	save settings to restore
			RenderTexture PreTarget = RenderTexture.active;
			var PreCameraTarget = mCaptureCamera.targetTexture;

			mCaptureCamera.targetTexture = mCaptureCameraTexture;
			RenderTexture.active = mCaptureCameraTexture;
			mCaptureCamera.Render();

			//	aaaand restore.
			RenderTexture.active = PreTarget;
			mCaptureCamera.targetTexture = PreCameraTarget;
		}

	}

	void Update()
	{

		//	write latest stream data
		if (mCast != null)
		{
			//	check where to output the camera, if we haven't already
			bool CameraCaptureOutput = false;

			//	if we want to write the capture texture, and we havent... do it now
			if (!CameraCaptureOutput && mCaptureCameraTexture != null)
			{
				mCast.UpdateTexture(mCaptureCameraTexture, 0);
			}
		}

	}
}
