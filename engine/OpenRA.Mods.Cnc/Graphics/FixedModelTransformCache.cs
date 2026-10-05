// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, licensed under the GNU General Public License v3.

using System;

namespace OpenRA.Mods.Cnc.Graphics
{
	// Entries own their arrays for their entire lifetime. Deferred render functions can
	// retain an evicted entry: neither eviction nor another request overwrites it.
	sealed class FixedModelTransformCache
	{
		static readonly float[] GroundNormal = { 0, 0, 1, 1 };
		static readonly float[] ZVector = { 0, 0, 1, 1 };
		readonly Entry[] entries = new Entry[8];
		int next;

		internal sealed class Entry
		{
			internal readonly Int32Matrix4x4 Camera, Ground;
			internal readonly int LightYaw, LightPitch, ScaleBits;
			internal readonly float[] ScaleTransform, ShadowTransform, InvShadowTransform;
			internal readonly float[] CameraTransform, InvCameraTransform, ShadowScreenTransform, ShadowGroundNormal;
			internal readonly float ShadowDirection;

			internal Entry(Int32Matrix4x4 camera, float scale, Int32Matrix4x4 groundOrientation, WRot lightSource)
			{
				Camera = camera;
				Ground = groundOrientation;
				LightYaw = lightSource.Yaw.Angle;
				LightPitch = lightSource.Pitch.Angle;
				ScaleBits = BitConverter.SingleToInt32Bits(scale);

				ScaleTransform = Util.ScaleMatrix(scale, scale, scale);
				var lightYaw = Util.MakeFloatMatrix(new WRot(WAngle.Zero, WAngle.Zero, -lightSource.Yaw).AsMatrix());
				var lightPitch = Util.MakeFloatMatrix(new WRot(WAngle.Zero, -lightSource.Pitch, WAngle.Zero).AsMatrix());
				var ground = Util.MakeFloatMatrix(groundOrientation);
				ShadowTransform = Util.MatrixMultiply(Util.MatrixMultiply(lightPitch, lightYaw), Util.MatrixInverse(ground));
				var groundNormal = Util.MatrixVectorMultiply(ground, GroundNormal);
				InvShadowTransform = Util.MatrixInverse(ShadowTransform);
				CameraTransform = Util.MakeFloatMatrix(camera);
				InvCameraTransform = Util.MatrixInverse(CameraTransform);
				if (InvCameraTransform == null)
					throw new InvalidOperationException("Failed to invert the cameraTransform matrix during RenderAsync.");

				ShadowScreenTransform = Util.MatrixMultiply(CameraTransform, InvShadowTransform);
				ShadowGroundNormal = Util.MatrixVectorMultiply(ShadowTransform, groundNormal);
				var screenLightVector = Util.MatrixVectorMultiply(InvShadowTransform, ZVector);
				screenLightVector = Util.MatrixVectorMultiply(CameraTransform, screenLightVector);
				ShadowDirection = -screenLightVector[2] / screenLightVector[1];
			}
		}

		internal Entry Get(WRot camera, float scale, WRot groundOrientation, WRot lightSource)
		{
			// WRot equality compares Euler fields, which may hide quaternion rounding.
			var cameraMatrix = camera.AsMatrix();
			var groundMatrix = groundOrientation.AsMatrix();
			var scaleBits = BitConverter.SingleToInt32Bits(scale);
			foreach (var entry in entries)
				if (entry != null && entry.Camera == cameraMatrix && entry.Ground == groundMatrix &&
					entry.LightYaw == lightSource.Yaw.Angle && entry.LightPitch == lightSource.Pitch.Angle && entry.ScaleBits == scaleBits)
					return entry;

			var created = new Entry(cameraMatrix, scale, groundMatrix, lightSource);
			entries[next] = created;
			next = (next + 1) % entries.Length;
			return created;
		}

		internal void Clear()
		{
			Array.Clear(entries, 0, entries.Length);
			next = 0;
		}
	}
}
