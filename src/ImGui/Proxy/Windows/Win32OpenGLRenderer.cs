﻿using System;
using CSharpGL;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGui.Common.Primitive;

namespace ImGui
{
    internal partial class Win32OpenGLRenderer : IRenderer
    {

        OpenGLMaterial m = new OpenGLMaterial(
            vertexShader: @"
#version 330
uniform mat4 ProjMtx;
in vec2 Position;
in vec2 UV;
in vec4 Color;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
	Frag_UV = UV;
	Frag_Color = Color;
	gl_Position = ProjMtx * vec4(Position.xy,0,1);
}
",
            fragmentShader: @"
#version 330
uniform sampler2D Texture;
in vec2 Frag_UV;
in vec4 Frag_Color;
out vec4 Out_Color;
void main()
{
	Out_Color = Frag_Color;
}
"
            );

        OpenGLMaterial mImage = new OpenGLMaterial(
            vertexShader: @"
#version 330
uniform mat4 ProjMtx;
in vec2 Position;
in vec2 UV;
in vec4 Color;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
	Frag_UV = UV;
	Frag_Color = Color;
	gl_Position = ProjMtx * vec4(Position.xy,0,1);
}
",
            fragmentShader: @"
#version 330
uniform sampler2D Texture;
in vec2 Frag_UV;
in vec4 Frag_Color;
out vec4 Out_Color;
void main()
{
	Out_Color = Frag_Color * texture( Texture, Frag_UV.st);
}
"
            );

        OpenGLMaterial GlyphMaterial = new OpenGLMaterial(
    vertexShader: @"
#version 330
uniform mat4 ProjMtx;
in vec2 Position;
in vec2 UV;
in vec4 Color;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
	Frag_UV = UV;
	Frag_Color = Color;
	gl_Position = ProjMtx * vec4(Position.xy,0,1);
}
",
    fragmentShader: @"
#version 330
in vec2 Frag_UV;
in vec4 Frag_Color;
out vec4 Out_Color;
void main()
{
	if (Frag_UV.s * Frag_UV.s - Frag_UV.t > 0.0)
	{
		discard;
	}
	Out_Color = Frag_Color;
}
"
    );
        OpenGLMaterial TextMaterial = new OpenGLMaterial(
    vertexShader: @"
#version 330
uniform mat4 ProjMtx;
in vec2 Position;
in vec2 UV;
in vec4 Color;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
	Frag_UV = UV;
	Frag_Color = Color;
	gl_Position = ProjMtx * vec4(Position.xy,0,1);
}
",
    fragmentShader: @"
