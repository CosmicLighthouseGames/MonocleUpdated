﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Text.RegularExpressions;
using System.IO;

namespace Monocle
{
	public class PixelFontCharacter
	{
		public int Character;
		public MTexture Texture;
		public int XOffset;
		public int YOffset;
		public int XAdvance;
		public Dictionary<int, int> Kerning = new Dictionary<int, int>();

		public PixelFontCharacter(int character, MTexture texture, XmlElement xml)
		{
			Character = character;
			Texture = texture.GetSubtexture(xml.AttrInt("x"), xml.AttrInt("y"), xml.AttrInt("width"), xml.AttrInt("height"));
			XOffset = xml.AttrInt("xoffset");
			YOffset = xml.AttrInt("yoffset");
			XAdvance = xml.AttrInt("xadvance");
		}
	}

	public class PixelFontSize
	{
		public List<MTexture> Textures;
		public Dictionary<int, PixelFontCharacter> Characters;
		public int LineHeight;
		public float Size;
		public bool Outline;

		public static Material FontMaterial;

		private StringBuilder temp = new StringBuilder();

		public string AutoNewline(string text, int width)
		{
			if (string.IsNullOrEmpty(text))
				return text;

			temp.Clear();

			var words = Regex.Split(text, @"(\s)");
			var lineWidth = 0f;

			foreach (var word in words)
			{
				var wordWidth = Measure(word).X;
				if (wordWidth + lineWidth > width)
				{
					temp.Append('\n');
					lineWidth = 0;

					if (word.Equals(" "))
						continue;
				}

				// this word is longer than the max-width, split where ever we can
				if (wordWidth > width)
				{
					int i = 1, start = 0;
					for (; i < word.Length; i++)
						if (i - start > 1 && Measure(word.Substring(start, i - start - 1)).X > width)
						{
							temp.Append(word.Substring(start, i - start - 1));
							temp.Append('\n');
							start = i - 1;
						}


					var remaining = word.Substring(start, word.Length - start);
					temp.Append(remaining);
					lineWidth += Measure(remaining).X;
				}
				// normal word, add it
				else
				{
					lineWidth += wordWidth;
					temp.Append(word);
				}
			}

			return temp.ToString();
		}

		public PixelFontCharacter Get(int id)
		{
			PixelFontCharacter val = null;
			if (Characters.TryGetValue(id, out val))
				return val;
			return null;
		}

		public Vector2 Measure(char text)
		{
			PixelFontCharacter c = null;
			if (Characters.TryGetValue(text, out c))
				return new Vector2(c.XAdvance, LineHeight);
			return Vector2.Zero;
		}

		public Vector2 Measure(string text)
		{
			if (string.IsNullOrEmpty(text))
				return Vector2.Zero;

			var size = new Vector2(0, LineHeight);
			var currentLineWidth = 0f;

			for (var i = 0; i < text.Length; i++)
			{
				if (text[i] == '\n')
				{
					size.Y += LineHeight;
					if (currentLineWidth > size.X)
						size.X = currentLineWidth;
					currentLineWidth = 0f;
				}
				else
				{
					PixelFontCharacter c = null;
					if (Characters.TryGetValue(text[i], out c))
					{

						int kerning;
						if (i < text.Length - 1 && c.Kerning.TryGetValue(text[i + 1], out kerning)) {
							currentLineWidth += kerning;
						}
						if (i == text.Length - 1) {
							currentLineWidth += c.Texture.Width;
						}
						else {
							currentLineWidth += c.XAdvance;
						}
					}
				}
			}

			if (currentLineWidth > size.X)
				size.X = currentLineWidth;

			return size /= Engine.PixelsPerUnit;
		}
		public Vector2 MeasurePartial(string text, int length) {
			if (string.IsNullOrEmpty(text))
				return Vector2.Zero;

			var size = new Vector2(0, LineHeight);
			var currentLineWidth = 0f;

			for (var i = 0; i < Math.Min(length + 1, text.Length); i++) {
				if (text[i] == '\n') {
					size.Y += LineHeight;
					if (currentLineWidth > size.X)
						size.X = currentLineWidth;
					currentLineWidth = 0f;
				}
				else {
					PixelFontCharacter c = null;
					if (Characters.TryGetValue(text[i], out c)) {

						int kerning;
						if (i < text.Length - 1 && c.Kerning.TryGetValue(text[i + 1], out kerning)) {
							currentLineWidth += kerning;
						}
						if (i == text.Length - 1) {
							currentLineWidth += c.Texture.Width;
						}
						else {
							currentLineWidth += c.XAdvance;
						}
					}
				}
			}

			if (currentLineWidth > size.X)
				size.X = currentLineWidth;

			return size /= Engine.PixelsPerUnit;
		}

