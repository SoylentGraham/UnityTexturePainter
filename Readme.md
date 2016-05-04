UnityTexturePainter
-----------------

Simple example of painting straight onto a mesh with a shader.

Video (some genius used the first empty frame as the thumbnail)
[![Video on twitter](https://pbs.twimg.com/ext_tw_video_thumb/727952560884191236/pu/img/2gOu6uKw8QCZNFMv.jpg)](https://twitter.com/soylentgraham/status/727953981616574465)

+ Attach `PaintMe.cs` to any mesh with a `Collider`
+ When the user clicks, a raycast will try and find any object with `PaintAt()` on it
+ When this is called we grab the UV's where we hit (note: some untextured objects will only have `0,0` for a uv!) and start painting
+ The first time, a `RenderTexture` is setup, copies the texture from the material on us (or clears a colour if no texture) and assigns itself to the material
+ then (and on subsequent hits) it will set a point to draw a circle at on a shader, then do a blit onto the existing texture

Not the MOST effecient way, but a thousand times better than using SetPixel. And now you can do shapes, masks, gradients, wobbly effects.

Enjoy!
