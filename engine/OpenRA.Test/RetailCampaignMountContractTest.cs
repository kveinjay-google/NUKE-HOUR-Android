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

using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCampaignMountContractTest
	{
		[Test]
		public void RetailCampaignArchivesAreOptionalRuntimeSourcesOnly()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (!Directory.Exists(Path.Combine(root, "ios", "OpenRA.iOS")))
				root = Directory.GetParent(root)?.FullName ??
					throw new AssertionException("Could not find repository root.");

			var manifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			StringAssert.Contains("~maps01.mix: retailcampaign01", manifest);
			StringAssert.Contains("~maps02.mix: retailcampaign02", manifest);
			StringAssert.Contains("~movies01.mix: retailmovies01", manifest);
			StringAssert.Contains("~movies02.mix: retailmovies02", manifest);
			StringAssert.DoesNotContain("\n\t\tmaps01.mix", manifest);
			StringAssert.DoesNotContain("\n\t\tmaps02.mix", manifest);
		}
	}
}