		public float WidthToNextLine(string text, int start)
		{
			if (string.IsNullOrEmpty(text))
				return 0;

			var currentLineWidth = 0f;

			for (int i = start, j = text.Length; i < j; i++)
			{
				if (text[i] == '\n')
					break;

				PixelFontCharacter c = null;
				if (Characters.TryGetValue(text[i], out c))
				{
					currentLineWidth += c.XAdvance;

					int kerning;
					if (i < j - 1 && c.Kerning.TryGetValue(text[i + 1], out kerning))
						currentLineWidth += kerning;
				}
			}

			return currentLineWidth;
		}

		public float HeightOf(string text)
		{
			if (string.IsNullOrEmpty(text))
				return 0;

			int lines = 1;
			if (text.IndexOf('\n') >= 0)
				for (int i = 0; i < text.Length; i++)
					if (text[i] == '\n')
						lines++;
			return lines * LineHeight;
		}

		public void Draw(char character, Vector3 position, Vector2 justify, Vector2 scale, Color color)
		{
			if (char.IsWhiteSpace(character))
				return;

			PixelFontCharacter c = null;
			if (Characters.TryGetValue(character, out c))
			{
				var measure = Measure(character);
				var justified = new Vector2(measure.X * justify.X, measure.Y * justify.Y);
				var pos = position + (new Vector3(c.XOffset, c.YOffset, 0) - justified.XY_()) * scale.XY_(1);
				Monocle.Draw.Texture(c.Texture, Calc.Floor(pos), Vector2.Zero, color, scale, FontMaterial??Monocle.Draw.DefaultMaterial);
			}
		}
		 
		public void Draw(string text, Vector3 position, Vector2 justify, Quaternion rotation, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke, Color strokeColor)
		{
			if (string.IsNullOrEmpty(text))
				return;

			var mat = FontMaterial??Monocle.Draw.DefaultMaterial;

			//scale *= Engine.PixelsPerUnit;

			var offset = Vector2.Zero;
			var lineWidth = (justify.X != 0 ? WidthToNextLine(text, 0) : 0);
			var justified = new Vector2(lineWidth * justify.X, HeightOf(text) * justify.Y);

			var up = Vector3.Transform(Vector3.Up / Engine.PixelsPerUnit, rotation);
			var right = Vector3.Transform(Vector3.Right / Engine.PixelsPerUnit, rotation);

			
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '\n')
				{
					offset.X = 0;
					offset.Y -= LineHeight;
					if (justify.X != 0)
						justified.X = WidthToNextLine(text, i + 1) * justify.X;
					continue;
				}

