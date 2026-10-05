using System;
using System.IO;
using System.Collections.Generic;

namespace OpenRA.Android;

enum AndroidStartupFailureCategory
{
	General,
	Graphics,
	Network,
	Content,
	Map,
	Memory,
	Runtime
}

readonly record struct AndroidStartupFailureInfo(string Code, AndroidStartupFailureCategory Category);

static class AndroidStartupFailureClassifier
{
	internal static IEnumerable<Exception> Enumerate(Exception error)
	{
		var pending = new Stack<Exception>();
		var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
		pending.Push(error);
		while (pending.Count > 0 && seen.Count < 128)
		{
			var current = pending.Pop();
			if (!seen.Add(current))
				continue;
			yield return current;
			if (current is AggregateException aggregate)
			{
				for (var i = Math.Min(aggregate.InnerExceptions.Count, 128) - 1; i >= 0; i--)
					pending.Push(aggregate.InnerExceptions[i]);
			}
			else if (current.InnerException != null)
				pending.Push(current.InnerException);
		}
	}

	public static AndroidStartupFailureInfo Classify(Exception error)
	{
		// Memory failure wins even when a wrapper mentions graphics or content.
		foreach (var current in Enumerate(error))
			if (current is OutOfMemoryException)
				return new AndroidStartupFailureInfo("NH-ANDROID-MEM-4001", AndroidStartupFailureCategory.Memory);

		foreach (var current in Enumerate(error))
		{
			var result = ClassifyNode(current);
			if (result.Category != AndroidStartupFailureCategory.General)
				return result;
		}

		return new AndroidStartupFailureInfo("NH-ANDROID-START-0001", AndroidStartupFailureCategory.General);
	}

	internal static string SafeMessage(Exception error)
	{
		if (error is AggregateException aggregate)
			return $"Aggregate failure ({aggregate.InnerExceptions.Count} inner exceptions); see individual causes";
		try { return error.Message ?? string.Empty; }
		catch (Exception) { return "[message unavailable: exception getter failed]"; }
	}

	internal static AndroidStartupFailureInfo ClassifyNode(Exception current)
	{
		if (current is OutOfMemoryException)
			return new AndroidStartupFailureInfo("NH-ANDROID-MEM-4001", AndroidStartupFailureCategory.Memory);

		var message = SafeMessage(current);
		if (current is InvalidProgramException ||
			message.Contains("shader", StringComparison.OrdinalIgnoreCase) ||
			message.Contains("OpenGL", StringComparison.OrdinalIgnoreCase))
			return new AndroidStartupFailureInfo("NH-ANDROID-GFX-1001", AndroidStartupFailureCategory.Graphics);

		if (current is ExecutionEngineException ||
			message.Contains("aot-only mode", StringComparison.OrdinalIgnoreCase))
			return new AndroidStartupFailureInfo("NH-ANDROID-RUNTIME-5001", AndroidStartupFailureCategory.Runtime);

		if (message.Contains("disconnected client", StringComparison.OrdinalIgnoreCase) ||
			message.Contains("connection", StringComparison.OrdinalIgnoreCase) &&
			current.GetType().Namespace?.StartsWith("OpenRA.Network", StringComparison.Ordinal) == true)
			return new AndroidStartupFailureInfo("NH-ANDROID-NET-2001", AndroidStartupFailureCategory.Network);

		if (message.Contains("Could not find map", StringComparison.OrdinalIgnoreCase) ||
			message.Contains("map is unavailable", StringComparison.OrdinalIgnoreCase) ||
			message.Contains("map package", StringComparison.OrdinalIgnoreCase))
			return new AndroidStartupFailureInfo("NH-ANDROID-MAP-3101", AndroidStartupFailureCategory.Map);

		if (current is IOException || current is UnauthorizedAccessException ||
			message.Contains("MIX", StringComparison.OrdinalIgnoreCase) ||
			message.Contains("content", StringComparison.OrdinalIgnoreCase))
			return new AndroidStartupFailureInfo("NH-ANDROID-CONTENT-3001", AndroidStartupFailureCategory.Content);
		return new AndroidStartupFailureInfo("NH-ANDROID-START-0001", AndroidStartupFailureCategory.General);
	}
}
