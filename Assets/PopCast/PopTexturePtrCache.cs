/*

	Unity causes large stalls when reading the GetNativePtr from textures as it requires a block to the thread.
	Not cool, so caching it ourselves

*/
using UnityEngine;
using System.Collections;					// required for Coroutines
using System.Runtime.InteropServices;		// required for DllImport
using System;								// requred for IntPtr
using System.Text;
using System.Collections.Generic;


public class PopTexturePtrCache<TEXTURE> where TEXTURE : Texture
{
	public TEXTURE			mTexture;
	public System.IntPtr	mPtr;
};

public class PopTexturePtrCache
{
	static public System.IntPtr GetCache<T>(ref PopTexturePtrCache<T> Cache,T texture) where T : Texture
	{
		if (Cache==null)
			return texture.GetNativeTexturePtr();

		if ( texture.Equals(Cache.mTexture) )
			return Cache.mPtr;
		Cache.mPtr = texture.GetNativeTexturePtr();
		if ( Cache.mPtr != System.IntPtr.Zero )
			Cache.mTexture = texture;
		return Cache.mPtr;
	}

	static public System.IntPtr GetCache(ref PopTexturePtrCache<RenderTexture> Cache,RenderTexture texture)
	{
		if (Cache==null)
			return texture.GetNativeTexturePtr();

		if ( texture.Equals(Cache.mTexture) )
			return Cache.mPtr;
		var Prev = RenderTexture.active;
		RenderTexture.active = texture;
		Cache.mPtr = texture.GetNativeTexturePtr();
		RenderTexture.active = Prev;
		if ( Cache.mPtr != System.IntPtr.Zero )
			Cache.mTexture = texture;
		return Cache.mPtr;
	}
};