				PixelFontCharacter c = null;
				if (Characters.TryGetValue(text[i], out c))
				{
					var pos = position;
					pos += Vector3.Transform(((offset + new Vector2(c.XOffset, LineHeight - (c.YOffset + c.Texture.Height)) - justified) * scale / Engine.PixelsPerUnit).XY_(), rotation);
					//pos.Round();

					// draw stroke
					if (stroke > 0 && !Outline)
					{
						Monocle.Draw.Depth--;
						if (edgeDepth > 0)
						{
							Monocle.Draw.Texture(c.Texture, pos + right * -stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
							for (var j = -stroke; j < edgeDepth + stroke; j += stroke)
							{
								Monocle.Draw.Texture(c.Texture, pos + right * -stroke + up * j, Vector2.Zero, scale, rotation, strokeColor, mat);
								Monocle.Draw.Texture(c.Texture, pos + right * stroke + up * j, Vector2.Zero, scale, rotation, strokeColor, mat);
							}
							Monocle.Draw.Texture(c.Texture, pos + right * -stroke + up * (edgeDepth + stroke), Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + up * (edgeDepth + stroke), Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + right * stroke + up * (edgeDepth + stroke), Vector2.Zero, scale, rotation, strokeColor, mat);
						}
						else
						{
							Monocle.Draw.Texture(c.Texture,pos + (-right - up) * stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + (-up) * stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + (right - up) * stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + (-right) * stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + (right) * stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + (-right + up) * stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + (up) * stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
							Monocle.Draw.Texture(c.Texture,pos + (right + up) * stroke, Vector2.Zero, scale, rotation, strokeColor, mat);
						}
						Monocle.Draw.Depth++;
					}

					// draw edge
					if (edgeDepth > 0)
						Monocle.Draw.Texture(c.Texture,pos + up * edgeDepth, Vector2.Zero, scale, rotation, edgeColor, mat);

					// draw normal character
					Monocle.Draw.Texture(c.Texture,pos, Vector2.Zero, scale, rotation, color, mat);

					offset.X += c.XAdvance;

					int kerning;
					if (i < text.Length - 1 && c.Kerning.TryGetValue(text[i + 1], out kerning))
						offset.X += kerning;
				}
				else {
					//throw new NotImplementedException($"Character '{text[i]}' (index {(int)text[i]}) is not supported for this font");
				}
			}

		}

		public void Draw(string text, Vector3 position, Color color)
		{
			Draw(text, position, Vector2.Zero, Quaternion.Identity, Vector2.One, color, 0, Color.Transparent, 0, Color.Transparent);
		}

		public void Draw(string text, Vector3 position, Vector2 justify, Vector2 scale, Color color)
		{
			Draw(text, position, justify, Quaternion.Identity, scale, color, 0, Color.Transparent, 0, Color.Transparent);
		}

		public void DrawOutline(string text, Vector3 position, Vector2 justify, Vector2 scale, Color color, float stroke, Color strokeColor)
		{
			Draw(text, position, justify, Quaternion.Identity, scale, color, 0f, Color.Transparent, stroke, strokeColor);
		}

		public void DrawEdgeOutline(string text, Vector3 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke = 0f, Color strokeColor = default(Color))
		{
			Draw(text, position, justify, Quaternion.Identity, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}
	}

	public class PixelFont
	{
		public List<PixelFontSize> Sizes = new List<PixelFontSize>();
		public List<MTexture> Textures;

		public PixelFont()
		{
		}
		
		public PixelFont AddFontSize(string path, Atlas atlas = null, bool outline = false)
		{
			var data = Calc.LoadXML(path)["font"];
			return AddFontSize(path, data, atlas, outline);
		}

		public PixelFont AddFontSize(string path, XmlElement data, Atlas atlas = null, bool outline = false)
		{
			// check if size already exists
			var size = data["info"].AttrFloat("size");
			foreach (var fs in Sizes)
				if (fs.Size == size)
					return this;

			// get textures
			Textures = new List<MTexture>();
			var pages = data["pages"];
			foreach (XmlElement page in pages)
			{
				var file = page.Attr("file");
				var atlasPath = Path.GetFileNameWithoutExtension(file);

				if (atlas != null && atlas.Has(atlasPath))
				{
					if (atlas.Has(Path.Combine(path, atlasPath))) {
						Textures.Add(atlas[Path.Combine(path, atlasPath)]);
					}
					else {
						Textures.Add(atlas[atlasPath]);
					}
				}
				else
				{
					var dir = Path.GetDirectoryName(path);
					Textures.Add(MTexture.FromFile(Path.Combine(dir, file)));
				}
			}

			// create font size
			var fontSize = new PixelFontSize()
			{
				Textures = Textures,
				Characters = new Dictionary<int, PixelFontCharacter>(),
				LineHeight = data["common"].AttrInt("lineHeight"),
				Size = size,
				Outline = outline
			};

			// get characters
			foreach (XmlElement character in data["chars"])
			{
				int id = character.AttrInt("id");
				int page = character.AttrInt("page", 0);
				fontSize.Characters.Add(id, new PixelFontCharacter(id, Textures[page], character));
			}

			// get kerning
			if (data["kernings"] != null)
				foreach (XmlElement kerning in data["kernings"])
				{
					var from = kerning.AttrInt("first");
					var to = kerning.AttrInt("second");
					var push = kerning.AttrInt("amount");

					PixelFontCharacter c = null;
					if (fontSize.Characters.TryGetValue(from, out c))
						c.Kerning.Add(to, push);
				}

			// add font size
			Sizes.Add(fontSize);
			Sizes.Sort((a, b) => { return Math.Sign(a.Size - b.Size); });

			return this;
		}