#version 330
uniform sampler2D Texture;
in vec2 Frag_UV;
in vec4 Frag_Color;
out vec4 Out_Color;
void main()
{
	vec4 color = texture(Texture, Frag_UV.st);
	int m = int(mod(color.r*255, 2));
	if(m == 1)
	{
		Out_Color = vec4(0,0,0,1);
	}
	else
	{
		discard;
	}
}
"
// MSAA fragment shader, should be used together with multi-sampled framebuffer
/*
    #version 330
    #extension GL_ARB_texture_multisample : enable
    uniform sampler2DMS Texture;
    in vec2 Frag_UV;
    in vec4 Frag_Color;
    out vec4 Out_Color;

    int samples = 4;
    float div = 1.0/samples;

    void main()
    {
        int count = 0;
        ivec2 texcoord = ivec2(textureSize(Texture) * Frag_UV); // used to fetch msaa texel location
        for (int i=0;i<samples;i++)
        {
            float r = texelFetch(Texture, texcoord, i).r;
            int m = int(mod(r*255, 2));
            if(m == 1)
            {
                count = count + 1;
            }
        }

        if(count == 0)
        {
            discard;
        }

        Out_Color = vec4(0,0,0,count * div);
    }
*/
    );
        //Helper for some GL functions
        private static readonly int[] IntBuffer = { 0, 0, 0, 0 };
        private static readonly float[] FloatBuffer = { 0, 0, 0, 0 };

        //#START
        private static readonly uint[] UIntBuffer = { 0, 0, 0, 0 };
        uint renderedTexture;
        uint TextFrameBuffer;
        ImGui.Internal.List<DrawVertex> QuadVertices = new ImGui.Internal.List<DrawVertex>(4);
        ImGui.Internal.List<DrawIndex> QuadIndices = new ImGui.Internal.List<DrawIndex>(6);
        //#END

        public void Init(IntPtr windowHandle, Size size)
        {
            CreateOpenGLContext((IntPtr)windowHandle);

            GL.Enable(GL.GL_MULTISAMPLE);

            m.Init();
            mImage.Init();
            GlyphMaterial.Init();
            TextMaterial.Init();
            {
                QuadVertices.Add(new DrawVertex { pos = (0, 0), uv = (0, 0), color = (ColorF)Color.Black });
                QuadVertices.Add(new DrawVertex { pos = (0, 0), uv = (0, 1), color = (ColorF)Color.Black });
                QuadVertices.Add(new DrawVertex { pos = (0, 0), uv = (1, 1), color = (ColorF)Color.Black });
                QuadVertices.Add(new DrawVertex { pos = (0, 0), uv = (1, 0), color = (ColorF)Color.Black });

                QuadIndices.Add(new DrawIndex { Index = 0 });
                QuadIndices.Add(new DrawIndex { Index = 1 });
                QuadIndices.Add(new DrawIndex { Index = 2 });
                QuadIndices.Add(new DrawIndex { Index = 2 });
                QuadIndices.Add(new DrawIndex { Index = 3 });
                QuadIndices.Add(new DrawIndex { Index = 0 });
            }

            // render target
            {
                GL.GenFramebuffersEXT(1, UIntBuffer);
                TextFrameBuffer = UIntBuffer[0];
                GL.BindFramebufferEXT(GL.GL_FRAMEBUFFER_EXT, TextFrameBuffer);

                //create the texture which will contain the RGB output of our shader.
                // The texture we're going to render to
                GL.GenTextures(1, UIntBuffer);
                renderedTexture = UIntBuffer[0];

                // "Bind" the newly created texture : all future texture functions will modify this texture
                GL.BindTexture(GL.GL_TEXTURE_2D, renderedTexture);

                // Give an empty image to OpenGL ( the last "0" )
                GL.TexImage2D(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA, (int)size.Width, (int)size.Height, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, IntPtr.Zero);

                // Poor filtering. Needed !
                GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_NEAREST);
                GL.TexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_NEAREST);

                // configure our framebuffer
                // Set "renderedTexture" as our colour attachement #0
                GL.FramebufferTexture(GL.GL_FRAMEBUFFER_EXT, GL.GL_COLOR_ATTACHMENT0_EXT, renderedTexture, 0);

                // Set the list of draw buffers.
                uint[] DrawBuffers = { GL.GL_COLOR_ATTACHMENT0_EXT };
                GL.DrawBuffers(1, DrawBuffers); // "1" is the size of DrawBuffers

                // check that our framebuffer is ok
                if (GL.CheckFramebufferStatusEXT(GL.GL_FRAMEBUFFER_EXT) != GL.GL_FRAMEBUFFER_COMPLETE_EXT)
                {
                    throw new Exception("Failed to create framebuffer.");
                }

                // restore to default framebuffer
                GL.BindFramebufferEXT(GL.GL_FRAMEBUFFER_EXT, 0);
                // unbind texture
                GL.BindTexture(GL.GL_TEXTURE_2D, 0);
            }

            // Other state
            GL.Disable(GL.GL_CULL_FACE);
            GL.Disable(GL.GL_DEPTH_TEST);
            GL.DepthFunc(GL.GL_NEVER);
            GL.Enable(GL.GL_SCISSOR_TEST);
            var clearColor = Color.Argb(255, 114, 144, 154);//TODO this should be the background color of Form
            GL.ClearColor((float)clearColor.R, (float)clearColor.G, (float)clearColor.B, (float)clearColor.A);

            Utility.CheckGLError();
        }

        public void Clear()
        {
            GL.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
        }

        private static void DoRender(OpenGLMaterial material,
            ImGui.Internal.List<DrawCommand> commandBuffer, ImGui.Internal.List<DrawIndex> indexBuffer, ImGui.Internal.List<DrawVertex> vertexBuffer,
            int width, int height)
        {
            // Backup GL state
            GL.GetIntegerv(GL.GL_CURRENT_PROGRAM, IntBuffer); int last_program = IntBuffer[0];
            GL.GetIntegerv(GL.GL_TEXTURE_BINDING_2D, IntBuffer); int last_texture = IntBuffer[0];
            GL.GetIntegerv(GL.GL_ACTIVE_TEXTURE, IntBuffer); int last_active_texture = IntBuffer[0];
            GL.GetIntegerv(GL.GL_ARRAY_BUFFER_BINDING, IntBuffer); int last_array_buffer = IntBuffer[0];
            GL.GetIntegerv(GL.GL_ELEMENT_ARRAY_BUFFER_BINDING, IntBuffer); int last_element_array_buffer = IntBuffer[0];
            GL.GetIntegerv(GL.GL_VERTEX_ARRAY_BINDING, IntBuffer);int last_vertex_array = IntBuffer[0];
            GL.GetIntegerv(GL.GL_BLEND_SRC, IntBuffer); int last_blend_src = IntBuffer[0];
            GL.GetIntegerv(GL.GL_BLEND_DST, IntBuffer); int last_blend_dst = IntBuffer[0];
            GL.GetIntegerv(GL.GL_BLEND_EQUATION_RGB, IntBuffer); int last_blend_equation_rgb = IntBuffer[0];
            GL.GetIntegerv(GL.GL_BLEND_EQUATION_ALPHA, IntBuffer);int last_blend_equation_alpha = IntBuffer[0];
            GL.GetIntegerv(GL.GL_VIEWPORT, IntBuffer); Rect last_viewport = new Rect(IntBuffer[0], IntBuffer[1], IntBuffer[2], IntBuffer[3]);
            uint last_enable_blend = GL.IsEnabled(GL.GL_BLEND);
            uint last_enable_cull_face = GL.IsEnabled(GL.GL_CULL_FACE);
            uint last_enable_depth_test = GL.IsEnabled(GL.GL_DEPTH_TEST);
            uint last_enable_scissor_test = GL.IsEnabled(GL.GL_SCISSOR_TEST);

            // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled
            GL.Enable(GL.GL_BLEND);
            GL.BlendEquation(GL.GL_FUNC_ADD_EXT);
            GL.BlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
            GL.Disable(GL.GL_CULL_FACE);
            GL.Disable(GL.GL_DEPTH_TEST);
            GL.DepthFunc(GL.GL_NEVER);
            GL.Enable(GL.GL_SCISSOR_TEST);

            // Setup viewport, orthographic projection matrix
            GL.Viewport(0, 0, width, height);
            GLM.mat4 ortho_projection = GLM.glm.ortho(0.0f, width, height, 0.0f, -5.0f, 5.0f);
            material.program.Bind();
            material.program.SetUniformMatrix4("ProjMtx", ortho_projection.to_array());//FIXME make GLM.mat4.to_array() not create a new array

            // Send vertex and index data
            GL.BindVertexArray(material.vaoHandle);
            GL.BindBuffer(GL.GL_ARRAY_BUFFER, material.positionVboHandle);
            GL.BufferData(GL.GL_ARRAY_BUFFER, vertexBuffer.Count * Marshal.SizeOf<DrawVertex>(), vertexBuffer.Pointer, GL.GL_STREAM_DRAW);
            GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, material.elementsHandle);
            GL.BufferData(GL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer.Count * Marshal.SizeOf<DrawIndex>(), indexBuffer.Pointer, GL.GL_STREAM_DRAW);

            Utility.CheckGLError();

            // Draw
            var indexBufferOffset = IntPtr.Zero;
            foreach (var drawCmd in commandBuffer)
            {
                var clipRect = drawCmd.ClipRect;
                if (drawCmd.TextureData != null)
                {
                    GL.ActiveTexture(GL.GL_TEXTURE0);
                    GL.BindTexture(GL.GL_TEXTURE_2D, (uint)drawCmd.TextureData.GetNativeTextureId());
                }
                GL.Scissor((int) clipRect.X, (int) (height - clipRect.Height - clipRect.Y), (int) clipRect.Width, (int) clipRect.Height);
                GL.DrawElements(GL.GL_TRIANGLES, drawCmd.ElemCount, GL.GL_UNSIGNED_INT, indexBufferOffset);
                indexBufferOffset = IntPtr.Add(indexBufferOffset, drawCmd.ElemCount*Marshal.SizeOf<DrawIndex>());

                Utility.CheckGLError();
            }

            // Restore modified GL state
            GL.UseProgram((uint)last_program);
            GL.ActiveTexture((uint)last_active_texture);
            GL.BindTexture(GL.GL_TEXTURE_2D, (uint)last_texture);
            GL.BindVertexArray((uint)last_vertex_array);
            GL.BindBuffer(GL.GL_ARRAY_BUFFER, (uint)last_array_buffer);
            GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, (uint)last_element_array_buffer);
            GL.BlendEquationSeparate((uint)last_blend_equation_rgb, (uint)last_blend_equation_alpha);
            GL.BlendFunc((uint)last_blend_src, (uint)last_blend_dst);
            if (last_enable_blend == GL.GL_TRUE) GL.Enable(GL.GL_BLEND); else GL.Disable(GL.GL_BLEND);
            if (last_enable_cull_face == GL.GL_TRUE) GL.Enable(GL.GL_CULL_FACE); else GL.Disable(GL.GL_CULL_FACE);
            if (last_enable_depth_test == GL.GL_TRUE) GL.Enable(GL.GL_DEPTH_TEST); else GL.Disable(GL.GL_DEPTH_TEST);
            if (last_enable_scissor_test == GL.GL_TRUE) GL.Enable(GL.GL_SCISSOR_TEST); else GL.Disable(GL.GL_SCISSOR_TEST);
            GL.Viewport((int)last_viewport.X, (int)last_viewport.Y, (int)last_viewport.Width, (int)last_viewport.Height);
        }

        public void RenderDrawList(DrawList drawList, int width, int height)
        {
            DoRender(m, drawList.DrawBuffer.CommandBuffer, drawList.DrawBuffer.IndexBuffer, drawList.DrawBuffer.VertexBuffer, width, height);
            DoRender(mImage, drawList.ImageBuffer.CommandBuffer, drawList.ImageBuffer.IndexBuffer, drawList.ImageBuffer.VertexBuffer, width, height);

            DrawTextMesh(drawList.TextMesh, width, height);
        }
        bool CompositeText = true;
        /// <summary>
        /// Draw text mesh (to text framebuffer)
        /// </summary>
        /// <param name="textMesh"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public void DrawTextMesh(TextMesh textMesh, int width, int height)
        {
            // Backup GL state
            GL.GetIntegerv(GL.GL_FRAMEBUFFER_BINDING_EXT, IntBuffer); int last_framebuffer_binding = IntBuffer[0];
            GL.GetIntegerv(GL.GL_CURRENT_PROGRAM, IntBuffer); int last_program = IntBuffer[0];
            GL.GetIntegerv(GL.GL_TEXTURE_BINDING_2D, IntBuffer); int last_texture = IntBuffer[0];
            GL.GetIntegerv(GL.GL_ACTIVE_TEXTURE, IntBuffer); int last_active_texture = IntBuffer[0];
            GL.GetIntegerv(GL.GL_ARRAY_BUFFER_BINDING, IntBuffer); int last_array_buffer = IntBuffer[0];
            GL.GetIntegerv(GL.GL_ELEMENT_ARRAY_BUFFER_BINDING, IntBuffer); int last_element_array_buffer = IntBuffer[0];
            GL.GetIntegerv(GL.GL_VERTEX_ARRAY_BINDING, IntBuffer); int last_vertex_array = IntBuffer[0];
            GL.GetIntegerv(GL.GL_BLEND_SRC, IntBuffer); int last_blend_src = IntBuffer[0];
            GL.GetIntegerv(GL.GL_BLEND_DST, IntBuffer); int last_blend_dst = IntBuffer[0];
            GL.GetIntegerv(GL.GL_BLEND_EQUATION_RGB, IntBuffer); int last_blend_equation_rgb = IntBuffer[0];
            GL.GetIntegerv(GL.GL_BLEND_EQUATION_ALPHA, IntBuffer); int last_blend_equation_alpha = IntBuffer[0];
            GL.GetFloatv(GL.GL_COLOR_CLEAR_VALUE, FloatBuffer);
            float last_clear_color_r = FloatBuffer[0];
            float last_clear_color_g = FloatBuffer[1];
            float last_clear_color_b = FloatBuffer[2];
            float last_clear_color_a = FloatBuffer[3];
            GL.GetIntegerv(GL.GL_VIEWPORT, IntBuffer); Rect last_viewport = new Rect(IntBuffer[0], IntBuffer[1], IntBuffer[2], IntBuffer[3]);
            uint last_enable_blend = GL.IsEnabled(GL.GL_BLEND);
            uint last_enable_cull_face = GL.IsEnabled(GL.GL_CULL_FACE);
            uint last_enable_depth_test = GL.IsEnabled(GL.GL_DEPTH_TEST);
            uint last_enable_scissor_test = GL.IsEnabled(GL.GL_SCISSOR_TEST);

            GLM.mat4 ortho_projection = GLM.glm.ortho(0.0f, width, height, 0.0f, -5.0f, 5.0f);
            GL.Viewport(0, 0, width, height);

            // Draw text mesh
            {
                if(CompositeText)
                {
                    GL.BindFramebufferEXT(GL.GL_FRAMEBUFFER_EXT, TextFrameBuffer);// Render to text framebuffer
                }
                GL.ClearColor(0, 0, 0, 0);//Clear framebuffer to Color.Clear
                GL.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT); //clear text framebuffer to Color.Clear

                GL.Enable(GL.GL_BLEND);
                GL.BlendEquation(GL.GL_FUNC_ADD_EXT);
                GL.BlendFunc(GL.GL_ONE, GL.GL_ONE);

                Utility.CheckGLError();

                // Draw triangles
                {
                    var material = GlyphMaterial;
                    var vertexBuffer = textMesh.VertexBuffer;
                    var indexBuffer = textMesh.IndexBuffer;

                    material.program.Bind();
                    material.program.SetUniformMatrix4("ProjMtx", ortho_projection.to_array());//FIXME make GLM.mat4.to_array() not create a new array

                    // Send vertex data
                    GL.BindVertexArray(material.vaoHandle);
                    GL.BindBuffer(GL.GL_ARRAY_BUFFER, material.positionVboHandle);
                    GL.BufferData(GL.GL_ARRAY_BUFFER, vertexBuffer.Count * Marshal.SizeOf<DrawVertex>(), vertexBuffer.Pointer, GL.GL_STREAM_DRAW);
                    GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, material.elementsHandle);
                    GL.BufferData(GL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer.Count * Marshal.SizeOf<DrawIndex>(), indexBuffer.Pointer, GL.GL_STREAM_DRAW);

                    var drawCmd = textMesh.Command0;
                    var clipRect = drawCmd.ClipRect;
                    GL.Scissor((int)clipRect.X, (int)(height - clipRect.Height - clipRect.Y), (int)clipRect.Width, (int)clipRect.Height);
                    GL.DrawElements(GL.GL_TRIANGLES, indexBuffer.Count, GL.GL_UNSIGNED_INT, IntPtr.Zero);
                }

                Utility.CheckGLError();

                // Draw quadratic bezier segments
