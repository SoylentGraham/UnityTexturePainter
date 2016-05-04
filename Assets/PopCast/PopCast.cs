using UnityEngine;
using System.Collections;					// required for Coroutines
using System.Runtime.InteropServices;		// required for DllImport
using System;                               // requred for IntPtr


[Serializable]
public class PopCastMeta
{
	public int BackgroundGpuJobCount;
	public int InstanceCount;
	public int MuxerInputQueueCount;
	public int MuxerDefferedQueueCount;
	public int BytesWritten;
	public int PendingWrites;
	public int PendingEncodedFrames;
	public int PushedFrameCount;
	public int PendingFrameCount;
};



[Serializable]
public class  PopCastParams
{
    [Tooltip("if we record to a local file, pop up explorer/finder with the file when its ready")]
    public bool ShowFinishedFile = false;

    [Tooltip("Record smooth video, or skip for live casting")]
    public bool SkipFrames = false;

	[Tooltip("Debug writer by drawing plain blocks of colour")]
	public bool PushDebugFrames = false;
	

	[Header("Mpeg/video Parameters")]

	[Tooltip("Bitrate for encoder in mb/s. 32 seems to be the h264 limit on windows")]
	[Range(0, 32)]
	public float BitRateMegaBytesPerSec = 32.0f;

	
	[Header("Gif Parameters")]

	[Tooltip("Use transparency between frames to reduce file size")]
    public bool Gif_AllowIntraFrames = true;

    [Tooltip("Make a debug palette")]
    public bool Gif_DebugPalette = false;

    [Tooltip("Render the pallete top to bottom")]
    public bool Gif_DebugIndexes = false;

    [Tooltip("Highlight transparent regions")]
    public bool Gif_DebugTransparency = false;

    [Tooltip("Don't use any GPU for conversion, encoding etc")]
    public bool Gif_CPUOnly = false;

	[Tooltip("Debug gif LZW compression by turning this off")]
	public bool Gif_LzwCompression = true;

}


public class PopCast
{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
	private const string PluginName = "PopCastOsx";
#elif UNITY_ANDROID
	private const string PluginName = "PopCast";
#elif UNITY_IOS
	//private const string PluginName = "PopCastIos";
	private const string PluginName = "__Internal";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
	private const string PluginName = "PopCast";
#endif

	//	gr: after a huge amount of headaches... we're passing a bitfield for params. NOT a struct
	private enum PopCastFlags
	{
		None						= 0,
		ShowFinishedFile			= 1<<0,
		Gif_AllowIntraFrames		= 1<<1,
		Gif_DebugPalette			= 1<<2,
		Gif_DebugIndexes			= 1<<3,
		Gif_DebugTransparency		= 1<<4,
        SkipFrames                  = 1<<5,
		Gif_CpuOnly					= 1<<6,
		Gif_LzwCompression			= 1<<7,
	};

	private uint		mInstance = 0;
	private static int	mPluginEventId = PopCast_GetPluginEventId();
    private bool		mPushDebugFrames = false;
#if UNITY_EDITOR
	public static bool mAllowBackgroundProcessing = true;
#endif

	//	cache the texture ptr's. Unity docs say accessing them causes a GPU sync, I don't believe they do, BUT we want to avoid setting the active render texture anyway
	private PopTexturePtrCache<Texture2D>		mTexture2DPtrCache = new PopTexturePtrCache<Texture2D>();
	private PopTexturePtrCache<RenderTexture>	mRenderTexturePtrCache = new PopTexturePtrCache<RenderTexture>();

	//	add your own callbacks here to catch debug output (eg. to a GUI)
	public static System.Action<string> DebugCallback;
	public static bool EnableDebugLog
	{
		get
		{
			return DebugCallbackWrapper != null;
		}
		set
		{
			if (value == false)
			{
				DebugCallback -= DebugCallbackWrapper;
				DebugCallbackWrapper = null;
			}
			else {
				if (DebugCallbackWrapper == null)
				{
					DebugCallbackWrapper = (string Message) => {
						Debug.Log(Message);
					};
					DebugCallback += DebugCallbackWrapper;
				}
			}
		}

	}

	//	internal wrapper to allow global toggling via inspector - this is essentially our "is enabled" bool
	private static System.Action<string> DebugCallbackWrapper;
	

