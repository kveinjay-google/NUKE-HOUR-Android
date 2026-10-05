#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OpenRA.Server
{
	public static class DedicatedServerControlProtocol
	{
		public const int MaxFrameBytes = 64 * 1024;

		public static string ReadFrame(Stream stream)
		{
			using var buffer = new MemoryStream();
			while (true)
			{
				var value = stream.ReadByte();
				if (value < 0)
					throw new EndOfStreamException("Control frame did not end with a newline.");
				if (value == '\n')
					return Encoding.UTF8.GetString(buffer.ToArray());

				buffer.WriteByte((byte)value);
				if (buffer.Length > MaxFrameBytes)
					throw new InvalidDataException("Control frame exceeds 64 KiB.");
			}
		}

		public static void WriteFrame(Stream stream, object response)
		{
			var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response) + "\n");
			if (payload.Length > MaxFrameBytes)
				throw new InvalidDataException("Control response exceeds 64 KiB.");
			stream.Write(payload, 0, payload.Length);
			stream.Flush();
		}
	}

	public sealed class DedicatedServerControl : IDisposable
	{
		[DllImport("libc", SetLastError = true)]
		static extern int chmod(string path, uint mode);

		static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
		{
			"status", "players", "broadcast", "kick", "ban", "reload-policy", "shutdown"
		};

		readonly string socketPath;
		readonly Func<string, JsonElement, object> handler;
		readonly Socket listener;
		readonly Thread thread;
		volatile bool disposed;

		public DedicatedServerControl(string socketPath, Func<string, JsonElement, object> handler)
		{
			this.socketPath = socketPath ?? throw new ArgumentNullException(nameof(socketPath));
			this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
			var directory = Path.GetDirectoryName(socketPath);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);
			File.Delete(socketPath);

			listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
			listener.Bind(new UnixDomainSocketEndPoint(socketPath));
			listener.Listen(8);
			if (!OperatingSystem.IsWindows() && chmod(socketPath, 0x180) != 0)
				throw new IOException("Failed to restrict dedicated control socket permissions.");

			thread = new Thread(Run) { Name = "Dedicated server control", IsBackground = true };
			thread.Start();
		}

		void Run()
		{
			while (!disposed)
				try
				{
					using var client = listener.Accept();
					client.ReceiveTimeout = 5000;
					client.SendTimeout = 5000;
					using var stream = new NetworkStream(client, ownsSocket: false);
					Handle(stream);
				}
				catch (SocketException)
				{
					if (!disposed)
						Thread.Sleep(50);
				}
				catch (ObjectDisposedException) when (disposed) { }
				catch (Exception e)
				{
					Log.Write("server", $"Dedicated control request failed: {e.Message}");
				}
		}

		void Handle(Stream stream)
		{
			string id = null;
			try
			{
				using var document = JsonDocument.Parse(DedicatedServerControlProtocol.ReadFrame(stream));
				var root = document.RootElement;
				if (root.ValueKind != JsonValueKind.Object ||
					!root.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String ||
					!root.TryGetProperty("operation", out var operationElement) || operationElement.ValueKind != JsonValueKind.String)
					throw new InvalidDataException("Control request is missing required fields.");

				id = idElement.GetString();
				var operation = operationElement.GetString();
				if (string.IsNullOrEmpty(id) || id.Length > 128 || !Operations.Contains(operation))
					throw new InvalidDataException("Control request is invalid.");

				var allowedFields = new[] { "id", "operation", "arguments" };
				if (root.EnumerateObject().Any(property => !allowedFields.Contains(property.Name)))
					throw new InvalidDataException("Control request contains unknown fields.");

				var arguments = root.TryGetProperty("arguments", out var argumentElement)
					? argumentElement.Clone() : JsonDocument.Parse("{}").RootElement.Clone();
				var result = handler(operation, arguments);
				DedicatedServerControlProtocol.WriteFrame(stream, new { id, ok = true, result });
			}
			catch (Exception e)
			{
				var code = e is DedicatedServerControlException control ? control.Code : "invalid_request";
				DedicatedServerControlProtocol.WriteFrame(stream, new { id, ok = false, error = code });
			}
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			listener.Dispose();
			thread.Join(TimeSpan.FromSeconds(2));
			try { File.Delete(socketPath); }
			catch { }
		}
	}

	public sealed class DedicatedServerControlException : Exception
	{
		public string Code { get; }

		public DedicatedServerControlException(string code)
			: base(code)
		{
			Code = code;
		}
	}
}
