﻿using System;
using System.Runtime.InteropServices;
using ImGui.Common.Primitive;

namespace ImGui
{
    /// <summary>
    /// Texture-related functions
    /// </summary>
    public interface ITexture : IDisposable
    {
        /// <summary>
        /// Load image data from byte array into the texture.
        /// </summary>
        /// <returns>succeeded?true:false</returns>
        void LoadImage(byte[] data);

        /// <summary>
        /// Load image data from a file into the texture.
        /// </summary>
        /// <returns>succeeded?true:false</returns>
        void LoadImage(string filePath);

        /// <summary>
        /// Width of the texture in pixels. (Read Only)
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Height of the texture in pixels. (Read Only)
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Size of the texture. (Read Only)
        /// </summary>
        Size Size { get; }

        /// <summary>
        /// Retrieve a native (underlying graphics API) pointer to the texture resource.
        /// </summary>
        /// <returns>
        /// e.g. The id of the OpenGL texture object, converted to an `IntPtr`.
        /// </returns>
        IntPtr GetNativeTexturePtr();

        /// <summary>
        /// Retrieve an graphics-API-specific id of the texture resource.
        /// </summary>
        /// <returns>
        /// e.g. The id of the OpenGL texture object.
        /// </returns>
        int GetNativeTextureId();
    }
}