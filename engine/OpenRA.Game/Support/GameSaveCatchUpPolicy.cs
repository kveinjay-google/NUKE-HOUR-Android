namespace OpenRA
{
	public static class GameSaveCatchUpPolicy
	{
		// Yield to the native event loop regularly, including when the server has
		// not delivered the next replay packet yet. Never accelerate live gameplay.
		public static bool ShouldContinue(bool loading, int iterations, long elapsedMilliseconds)
			=> loading && iterations < 64 && elapsedMilliseconds < 8;
	}
}
