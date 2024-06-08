# UGUI Vertex Effect

Adds effects to UGUI graphics in a hierarchial way, e.g. A Bend effect on a top level canvas affects all children.
All effects operate on the geometry rather than the shader which allows it to be used in conjuntion with any UI shader, for example [Unity-UI-Rounded-Corners](https://github.com/kirevdokimov/Unity-UI-Rounded-Corners) and [UIEffect](https://github.com/mob-sakai/UIEffect).

## Performance

Though a good chunk of time has been devoted to performance, there are some limiting factors, it leverages UGUI's BaseMeshEffect which is CPU and non multithreaded. So the code is setup to be as fast as I can make it on a single thread. It's pretty fast but it scales with the number of Graphics.

## Effects

<b>Bend</b> - Curves UI around a cylinder<br>

https://github.com/markeahogan/uguivertexeffect-dev/assets/6376138/5413d397-e2c8-450e-98a8-5f87867e7fb9

<b>Feathered Edge</b> - Similar to RectMask2D but supporting rotation<br>

https://github.com/markeahogan/uguivertexeffect-dev/assets/6376138/513439dd-3957-489a-8d46-cad04fb7da1a

<b>Bezier Patch</b> - Similar to Photoshop's Warp<br>

https://github.com/markeahogan/uguivertexeffect-dev/assets/6376138/fb007000-2f36-4c1a-a7e9-7e7a0f993227


<b>Gradient</b> - A gradient across the graphic (great in conjuntion with [GradientColorSpace](https://github.com/markeahogan/GradientColorSpace))<br>

https://github.com/markeahogan/uguivertexeffect-dev/assets/6376138/b0be334f-9d4e-4f27-9750-0c31cfc2c942


<b>Nine Slice</b> - Turns anything into a nine sliced sprite, useful for SVGs<br>

https://github.com/markeahogan/uguivertexeffect-dev/assets/6376138/85d23e4d-56f8-4017-b4b2-8628476216d6



## Raycasting

Effects such as Bend and Bezier Warp move the Graphic's visual away from it's RectTransform but it's clickable area doesn't change. 
To counter this the current strategy is to get the event camera's position relative to the Graphic's visual, then counter position the event camera relative to the Graphic's Rect.

## Known Issues

- RectMask2D's shader clipping expects a rect, bend can move verts outside of the rect, use Mask as a workaround
- Raycasting is still against a Rect, so is an approximation, very distorted UI will not be raycasted accurately
- Sliders act like theyre in their unmodified position
- If the event ray doesnt intersect the plane of the Graphic's Rect the hit is not detected. i.e. With a canvas bent 360 with the player at the center, the graphics behind the player will not recieve events
