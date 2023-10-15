using System.Runtime.CompilerServices;

namespace BDArmory.Extensions
{
    /// <summary>
    /// Extensions to floats.
    /// 
    /// These would be better if they were properties instead of full-fledged functions, but that isn't part of C# at the time this was written.
    /// </summary>
    public static class FloatExtensions
    {
        /// <summary>
        /// Avoid local temporaries when all you want is simply the square of a float.
        /// </summary>
        /// <returns>The square of f.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sqr(this float f) => f * f;

        /// <summary>
        /// Avoid local temporaries when all you want is simply the cube of a float.
        /// </summary>
        /// <returns>The cube of f.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cube(this float f) => f * f * f;
    }
}