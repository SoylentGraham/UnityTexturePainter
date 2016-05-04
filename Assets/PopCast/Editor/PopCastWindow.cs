using UnityEngine;
using System.Collections;
using UnityEditor;



public class PopCastWindow : EditorWindow
{
	[Header("Use full path, or StreamingAssets/ or DataPath/ Use a filename or *.gif to timestamp filename")]
	public string Filename = "file:StreamingAssets/*.gif";

	public PopCastParams Parameters;

	[Header("If no camera specified, the main camera will be recorded")]
	public Camera RecordCamera = null;

	[Header("If no target texture set, one will be created")]
	public RenderTexture RecordToTexture = null;

	[Header("Push fake textures for debugging")]
	public bool PushFakeTextures = false;

	private PopCast mPopCast;

	[MenuItem("PopCast/Editor recorder")]

	public static void ShowWindow()
	{
		EditorWindow.GetWindow(typeof(PopCastWindow));
	}

	void OnGUI()
	{
		if (mPopCast != null)
		{
			if (GUILayout.Button("Stop recording"))
			{
				StopRecording();
			}
		}
		else {
			if (GUILayout.Button("Start recording"))
			{
				StartRecording();
			}
		}

		//	reflect built in properties
		var ThisEditor = Editor.CreateEditor(this);
		PopCastInspector.ApplyInspector(mPopCast, this, ThisEditor);

		ThisEditor.OnInspectorGUI();
	}

	void StopRecording()
	{
		mPopCast = null;
		System.GC.Collect();
	}

	void StartRecording()
	{
		if (RecordCamera == null)
		{
			RecordCamera = Camera.allCameras[0];
			Debug.Log(Camera.allCameras);
		}

		//	if no texture provided, make one
		if (RecordToTexture == null)
			RecordToTexture = new RenderTexture(RecordCamera.pixelWidth, RecordCamera.pixelHeight, 16);

		mPopCast = new PopCast(Filename, Parameters);
		PopCast.EnableDebugLog = true;

	}

	void LateUpdate()
	{
		//Debug.Log ("Popcast window LateUpdate");
		//	was in lateupdate
		//	update recording
		if (mPopCast != null && !PushFakeTextures)
		{
			//	save settings to restore
			RenderTexture PreTarget = RenderTexture.active;
			var PreCameraTarget = RecordCamera.targetTexture;

			RecordCamera.targetTexture = RecordToTexture;
			RenderTexture.active = RecordToTexture;
			RecordCamera.Render();

			//	aaaand restore.
			RenderTexture.active = PreTarget;
			RecordCamera.targetTexture = PreCameraTarget;
		}

	}

	void Update()
	{
		//Debug.Log ("Popcast window update");
		//	update texture
		LateUpdate();

		//	write latest stream data
		if (mPopCast != null)
		{
			if (PushFakeTextures)
				mPopCast.UpdateFakeTexture(0);
			else
				mPopCast.UpdateTexture(RecordToTexture, 0);
		}


	}
}
