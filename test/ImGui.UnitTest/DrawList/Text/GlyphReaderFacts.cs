﻿using System;
using System.Collections.Generic;
using System.Text;
using Typography.OpenFont;
using Xunit;
using System.Diagnostics;
using ImGui.Common.Primitive;
using Xunit.Abstractions;

namespace ImGui.UnitTest
{
    public class GlyphReaderFacts
    {
        public class TheReadMethod
        {
            private readonly ITestOutputHelper o;
            public TheReadMethod(ITestOutputHelper output)
            {
                o = output;
            }

            [Theory]
            [InlineData("msjh.ttf", 'D', 400)]
            [InlineData("DroidSans.ttf", 'o', 400)]
            [InlineData("msjh.ttf", '乐', 400)]
            public void Read(string fontFileName, char character, int fontSize)
            {
                Typeface typeFace;
                using (var fs = Utility.ReadFile(Utility.FontDir + fontFileName))
                {
                    var reader = new OpenFontReader();
                    typeFace = reader.Read(fs);
                }

                Glyph glyph = typeFace.Lookup(character);

                // read polygons and bezier segments
                var polygons = new List<List<Point>>();
                var bezierSegments = new List<(Point, Point, Point)>();
                GlyphLoader.Read(glyph, out polygons, out bezierSegments);

                //print to test output
                for (int i = 0; i < polygons.Count; i++)
                {
                    o.WriteLine("Polygon " + i);
                    var polygon = polygons[i];
                    foreach (var p in polygon)
                    {
                        o.WriteLine("{0}, {1}", (int)p.X, (int)p.Y);
                    }
                    o.WriteLine("");
                }

                foreach (var segment in bezierSegments)
                {
                    o.WriteLine("<{0}, {1}> <{2}, {3}> <{4}, {5}>",
                        (int)segment.Item1.X, (int)segment.Item1.Y,
                        (int)segment.Item2.X, (int)segment.Item2.Y,
                        (int)segment.Item3.X, (int)segment.Item3.Y);
                }

                o.WriteLine("");

                //FIXME move/scale the rendered glyph to visible region of the cairo surface.

                // draw to an image
                using (Cairo.ImageSurface surface = new Cairo.ImageSurface(Cairo.Format.Argb32, 2000, 2000))
                using (Cairo.Context g = new Cairo.Context(surface))
                {
                    for (int i = 0; i < polygons.Count; i++)
                    {
                        var polygon = polygons[i];
                        g.MoveTo(polygon[0].X, polygon[0].Y);
                        foreach (var point in polygon)
                        {
                            g.LineTo(point.X, point.Y);
                        }
                        g.ClosePath();
                    }
                    g.SetSourceColor(new Cairo.Color(0, 0, 0));
                    g.LineWidth = 2;
                    g.StrokePreserve();
                    g.SetSourceColor(new Cairo.Color(0.8, 0, 0));
                    g.Fill();

                    foreach(var segment in bezierSegments)
                    {
                        var p0 = segment.Item1;
                        var c = segment.Item2;
                        var p1 = segment.Item3;
                        g.MoveTo(p0.X, p0.Y);
                        g.QuadraticTo(c.X, c.Y, p1.X, p1.Y);
                    }
                    g.SetSourceColor(new Cairo.Color(0.5, 0.5, 0));
                    g.Stroke();

                    var path = string.Format("D:\\ImGui.UnitTest\\GlyphReaderFacts.TheReadMethod.Read_{0}_{1}_{2}.png",
                        fontFileName, character, fontSize);
                    surface.WriteToPng(path);

                    Util.OpenImage(path);

                    // Now inspect the image to check whether the glyph is correct
                    // TODO Are there a better way to do such kind of unit-test?
                }
            }
        }
    }
}
