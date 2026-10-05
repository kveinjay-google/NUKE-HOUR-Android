#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenRA.Mods.RA2.Missions
{
	/// <summary>
	/// Reads mission text from the player's RA2 CSF string table.  CSF strings
	/// are stored as bitwise-inverted UTF-16LE and may carry an unused metadata
	/// suffix.  No decoded retail text is persisted into the public bundle.
	/// </summary>
	public sealed class Ra2CsfTable
	{
		const uint CsfPrefix = 0x43534620;
		const uint LabelPrefix = 0x4C424C20;
		const uint StringPrefix = 0x53545220;
		const uint StringWithExtraPrefix = 0x53545257;
		const uint MaximumLabels = 250000;
		const uint MaximumStringCharacters = 16 * 1024 * 1024;

		readonly Dictionary<string, string> strings;

		Ra2CsfTable(Dictionary<string, string> strings)
		{
			this.strings = strings;
		}

		public static Ra2CsfTable Parse(Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
			if (reader.ReadUInt32() != CsfPrefix)
				throw new InvalidDataException("Invalid RA2 CSF header.");

			reader.ReadUInt32();
			var labelCount = reader.ReadUInt32();
			reader.ReadUInt32();
			reader.ReadUInt32();
			reader.ReadUInt32();
			if (labelCount > MaximumLabels)
				throw new InvalidDataException($"RA2 CSF label count {labelCount} is not plausible.");

			var result = new Dictionary<string, string>((int)labelCount, StringComparer.OrdinalIgnoreCase);
			for (var i = 0u; i < labelCount; i++)
			{
				if (reader.ReadUInt32() != LabelPrefix)
					throw new InvalidDataException($"RA2 CSF label {i} has an invalid prefix.");

				var pairCount = reader.ReadUInt32();
				var labelLength = reader.ReadUInt32();
				var label = Encoding.ASCII.GetString(ReadExactly(reader, labelLength));
				for (var pair = 0u; pair < pairCount; pair++)
				{
					var value = ReadString(reader);
					if (pair == 0)
						result[label] = value;
				}
			}

			return new Ra2CsfTable(result);
		}

		public bool TryResolve(string label, out string value)
			=> strings.TryGetValue(label, out value);

		static string ReadString(BinaryReader reader)
		{
			var prefix = reader.ReadUInt32();
			var hasExtra = prefix == StringWithExtraPrefix;
			if (prefix != StringPrefix && !hasExtra)
				throw new InvalidDataException("RA2 CSF string has an invalid prefix.");

			var characterCount = reader.ReadUInt32();
			if (characterCount > MaximumStringCharacters)
				throw new InvalidDataException($"RA2 CSF string length {characterCount} is not plausible.");

			var encoded = ReadExactly(reader, checked(characterCount * 2));
			for (var i = 0; i < encoded.Length; i++)
				encoded[i] = (byte)~encoded[i];

			var value = Encoding.Unicode.GetString(encoded);
			if (hasExtra)
				ReadExactly(reader, reader.ReadUInt32());

			return value;
		}

		static byte[] ReadExactly(BinaryReader reader, uint count)
		{
			if (count > int.MaxValue)
				throw new InvalidDataException($"RA2 CSF block length {count} is not plausible.");

			var bytes = reader.ReadBytes((int)count);
			if (bytes.Length != (int)count)
				throw new EndOfStreamException("Unexpected end of RA2 CSF string table.");
			return bytes;
		}
	}
}