	[DllImport (PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern uint		PopCast_Alloc(String Filename,uint Params,float RateMegaBytesPerSec);
	
	[DllImport (PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool		PopCast_Free(uint Instance);

	[DllImport (PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void		PopCast_EnumDevices();

	[DllImport (PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int		PopCast_GetPluginEventId();
	
	[DllImport (PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool		PopCast_UpdateTexture2D(uint Instance,System.IntPtr TextureId,int Width,int Height,TextureFormat textureFormat,int StreamIndex);

	[DllImport (PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool		PopCast_UpdateRenderTexture(uint Instance,System.IntPtr TextureId,int Width,int Height,RenderTextureFormat textureFormat,int StreamIndex);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool		PopCast_UpdateTextureDebug(uint Instance,int StreamIndex);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern uint		PopCast_GetBackgroundGpuJobCount();

	[DllImport(PluginName,CallingConvention = CallingConvention.Cdecl)]
	private static extern System.IntPtr	PopCast_GetMetaJson(uint Instance);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void		PopCast_ReleaseString(System.IntPtr Str);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int		PopCast_GetPendingFrameCount(uint Instance);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern System.IntPtr PopCast_PopDebugString();

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void		PopCast_ReleaseDebugString(System.IntPtr String);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	public static extern void		PopCast_ReleaseAllExports();



	public PopCast(string Filename,PopCastParams Params)
	{
		mPushDebugFrames = Params.PushDebugFrames;

		PopCastFlags ParamFlags = 0;
		ParamFlags |= Params.ShowFinishedFile			? PopCastFlags.ShowFinishedFile : PopCastFlags.None;
		ParamFlags |= Params.Gif_AllowIntraFrames		? PopCastFlags.Gif_AllowIntraFrames : PopCastFlags.None;
		ParamFlags |= Params.Gif_DebugPalette			? PopCastFlags.Gif_DebugPalette : PopCastFlags.None;
		ParamFlags |= Params.Gif_DebugIndexes			? PopCastFlags.Gif_DebugIndexes : PopCastFlags.None;
		ParamFlags |= Params.Gif_DebugTransparency		? PopCastFlags.Gif_DebugTransparency : PopCastFlags.None;
		ParamFlags |= Params.SkipFrames					? PopCastFlags.SkipFrames : PopCastFlags.None;
        ParamFlags |= Params.Gif_CPUOnly                ? PopCastFlags.Gif_CpuOnly : PopCastFlags.None;
		ParamFlags |= Params.Gif_LzwCompression			? PopCastFlags.Gif_LzwCompression : PopCastFlags.None;

		uint ParamFlags32 = Convert.ToUInt32 (ParamFlags);

		Filename = ResolveFilename (Filename);
		Debug.LogWarning ("resolved filename: " + Filename);
		mInstance = PopCast_Alloc ( Filename, ParamFlags32, Params.BitRateMegaBytesPerSec);

		//	if this fails, capture the flush and throw an exception
		if (mInstance == 0) {
			string AllocError = "";
			FlushDebug (
				(string Error) => {
				AllocError += Error; }
			);
			if ( AllocError.Length == 0 )
				AllocError = "No error detected";
			throw new System.Exception("PopCast failed: " + AllocError);
		}
	}
	
	~PopCast()
	{
		Free();
	}

	public void Free()
	{
		PopCast_Free(mInstance);
		FlushDebug();
	}

	//	returns negative on error (already free?)
	public int GetPendingFrameCount()
	{
		var Pending = PopCast_GetPendingFrameCount( mInstance );
		return Pending;
	}

	public static void EnumDevices()
	{
		PopCast_EnumDevices();
	}

	public static uint GetBackgroundGpuJobCount()
	{
		return PopCast_GetBackgroundGpuJobCount();
	}

	public bool IsAllocated()
	{
		return mInstance != 0;
	}

	void FlushDebug()
	{
		FlushDebug(DebugCallback);
	}

	/// <summary>Flush all debug text to a callback function
	///	</summary>
	public static void FlushDebug(System.Action<string> Callback)
	{
		int MaxFlushPerFrame = 100;
		int i = 0;
		while (i++ < MaxFlushPerFrame)
		{
			System.IntPtr StringPtr = PopCast_PopDebugString();

			//	no more strings to pop
			if (StringPtr == System.IntPtr.Zero)
				break;

			try
			{
				string Str = Marshal.PtrToStringAnsi(StringPtr);
				if (Callback != null)
					Callback.Invoke(Str);
				PopCast_ReleaseDebugString(StringPtr);
			}
			catch
			{
				PopCast_ReleaseDebugString(StringPtr);
				throw;
			}
		}
	}

	public void UpdateTexture(Texture Target,int StreamIndex)
	{
		Update ();
		FlushDebug();

		if (mPushDebugFrames )
		{
			UpdateFakeTexture(StreamIndex);
			return;
		}

		if (Target is RenderTexture) {
			RenderTexture Target_rt = Target as RenderTexture;
			PopCast_UpdateRenderTexture (mInstance, PopTexturePtrCache.GetCache (ref mRenderTexturePtrCache, Target_rt), Target.width, Target.height, Target_rt.format, StreamIndex);
		}
		if (Target is Texture2D) {
			Texture2D Target_2d = Target as Texture2D;
			PopCast_UpdateTexture2D (mInstance, PopTexturePtrCache.GetCache (ref mTexture2DPtrCache, Target_2d), Target.width, Target.height, Target_2d.format, StreamIndex);
		}
		FlushDebug ();
	}

	public void UpdateFakeTexture(int StreamIndex)
	{
		Update();
		FlushDebug();
		PopCast_UpdateTextureDebug(mInstance, StreamIndex);
		FlushDebug();
	}

	public static void Update()
	{
		GL.IssuePluginEvent (mPluginEventId);
	}

	static public string GetVersion()
	{
		return "GIT_REVISION";
	}


	public static string ResolveFilename(string Filename)
	{
		//	if the filename has a protocol, don't change it
		//	file:		Export to a file. if there isn't an explicit path found in the filename (eg. c:/gifs from c:/gifs/cast.gif) the persistent path will be pre-pended. so file:cast.gif turns into <persistent-path>/cast.gif
		
		//	resolve local filenames automatically regardless of prefix
		string Prefix = null;
		char[] Delin = ":".ToCharArray ();
		string[] Parts = Filename.Split (Delin, 2);
		if (Parts.Length > 1) {
			Prefix = Parts [0] + ":";
			Filename = Parts [1];
		}

		string StreamingAssetsPrefix_A = "StreamingAssets" + System.IO.Path.DirectorySeparatorChar;
		string StreamingAssetsPrefix_B = "StreamingAssets" + System.IO.Path.AltDirectorySeparatorChar;

		//	gr: being explicit with files atm
		if ( Prefix == "file:" )
		{
			//	if not a full path, put it in persistent data
			if ( !System.IO.Path.IsPathRooted(Filename) )
			{
				if ( Filename.StartsWith(StreamingAssetsPrefix_A,StringComparison.CurrentCultureIgnoreCase) )
				{
					Filename = Filename.Remove(0,StreamingAssetsPrefix_A.Length);
					Filename = System.IO.Path.Combine (Application.streamingAssetsPath, Filename);
				}
				else if ( Filename.StartsWith(StreamingAssetsPrefix_B,StringComparison.CurrentCultureIgnoreCase) )
				{
					Filename = Filename.Remove(0,StreamingAssetsPrefix_B.Length);
					Filename = System.IO.Path.Combine (Application.streamingAssetsPath, Filename);
				}
				else
				{
					Filename = System.IO.Path.Combine (Application.persistentDataPath, Filename);
				}

			}

			//	if there is a * in the filename, insert time. That allows file:*.gif or file:streamingassets/*.gif
			string DateString = System.DateTime.Now.ToShortDateString() + "_" + System.DateTime.Now.ToShortTimeString();
			DateString = DateString.Replace(System.IO.Path.DirectorySeparatorChar, '_');
			DateString = DateString.Replace(System.IO.Path.AltDirectorySeparatorChar, '_');
			DateString = DateString.Replace(' ', '_');
			DateString = DateString.Replace(':', '_');
			Filename = Filename.Replace("*", DateString);
		}

		if (Prefix != null)
			Filename = Prefix + Filename;
		
		return Filename;
	}


	public PopCastMeta GetMeta()
	{
		var JsonPtr = PopCast_GetMetaJson(mInstance);
		if (JsonPtr == System.IntPtr.Zero)
		{
			Debug.LogError("PopCast_GetMetaJson returned null");
			return new PopCastMeta();
		}
		try
		{
			var Json = Marshal.PtrToStringAnsi(JsonPtr);
			PopCastMeta Meta = JsonUtility.FromJson<PopCastMeta>(Json);
			PopCast_ReleaseString(JsonPtr);
			return Meta;
		}
		catch (System.Exception e)
		{
			Debug.LogError("Error getting PopCast Meta JSON: " + e.Message);
			PopCast_ReleaseString(JsonPtr);
			return new PopCastMeta();
		}
	}

}