#if (false)
                {
                    var material = GlyphMaterial;
                    var vertexBuffer = textMesh.BezierVertexBuffer;
                    var indexBuffer = textMesh.BezierIndexBuffer;

                    material.program.Bind();
                    material.program.SetUniformMatrix4("ProjMtx", ortho_projection.to_array());//FIXME make GLM.mat4.to_array() not create a new array

                    // Send vertex data
                    GL.BindVertexArray(material.vaoHandle);
                    GL.BindBuffer(GL.GL_ARRAY_BUFFER, material.positionVboHandle);
                    GL.BufferData(GL.GL_ARRAY_BUFFER, vertexBuffer.Count * Marshal.SizeOf<DrawVertex>(), vertexBuffer.Pointer, GL.GL_STREAM_DRAW);
                    GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, material.elementsHandle);
                    GL.BufferData(GL.GL_ELEMENT_ARRAY_BUFFER, indexBuffer.Count * Marshal.SizeOf<DrawIndex>(), indexBuffer.Pointer, GL.GL_STREAM_DRAW);

                    var drawCmd = textMesh.Command1;
                    var clipRect = drawCmd.ClipRect;
                    GL.Scissor((int)clipRect.X, (int)(height - clipRect.Height - clipRect.Y), (int)clipRect.Width, (int)clipRect.Height);
                    GL.DrawElements(GL.GL_TRIANGLES, indexBuffer.Count, GL.GL_UNSIGNED_INT, IntPtr.Zero);
                }