		public PixelFontSize Get(float size)
		{
			for (int i = 0, j = Sizes.Count - 1; i < j; i++)
				if (Sizes[i].Size >= size)
					return Sizes[i];
			return Sizes[Sizes.Count - 1];
		}

		public Vector2 MeasureString(string text) {
			var font = Sizes[0];

			return font.Measure(text);
		}
		public Vector2 MeasurePartialString(string text, int length) {
			var font = Sizes[0];

			return font.MeasurePartial(text, length);
		}
		public int GetAdvance(char text) {
			var font = Sizes[0];

			PixelFontCharacter c;
			if (font.Characters.TryGetValue(text, out c)) {
				return c.XAdvance;
			}

			return 0;
		}
		public Vector2 MeasureString(string text, float size) {
			var font = Sizes[0];

			return font.Measure(text);
		}

		public void Draw(float baseSize, char character, Vector3 position, Vector2 justify, Vector2 scale, Color color)
		{
			var fontSize = Get(baseSize * Math.Max(scale.X, scale.Y));
			scale *= (baseSize / fontSize.Size);
			fontSize.Draw(character, position, justify, scale, color);
		}

		public void Draw(float baseSize, string text, Vector3 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke, Color strokeColor)
		{
			var fontSize = Get(baseSize * Math.Max(scale.X, scale.Y));
			scale *= (baseSize / fontSize.Size);
			fontSize.Draw(text, position, justify, Quaternion.Identity, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}

		public void Draw(float baseSize, string text, Vector3 position, Color color)
		{
			var scale = Vector2.One;
			var fontSize = Get(baseSize * Math.Max(scale.X, scale.Y));
			scale *= (baseSize / fontSize.Size);
			fontSize.Draw(text, position, Vector2.Zero, Quaternion.Identity, Vector2.One, color, 0, Color.Transparent, 0, Color.Transparent);
		}

		public void Draw(float baseSize, string text, Vector3 position, Vector2 justify, Vector2 scale, Color color)
		{
			var fontSize = Get(baseSize * Math.Max(scale.X, scale.Y));
			scale *= (baseSize / fontSize.Size);
			fontSize.Draw(text, position, justify, Quaternion.Identity, scale, color, 0, Color.Transparent, 0, Color.Transparent);
		}

		public void DrawOutline(float baseSize, string text, Vector3 position, Vector2 justify, Vector2 scale, Color color, float stroke, Color strokeColor)
		{
			var fontSize = Get(baseSize * Math.Max(scale.X, scale.Y));
			scale *= (baseSize / fontSize.Size);
			fontSize.Draw(text, position, justify, Quaternion.Identity, scale, color, 0f, Color.Transparent, stroke, strokeColor);
		}

		public void DrawEdgeOutline(float baseSize, string text, Vector3 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke = 0f, Color strokeColor = default(Color))
		{
			var fontSize = Get(baseSize * Math.Max(scale.X, scale.Y));
			scale *= (baseSize / fontSize.Size);
			fontSize.Draw(text, position, justify, Quaternion.Identity, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}

		public void Draw(char character, Vector3 position, Vector2 justify, Vector2 scale, Color color)
		{
			var fontSize = Sizes[0];
			fontSize.Draw(character, position, justify, scale, color);
		}

		public void Draw(string text, Vector3 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke, Color strokeColor) {
			var fontSize = Sizes[0];
			fontSize.Draw(text, position, justify, Quaternion.Identity, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}

		public void Draw(string text, Vector3 position, Color color)
		{
			var fontSize = Sizes[0];
			fontSize.Draw(text, position, Vector2.Zero, Quaternion.Identity, Vector2.One, color, 0, Color.Transparent, 0, Color.Transparent);
		}

		public void Draw(string text, Vector3 position, Vector2 justify, Vector2 scale, Color color) {
			var fontSize = Sizes[0];
			fontSize.Draw(text, position, justify, Quaternion.Identity, scale, color, 0, Color.Transparent, 0, Color.Transparent);
		}

		public void Draw(string text, Vector3 position, Vector2 justify, Quaternion rotation, Vector2 scale, Color color) {
			var fontSize = Sizes[0];
			fontSize.Draw(text, position, justify, rotation, scale, color, 0, Color.Transparent, 0, Color.Transparent);
		}

		public void DrawOutline(string text, Vector3 position, Vector2 justify, Vector2 scale, Color color, float stroke, Color strokeColor) {
			var fontSize = Sizes[0];
			fontSize.Draw(text, position, justify, Quaternion.Identity, scale, color, 0f, Color.Transparent, stroke, strokeColor);
		}

		public void DrawEdgeOutline(string text, Vector3 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke = 0f, Color strokeColor = default(Color)) {
			var fontSize = Sizes[0];
			fontSize.Draw(text, position, justify, Quaternion.Identity, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}


		public void Draw(float baseSize, char character, Vector2 position, Vector2 justify, Vector2 scale, Color color) {
			Draw(baseSize, character, position.XY_(), justify, scale, color);
		}

		public void Draw(float baseSize, string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke, Color strokeColor) {
			Draw(baseSize, text, position.XY_(), justify, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}

		public void Draw(float baseSize, string text, Vector2 position, Color color) {
			Draw(baseSize, text, position.XY_(), color);
		}

		public void Draw(float baseSize, string text, Vector2 position, Vector2 justify, Vector2 scale, Color color) {
			Draw(baseSize, text, position.XY_(), justify, scale, color);
		}

		public void DrawOutline(float baseSize, string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float stroke, Color strokeColor) {
			DrawOutline(baseSize,text,position.XY_(), justify, scale, color, stroke, strokeColor);
		}

		public void DrawEdgeOutline(float baseSize, string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke = 0f, Color strokeColor = default(Color)) {
			DrawEdgeOutline(baseSize, text, position.XY_(), justify, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}

		public void Draw(char character, Vector2 position, Vector2 justify, Vector2 scale, Color color) {
			Draw(character, position.XY_(), justify, scale, color);
		}

		public void Draw(string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke, Color strokeColor) {
			Draw(text, position.XY_(), justify, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}

		public void Draw(string text, Vector2 position, Color color) {
			Draw(text, position.XY_(), color);
		}

		public void Draw(string text, Vector2 position, Vector2 justify, Vector2 scale, Color color) {
			Draw(text, position.XY_(), justify, scale, color);
		}

		public void DrawOutline(string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float stroke, Color strokeColor) {
			DrawOutline(text, position.XY_(), justify, scale, color, stroke, strokeColor);
		}

		public void DrawEdgeOutline(string text, Vector2 position, Vector2 justify, Vector2 scale, Color color, float edgeDepth, Color edgeColor, float stroke = 0f, Color strokeColor = default(Color)) {
			DrawEdgeOutline(text, position.XY_(), justify, scale, color, edgeDepth, edgeColor, stroke, strokeColor);
		}


		public void Dispose()
		{
			foreach (var tex in Textures)
				tex.Dispose();
			Sizes.Clear();
		}
	}
}
