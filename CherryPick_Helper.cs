using System.Runtime.CompilerServices;
using FrooxEngine;


namespace CherryPick;

public static class CherryPick_Helper
{
    // Out of the total string length, how many characters actually match the query. Gives decent results.
    public static float MatchRatioInsensitive(string? source, string[] queries)
    {
        if (source == null)
            return 0f;

        float totalScore = 0f;
        int indexFound = 1;


        for (int i = 0; i < queries.Length; i++)
        {
            string item = queries[i];
            int score = source.IndexOf(item, StringComparison.OrdinalIgnoreCase);

            indexFound *= IsPositive(score); // If this is ever zero, the score will remain zero


            // Sum the score, but make it zero if any query was not found
            totalScore = indexFound * (totalScore + item.Length / (source.Length + i + 1f));
        }


        return totalScore;
    }
    
    /// <summary>
    /// Bitwise check to see if an integer is positive
    /// </summary>
    /// <param name="value">Integer to check</param>
    /// <returns>1 if positive, otherwise 0</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IsPositive(int value)
    {
        return 1 + ((value & int.MinValue) >> 31);
    }
}