#endif

                // TODO combine two drawcalls, and actually triangles and bezier segments can be saved in one vertexbuffer and indexbuffer
            }

            //byte[] pixels = new byte[4 * width * height];
            //GL.ReadPixels(0, 0, width, height, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, pixels);
            //var img = ImageSharp.Image.LoadPixelData<ImageSharp.Rgba32>(pixels, width, height);
            //img.Save("D:\\1.png");

            // Composite text framebuffer to the default framebuffer
            if (CompositeText)
            {
                GL.BindFramebufferEXT(GL.GL_FRAMEBUFFER_EXT, 0);

                GL.Enable(GL.GL_BLEND);
                GL.BlendEquation(GL.GL_FUNC_ADD_EXT);
                //GL.BlendFunc(GL.GL_ONE, GL.GL_ZERO);
                GL.BlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);

                OpenGLMaterial material = TextMaterial;
                material.program.Bind();
                material.program.SetUniformMatrix4("ProjMtx", ortho_projection.to_array());//FIXME make GLM.mat4.to_array() not create a new array

                // create vertex and index data that fills the screen
                QuadVertices[0] = new DrawVertex { pos = (0, 0), uv = (0, 1), color = (ColorF)Color.Clear };
                QuadVertices[1] = new DrawVertex { pos = (0, height), uv = (0, 0), color = (ColorF)Color.Clear };
                QuadVertices[2] = new DrawVertex { pos = (width, height), uv = (1, 0), color = (ColorF)Color.Clear };
                QuadVertices[3] = new DrawVertex { pos = (width, 0), uv = (1, 1), color = (ColorF)Color.Clear };

                QuadIndices[0] = new DrawIndex { Index = 0 };
                QuadIndices[1] = new DrawIndex { Index = 1 };
                QuadIndices[2] = new DrawIndex { Index = 2 };
                QuadIndices[3] = new DrawIndex { Index = 2 };
                QuadIndices[4] = new DrawIndex { Index = 3 };
                QuadIndices[5] = new DrawIndex { Index = 0 };

                // Send vertex and index data
                GL.BindVertexArray(material.vaoHandle);
                GL.BindBuffer(GL.GL_ARRAY_BUFFER, material.positionVboHandle);
                GL.BufferData(GL.GL_ARRAY_BUFFER, QuadVertices.Count * Marshal.SizeOf<DrawVertex>(), QuadVertices.Pointer, GL.GL_STREAM_DRAW);
                GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, material.elementsHandle);
                GL.BufferData(GL.GL_ELEMENT_ARRAY_BUFFER, QuadIndices.Count * Marshal.SizeOf<DrawIndex>(), QuadIndices.Pointer, GL.GL_STREAM_DRAW);

                Utility.CheckGLError();

                // Draw
                var indexBufferOffset = IntPtr.Zero;
                GL.ActiveTexture(GL.GL_TEXTURE0);
                GL.BindTexture(GL.GL_TEXTURE_2D, renderedTexture);
                GL.DrawElements(GL.GL_TRIANGLES, QuadIndices.Count, GL.GL_UNSIGNED_INT, indexBufferOffset);
                indexBufferOffset = IntPtr.Add(indexBufferOffset, QuadIndices.Count * Marshal.SizeOf<DrawIndex>());

                Utility.CheckGLError();
            }

            // Restore modified GL state
            GL.BindFramebufferEXT(GL.GL_FRAMEBUFFER_EXT, (uint)last_framebuffer_binding);
            GL.UseProgram((uint)last_program);
            GL.ActiveTexture((uint)last_active_texture);
            GL.BindTexture(GL.GL_TEXTURE_2D, (uint)last_texture);
            GL.BindVertexArray((uint)last_vertex_array);
            GL.BindBuffer(GL.GL_ARRAY_BUFFER, (uint)last_array_buffer);
            GL.BindBuffer(GL.GL_ELEMENT_ARRAY_BUFFER, (uint)last_element_array_buffer);
            GL.BlendEquationSeparate((uint)last_blend_equation_rgb, (uint)last_blend_equation_alpha);
            GL.BlendFunc((uint)last_blend_src, (uint)last_blend_dst);
            if (last_enable_blend == GL.GL_TRUE) GL.Enable(GL.GL_BLEND); else GL.Disable(GL.GL_BLEND);
            if (last_enable_cull_face == GL.GL_TRUE) GL.Enable(GL.GL_CULL_FACE); else GL.Disable(GL.GL_CULL_FACE);
            if (last_enable_depth_test == GL.GL_TRUE) GL.Enable(GL.GL_DEPTH_TEST); else GL.Disable(GL.GL_DEPTH_TEST);
            if (last_enable_scissor_test == GL.GL_TRUE) GL.Enable(GL.GL_SCISSOR_TEST); else GL.Disable(GL.GL_SCISSOR_TEST);
            GL.ClearColor(last_clear_color_r, last_clear_color_g, last_clear_color_b, last_clear_color_a);
            GL.Viewport((int)last_viewport.X, (int)last_viewport.Y, (int)last_viewport.Width, (int)last_viewport.Height);
        }

        public void ShutDown()
        {
            m.ShutDown();
            mImage.ShutDown();
            GlyphMaterial.ShutDown();
            TextMaterial.ShutDown();
        }
    }
}
