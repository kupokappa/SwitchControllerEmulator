using System;

namespace CustomExtensions {
	public static class MathExtensions {
		/// <summary>
		/// Converts degrees to radians.
		/// </summary>
		/// <param name="val">The value to convert to radians.</param>
		/// <returns>The value in radians.</returns>
		public static double ToRadians(this double val) {
			return (Math.PI / 180) * val;
		}
	}
}
